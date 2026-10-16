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
    public IReadOnlySet<uint> Regions => regions;
    public IReadOnlySet<uint> Expansions => expansions;
    public bool HasSelection => regions.Count > 0 || expansions.Count > 0;

    public bool Draw(FishingGeography options)
    {
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
        if (!open) return false;
        var changed = false;
        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo(UiText.Label(T.GeographyExpansion, "CatalogExpansion"), expansionPreview))
        {
            if (ImGui.SmallButton(UiText.Label(T.GeographyAll, "ClearExpansions")))
            {
                changed |= expansions.Count > 0;
                expansions.Clear();
            }
            if (options.Expansions.Length == 0) ImGui.TextUnformatted(UiText.Get(T.GeographyNoOptions));
            foreach (var option in options.Expansions)
            {
                var selected = expansions.Contains(option.Id);
                if (ImGui.Checkbox($"{option.Name}###Expansion{option.Id}", ref selected))
                {
                    if (selected) expansions.Add(option.Id); else expansions.Remove(option.Id);
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo(UiText.Label(T.GeographyRegion, "CatalogRegions"), regionPreview))
        {
            if (ImGui.SmallButton(UiText.Label(T.GeographyAll, "ClearRegions")))
            {
                changed |= regions.Count > 0;
                regions.Clear();
            }
            ImGui.SetNextItemWidth(260);
            ImGui.InputText(UiText.Label(T.GeographySearch, "RegionSearch"), ref regionSearch, 100);
            if (ImGui.BeginChild("RegionChoices", new Vector2(360, ImGui.GetTextLineHeightWithSpacing() * 12), true))
            {
                var found = false;
                foreach (var option in options.Regions)
                {
                    if (regionSearch.Length > 0 && !option.Name.Contains(regionSearch, StringComparison.OrdinalIgnoreCase) &&
                        !option.ExpansionName.Contains(regionSearch, StringComparison.OrdinalIgnoreCase)) continue;
                    found = true;
                    var selected = regions.Contains(option.Id);
                    var label = UiText.Get(T.GeographyRegionOption, option.Name,
                        string.IsNullOrWhiteSpace(option.ExpansionName) ? UiText.Get(T.ConditionUnknown) : option.ExpansionName);
                    if (ImGui.Checkbox($"{label}###Region{option.Id}", ref selected))
                    {
                        if (selected) regions.Add(option.Id); else regions.Remove(option.Id);
                        changed = true;
                    }
                }
                if (!found) ImGui.TextUnformatted(UiText.Get(T.GeographyNoOptions));
            }
            ImGui.EndChild();
            ImGui.EndCombo();
        }
        return changed;
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
