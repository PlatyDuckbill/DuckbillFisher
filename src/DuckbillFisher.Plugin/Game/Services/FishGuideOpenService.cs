using System.Diagnostics;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Explicit guide-opening clicks only. No chat commands, automation, or collection inference.</summary>
internal sealed class FishGuideOpenService : IDisposable
{
    private readonly IGameGui gameGui;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IFramework framework;
    private readonly FishPreferences preferences;
    private readonly FishCatalog catalog;
    private readonly NativeFishCollectionReader compatibility;
    private readonly object gate = new();
    private (int Session, ulong Character, long At)? pending;
    private UiMessage? result;
    private int resultSession;
    private bool disposed;

    public FishGuideOpenService(IGameGui gameGui, IClientState clientState, IPlayerState playerState,
        IFramework framework, FishPreferences preferences, FishCatalog catalog, NativeFishCollectionReader compatibility)
    {
        this.gameGui = gameGui;
        this.clientState = clientState;
        this.playerState = playerState;
        this.framework = framework;
        this.preferences = preferences;
        this.catalog = catalog;
        this.compatibility = compatibility;
        framework.Update += OnUpdate;
    }

    public bool IsSupported => compatibility.IsCompatible;
    public UiMessage CompatibilityReason => compatibility.Compatibility;

    public UiMessage? Failure(int session)
    {
        lock (gate) return session == resultSession ? result : null;
    }

    public void Request(FishPreferencesView personal)
    {
        lock (gate)
        {
            if (disposed || !personal.HasCharacter) return;
            pending ??= (personal.Session, personal.CharacterId, Stopwatch.GetTimestamp());
            result = null;
        }
    }

    private void OnUpdate(IFramework _)
    {
        lock (gate)
        {
            if (disposed || pending is not { } request) return;
            pending = null;
            var personal = preferences.View;
            if (Stopwatch.GetElapsedTime(request.At) > TimeSpan.FromSeconds(2) ||
                !personal.HasCharacter || personal.Session != request.Session || personal.CharacterId != request.Character)
                return;
            resultSession = personal.Session;
            var fish = catalog.View.Rows.FirstOrDefault(NativeFishCollectionReader.Supports);
            if (fish is null)
            {
                catalog.RequestLoad();
                result = new(T.GuideOpenDataWaiting);
                return;
            }
            result = Open(fish.ItemId, request.Character);
        }
    }

    private unsafe UiMessage? Open(uint itemId, ulong expectedCharacter)
    {
        if (!compatibility.IsCompatible) return compatibility.Compatibility;
        if (!framework.IsInFrameworkUpdateThread) return new(T.NativeWrongThread);
        if (!clientState.IsLoggedIn || !playerState.IsLoaded || playerState.ContentId != expectedCharacter)
            return new(T.NativePlayerRequired);

        // Installed KR module/source and the executable's unique signature match are recorded
        // in MVP-01-GUIDE-OPEN.md. Never retain the borrowed agent or write its fields.
        var agent = gameGui.GetAgentById((int)AgentId.FishGuide);
        if (agent.IsNull || AgentFishGuide.MemberFunctionPointers.OpenForItemId == null)
            return new(T.GuideOpenUnavailable);
        var guide = (AgentFishGuide*)agent.Address;
        if (agent.IsAddonShown && guide->Mode == 0) agent.FocusAddon();
        else guide->OpenForItemId(itemId, false);
        // Opening is not proof of receipt; the existing full-guide observer decides readiness.
        return null;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            pending = null;
        }
        framework.Update -= OnUpdate;
    }
}
