namespace DuckbillFisher.Plugin;

internal sealed record FishingRegionOption(uint Id, string Name, uint? ExpansionId, string ExpansionName);
internal sealed record FishingExpansionOption(uint Id, string Name);

internal sealed record FishingGeography(FishingRegionOption[] Regions, FishingExpansionOption[] Expansions)
{
    // KR 2026.10.06 housing TerritoryType.ExVersion is 0 even for later residential districts.
    // Local sheet IDs + official 4.0/6.0 notes: see DATA-SOURCES, collection geography audit.
    public static uint ExpansionId(uint territoryId, uint sheetExpansionId, bool supportedBuild) => supportedBuild
        ? territoryId switch { 641 => 2, 979 => 4, _ => sheetExpansionId }
        : sheetExpansionId;

    public static FishingGeography FromRows(IEnumerable<FishGuideRow> rows)
    {
        var spots = rows.Where(row => row.IsDisplayable && !row.IsOceanFishing).SelectMany(row => row.Spots).ToArray();
        var regions = spots.Where(spot => spot.TerritoryId != 0 && !string.IsNullOrWhiteSpace(spot.Zone))
            .DistinctBy(spot => spot.TerritoryId)
            .OrderBy(spot => spot.ExpansionId).ThenBy(spot => spot.Zone, StringComparer.OrdinalIgnoreCase)
            .Select(spot => new FishingRegionOption(spot.TerritoryId, spot.Zone, spot.ExpansionId, spot.ExpansionName)).ToArray();
        var expansions = spots.Where(spot => spot.ExpansionId.HasValue && !string.IsNullOrWhiteSpace(spot.ExpansionName))
            .DistinctBy(spot => spot.ExpansionId).OrderBy(spot => spot.ExpansionId)
            .Select(spot => new FishingExpansionOption(spot.ExpansionId!.Value, spot.ExpansionName)).ToArray();
        return new(regions, expansions);
    }
}
