namespace DuckbillFisher.Core;

public readonly record struct FishingLocation(uint TerritoryId, uint? ExpansionId);

public static class FishLocationFilter
{
    public static bool Matches(IReadOnlyList<FishingLocation> locations, IReadOnlySet<uint> territories,
        IReadOnlySet<uint> expansions, bool currentOnly, uint? currentTerritory)
    {
        if (territories.Count == 0 && expansions.Count == 0 && !currentOnly) return true;
        foreach (var location in locations)
            if (MatchesLocation(location, territories, expansions, currentOnly, currentTerritory)) return true;
        return false;
    }

    // Each selected axis must match this same location, not separate locations of one fish.
    public static bool MatchesLocation(FishingLocation location, IReadOnlySet<uint> territories,
        IReadOnlySet<uint> expansions, bool currentOnly, uint? currentTerritory) =>
        location.TerritoryId != 0 && (!currentOnly || currentTerritory == location.TerritoryId) &&
        (territories.Count == 0 || territories.Contains(location.TerritoryId)) &&
        (expansions.Count == 0 || location.ExpansionId is { } expansion && expansions.Contains(expansion));
}
