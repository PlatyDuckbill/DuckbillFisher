using System.Collections.Frozen;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

// Public for Dalamud's configuration serializer. Content IDs stay in local configuration only.
public sealed class FishPreferencesConfiguration : IPluginConfiguration
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public HashSet<uint> Favorites { get; set; } = [];
    public Dictionary<ulong, Dictionary<uint, bool>> Characters { get; set; } = [];
}

internal sealed record FishPreferencesView(int Revision, int Session, bool HasCharacter, bool CanEdit,
    IReadOnlySet<uint> Favorites, IReadOnlyDictionary<uint, bool> ManualCaught, UiMessage? Error)
{
    // Automatic collection observations will be connected separately after readiness validation.
    public CollectionStatus Collection(uint fishId) => CollectionFilter.Resolve(CollectionStatus.Unknown,
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
            if (pending.Count == 0)
                return;
            var candidate = new FishPreferencesConfiguration
            {
                Favorites = [.. configuration.Favorites],
                Characters = configuration.Characters.ToDictionary(pair => pair.Key,
                    pair => new Dictionary<uint, bool>(pair.Value)),
            };
            var changed = false;
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
            try
            {
                pluginInterface.SavePluginConfig(candidate);
                configuration = candidate;
                if (view.Error?.Key == T.PreferencesSaveFailed)
                    view = view with { Error = null };
            }
            catch (Exception ex)
            {
                log.Error(ex, "Could not save fish preferences; retaining the previous in-memory state.");
                view = view with { Error = new(T.PreferencesSaveFailed) };
            }
            Publish();
        }
    }

    private void Publish()
    {
        var manual = characterId != 0 && configuration.Characters.TryGetValue(characterId, out var saved)
            ? saved.ToFrozenDictionary() : FrozenDictionary<uint, bool>.Empty;
        view = new(view.Revision + 1, view.Session, characterId != 0, writable,
            configuration.Favorites.ToFrozenSet(), manual, view.Error);
    }

    private static bool IsValid(FishPreferencesConfiguration value) =>
        value.Version == FishPreferencesConfiguration.CurrentVersion && value.Favorites is not null &&
        !value.Favorites.Contains(0) && value.Characters is not null &&
        value.Characters.All(pair => pair.Key != 0 && pair.Value is not null && !pair.Value.ContainsKey(0));

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            pending.Clear();
        }
        framework.Update -= OnUpdate;
    }

    private sealed record Edit(uint FishId, bool? Value, bool Manual, int Session);
}
