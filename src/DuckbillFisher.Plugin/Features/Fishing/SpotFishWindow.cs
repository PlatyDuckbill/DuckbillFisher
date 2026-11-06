using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class SpotFishWindow : Window
{
    private readonly FishingTracker tracker;
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly Action<FishGuideRow> showDetail;
    private readonly FishRowRenderer rows;
    private readonly OceanFishing ocean;
    private uint lastOceanSpot;
    private OceanFishListState lastOceanState;
    private bool wasOcean;
    private bool narrowOcean = true;
    private bool lastNarrowOcean;

    public SpotFishWindow(FishingTracker tracker, FishCatalog catalog, FishPreferences preferences,
        FishCollectionObserver collection, ITextureProvider textures, FishingMapService maps, Action<FishGuideRow> showDetail,
        OceanFishing ocean)
        : base("DuckbillFisher###DuckbillFisher.SpotFish")
    {
        this.tracker = tracker; this.catalog = catalog; this.preferences = preferences;
        this.collection = collection; this.showDetail = showDetail;
        this.ocean = ocean;
        rows = new(textures, maps);
        Size = new Vector2(560, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(340, 280), MaximumSize = new Vector2(float.MaxValue) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        BgAlpha = UiWidgets.BackgroundAlpha;
    }

    public override void PreDraw() => WindowName = $"{UiText.Get(ocean.View.IsOceanTerritory || tracker.View.State == FishingTrackerState.OceanUnsupported
        ? T.OceanFishTitle : T.TimerSpotFish)}###DuckbillFisher.SpotFish";
    public override void OnOpen() => ocean.SetActive(OceanConsumer.FishList, true);
    public override void OnClose() => ocean.SetActive(OceanConsumer.FishList, false);

    public override void Draw()
    {
        var view = tracker.View;
        var personal = preferences.View;
        var voyage = ocean.View;
        var isOcean = voyage.IsOceanTerritory || view.State == FishingTrackerState.OceanUnsupported;
        var oceanFish = voyage.FishList;
        var observed = collection.View;
        var hideCaught = personal.SpotFishHideCaught;
        ImGui.TextWrapped(isOcean ? UiWidgets.OceanSpotLabel(oceanFish) : UiWidgets.SpotLabel(view));
        if (isOcean)
        {
            if (ImGui.Checkbox(UiText.Label(T.OceanFishFilter, "OceanConditionFilter"), ref narrowOcean)) ImGui.SetScrollY(0);
            UiWidgets.Tooltip(UiText.Get(T.OceanFishConditionsHint));
            if (oceanFish.State == OceanFishListState.Ready)
            {
                ImGui.TextWrapped(UiText.Get(T.OceanFishObserved,
                    UiText.Get(oceanFish.Time switch { 1 => T.OceanTimeNight, 2 => T.OceanTimeDay, 3 => T.OceanTimeSunset, _ => T.ConditionUnknown }),
                    oceanFish.Weather.Length > 0 ? oceanFish.Weather : UiText.Get(T.ConditionUnknown),
                    UiText.Get(oceanFish.Intuition switch { true => T.OceanIntuitionPresent, false => T.OceanIntuitionAbsent, _ => T.OceanIntuitionUnknown })));
                UiWidgets.Tooltip(UiText.Get(T.OceanFishConditionsHint));
                if (!oceanFish.ConditionsReady) ImGui.TextWrapped(UiText.Get(T.OceanConditionsUnavailable));
            }
        }
        ImGui.BeginDisabled(!personal.CanEdit);
        if (ImGui.Button(UiText.Label(hideCaught ? T.TimerHideCaught : T.TimerAllFish, "TimerCollectionFilter")))
        {
            hideCaught = !hideCaught;
            preferences.SetSpotFishHideCaught(hideCaught);
        }
        ImGui.EndDisabled();
        var oceanRows = narrowOcean ? oceanFish.Candidates : oceanFish.Rows;
        var matches = isOcean
            ? hideCaught ? oceanRows.Where(f => personal.Collection(f.FishParameterId, observed) != CollectionStatus.Caught).ToArray() : oceanRows
            : hideCaught ? view.RemainingSpotFish : view.AllSpotFish;
        UiWidgets.FlowNext(ImGui.CalcTextSize(UiText.Get(T.TimerFishCount, matches.Length)).X,
            ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(UiText.Get(T.TimerFishCount, matches.Length));
        if (isOcean && narrowOcean)
        {
            var unknown = matches.Count(f => oceanFish.Unconfirmed.Contains(f.FishParameterId));
            if (unknown > 0) ImGui.TextWrapped(UiText.Get(T.OceanFishUnconfirmedCount, unknown));
        }
        if (ImGui.BeginChild("SpotFishRows"))
        {
            if (wasOcean != isOcean || isOcean && (lastOceanSpot != oceanFish.FishingSpotId || lastOceanState != oceanFish.State || lastNarrowOcean != narrowOcean))
                ImGui.SetScrollY(0);
            wasOcean = isOcean; lastOceanSpot = oceanFish.FishingSpotId; lastOceanState = oceanFish.State;
            lastNarrowOcean = narrowOcean;
            if (matches.Length == 0 && (!isOcean || oceanFish.State == OceanFishListState.Ready))
                ImGui.TextWrapped(isOcean || view.Spot is not null ? UiText.Get(T.TimerNoFish) : UiWidgets.SpotLabel(view));
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                var height = FishRowRenderer.GetHeight(ImGui.GetContentRegionAvail().X);
                clipper.Begin(matches.Length, height + ImGui.GetStyle().ItemSpacing.Y);
                var snapshot = catalog.View;
                var now = DateTimeOffset.UtcNow;
                while (clipper.Step())
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        var fish = matches[i];
                        var forecast = isOcean ? null : snapshot.Windows.GetValueOrDefault(fish.FishParameterId);
                        var otherRegion = !isOcean && fish.Details?.Spot is { } source && source.TerritoryId != view.Spot?.TerritoryId;
                        if (rows.Draw(fish, height, false, UiText.Get(FishRowRenderer.CollectionKey(personal, observed, fish.FishParameterId)),
                            isOcean ? narrowOcean && oceanFish.Unconfirmed.Contains(fish.FishParameterId)
                                ? UiText.Get(T.OceanFishUnconfirmedRow, oceanFish.Area) : oceanFish.Area
                                : view.Spot?.Name ?? "", forecast, now, otherRegion, personal.Favorites.Contains(fish.FishParameterId),
                            compact: true, spot: isOcean ? null : view.Spot, canOpenMap: personal.HasCharacter,
                            collectionStatus: personal.Collection(fish.FishParameterId, observed)))
                            showDetail(fish);
                    }
            }
            finally { clipper.Destroy(); }
        }
        ImGui.EndChild();
    }
}
