using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Only explicit button clicks enqueue map flags. No game commands or input simulation.</summary>
internal sealed class FishingMapService : IDisposable
{
    private readonly IGameGui gameGui;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IFramework framework;
    private readonly object gate = new();
    private FishingSpotView? pending;
    private ulong? pendingCharacter;
    private UiMessage? result;
    private bool disposed;

    public FishingMapService(IGameGui gameGui, IClientState clientState, IPlayerState playerState, IFramework framework)
    {
        this.gameGui = gameGui;
        this.clientState = clientState;
        this.playerState = playerState;
        this.framework = framework;
        framework.Update += OnUpdate;
    }

    public UiMessage? Result { get { lock (gate) return result; } }

    public void Request(FishingSpotView spot, ulong? expectedCharacter = null)
    {
        lock (gate)
            if (!disposed)
            {
                pending = spot;
                pendingCharacter = expectedCharacter;
            }
    }

    private void OnUpdate(IFramework _)
    {
        lock (gate)
        {
            if (disposed || pending is not { } spot)
                return;
            pending = null;
            var character = pendingCharacter;
            pendingCharacter = null;
            if (!clientState.IsLoggedIn || (character is { } expected &&
                (!playerState.IsLoaded || playerState.ContentId != expected)))
            {
                result = new(T.MapPlayerRequired);
                return;
            }
            try
            {
                if (spot.MapId == 0 || spot.TerritoryId == 0 || !float.IsFinite(spot.X) || !float.IsFinite(spot.Y))
                    result = new(T.MapUnavailable);
                else
                    result = new(gameGui.OpenMapWithMapLink(new MapLinkPayload(spot.TerritoryId, spot.MapId, spot.X, spot.Y))
                        ? T.MapOpened : T.MapOpenFailed, spot.Name);
            }
            catch (Exception ex)
            {
                result = new(T.MapError, ex.GetType().Name);
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            pending = null;
            pendingCharacter = null;
            framework.Update -= OnUpdate;
        }
    }
}
