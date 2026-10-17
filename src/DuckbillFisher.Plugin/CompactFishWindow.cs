using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class CompactFishWindow : Window
{
    private readonly CompactFishObserver observer;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly FishRowRenderer rows;
    private readonly Action<FishGuideRow> showDetail;
    private bool jumpUpcoming;
    private bool jumpTop;
    public Action? ShowMain { get; set; }

    public CompactFishWindow(CompactFishObserver observer, FishPreferences preferences, FishCollectionObserver collection,
        ITextureProvider textures, Action<FishGuideRow> showDetail, FishingMapService maps) : base("DuckbillFisher###DuckbillFisher.Compact")
    {
        this.observer = observer;
        this.preferences = preferences;
        this.collection = collection;
        this.showDetail = showDetail;
        rows = new(textures, maps);
        Size = new Vector2(370, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        BgAlpha = UiWidgets.BackgroundAlpha;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(300, 260), MaximumSize = new Vector2(float.MaxValue) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public override void OnOpen() => observer.SetActive(true);
    public override void OnClose() => observer.SetActive(false);
    public override void PreDraw() => WindowName = $"{UiText.Get(T.CompactWindowTitle)}###DuckbillFisher.Compact";

    public override void Draw()
    {
        var personal = preferences.View;
        var view = observer.View;
        ImGui.BeginDisabled(view.Upcoming.Length == 0);
        if (ImGui.Button(UiText.Label(T.JumpUpcoming, "CompactJumpUpcoming"))) jumpUpcoming = true;
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (UiWidgets.IconButton(FontAwesomeIcon.ArrowUp, "CompactJumpTop", UiText.Get(T.JumpTop))) jumpTop = true;
        var right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        ImGui.SameLine();
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(),
            right - ImGui.GetFrameHeight() * 2 - ImGui.GetStyle().ItemSpacing.X));
        if (UiWidgets.IconButton(FontAwesomeIcon.List, "OpenMain", UiText.Get(T.ToggleMainWindow))) ShowMain?.Invoke();
        ImGui.SameLine();
        if (UiWidgets.IconButton(FontAwesomeIcon.Cog, "CompactOptions", UiText.Get(T.CompactOptions))) ImGui.OpenPopup("CompactOptions");
        ImGui.SetNextWindowSizeConstraints(new Vector2(ImGui.GetFontSize() * 23, 0),
            new Vector2(ImGui.GetFontSize() * 30, ImGui.GetMainViewport().WorkSize.Y * 0.8f));
        if (ImGui.BeginPopup("CompactOptions"))
        {
            var minutes = personal.Compact.UpcomingMinutes;
            ImGui.BeginDisabled(!personal.CanEdit);
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 8);
            if (ImGui.InputInt(UiText.Label(T.CompactHorizon, "CompactHorizon"), ref minutes))
                preferences.SetCompact(personal.Compact with { UpcomingMinutes = Math.Clamp(minutes, 1, 180) });
            ImGui.EndDisabled();
            ImGui.TextWrapped(UiText.Get(T.CompactScope));
            ImGui.EndPopup();
        }
        if (personal.Error is { } error) ImGui.TextWrapped(error.Display);
        if (ImGui.BeginChild("CompactRows"))
        {
            if (jumpTop) { ImGui.SetScrollY(0); jumpTop = false; }
            if (view.Status.Key != T.CatalogLoaded) ImGui.TextWrapped(view.Status.Display);
            Group(T.CompactOpen, view.Open, personal);
            if (jumpUpcoming) { ImGui.SetScrollY(ImGui.GetCursorPosY() - ImGui.GetStyle().WindowPadding.Y); jumpUpcoming = false; }
            Group(T.CompactUpcoming, view.Upcoming, personal);
            if (view.Status.Key == T.CatalogLoaded && view.Open.Length == 0 && view.Upcoming.Length == 0)
                ImGui.TextWrapped(UiText.Get(T.CompactEmpty, personal.Compact.UpcomingMinutes));
        }
        ImGui.EndChild();
    }

    private void Group(T title, CompactFishEntry[] entries, FishPreferencesView personal)
    {
        ImGui.TextUnformatted(UiText.Get(title, entries.Length));
        ImGui.Separator();
        var observed = collection.View;
        var now = DateTimeOffset.UtcNow;
        var clipper = ImGui.ImGuiListClipper();
        try
        {
            var height = FishRowRenderer.GetHeight(ImGui.GetContentRegionAvail().X, compact: true);
            clipper.Begin(entries.Length, height + ImGui.GetStyle().ItemSpacing.Y);
            while (clipper.Step())
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var entry = entries[i];
                    var state = UiText.Get(FishRowRenderer.CollectionKey(personal, observed, entry.Fish.FishParameterId));
                    var place = entry.Spot?.Name ?? UiText.Get(T.DetailLocationUnknown);
                    if (rows.Draw(entry.Fish, height, false, state, place, entry.Window, now,
                        usesOtherRegion: entry.UsesOtherRegion, favorite: personal.Favorites.Contains(entry.Fish.FishParameterId), compact: true,
                        spot: entry.Spot, canOpenMap: personal.HasCharacter)) showDetail(entry.Fish);
                }
        }
        finally { clipper.Destroy(); }
        ImGui.Spacing();
    }
}
