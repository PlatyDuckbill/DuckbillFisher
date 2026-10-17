using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class FishDetailWindow : Window
{
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly FishDetailPanel panel;
    private uint? selectedId;
    private FishGuideRow? selected;
    private IReadOnlyList<FishGuideRow>? lastRows;
    private int session;
    private bool resetScroll;

    public FishDetailWindow(FishCatalog catalog, FishPreferences preferences, FishCollectionObserver collection,
        FishDetailPanel panel) : base("DuckbillFisher###DuckbillFisher.Detail")
    {
        this.catalog = catalog;
        this.preferences = preferences;
        this.collection = collection;
        this.panel = panel;
        Size = new Vector2(420, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(320, 300), MaximumSize = new Vector2(float.MaxValue) };
    }

    public void Show(FishGuideRow fish)
    {
        selectedId = fish.FishParameterId;
        selected = fish;
        lastRows = catalog.View.Rows;
        session = preferences.View.Session;
        resetScroll = true;
        IsOpen = true;
    }

    public override void PreDraw() => WindowName = $"{UiText.Get(T.DetailWindowTitle)}###DuckbillFisher.Detail";

    public override void Draw()
    {
        var personal = preferences.View;
        var view = catalog.View;
        if (personal.Session != session)
        {
            selectedId = null;
            selected = null;
            session = personal.Session;
        }
        if (!ReferenceEquals(lastRows, view.Rows))
        {
            lastRows = view.Rows;
            selected = selectedId is { } id ? view.Rows.FirstOrDefault(fish => fish.FishParameterId == id) : null;
        }
        if (resetScroll) { ImGui.SetScrollY(0); resetScroll = false; }
        UiWidgets.GuideBanner(collection.View, personal);
        panel.Draw(selected, view, personal, collection.View);
    }
}
