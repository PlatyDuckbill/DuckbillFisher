using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Displays cached static rows. It does not read or infer character collection status.</summary>
internal sealed class FishListPanel(FishCatalog catalog, ITextureProvider textures)
{
    private static readonly StringComparer NameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("ko-KR"), true);
    private IReadOnlyList<FishGuideRow>? lastRows;
    private FishGuideRow[] matches = [];
    private string search = "";
    private bool inLogOnly = true;
    private bool requested;
    private bool resetScroll;
    private int sort;
    private uint? selectedId;

    public void Draw()
    {
        if (!requested)
        {
            catalog.RequestLoad();
            requested = true;
        }
        var view = catalog.View;
        var changed = !ReferenceEquals(lastRows, view.Rows);
        lastRows = view.Rows;
        ImGui.TextWrapped(view.Status.Display);
        ImGui.SetNextItemWidth(280);
        changed |= ImGui.InputText(UiText.Label(T.FishSearch, "CatalogSearch"), ref search, 100);
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogInLogOnly, "CatalogInLogOnly"), ref inLogOnly);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        string[] sortNames = [UiText.Get(T.CatalogSortName), UiText.Get(T.CatalogSortId)];
        changed |= ImGui.Combo(UiText.Label(T.CatalogSort, "CatalogSort"), ref sort, sortNames, sortNames.Length);
        if (ImGui.Button(UiText.Label(T.ReloadFish, "ReloadCatalog")))
            catalog.RequestLoad(reload: true);

        // Rebuild only for data or user input changes, never on every frame or scroll step.
        if (changed)
            UpdateMatches(view.Rows);
        ImGui.TextUnformatted(UiText.Get(T.CatalogCount, matches.Length, view.Rows.Count));
        ImGui.TextWrapped(UiText.Get(T.CatalogDetailsPending));
        ImGui.Separator();

        if (ImGui.BeginChild("CatalogRows", new Vector2(0, 0), true))
        {
            if (resetScroll)
            {
                ImGui.SetScrollY(0);
                resetScroll = false;
            }
            if (matches.Length == 0)
                ImGui.TextWrapped(UiText.Get(T.CatalogNoMatches));
            var rowHeight = ImGui.GetTextLineHeight() * 3.2f;
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                clipper.Begin(matches.Length, rowHeight + ImGui.GetStyle().ItemSpacing.Y);
                while (clipper.Step())
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        DrawRow(matches[i], rowHeight);
            }
            finally
            {
                clipper.Destroy();
            }
        }
        ImGui.EndChild();
    }

    private void UpdateMatches(IReadOnlyList<FishGuideRow> rows)
    {
        var term = search.Trim();
        var filtered = rows.Where(row => (!inLogOnly || row.IsInLog) &&
            (term.Length == 0 || row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
             row.FishParameterId.ToString(CultureInfo.InvariantCulture) == term));
        matches = (sort == 0
            ? filtered.OrderBy(row => row.Name, NameComparer).ThenBy(row => row.FishParameterId)
            : filtered.OrderBy(row => row.FishParameterId)).ToArray();
        resetScroll = true;
    }

    private void DrawRow(FishGuideRow fish, float height)
    {
        ImGui.PushID((int)fish.FishParameterId);
        var start = ImGui.GetCursorPos();
        if (ImGui.Selectable("###CatalogRow", selectedId == fish.FishParameterId,
                ImGuiSelectableFlags.None, new Vector2(ImGui.GetContentRegionAvail().X, height)))
            selectedId = fish.FishParameterId;
        var next = ImGui.GetCursorPos();
        var iconSize = height - 8;
        ImGui.SetCursorPos(start + new Vector2(4, 4));
        // Shared immediate textures are owned by Dalamud; do not dispose the borrowed wrap.
        if (fish.IconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(fish.IconId), out var icon)
            && icon.TryGetWrap(out var wrap, out _))
            ImGui.Image(wrap.Handle, new Vector2(iconSize));
        else
            ImGui.Dummy(new Vector2(iconSize));
        ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4));
        ImGui.TextUnformatted(fish.DisplayName);
        ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4 + ImGui.GetTextLineHeightWithSpacing()));
        ImGui.TextDisabled(UiText.Get(T.CatalogRowInfo, fish.FishParameterId));
        ImGui.SetCursorPos(next);
        ImGui.PopID();
    }
}
