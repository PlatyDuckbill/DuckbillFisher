using System.Numerics;
using Dalamud.Bindings.ImGui;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Owns UI selections only. Options and spot metadata are prepared by the catalog.</summary>
internal sealed class FishGeographyPanel
{
    private readonly HashSet<uint> regions = [];
    private readonly HashSet<uint> expansions = [];
    private string regionSearch = "";
    private FishingGeography? cachedOptions;
    private int expansionRevision;
    private int regionRevision;
    private int cachedExpansionRevision = -1;
    private int cachedRegionRevision = -1;
    private string cachedSearch = "";
    private FishingRegionOption[] scopedRegions = [];
    private FishingRegionOption[] visibleRegions = [];
    private uint[] hiddenRegions = [];
    public IReadOnlySet<uint> Regions => regions;
    public IReadOnlySet<uint> Expansions => expansions;
    public bool HasSelection => regions.Count > 0 || expansions.Count > 0;

    public bool Draw(FishingGeography options)
    {
        UpdateOptions(options);
        var regionPreview = Preview(regions, id => options.Regions.FirstOrDefault(option => option.Id == id)?.Name);
        var expansionPreview = Preview(expansions, id => options.Expansions.FirstOrDefault(option => option.Id == id)?.Name);
        var open = ImGui.CollapsingHeader(UiText.Label(T.GeographyHeading, "CatalogGeography"));
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35);
            ImGui.TextUnformatted(UiText.Get(T.GeographyHint));
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
        if (HasSelection)
            ImGui.TextWrapped(UiText.Get(T.GeographySelection, regionPreview, expansionPreview));
        var changed = false;
        if (open)
            changed |= DrawExpansions(options);
        UpdateOptions(options);
        changed |= DrawHiddenRegions(options);
        if (!open) return changed;

        if (ImGui.Button($"{UiText.Get(T.GeographyChooseRegions, regionPreview)}###ChooseRegions"))
            ImGui.OpenPopup("GeographyRegionPicker");
        ImGui.SameLine();
        if (ImGui.SmallButton(UiText.Label(T.GeographyClearRegions, "ClearRegionsOutside")))
        {
            changed |= ClearRegions();
            UpdateOptions(options);
        }
        if (ImGui.IsPopupOpen("GeographyRegionPicker"))
        {
            var workSize = ImGui.GetMainViewport().WorkSize;
            // An explicit popup size overrides combo auto-height. Only its list child scrolls.
            ImGui.SetNextWindowSize(new Vector2(Math.Min(ImGui.GetFontSize() * 38, workSize.X - 32),
                Math.Min(ImGui.GetTextLineHeightWithSpacing() * 20, workSize.Y - 32)), ImGuiCond.Always);
            if (ImGui.BeginPopup("GeographyRegionPicker", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                changed |= DrawRegionPicker(options);
                ImGui.EndPopup();
            }
        }
        return changed;
    }

    private bool DrawExpansions(FishingGeography options)
    {
        var changed = false;
        ImGui.TextUnformatted(UiText.Get(T.GeographyExpansion));
        ImGui.SameLine();
        if (ImGui.SmallButton(UiText.Label(T.GeographyAll, "ClearExpansions")) && expansions.Count > 0)
        {
            expansions.Clear();
            expansionRevision++;
            changed = true;
        }
        var columns = Math.Clamp((int)(ImGui.GetContentRegionAvail().X / (ImGui.GetFontSize() * 14)), 1, 3);
        if (ImGui.BeginTable("GeographyExpansions", columns, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var option in options.Expansions)
            {
                ImGui.TableNextColumn();
                var selected = expansions.Contains(option.Id);
                if (ImGui.Checkbox($"{option.Name}###Expansion{option.Id}", ref selected))
                {
                    if (selected) expansions.Add(option.Id); else expansions.Remove(option.Id);
                    expansionRevision++;
                    changed = true;
                }
            }
            ImGui.EndTable();
        }
        if (options.Expansions.Length == 0) ImGui.TextUnformatted(UiText.Get(T.GeographyNoOptions));
        return changed;
    }

    private bool DrawRegionPicker(FishingGeography options)
    {
        var changed = false;
        ImGui.TextUnformatted(UiText.Get(T.GeographyRegion));
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.65f);
        ImGui.InputText(UiText.Label(T.GeographySearch, "RegionSearch"), ref regionSearch, 100);
        if (ImGui.SmallButton(UiText.Label(T.GeographyClearRegions, "ClearRegions")))
            changed |= ClearRegions();
        ImGui.SameLine();
        if (ImGui.SmallButton(UiText.Label(T.Close, "CloseRegionPicker"))) ImGui.CloseCurrentPopup();
        UpdateOptions(options);
        changed |= DrawHiddenRegions(options);
        ImGui.TextDisabled(UiText.Get(T.GeographyScopeCount, scopedRegions.Length, visibleRegions.Length));
        if (ImGui.BeginChild("RegionChoices", new Vector2(0, 0), true))
        {
            var first = true;
            uint? previousExpansion = null;
            foreach (var option in visibleRegions)
            {
                if (first || option.ExpansionId != previousExpansion)
                {
                    if (!first) ImGui.Separator();
                    ImGui.TextDisabled(string.IsNullOrWhiteSpace(option.ExpansionName)
                        ? UiText.Get(T.ConditionUnknown) : option.ExpansionName);
                    first = false;
                    previousExpansion = option.ExpansionId;
                }
                var selected = regions.Contains(option.Id);
                if (ImGui.Checkbox($"{option.Name}###Region{option.Id}", ref selected))
                {
                    if (selected) regions.Add(option.Id); else regions.Remove(option.Id);
                    regionRevision++;
                    changed = true;
                }
            }
            if (visibleRegions.Length == 0) ImGui.TextUnformatted(UiText.Get(T.GeographyNoOptions));
        }
        ImGui.EndChild();
        return changed;
    }

    private bool DrawHiddenRegions(FishingGeography options)
    {
        if (hiddenRegions.Length == 0) return false;
        ImGui.TextWrapped(UiText.Get(T.GeographyHiddenRegions, hiddenRegions.Length));
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35);
            foreach (var id in hiddenRegions)
                ImGui.TextUnformatted(options.Regions.FirstOrDefault(option => option.Id == id)?.Name ?? UiText.Get(T.ConditionUnknown));
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
        if (!ImGui.SmallButton(UiText.Label(T.GeographyClearHidden, "ClearHiddenRegions"))) return false;
        foreach (var id in hiddenRegions) regions.Remove(id);
        regionRevision++;
        UpdateOptions(options);
        return true;
    }

    private bool ClearRegions()
    {
        if (regions.Count == 0) return false;
        regions.Clear();
        regionRevision++;
        return true;
    }

    private void UpdateOptions(FishingGeography options)
    {
        var scopeChanged = !ReferenceEquals(cachedOptions, options) || cachedExpansionRevision != expansionRevision;
        if (scopeChanged)
        {
            scopedRegions = options.Regions.Where(option => expansions.Count == 0 ||
                option.ExpansionId is { } expansion && expansions.Contains(expansion)).ToArray();
            cachedOptions = options;
            cachedExpansionRevision = expansionRevision;
        }
        if (scopeChanged || cachedSearch != regionSearch)
        {
            var term = regionSearch.Trim();
            visibleRegions = scopedRegions.Where(option => term.Length == 0 || option.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                option.ExpansionName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
            cachedSearch = regionSearch;
        }
        if (scopeChanged || cachedRegionRevision != regionRevision)
        {
            var available = scopedRegions.Select(option => option.Id).ToHashSet();
            hiddenRegions = regions.Where(id => !available.Contains(id)).Order().ToArray();
            cachedRegionRevision = regionRevision;
        }
    }

    public bool Matches(FishGuideRow row, FishingRegionView current, bool currentOnly) =>
        FishLocationFilter.Matches(row.Locations, regions, expansions, currentOnly,
            current.State == FishingRegionState.Ready ? current.TerritoryId : null);

    public bool MatchesSpot(FishingSpotView spot, FishingRegionView current, bool currentOnly) =>
        FishLocationFilter.MatchesLocation(spot.Location, regions, expansions, currentOnly,
            current.State == FishingRegionState.Ready ? current.TerritoryId : null);

    private static string Preview(HashSet<uint> selected, Func<uint, string?> name) => selected.Count switch
    {
        0 => UiText.Get(T.GeographyAll),
        1 => name(selected.First()) ?? UiText.Get(T.ConditionUnknown),
        _ => UiText.Get(T.GeographySelectedCount, selected.Count),
    };
}
