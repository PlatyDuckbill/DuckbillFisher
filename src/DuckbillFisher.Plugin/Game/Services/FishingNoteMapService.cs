using System.Globalization;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace DuckbillFisher.Plugin;

internal sealed record FishingNoteSpot(uint Id, ushort Order);
internal sealed record FishingNoteArea(uint MapId, FishingNoteSpot[] Spots);

/// <summary>Only an explicit left click on an undiscovered notebook hole requests an existing public map link.</summary>
internal sealed class FishingNoteMapService : IDisposable
{
    private readonly IAddonLifecycle lifecycle;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IDataManager data;
    private readonly FishCatalog catalog;
    private readonly FishingMapService maps;
    private readonly NativeFishingNoteReader reader;
    private readonly IPluginLog log;
    private IReadOnlyDictionary<(uint Region, uint Area), FishingNoteArea> areas =
        new Dictionary<(uint, uint), FishingNoteArea>();
    private FishingNoteMapReading last = new(FishingNoteMapState.NotSampled);
    private bool indexLoaded;
    private bool indexFailed;
    private int clicks;
    private bool disposed;

    public FishingNoteMapService(IAddonLifecycle lifecycle, IFramework framework, IClientState client,
        IPlayerState player, IDataManager data, IGameGui gui, FishCatalog catalog, FishingMapService maps, IPluginLog log)
    {
        this.lifecycle = lifecycle;
        this.framework = framework;
        this.client = client;
        this.player = player;
        this.data = data;
        this.catalog = catalog;
        this.maps = maps;
        this.log = log;
        reader = new(gui, framework);
        framework.Update += OnUpdate;
        lifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "FishingNote", OnReceive);
    }

    private void OnUpdate(IFramework _)
    {
        if (disposed || indexLoaded || indexFailed) return;
        try
        {
            // Same grouping as the notebook cache: explicit sheet overrides, otherwise territory names.
            // Include unmappable entries in ordinal counts so they cannot shift later fishing holes.
            var sheet = data.GetExcelSheet<FishingSpot>();
            if (sheet is null) return;
            areas = sheet.Where(s => s.TerritoryType.RowId != 0)
                .GroupBy(s => (Region: s.PlaceNameMain.RowId != 0 ? s.PlaceNameMain.RowId : s.TerritoryType.Value.PlaceNameRegion.RowId,
                    Area: s.PlaceNameSub.RowId != 0 ? s.PlaceNameSub.RowId : s.TerritoryType.Value.PlaceName.RowId))
                .ToDictionary(g => g.Key, g => new FishingNoteArea(
                    g.Select(s => s.TerritoryType.Value.Map.RowId).Distinct().Count() == 1 &&
                    g.Select(s => s.Order).Distinct().Count() == g.Count() ? g.First().TerritoryType.Value.Map.RowId : 0,
                    g.OrderBy(s => s.Order).Select(s => new FishingNoteSpot(s.RowId, s.Order)).ToArray()));
            indexLoaded = true;
        }
        catch (Exception ex)
        {
            indexFailed = true;
            last = new(FishingNoteMapState.DataUnavailable);
            log.Warning("FishingNoteMap index unavailable: {Reason}", ex.GetType().Name);
        }
    }

    private void OnReceive(AddonEvent _, AddonArgs args)
    {
        if (disposed || args is not AddonReceiveEventArgs receive ||
            receive.AtkEventType != AddonEventType.ListItemClick || receive.EventParam != 1) return;
        clicks++;
        if (!framework.IsInFrameworkUpdateThread) { last = new(FishingNoteMapState.WrongThread); return; }
        if (!client.IsLoggedIn || !player.IsLoaded || player.ContentId == 0)
        { last = new(FishingNoteMapState.PlayerUnavailable); return; }
        if (!indexLoaded) { last = new(indexFailed ? FishingNoteMapState.DataUnavailable : FishingNoteMapState.DataWaiting); return; }
        var character = player.ContentId;
        // Native reads deliberately stay outside managed exception handling.
        last = reader.Read(receive, character, areas);
        if (last.State != FishingNoteMapState.Undiscovered) return;
        if (!catalog.View.Spots.TryGetValue(last.SpotId, out var spot))
        {
            catalog.RequestLoad();
            last = last with { State = catalog.View.Rows.Count == 0 ? FishingNoteMapState.DataWaiting : FishingNoteMapState.MapUnavailable };
            return;
        }
        maps.Request(spot, character);
        last = last with { State = FishingNoteMapState.Requested };
    }

    public string CreateReport()
    {
        var sample = last;
        return string.Create(CultureInfo.InvariantCulture,
            $"FishingNoteMap/v2: LastState={sample.State}; Check={sample.Check}; Clicks={clicks}; IndexLoaded={indexLoaded}; Areas={areas.Count}; SpotId={sample.SpotId}; AreaId={sample.AreaId}; Ordinal={sample.Ordinal}; Flags={sample.Flags}; ItemCount={sample.ItemCount}; ComponentNodes={sample.ComponentNodes}; ComponentCapacity={sample.ComponentCapacity}; ComponentKind={sample.ComponentKind}");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, "FishingNote", OnReceive);
        framework.Update -= OnUpdate;
    }
}
