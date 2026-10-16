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
    public const int CurrentVersion = 7;
    public int Version { get; set; } = CurrentVersion;
    public HashSet<uint> Favorites { get; set; } = [];
    public Dictionary<ulong, Dictionary<uint, bool>> Characters { get; set; } = [];
    public bool SpawnAlertsEnabled { get; set; }
    public int SpawnAlertLeadMinutes { get; set; } = SpawnAlertScheduler.DefaultLeadMinutes;
    public SpawnAlertChannel SpawnAlertChannel { get; set; }
    public ushort SpawnAlertChannelId { get; set; } = (ushort)XivChatType.SystemMessage;
    public int SpawnAlertSound { get; set; }
    public SpawnAlertAudience SpawnAlertAudience { get; set; } = SpawnAlertAudience.AllFavorites;
    public bool RemoveFavoriteOnRegistration { get; set; }
    public Dictionary<uint, int> FishAlertLeadMinutes { get; set; } = [];
    // Last reserved window start per fish/character. Also suppresses repeats after clock rollback.
    public Dictionary<ulong, Dictionary<uint, long>> AlertWindows { get; set; } = [];
}

internal sealed record FishPreferencesView(int Revision, int Session, bool HasCharacter, bool CanEdit,
    IReadOnlySet<uint> Favorites, IReadOnlyDictionary<uint, bool> ManualCaught, UiMessage? Error)
{
    internal ulong CharacterId { get; init; }
    public bool SpawnAlertsEnabled { get; init; }
    public int SpawnAlertLeadMinutes { get; init; } = SpawnAlertScheduler.DefaultLeadMinutes;
    public XivChatType SpawnAlertChannel { get; init; } = XivChatType.SystemMessage;
    public int SpawnAlertSound { get; init; }
    public SpawnAlertAudience SpawnAlertAudience { get; init; } = SpawnAlertAudience.AllFavorites;
    public bool RemoveFavoriteOnRegistration { get; init; }
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

    public void SetFavorite(uint fishId, bool favorite)
    {
        lock (gate)
            if (!disposed && writable && fishId != 0)
                pending.Enqueue(new(fishId, favorite, false, view.Session));
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
                    ChatChannelChoices.Supports(channel) ? channel : XivChatType.SystemMessage, Math.Clamp(sound, 0, 16),
                    Enum.IsDefined(audience) ? audience : SpawnAlertAudience.AllFavorites, removeOnRegistration);
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
            if (pending.Count == 0 && pendingAlerts is null && pendingFishAlerts.Count == 0)
                return;
            var candidate = CopyConfiguration();
            var changed = false;
            if (pendingAlerts is { } alertSettings)
            {
                changed = candidate.SpawnAlertsEnabled != alertSettings.Enabled || candidate.SpawnAlertLeadMinutes != alertSettings.LeadMinutes ||
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
        var manual = characterId != 0 && configuration.Characters.TryGetValue(characterId, out var saved)
            ? saved.ToFrozenDictionary() : FrozenDictionary<uint, bool>.Empty;
        view = new(view.Revision + 1, view.Session, characterId != 0, writable,
            configuration.Favorites.ToFrozenSet(), manual, view.Error)
        {
            CharacterId = characterId, SpawnAlertsEnabled = configuration.SpawnAlertsEnabled,
            SpawnAlertLeadMinutes = configuration.SpawnAlertLeadMinutes,
            SpawnAlertChannel = (XivChatType)configuration.SpawnAlertChannelId, SpawnAlertSound = configuration.SpawnAlertSound,
            SpawnAlertAudience = configuration.SpawnAlertAudience, RemoveFavoriteOnRegistration = configuration.RemoveFavoriteOnRegistration,
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
        SpawnAlertAudience = configuration.SpawnAlertAudience, RemoveFavoriteOnRegistration = configuration.RemoveFavoriteOnRegistration,
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
        value.Version is 1 or 2 or 3 or 4 or 5 or 6 or FishPreferencesConfiguration.CurrentVersion && value.Favorites is not null &&
        !value.Favorites.Contains(0) && value.Characters is not null &&
        value.Characters.All(pair => pair.Key != 0 && pair.Value is not null && !pair.Value.ContainsKey(0)) &&
        value.SpawnAlertLeadMinutes is >= SpawnAlertScheduler.MinLeadMinutes and <= SpawnAlertScheduler.MaxLeadMinutes &&
        (value.Version <= 3 ? Enum.IsDefined(value.SpawnAlertChannel) : ChatChannelChoices.IsKnown((XivChatType)value.SpawnAlertChannelId)) &&
        value.SpawnAlertSound is >= 0 and <= 16 &&
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
        }
        framework.Update -= OnUpdate;
    }

    private sealed record Edit(uint FishId, bool? Value, bool Manual, int Session);
}
