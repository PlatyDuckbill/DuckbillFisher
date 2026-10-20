using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
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
    private bool hideCaught = true;

    public SpotFishWindow(FishingTracker tracker, FishCatalog catalog, FishPreferences preferences,
        FishCollectionObserver collection, ITextureProvider textures, FishingMapService maps, Action<FishGuideRow> showDetail)
        : base("DuckbillFisher###DuckbillFisher.SpotFish")
    {
        this.tracker = tracker; this.catalog = catalog; this.preferences = preferences;
        this.collection = collection; this.showDetail = showDetail;
        rows = new(textures, maps);
        Size = new Vector2(560, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(340, 280), MaximumSize = new Vector2(float.MaxValue) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        BgAlpha = UiWidgets.BackgroundAlpha;
    }

    public override void PreDraw() => WindowName = $"{UiText.Get(T.TimerSpotFish)}###DuckbillFisher.SpotFish";

    public override void Draw()
    {
        var view = tracker.View;
        ImGui.TextWrapped(view.Spot is { } spot ? view.IsLastSpot ? UiText.Get(T.TimerLastSpot, spot.Name) : spot.Name : UiText.Get(T.TimerSpotPending));
        if (ImGui.Button(UiText.Label(hideCaught ? T.TimerHideCaught : T.TimerAllFish, "TimerCollectionFilter"))) hideCaught = !hideCaught;
        var matches = hideCaught ? view.RemainingSpotFish : view.AllSpotFish;
        ImGui.SameLine();
        ImGui.TextDisabled(UiText.Get(T.TimerFishCount, matches.Length));
        if (ImGui.BeginChild("SpotFishRows"))
        {
            if (matches.Length == 0) ImGui.TextWrapped(UiText.Get(view.Spot is null ? T.TimerSpotPending : T.TimerNoFish));
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                var height = FishRowRenderer.GetHeight(ImGui.GetContentRegionAvail().X);
                clipper.Begin(matches.Length, height + ImGui.GetStyle().ItemSpacing.Y);
                var personal = preferences.View;
                var observed = collection.View;
                var snapshot = catalog.View;
                var now = DateTimeOffset.UtcNow;
                while (clipper.Step())
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        var fish = matches[i];
                        var forecast = snapshot.Windows.GetValueOrDefault(fish.FishParameterId);
                        var otherRegion = fish.Details?.Spot is { } source && source.TerritoryId != view.Spot?.TerritoryId;
                        if (rows.Draw(fish, height, false, UiText.Get(FishRowRenderer.CollectionKey(personal, observed, fish.FishParameterId)),
                            view.Spot?.Name ?? "", forecast, now, otherRegion, personal.Favorites.Contains(fish.FishParameterId),
                            compact: true, spot: view.Spot, canOpenMap: personal.HasCharacter,
                            collectionStatus: personal.Collection(fish.FishParameterId, observed)))
                            showDetail(fish);
                    }
            }
            finally { clipper.Destroy(); }
        }
        ImGui.EndChild();
    }
}
