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
    public Action? ShowMain { get; set; }

    public CompactFishWindow(CompactFishObserver observer, FishPreferences preferences, FishCollectionObserver collection,
        ITextureProvider textures, Action<FishGuideRow> showDetail) : base("DuckbillFisher###DuckbillFisher.Compact")
    {
        this.observer = observer;
        this.preferences = preferences;
        this.collection = collection;
        this.showDetail = showDetail;
        rows = new(textures);
        Size = new Vector2(370, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(300, 260), MaximumSize = new Vector2(float.MaxValue) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public override void OnOpen() => observer.SetActive(true);
    public override void OnClose() => observer.SetActive(false);
    public override void PreDraw() => WindowName = $"{UiText.Get(T.CompactWindowTitle)}###DuckbillFisher.Compact";

    public override void Draw()
    {
        var personal = preferences.View;
        var favorites = personal.Compact.FavoritesOnly;
        ImGui.BeginDisabled(!personal.CanEdit);
        if (ImGui.Checkbox(UiText.Label(T.CatalogFavoritesOnly, "CompactFavorites"), ref favorites))
            preferences.SetCompact(personal.Compact with { FavoritesOnly = favorites });
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (UiWidgets.IconButton(FontAwesomeIcon.Cog, "CompactOptions", UiText.Get(T.CompactOptions))) ImGui.OpenPopup("CompactOptions");
        ImGui.SameLine();
        if (UiWidgets.IconButton(FontAwesomeIcon.List, "OpenMain", UiText.Get(T.OpenMainWindow))) ShowMain?.Invoke();
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
        var view = observer.View;
        if (ImGui.BeginChild("CompactRows"))
        {
            if (view.Status.Key != T.CatalogLoaded) ImGui.TextWrapped(view.Status.Display);
            Group(T.CompactOpen, view.Open, personal);
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
            clipper.Begin(entries.Length, FishRowRenderer.CompactHeight + ImGui.GetStyle().ItemSpacing.Y);
            while (clipper.Step())
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var entry = entries[i];
                    var state = UiText.Get(FishRowRenderer.CollectionKey(personal, observed, entry.Fish.FishParameterId));
                    var place = entry.Fish.Details?.Spot?.Name ?? entry.Fish.Spots.FirstOrDefault()?.Name ?? UiText.Get(T.DetailLocationUnknown);
                    if (rows.Draw(entry.Fish, FishRowRenderer.CompactHeight, false, state, place, entry.Window, now,
                        favorite: personal.Favorites.Contains(entry.Fish.FishParameterId), compact: true)) showDetail(entry.Fish);
                }
        }
        finally { clipper.Destroy(); }
        ImGui.Spacing();
    }
}
