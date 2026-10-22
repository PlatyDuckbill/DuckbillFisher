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
    private readonly FishGuideOpenService guideOpener;
    private uint? selectedId;
    private FishGuideRow? selected;
    private IReadOnlyList<FishGuideRow>? lastRows;
    private int session;
    private bool resetScroll;
    private bool focusRequested;
    private readonly bool comparison;
    private readonly List<uint> back = [];
    public uint? SelectedId => selectedId;

    public FishDetailWindow(FishCatalog catalog, FishPreferences preferences, FishCollectionObserver collection,
        FishDetailPanel panel, FishGuideOpenService guideOpener, bool comparison = false)
        : base($"DuckbillFisher###DuckbillFisher.{(comparison ? "CompareDetail" : "Detail")}")
    {
        this.catalog = catalog;
        this.preferences = preferences;
        this.collection = collection;
        this.panel = panel;
        this.guideOpener = guideOpener;
        this.comparison = comparison;
        Size = new Vector2(420, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        BgAlpha = UiWidgets.BackgroundAlpha;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(320, 300), MaximumSize = new Vector2(float.MaxValue) };
    }

    public void Show(FishGuideRow fish)
    {
        back.Clear();
        Select(fish);
    }

    public void Navigate(FishGuideRow fish)
    {
        if (session != preferences.View.Session)
        {
            back.Clear();
            selectedId = null;
            selected = null;
        }
        if (selectedId == fish.FishParameterId) return;
        if (selectedId is { } previous)
        {
            if (back.Count == 32) back.RemoveAt(0);
            back.Add(previous);
        }
        Select(fish);
    }

    private void Select(FishGuideRow fish)
    {
        selectedId = fish.FishParameterId;
        selected = fish;
        lastRows = catalog.View.Rows;
        session = preferences.View.Session;
        resetScroll = true;
        focusRequested = true;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        WindowName = $"{UiText.Get(comparison ? T.CompareDetailWindowTitle : T.DetailWindowTitle)}###DuckbillFisher.{(comparison ? "CompareDetail" : "Detail")}";
        if (focusRequested) { ImGui.SetNextWindowFocus(); focusRequested = false; }
    }

    public override void Draw()
    {
        var personal = preferences.View;
        var view = catalog.View;
        if (personal.Session != session)
        {
            selectedId = null;
            selected = null;
            back.Clear();
            session = personal.Session;
        }
        if (!ReferenceEquals(lastRows, view.Rows))
        {
            lastRows = view.Rows;
            selected = selectedId is { } id ? view.RowsById.GetValueOrDefault(id) : null;
        }
        if (comparison)
        {
            ImGui.BeginDisabled(back.Count == 0);
            if (ImGui.SmallButton(UiText.Label(T.RelatedDetailBack, "RelatedDetailBack")) && back.Count > 0)
            {
                var previous = back[^1];
                back.RemoveAt(back.Count - 1);
                if (view.RowsById.TryGetValue(previous, out var row)) Select(row);
            }
            ImGui.EndDisabled();
        }
        if (resetScroll) { ImGui.SetScrollY(0); resetScroll = false; }
        UiWidgets.GuideBanner(collection.View, personal, guideOpener);
        panel.Draw(selected, view, personal, collection.View);
    }
}
