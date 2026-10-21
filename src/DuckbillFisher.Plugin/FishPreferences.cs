using System.Collections.Frozen;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

// Schema <= 3 serialized this three-value enum. Retained only for migration.
public enum SpawnAlertChannel { System, Say, CrossWorldLinkshell1 }

// Public for Dalamud's configuration serializer. Content IDs stay in local configuration only.
public sealed class FishPreferencesConfiguration : IPluginConfiguration
{
    public const int CurrentVersion = 18;
    public int Version { get; set; } = CurrentVersion;
    public HashSet<uint> Favorites { get; set; } = [];
    public Dictionary<ulong, Dictionary<uint, bool>> Characters { get; set; } = [];
    public bool SpawnAlertsEnabled { get; set; }
    public int SpawnAlertLeadMinutes { get; set; } = SpawnAlertScheduler.DefaultLeadMinutes;
    public SpawnAlertChannel SpawnAlertChannel { get; set; }
    public ushort SpawnAlertChannelId { get; set; } = (ushort)XivChatType.SystemMessage;
    public int SpawnAlertSound { get; set; }
    public string CustomAlertSoundPath { get; set; } = "";
    public SpawnAlertAudience SpawnAlertAudience { get; set; } = SpawnAlertAudience.AllFavorites;
    public bool RemoveFavoriteOnRegistration { get; set; } = true;
    public CatalogSettings Catalog { get; set; } = new();
    public List<CatalogPreset> CatalogPresets { get; set; } = [];
    public UiLanguage Language { get; set; } = UiLanguage.Korean;
    public int UiScalePercent { get; set; } = UiScale.DefaultPercent;
    public bool DeveloperMode { get; set; }
    public string LastSeenChangelogVersion { get; set; } = "";
    public CompactViewSettings Compact { get; set; } = new();
    public SpawnScheduleSettings Schedule { get; set; } = new();
    public Dictionary<uint, int> FishAlertLeadMinutes { get; set; } = [];
    // Last reserved window start per fish/character. Also suppresses repeats after clock rollback.
    public Dictionary<ulong, Dictionary<uint, long>> AlertWindows { get; set; } = [];
}

internal sealed record FishPreferencesView(int Revision, int Session, bool HasCharacter, bool CanEdit,
    IReadOnlySet<uint> Favorites, IReadOnlyDictionary<uint, bool> ManualCaught, UiMessage? Error)
{
    // Only inputs to catalog/collection filtering; alert reservations do not invalidate them.
    public int CatalogRevision { get; init; }
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
    public IReadOnlyDictionary<uint, int> FishAlertLeadMinutes { get; init; } = FrozenDictionary<uint, int>.Empty;
    public int AlertLead(uint id) => FishAlertLeadMinutes.GetValueOrDefault(id, SpawnAlertLeadMinutes);
    internal IReadOnlyDictionary<uint, long> ReservedAlertWindows { get; init; } = FrozenDictionary<uint, long>.Empty;

    public CollectionStatus Collection(uint fishId, FishCollectionView observed) => CollectionFilter.Resolve(
        observed.Session == Session && HasCharacter ? observed.Collection(fishId) : CollectionStatus.Unknown,
        ManualCaught.TryGetValue(fishId, out var caught) ? caught : null);
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
    private (bool Enabled, int LeadMinutes, XivChatType Channel, int Sound, SpawnAlertAudience Audience, bool RemoveOnRegistration)? pendingAlerts;
    private readonly Dictionary<uint, int?> pendingFishAlerts = [];
    private CatalogSettings? pendingCatalog;
    private PresetEdit? pendingPreset;
    private (UiLanguage Language, bool DeveloperMode)? pendingInterface;
    private int? pendingUiScale;
    private CompactViewSettings? pendingCompact;
    private SpawnScheduleSettings? pendingSchedule;
    private string? pendingCustomSoundPath;
    private Version? pendingChangelogVersion;
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
            else if (saved is FishPreferencesConfiguration loaded && IsValid(loaded))
            {
                configuration = loaded;
                if (loaded.Version <= 3)
                    configuration.SpawnAlertChannelId = (ushort)(loaded.SpawnAlertChannel switch
                    {
                        SpawnAlertChannel.Say => XivChatType.Say,
                        SpawnAlertChannel.CrossWorldLinkshell1 => XivChatType.CrossLinkShell1,
                        _ => XivChatType.SystemMessage,
                    });
                // A removed display channel must not invalidate favorites or character records.
                if (!ChatChannelChoices.Supports((XivChatType)configuration.SpawnAlertChannelId))
                    configuration.SpawnAlertChannelId = (ushort)XivChatType.SystemMessage;
                // Schema 8 applies the requested enabled default once; later saved opt-outs are retained.
                if (loaded.Version < 8)
                    configuration.RemoveFavoriteOnRegistration = true;
                configuration.Catalog = loaded.Catalog.Copy();
                // Preserve the previously shared favorite filter once, then let the compact view change independently.
                if (loaded.Version < 15)
                    configuration.Compact = loaded.Compact with { FavoritesOnly = loaded.Catalog.FavoritesOnly };
                // A malformed optional preset must not discard favorites or character records.
                configuration.CatalogPresets = CatalogPreset.ReadValid(loaded.CatalogPresets);
                configuration.CustomAlertSoundPath ??= "";
                configuration.UiScalePercent = UiScale.Normalize(configuration.UiScalePercent);
                if (configuration.Schedule is null || !configuration.Schedule.IsValid()) configuration.Schedule = new();
                // Replace the initial schedule's six-hour default once; retain other deliberate horizons.
                if (loaded.Version < 17 && configuration.Schedule.Hours == 6)
                    configuration.Schedule = configuration.Schedule with { Hours = 3 };
                // Optional display state must not invalidate existing settings or character records.
                if (!System.Version.TryParse(configuration.LastSeenChangelogVersion, out _))
                    configuration.LastSeenChangelogVersion = "";
                configuration.Version = FishPreferencesConfiguration.CurrentVersion;
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
                view = view with { Session = view.Session + 1 };
                Publish();
            }
            else if (stateErrorChanged)
                Publish();
            if (pending.Count == 0 && pendingAlerts is null && pendingFishAlerts.Count == 0 && pendingCatalog is null && pendingPreset is null && pendingInterface is null && pendingUiScale is null && pendingCompact is null && pendingSchedule is null && pendingCustomSoundPath is null && pendingChangelogVersion is null)
                return;
            var candidate = CopyConfiguration();
            var changed = false;
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
            if (!changed)
                return;
            Save(candidate);
            Publish();
        }
    }

    private void Publish()
    {
        IReadOnlyDictionary<uint, bool> manual = characterId != 0 && configuration.Characters.TryGetValue(characterId, out var saved)
            ? saved : FrozenDictionary<uint, bool>.Empty;
        var favoritesChanged = !view.Favorites.SetEquals(configuration.Favorites);
        var manualChanged = view.ManualCaught.Count != manual.Count ||
            manual.Any(pair => !view.ManualCaught.TryGetValue(pair.Key, out var value) || value != pair.Value);
        var catalogChanged = view.CharacterId != characterId || favoritesChanged || manualChanged ||
            !view.Catalog.Matches(configuration.Catalog) || view.Language != configuration.Language;
        var catalogRevision = view.CatalogRevision + (catalogChanged ? 1 : 0);
        view = new(view.Revision + 1, view.Session, characterId != 0, writable,
            favoritesChanged ? configuration.Favorites.ToFrozenSet() : view.Favorites,
            manualChanged ? manual.ToFrozenDictionary() : view.ManualCaught, view.Error)
        {
            CatalogRevision = catalogRevision,
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
            FishAlertLeadMinutes = configuration.FishAlertLeadMinutes.ToFrozenDictionary(),
            ReservedAlertWindows = characterId != 0 && configuration.AlertWindows.TryGetValue(characterId, out var windows)
                ? windows.ToFrozenDictionary() : FrozenDictionary<uint, long>.Empty,
        };
    }

    private FishPreferencesConfiguration CopyConfiguration() => new()
    {
        Favorites = [.. configuration.Favorites],
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

    private static bool IsValid(FishPreferencesConfiguration value) =>
        value.Version is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or FishPreferencesConfiguration.CurrentVersion && value.Favorites is not null &&
        Enum.IsDefined(value.Language) &&
        value.Compact is not null && value.Compact.IsValid() &&
        value.Catalog is not null && value.Catalog.IsValid() &&
        !value.Favorites.Contains(0) && value.Characters is not null &&
        value.Characters.All(pair => pair.Key != 0 && pair.Value is not null && !pair.Value.ContainsKey(0)) &&
        value.SpawnAlertLeadMinutes is >= SpawnAlertScheduler.MinLeadMinutes and <= SpawnAlertScheduler.MaxLeadMinutes &&
        (value.Version <= 3 ? Enum.IsDefined(value.SpawnAlertChannel) : ChatChannelChoices.IsKnown((XivChatType)value.SpawnAlertChannelId)) &&
        (value.SpawnAlertSound is >= 0 and <= 16 || value.Version >= 12 && value.SpawnAlertSound == CustomAlertSound.Choice) &&
        Enum.IsDefined(value.SpawnAlertAudience) &&
        value.FishAlertLeadMinutes is not null && value.FishAlertLeadMinutes.All(pair => pair.Key != 0 &&
            pair.Value is >= SpawnAlertScheduler.MinLeadMinutes and <= SpawnAlertScheduler.MaxLeadMinutes) &&
        value.AlertWindows is not null && value.AlertWindows.All(pair => pair.Key != 0 && pair.Value is not null &&
            pair.Value.All(window => window.Key != 0 && window.Value is > 0 and <= 253402300799999));

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            pending.Clear();
            pendingAlerts = null;
            pendingFishAlerts.Clear();
            pendingCatalog = null;
            pendingPreset = null;
            pendingInterface = null;
            pendingUiScale = null;
            pendingCompact = null;
            pendingSchedule = null;
            pendingCustomSoundPath = null;
            pendingChangelogVersion = null;
        }
        framework.Update -= OnUpdate;
    }

    private sealed record Edit(uint FishId, bool? Value, bool Manual, int Session);
    private sealed record PresetEdit(CatalogPreset? Add, Guid Remove);
}
