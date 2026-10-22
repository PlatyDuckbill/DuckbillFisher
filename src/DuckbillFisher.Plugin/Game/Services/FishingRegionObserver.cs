using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace DuckbillFisher.Plugin;

internal enum FishingRegionState { Unavailable, Ready, ReadFailed }
internal sealed record FishingRegionView(FishingRegionState State, uint TerritoryId = 0, string Name = "");

/// <summary>Samples public location APIs and resolves names on Framework.Update only.</summary>
internal sealed class FishingRegionObserver : IDisposable
{
    private readonly IClientState clientState;
    private readonly ICondition conditions;
    private readonly IDataManager dataManager;
    private readonly IFramework framework;
    private readonly object gate = new();
    private FishingRegionView view = new(FishingRegionState.Unavailable);
    private long retryAt;
    private bool disposed;

    public FishingRegionObserver(IClientState clientState, ICondition conditions, IDataManager dataManager, IFramework framework)
    {
        this.clientState = clientState;
        this.conditions = conditions;
        this.dataManager = dataManager;
        this.framework = framework;
        framework.Update += OnUpdate;
    }

    public FishingRegionView View { get { lock (gate) return view; } }

    private void OnUpdate(IFramework _)
    {
        lock (gate)
        {
            if (disposed) return;
            var territoryId = clientState.IsLoggedIn && !conditions[ConditionFlag.BetweenAreas] &&
                !conditions[ConditionFlag.BetweenAreas51] ? (uint)clientState.TerritoryType : 0;
            if (territoryId == view.TerritoryId &&
                (view.State != FishingRegionState.ReadFailed || Environment.TickCount64 < retryAt))
                return;
            if (territoryId == 0)
            {
                view = new(FishingRegionState.Unavailable);
                return;
            }
            try
            {
                var row = dataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId);
                var name = row?.PlaceName.Value.Name.ExtractText();
                view = row is not null && !string.IsNullOrWhiteSpace(name)
                    ? new(FishingRegionState.Ready, territoryId, name)
                    : new(FishingRegionState.ReadFailed, territoryId);
            }
            catch (Exception)
            {
                view = new(FishingRegionState.ReadFailed, territoryId);
            }
            // A transient sheet failure must not trigger another lookup every frame.
            retryAt = Environment.TickCount64 + 1000;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            framework.Update -= OnUpdate;
            view = new(FishingRegionState.Unavailable);
        }
    }
}
