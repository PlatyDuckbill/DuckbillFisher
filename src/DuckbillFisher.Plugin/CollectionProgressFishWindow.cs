using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>One persistent list for the clicked expansion, region, or ocean scope. No all-catalog selection.</summary>
internal sealed class CollectionProgressFishWindow : Window
{
    private readonly CollectionProgress progress;
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private FishRowRenderer rows;
    private readonly ITextureProvider textures;
    private readonly FishingMapService maps;
    private readonly Action<FishGuideRow> showDetail;
    private CollectionProgressSelection? requested;
    private bool hideCaught = true;

    public CollectionProgressFishWindow(CollectionProgress progress, FishCatalog catalog, FishPreferences preferences,
        FishCollectionObserver collection, ITextureProvider textures, FishingMapService maps, Action<FishGuideRow> showDetail)
        : base("DuckbillFisher###DuckbillFisher.CollectionFish")
    {
        this.progress = progress; this.catalog = catalog; this.preferences = preferences;
        this.collection = collection; this.showDetail = showDetail;
        this.textures = textures; this.maps = maps;
        rows = new(textures, maps);
        Size = new Vector2(560, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(340, 280), MaximumSize = new Vector2(float.MaxValue) };
        BgAlpha = UiWidgets.BackgroundAlpha;
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public void Show(CollectionProgressSelection target)
    {
        // Row summaries belong only to the current scope; visiting expansions must not accumulate caches.
        if (requested != target) rows = new(textures, maps);
        requested = target;
        progress.Select(target, preferences.View.Session);
        IsOpen = true;
    }

    public override void PreDraw() => WindowName = $"{UiText.Get(T.CollectionProgressFishTitle)}###DuckbillFisher.CollectionFish";

    public override void OnClose()
    {
        progress.ClearSelection();
        requested = null;
        rows = new(textures, maps);
    }

    public override void Draw()
    {
        var personal = preferences.View;
        var view = progress.FishView;
        if (view.Session != personal.Session || view.Selection != requested || view.Name is null)
        {
            ImGui.TextWrapped(UiText.Get(T.CollectionProgressWaiting));
            return;
        }
        var gradeKey = requested?.Grade switch
        {
            FishGradeVisibility.BigIncludingLegendary => T.CollectionProgressBig,
            FishGradeVisibility.LegendaryOnly => T.CollectionProgressLegendary,
            _ => T.CollectionProgressAll,
        };
        ImGui.TextWrapped(UiText.Get(T.CollectionProgressListScope, view.Name, new UiMessage(gradeKey)));
        if (ImGui.Button(UiText.Label(hideCaught ? T.TimerHideCaught : T.TimerAllFish, "CollectionProgressListFilter"))) hideCaught = !hideCaught;
        UiWidgets.Tooltip(UiText.Get(T.CollectionProgressListHint));
        var matches = hideCaught ? view.RemainingFish : view.AllFish;
        ImGui.SameLine();
        ImGui.TextDisabled(UiText.Get(T.TimerFishCount, matches.Length));
        if (ImGui.BeginChild("CollectionProgressFishRows"))
        {
            if (matches.Length == 0) ImGui.TextWrapped(UiText.Get(T.TimerNoFish));
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                var height = FishRowRenderer.GetHeight(ImGui.GetContentRegionAvail().X);
                clipper.Begin(matches.Length, height + ImGui.GetStyle().ItemSpacing.Y);
                var observed = collection.View;
                var snapshot = catalog.View;
                var now = DateTimeOffset.UtcNow;
                while (clipper.Step())
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        var fish = matches[i];
                        var spot = fish.Spots.FirstOrDefault(spot => requested?.Area == CollectionProgressArea.Region
                            ? spot.TerritoryId == requested.RegionId && spot.ExpansionId == requested.ExpansionId
                            : spot.ExpansionId == requested?.ExpansionId);
                        var forecast = snapshot.Windows.GetValueOrDefault(fish.FishParameterId);
                        if (rows.Draw(fish, height, false, UiText.Get(FishRowRenderer.CollectionKey(personal, observed, fish.FishParameterId)),
                            spot?.Zone ?? fish.Ocean?.Area ?? "", forecast, now,
                            usesOtherRegion: spot is not null && fish.Details?.Spot?.TerritoryId != spot.TerritoryId,
                            favorite: personal.Favorites.Contains(fish.FishParameterId), compact: true, spot: spot,
                            canOpenMap: personal.HasCharacter, collectionStatus: personal.Collection(fish.FishParameterId, observed)))
                            showDetail(fish);
                    }
            }
            finally { clipper.Destroy(); }
        }
        ImGui.EndChild();
    }
}
