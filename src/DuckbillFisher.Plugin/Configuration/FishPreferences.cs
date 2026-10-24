using System.Collections.Frozen;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record FishPersonalResult(uint FishId, UiMessage Message);

internal sealed record FishPreferencesView(int Revision, int Session, bool HasCharacter, bool CanEdit,
    IReadOnlySet<uint> Favorites, IReadOnlyDictionary<uint, bool> ManualCaught, UiMessage? Error)
{
    // Only inputs to catalog/collection filtering; alert reservations do not invalidate them.
    public int CatalogRevision { get; init; }
    public int SettingsRestoreRevision { get; init; }
    internal ulong CharacterId { get; init; }
    public bool SpawnAlertsEnabled { get; init; }
    public int SpawnAlertLeadMinutes { get; init; } = SpawnAlertScheduler.DefaultLeadMinutes;
    public XivChatType SpawnAlertChannel { get; init; } = XivChatType.SystemMessage;
    public int SpawnAlertSound { get; init; }
    public string CustomAlertSoundPath { get; init; } = "";
    public SpawnAlertAudience SpawnAlertAudience { get; init; } = SpawnAlertAudience.AllFavorites;
    public bool RemoveFavoriteOnRegistration { get; init; } = true;
    public CatalogSettings Catalog { get; init; } = new();
    public IReadOnlyList<CatalogPreset> CatalogPresets { get; init; } = [];
    public UiMessage? CatalogPresetError { get; init; }
    public UiLanguage Language { get; init; } = UiLanguage.Korean;
    public int UiScalePercent { get; init; } = UiScale.DefaultPercent;
    public bool DeveloperMode { get; init; }
    public string LastSeenChangelogVersion { get; init; } = "";
    public CompactViewSettings Compact { get; init; } = new();
    public SpawnScheduleSettings Schedule { get; init; } = new();
    public CastingTimerSettings Timer { get; init; } = new();
    public IReadOnlyDictionary<uint, FishPersonalRecord> PersonalRecords { get; init; } = FrozenDictionary<uint, FishPersonalRecord>.Empty;
    public FishPersonalResult? PersonalResult { get; init; }
    public bool SpotFishHideCaught { get; init; } = true;
    public IReadOnlyDictionary<uint, int> FishAlertLeadMinutes { get; init; } = FrozenDictionary<uint, int>.Empty;
    public int AlertLead(uint id) => FishAlertLeadMinutes.GetValueOrDefault(id, SpawnAlertLeadMinutes);
    internal IReadOnlyDictionary<uint, long> ReservedAlertWindows { get; init; } = FrozenDictionary<uint, long>.Empty;

    public IReadOnlyDictionary<uint, bool> SavedCollection { get; init; } = FrozenDictionary<uint, bool>.Empty;
    public DateTimeOffset? CollectionSavedUtc { get; init; }

    public bool UsesSavedCollection(uint fishId, FishCollectionView observed) => HasCharacter &&
        !ManualCaught.ContainsKey(fishId) && SavedCollection.ContainsKey(fishId) &&
        (observed.Session != Session || observed.Collection(fishId) == CollectionStatus.Unknown);

    public CollectionStatus Collection(uint fishId, FishCollectionView observed)
    {
        if (!HasCharacter) return CollectionStatus.Unknown;
        var current = observed.Session == Session ? observed.Collection(fishId) : CollectionStatus.Unknown;
        if (current == CollectionStatus.Unknown && SavedCollection.TryGetValue(fishId, out var saved))
            current = saved ? CollectionStatus.Caught : CollectionStatus.Uncaught;
        return CollectionFilter.Resolve(current, ManualCaught.TryGetValue(fishId, out var manual) ? manual : null);
    }
}

/// <summary>Queues UI edits; samples character identity and persists changes on Framework.Update.</summary>
internal sealed class FishPreferences : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly object gate = new();
    private readonly Queue<Edit> pending = new();
    private readonly Queue<PersonalEdit> pendingPersonal = new();
    private (bool Enabled, int LeadMinutes, XivChatType Channel, int Sound, SpawnAlertAudience Audience, bool RemoveOnRegistration)? pendingAlerts;
    private readonly Dictionary<uint, int?> pendingFishAlerts = [];
    private CatalogSettings? pendingCatalog;
    private PresetEdit? pendingPreset;
    private (UiLanguage Language, bool DeveloperMode)? pendingInterface;
    private int? pendingUiScale;
    private CompactViewSettings? pendingCompact;
    private SpawnScheduleSettings? pendingSchedule;
    private CastingTimerSettings? pendingTimer;
    private bool? pendingSpotFishHideCaught;
    private string? pendingCustomSoundPath;
    private Version? pendingChangelogVersion;
    private (CommonSettingsBackup Settings, int Session, CancellationToken Token, Action<bool> Completed)? pendingRestore;
    private FishPreferencesConfiguration configuration = new();
    private FishPreferencesView view = new(0, 0, false, false,
        FrozenSet<uint>.Empty, FrozenDictionary<uint, bool>.Empty, null);
    private ulong characterId;
    private bool writable;
    private bool disposed;

    public FishPreferences(IDalamudPluginInterface pluginInterface, IClientState clientState,
        IPlayerState playerState, IFramework framework, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.clientState = clientState;
        this.playerState = playerState;
        this.framework = framework;
        this.log = log;
        try
        {
            var saved = pluginInterface.GetPluginConfig();
            if (saved is null && !pluginInterface.ConfigFile.Exists)
                writable = true;
            else if (saved is FishPreferencesConfiguration loaded && PreferencesValidation.IsValid(loaded))
            {
                configuration = loaded;
                PreferencesNormalization.Normalize(loaded);
                writable = true;
            }
            else
                view = view with { Error = new(T.PreferencesLoadFailed) };
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not load fish preferences; existing configuration will not be overwritten.");
            view = view with { Error = new(T.PreferencesLoadFailed) };
        }
        Publish();
        framework.Update += OnUpdate;
    }

    public FishPreferencesView View { get { lock (gate) return view; } }

    public void SaveFishNote(uint fishId, string note, string expectedNote, int session, int restoreRevision)
    {
        if (FishPersonalRecord.ValidNote(note))
            QueuePersonal(new(fishId, PersonalEditKind.Note, session, restoreRevision, Note: note, ExpectedNote: expectedNote));
    }
    public void SetFishAttempts(uint fishId, int attempts, int session, int restoreRevision)
    {
        if (attempts is >= 0 and <= FishPersonalRecord.MaxAttempts)
            QueuePersonal(new(fishId, PersonalEditKind.Attempts, session, restoreRevision, Amount: attempts));
    }
    public void AdjustFishAttempts(uint fishId, int delta, int session, int restoreRevision) =>
        QueuePersonal(new(fishId, PersonalEditKind.AttemptsDelta, session, restoreRevision, Amount: delta));
    public void SetFishTimerPreset(uint fishId, Guid? presetId, int session, int restoreRevision)
    {
        if (presetId != Guid.Empty)
            QueuePersonal(new(fishId, PersonalEditKind.Preset, session, restoreRevision, PresetId: presetId));
    }
    public void ApplyFishTimerPreset(uint fishId, int session, int restoreRevision) =>
        QueuePersonal(new(fishId, PersonalEditKind.ApplyPreset, session, restoreRevision));
    private void QueuePersonal(PersonalEdit edit)
    {
        lock (gate)
            if (!disposed && writable && edit.FishId != 0 && edit.Session == view.Session && edit.RestoreRevision == view.SettingsRestoreRevision)
                pendingPersonal.Enqueue(edit);
    }

    public CommonSettingsBackup? ExportSettings()
    {
        lock (gate) return !disposed && writable ? CommonSettingsBackup.From(configuration) : null;
    }

    public bool RestoreSettings(CommonSettingsBackup settings, int session, CancellationToken token, Action<bool> completed)
    {
        lock (gate)
        {
            if (disposed || !writable || session != view.Session || token.IsCancellationRequested || pendingRestore is not null) return false;
            pendingRestore = (settings, session, token, completed);
            return true;
        }
    }

    public void CancelSettingsRestore()
    {
        lock (gate)
        {
            pendingRestore?.Completed(false);
            pendingRestore = null;
        }
    }

    public bool SetManagedSoundPath(SoundSlot slot, string path, string previous, int expectedSession, int restoreRevision)
    {
        if (!framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Managed sound settings must be saved on Framework.Update.");
        lock (gate)
        {
            if (disposed || !writable || view.Session != expectedSession || view.SettingsRestoreRevision != restoreRevision ||
                pendingRestore is not null || !ManagedSoundFiles.UsesCustomSound(view, slot) ||
                ManagedSoundFiles.Selected(view, slot) != previous) return false;
            var candidate = CopyConfiguration();
            var common = CommonSettingsBackup.From(candidate);
            ManagedSoundFiles.Select(common, slot, path);
            common.Apply(candidate);
            var saved = Save(candidate);
            Publish();
            return saved;
        }
    }

    public void MarkChangelogSeen(Version version)
    {
        lock (gate)
            if (!disposed && writable && (pendingChangelogVersion is null || version > pendingChangelogVersion))
                pendingChangelogVersion = version;
    }

    public void SetInterface(UiLanguage language, bool developerMode)
    {
        lock (gate)
            if (!disposed && writable && Enum.IsDefined(language))
                pendingInterface = (language, developerMode);
    }

    public void SetUiScale(int percent)
    {
        lock (gate)
            if (!disposed && writable && UiScale.Choices.Contains(percent))
                pendingUiScale = percent;
    }

    public void SetCompact(CompactViewSettings settings)
    {
        lock (gate)
            if (!disposed && writable && settings.IsValid()) pendingCompact = settings;
    }

    public void SetSchedule(SpawnScheduleSettings settings)
    {
        lock (gate)
            if (!disposed && writable && settings.IsValid()) pendingSchedule = settings;
    }

    public void SetTimer(CastingTimerSettings settings)
    {
        lock (gate)
            if (!disposed && writable && settings.IsValid()) pendingTimer = settings.Copy();
    }

    public void SetSpotFishHideCaught(bool hideCaught)
    {
        lock (gate)
            if (!disposed && writable) pendingSpotFishHideCaught = hideCaught;
    }

    public void SetCatalogSettings(CatalogSettings settings)
    {
        lock (gate)
            if (!disposed && writable && settings.IsValid())
                pendingCatalog = settings.Copy();
    }

    public void SetFavorite(uint fishId, bool favorite)
    {
        lock (gate)
            if (!disposed && writable && fishId != 0)
                pending.Enqueue(new(fishId, favorite, false, view.Session));
    }

    public void SaveCatalogPreset(string name, CatalogSettings settings)
    {
        name = name.Trim();
        lock (gate)
            if (!disposed && writable && CatalogPreset.ValidName(name) && settings.IsValid())
                pendingPreset = new(new(Guid.NewGuid(), name, settings.Copy()), Guid.Empty);
    }

    public void DeleteCatalogPreset(Guid id)
    {
        lock (gate)
            if (!disposed && writable && id != Guid.Empty) pendingPreset = new(null, id);
    }

    public void SetManualCaught(uint fishId, bool? caught, int expectedSession)
    {
        lock (gate)
            if (!disposed && writable && characterId != 0 && fishId != 0 && expectedSession == view.Session)
                pending.Enqueue(new(fishId, caught, true, expectedSession));
    }

    public void SetSpawnAlerts(bool enabled, int leadMinutes, XivChatType channel, int sound,
        SpawnAlertAudience audience, bool removeOnRegistration)
    {
        lock (gate)
            if (!disposed && writable)
                pendingAlerts = (enabled, Math.Clamp(leadMinutes, SpawnAlertScheduler.MinLeadMinutes, SpawnAlertScheduler.MaxLeadMinutes),
                    ChatChannelChoices.Supports(channel) ? channel : XivChatType.SystemMessage, Math.Clamp(sound, 0, CustomAlertSound.Choice),
                    Enum.IsDefined(audience) ? audience : SpawnAlertAudience.AllFavorites, removeOnRegistration);
    }

    public void SetCustomSoundPath(string path)
    {
        lock (gate)
            if (!disposed && writable && path.Length <= 4096) pendingCustomSoundPath = path;
    }

    // Called only after the KR registration contract/item/session checks, never by a guide scan or manual edit.
    public void OnFishRegistered(uint fishId, int expectedSession)
    {
        if (!framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Registration preferences must be updated on Framework.Update.");
        lock (gate)
        {
            if (disposed || !writable || characterId == 0 || view.Session != expectedSession ||
                !configuration.RemoveFavoriteOnRegistration || !configuration.Favorites.Contains(fishId))
                return;
            var candidate = CopyConfiguration();
            candidate.Favorites.Remove(fishId);
            Save(candidate);
            Publish();
        }
    }

    // Framework only. A failed save retains the last durable backup; the observer retries later.
    public bool SaveCollection(int expectedSession, IReadOnlySet<uint> caught, IReadOnlySet<uint> uncaught, bool complete)
    {
        if (!framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Collection backups must be saved on Framework.Update.");
        lock (gate)
        {
            if (disposed || !writable || characterId == 0 || expectedSession != view.Session) return false;
            if (!complete && caught.Count == 0) return true;
            configuration.CollectionBackups.TryGetValue(characterId, out var previous);
            var states = complete ? new Dictionary<uint, bool>() : previous is null ? [] : new(previous.States);
            if (complete) foreach (var id in uncaught) states[id] = false;
            foreach (var id in caught) states[id] = true;
            if (states.Count == 0) return true;
            if (!complete && previous is not null && states.Count == previous.States.Count &&
                states.All(pair => previous.States.TryGetValue(pair.Key, out var old) && old == pair.Value)) return true;
            var backup = new FishCollectionBackup { UpdatedUtc = DateTimeOffset.UtcNow, States = states };
            if (!backup.IsValid()) return false;
            var candidate = CopyConfiguration();
            candidate.CollectionBackups[characterId] = backup;
            var success = Save(candidate);
            Publish();
            return success;
        }
    }

    public void SetFishAlertLead(uint id, int? minutes)
    {
        lock (gate)
            if (!disposed && writable && id != 0)
                pendingFishAlerts[id] = minutes is { } value
                    ? Math.Clamp(value, SpawnAlertScheduler.MinLeadMinutes, SpawnAlertScheduler.MaxLeadMinutes) : null;
    }

    // Framework only. Reserve durably before output: a crash may lose a notification, but cannot resend it.
    public IReadOnlyList<SpawnAlert>? ReserveAlerts(int expectedSession, IReadOnlyList<SpawnAlert> alerts)
    {
        if (!framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Alert reservations must be saved on Framework.Update.");
        lock (gate)
        {
            if (disposed || !writable || characterId == 0 || expectedSession != view.Session || !configuration.SpawnAlertsEnabled)
                return [];
            var candidate = CopyConfiguration();
            if (!candidate.AlertWindows.TryGetValue(characterId, out var saved))
                candidate.AlertWindows[characterId] = saved = [];
            var accepted = alerts.Where(alert => !saved.TryGetValue(alert.FishId, out var last) ||
                alert.Start.ToUnixTimeMilliseconds() > last).ToArray();
            if (accepted.Length == 0)
                return accepted;
            foreach (var alert in accepted)
                saved[alert.FishId] = alert.Start.ToUnixTimeMilliseconds();
            var success = Save(candidate);
            Publish();
            return success ? accepted : null;
        }
    }

    private void OnUpdate(IFramework _)
    {
        lock (gate)
        {
            if (disposed)
                return;
            // Always sample identity before applying queued edits, including logout/character changes.
            ulong currentCharacter;
            var stateErrorChanged = false;
            try
            {
                currentCharacter = clientState.IsLoggedIn && playerState.IsLoaded ? playerState.ContentId : 0;
                if (view.Error?.Key == T.PreferencesPlayerUnavailable)
                {
                    view = view with { Error = null };
                    stateErrorChanged = true;
                }
            }
            catch (Exception ex)
            {
                currentCharacter = 0;
                if (writable && view.Error?.Key != T.PreferencesPlayerUnavailable)
                {
                    log.Error(ex, "Could not identify the current character; manual preferences are unavailable.");
                    view = view with { Error = new(T.PreferencesPlayerUnavailable) };
                    stateErrorChanged = true;
                }
            }
            if (currentCharacter != characterId)
            {
                characterId = currentCharacter;
                view = view with { Session = view.Session + 1, PersonalResult = null };
                Publish();
            }
            else if (stateErrorChanged)
                Publish();
            if (pendingRestore is { } restore)
            {
                pendingRestore = null;
                var success = false;
                if (writable && restore.Session == view.Session && !restore.Token.IsCancellationRequested)
                {
                    var restored = CopyConfiguration();
                    restore.Settings.Apply(restored);
                    success = Save(restored);
                    if (success) view = view with { SettingsRestoreRevision = view.SettingsRestoreRevision + 1, CatalogPresetError = null, PersonalResult = null };
                    Publish();
                }
                restore.Completed(success);
                if (success)
                {
                    // Restore replaces common settings; discard older common edits, preserve manual collection edits.
                    pendingAlerts = null;
                    pendingFishAlerts.Clear();
                    pendingCatalog = null;
                    pendingPreset = null;
                    pendingInterface = null;
                    pendingUiScale = null;
                    pendingCompact = null;
                    pendingSchedule = null;
                    pendingTimer = null;
                    pendingSpotFishHideCaught = null;
                    pendingCustomSoundPath = null;
                    pendingPersonal.Clear();
                    var manualEdits = pending.Where(edit => edit.Manual).ToArray();
                    pending.Clear();
                    foreach (var edit in manualEdits) pending.Enqueue(edit);
                }
            }
            if (pending.Count == 0 && pendingPersonal.Count == 0 && pendingAlerts is null && pendingFishAlerts.Count == 0 && pendingCatalog is null && pendingPreset is null && pendingInterface is null && pendingUiScale is null && pendingCompact is null && pendingSchedule is null && pendingTimer is null && pendingSpotFishHideCaught is null && pendingCustomSoundPath is null && pendingChangelogVersion is null)
                return;
            var candidate = CopyConfiguration();
            var changed = false;
            var timerEdited = pendingTimer is not null;
            var personalEdited = pendingPersonal.Count > 0;
            FishPersonalResult? personalResult = null;
            if (pendingSpotFishHideCaught is { } hideCaught)
            {
                changed |= candidate.SpotFishHideCaught != hideCaught;
                candidate.SpotFishHideCaught = hideCaught;
                pendingSpotFishHideCaught = null;
            }
            if (pendingTimer is { } timer)
            {
                changed |= !candidate.Timer.Matches(timer);
                candidate.Timer = timer;
                pendingTimer = null;
            }
            if (pendingUiScale is { } scalePercent)
            {
                changed |= candidate.UiScalePercent != scalePercent;
                candidate.UiScalePercent = scalePercent;
                pendingUiScale = null;
            }
            if (pendingChangelogVersion is { } changelogVersion)
            {
                // Keep a newer acknowledgement when a developer temporarily loads an older DLL.
                if (!System.Version.TryParse(candidate.LastSeenChangelogVersion, out var seen) || changelogVersion > seen)
                {
                    candidate.LastSeenChangelogVersion = changelogVersion.ToString();
                    changed = true;
                }
                pendingChangelogVersion = null;
            }
            if (pendingCustomSoundPath is { } customSoundPath)
            {
                changed |= candidate.CustomAlertSoundPath != customSoundPath;
                candidate.CustomAlertSoundPath = customSoundPath;
                pendingCustomSoundPath = null;
            }
            if (pendingPreset is { } presetEdit)
            {
                pendingPreset = null;
                view = view with { CatalogPresetError = null };
                if (presetEdit.Add is { } preset)
                {
                    if (candidate.CatalogPresets.Count >= CatalogPreset.MaxCount)
                        view = view with { CatalogPresetError = new(T.PresetLimit, CatalogPreset.MaxCount) };
                    else if (candidate.CatalogPresets.Any(value => string.Equals(value.Name, preset.Name, StringComparison.OrdinalIgnoreCase)))
                        view = view with { CatalogPresetError = new(T.PresetDuplicateName) };
                    else
                    {
                        candidate.CatalogPresets.Add(preset);
                        changed = true;
                    }
                }
                else changed |= candidate.CatalogPresets.RemoveAll(value => value.Id == presetEdit.Remove) > 0;
            }
            if (pendingSchedule is { } schedule)
            {
                changed |= candidate.Schedule != schedule;
                candidate.Schedule = schedule;
                pendingSchedule = null;
            }
            if (pendingCompact is { } compact)
            {
                changed |= candidate.Compact != compact;
                candidate.Compact = compact;
                pendingCompact = null;
            }
            if (pendingInterface is { } ui)
            {
                changed |= candidate.Language != ui.Language || candidate.DeveloperMode != ui.DeveloperMode;
                candidate.Language = ui.Language;
                candidate.DeveloperMode = ui.DeveloperMode;
                pendingInterface = null;
            }
            if (pendingCatalog is { } catalogSettings)
            {
                changed |= !candidate.Catalog.Matches(catalogSettings);
                candidate.Catalog = catalogSettings;
                pendingCatalog = null;
            }
            if (pendingAlerts is { } alertSettings)
            {
                changed |= candidate.SpawnAlertsEnabled != alertSettings.Enabled || candidate.SpawnAlertLeadMinutes != alertSettings.LeadMinutes ||
                    candidate.SpawnAlertChannelId != (ushort)alertSettings.Channel || candidate.SpawnAlertSound != alertSettings.Sound ||
                    candidate.SpawnAlertAudience != alertSettings.Audience || candidate.RemoveFavoriteOnRegistration != alertSettings.RemoveOnRegistration;
                candidate.SpawnAlertsEnabled = alertSettings.Enabled;
                candidate.SpawnAlertLeadMinutes = alertSettings.LeadMinutes;
                candidate.SpawnAlertChannelId = (ushort)alertSettings.Channel;
                candidate.SpawnAlertSound = alertSettings.Sound;
                candidate.SpawnAlertAudience = alertSettings.Audience;
                candidate.RemoveFavoriteOnRegistration = alertSettings.RemoveOnRegistration;
                pendingAlerts = null;
            }
            foreach (var (id, minutes) in pendingFishAlerts)
            {
                if (minutes is { } value)
                {
                    changed |= !candidate.FishAlertLeadMinutes.TryGetValue(id, out var old) || old != value;
                    candidate.FishAlertLeadMinutes[id] = value;
                }
                else
                    changed |= candidate.FishAlertLeadMinutes.Remove(id);
            }
            pendingFishAlerts.Clear();
            while (pendingPersonal.TryDequeue(out var edit))
            {
                if (edit.Session != view.Session || edit.RestoreRevision != view.SettingsRestoreRevision) continue;
                var record = candidate.PersonalRecords.GetValueOrDefault(edit.FishId) ?? new();
                if (edit.Kind == PersonalEditKind.ApplyPreset)
                {
                    var preset = candidate.Timer.Presets.FirstOrDefault(value => value.Id == record.TimerPresetId);
                    if (preset is null) { personalResult = new(edit.FishId, new(T.FishPresetMissing)); continue; }
                    changed |= !candidate.Timer.Alerts.SequenceEqual(preset.Alerts);
                    candidate.Timer = candidate.Timer with { Alerts = [.. preset.Alerts] };
                    personalResult = new(edit.FishId, new(T.FishPresetApplied, preset.Name));
                    continue;
                }
                if (edit.Kind == PersonalEditKind.Note && record.Note != edit.ExpectedNote && record.Note != edit.Note)
                {
                    personalResult = new(edit.FishId, new(T.FishNoteConflict));
                    continue;
                }
                if (edit.Kind == PersonalEditKind.Preset && edit.PresetId is { } presetId && !candidate.Timer.Presets.Any(value => value.Id == presetId))
                {
                    personalResult = new(edit.FishId, new(T.FishPresetMissing));
                    continue;
                }
                var updated = edit.Kind switch
                {
                    PersonalEditKind.Note => record with { Note = edit.Note! },
                    PersonalEditKind.Attempts => record with { Attempts = edit.Amount },
                    PersonalEditKind.AttemptsDelta => record with { Attempts = (int)Math.Clamp((long)record.Attempts + edit.Amount, 0, FishPersonalRecord.MaxAttempts) },
                    PersonalEditKind.Preset => record with { TimerPresetId = edit.PresetId },
                    _ => record,
                };
                if (updated == record) continue;
                if (updated.IsEmpty()) changed |= candidate.PersonalRecords.Remove(edit.FishId);
                else if (!candidate.PersonalRecords.ContainsKey(edit.FishId) && candidate.PersonalRecords.Count >= FishPersonalRecord.MaxEntries)
                    personalResult = new(edit.FishId, new(T.FishPersonalLimit, FishPersonalRecord.MaxEntries));
                else { candidate.PersonalRecords[edit.FishId] = updated; changed = true; }
            }
            if (timerEdited) changed |= FishPersonalRecord.RemoveMissingPresets(candidate.PersonalRecords, candidate.Timer);
            while (pending.TryDequeue(out var edit))
            {
                if (!edit.Manual)
                {
                    changed |= edit.Value == true ? candidate.Favorites.Add(edit.FishId) : candidate.Favorites.Remove(edit.FishId);
                    continue;
                }
                if (characterId == 0 || edit.Session != view.Session)
                    continue;
                if (!candidate.Characters.TryGetValue(characterId, out var manual))
                    candidate.Characters[characterId] = manual = [];
                if (edit.Value is { } caught)
                {
                    changed |= !manual.TryGetValue(edit.FishId, out var old) || old != caught;
                    manual[edit.FishId] = caught;
                }
                else
                    changed |= manual.Remove(edit.FishId);
                if (manual.Count == 0)
                    candidate.Characters.Remove(characterId);
            }
            if (!changed && !personalEdited) return;
            var savedChanges = !changed || Save(candidate);
            if (personalEdited) view = view with { PersonalResult = savedChanges ? personalResult : null };
            Publish();
        }
    }

    private void Publish()
    {
        IReadOnlyDictionary<uint, bool> manual = characterId != 0 && configuration.Characters.TryGetValue(characterId, out var saved)
            ? saved : FrozenDictionary<uint, bool>.Empty;
        var backup = characterId != 0 ? configuration.CollectionBackups.GetValueOrDefault(characterId) : null;
        var savedStates = backup?.States;
        var backupChanged = view.SavedCollection.Count != (savedStates?.Count ?? 0) ||
            savedStates is not null && savedStates.Any(pair => !view.SavedCollection.TryGetValue(pair.Key, out var old) || old != pair.Value);
        var favoritesChanged = !view.Favorites.SetEquals(configuration.Favorites);
        var personalChanged = view.PersonalRecords.Count != configuration.PersonalRecords.Count ||
            configuration.PersonalRecords.Any(pair => !view.PersonalRecords.TryGetValue(pair.Key, out var record) || record != pair.Value);
        var manualChanged = view.ManualCaught.Count != manual.Count ||
            manual.Any(pair => !view.ManualCaught.TryGetValue(pair.Key, out var value) || value != pair.Value);
        var catalogChanged = view.CharacterId != characterId || favoritesChanged || manualChanged || backupChanged ||
            !view.Catalog.Matches(configuration.Catalog) || view.Language != configuration.Language;
        var catalogRevision = view.CatalogRevision + (catalogChanged ? 1 : 0);
        view = new(view.Revision + 1, view.Session, characterId != 0, writable,
            favoritesChanged ? configuration.Favorites.ToFrozenSet() : view.Favorites,
            manualChanged ? manual.ToFrozenDictionary() : view.ManualCaught, view.Error)
        {
            CatalogRevision = catalogRevision,
            SettingsRestoreRevision = view.SettingsRestoreRevision,
            SavedCollection = backupChanged ? savedStates?.ToFrozenDictionary() ?? FrozenDictionary<uint, bool>.Empty : view.SavedCollection,
            CollectionSavedUtc = backup?.UpdatedUtc,
            CharacterId = characterId, SpawnAlertsEnabled = configuration.SpawnAlertsEnabled,
            SpawnAlertLeadMinutes = configuration.SpawnAlertLeadMinutes,
            SpawnAlertChannel = (XivChatType)configuration.SpawnAlertChannelId, SpawnAlertSound = configuration.SpawnAlertSound,
            CustomAlertSoundPath = configuration.CustomAlertSoundPath,
            SpawnAlertAudience = configuration.SpawnAlertAudience, RemoveFavoriteOnRegistration = configuration.RemoveFavoriteOnRegistration,
            Catalog = configuration.Catalog.Copy(),
            CatalogPresets = configuration.CatalogPresets.Select(value => value.Copy()).ToArray(),
            CatalogPresetError = view.CatalogPresetError,
            Language = configuration.Language, DeveloperMode = configuration.DeveloperMode,
            UiScalePercent = configuration.UiScalePercent,
            LastSeenChangelogVersion = configuration.LastSeenChangelogVersion,
            Compact = configuration.Compact,
            Schedule = configuration.Schedule,
            Timer = configuration.Timer.Copy(),
            PersonalRecords = personalChanged ? configuration.PersonalRecords.ToFrozenDictionary() : view.PersonalRecords,
            PersonalResult = view.PersonalResult,
            SpotFishHideCaught = configuration.SpotFishHideCaught,
            FishAlertLeadMinutes = configuration.FishAlertLeadMinutes.ToFrozenDictionary(),
            ReservedAlertWindows = characterId != 0 && configuration.AlertWindows.TryGetValue(characterId, out var windows)
                ? windows.ToFrozenDictionary() : FrozenDictionary<uint, long>.Empty,
        };
    }

    private FishPreferencesConfiguration CopyConfiguration() => new()
    {
        Favorites = [.. configuration.Favorites],
        CollectionBackups = configuration.CollectionBackups.ToDictionary(pair => pair.Key, pair => pair.Value.Copy()),
        Characters = configuration.Characters.ToDictionary(pair => pair.Key, pair => new Dictionary<uint, bool>(pair.Value)),
        SpawnAlertsEnabled = configuration.SpawnAlertsEnabled,
        SpawnAlertLeadMinutes = configuration.SpawnAlertLeadMinutes,
        SpawnAlertChannelId = configuration.SpawnAlertChannelId, SpawnAlertSound = configuration.SpawnAlertSound,
        CustomAlertSoundPath = configuration.CustomAlertSoundPath,
        SpawnAlertAudience = configuration.SpawnAlertAudience, RemoveFavoriteOnRegistration = configuration.RemoveFavoriteOnRegistration,
        Catalog = configuration.Catalog.Copy(),
        CatalogPresets = configuration.CatalogPresets.Select(value => value.Copy()).ToList(),
        Language = configuration.Language, DeveloperMode = configuration.DeveloperMode,
        UiScalePercent = configuration.UiScalePercent,
        LastSeenChangelogVersion = configuration.LastSeenChangelogVersion,
        Compact = configuration.Compact,
        Schedule = configuration.Schedule,
        Timer = configuration.Timer.Copy(),
        PersonalRecords = new(configuration.PersonalRecords),
        SpotFishHideCaught = configuration.SpotFishHideCaught,
        FishAlertLeadMinutes = new(configuration.FishAlertLeadMinutes),
        AlertWindows = configuration.AlertWindows.ToDictionary(pair => pair.Key, pair => new Dictionary<uint, long>(pair.Value)),
    };

    private bool Save(FishPreferencesConfiguration candidate)
    {
        try
        {
            pluginInterface.SavePluginConfig(candidate);
            configuration = candidate;
            if (view.Error?.Key == T.PreferencesSaveFailed)
                view = view with { Error = null };
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not save fish preferences; retaining the previous in-memory state.");
            view = view with { Error = new(T.PreferencesSaveFailed) };
            return false;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            pending.Clear();
            pendingPersonal.Clear();
            pendingAlerts = null;
            pendingFishAlerts.Clear();
            pendingCatalog = null;
            pendingPreset = null;
            pendingInterface = null;
            pendingUiScale = null;
            pendingCompact = null;
            pendingSchedule = null;
            pendingTimer = null;
            pendingSpotFishHideCaught = null;
            pendingCustomSoundPath = null;
            pendingChangelogVersion = null;
            pendingRestore?.Completed(false);
            pendingRestore = null;
        }
        framework.Update -= OnUpdate;
    }

    private sealed record Edit(uint FishId, bool? Value, bool Manual, int Session);
    private sealed record PresetEdit(CatalogPreset? Add, Guid Remove);
    private enum PersonalEditKind { Note, Attempts, AttemptsDelta, Preset, ApplyPreset }
    private sealed record PersonalEdit(uint FishId, PersonalEditKind Kind, int Session, int RestoreRevision,
        string? Note = null, string? ExpectedNote = null, int Amount = 0, Guid? PresetId = null);
}
