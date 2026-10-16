namespace DuckbillFisher.Plugin;

internal sealed record FishingRegionOption(uint Id, string Name, uint? ExpansionId, string ExpansionName);
internal sealed record FishingExpansionOption(uint Id, string Name);

internal sealed record FishingGeography(FishingRegionOption[] Regions, FishingExpansionOption[] Expansions)
{
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
