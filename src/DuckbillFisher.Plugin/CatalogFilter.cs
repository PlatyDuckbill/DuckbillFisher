using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

/// <summary>Shared ordinary filters; search and diagnostic checks belong to the catalog UI only.</summary>
internal sealed class CatalogFilter(CatalogSettings settings)
{
    private readonly HashSet<uint> regions = settings.Regions.ToHashSet();
    private readonly HashSet<uint> expansions = settings.Expansions.ToHashSet();

    public bool MatchesScope(FishGuideRow fish, FishingRegionView region) =>
        (!settings.InLogOnly || fish.IsInLog) && FishLocationFilter.Matches(fish.Locations, regions, expansions,
            settings.CurrentRegionOnly, region.State == FishingRegionState.Ready ? region.TerritoryId : null);

    public bool Matches(FishGuideRow fish, FishPreferencesView personal, FishCollectionView collection, FishingRegionView region) =>
        MatchesScope(fish, region) && (settings.IncludeOceanFish || !fish.IsOceanFishing) &&
        FishClassificationFilter.Matches(fish.Grade, fish.TimeRestriction, settings.TimeRestrictedOnly, settings.Grade) &&
        (!settings.FavoritesOnly || personal.Favorites.Contains(fish.FishParameterId)) &&
        CollectionFilter.Matches(personal.Collection(fish.FishParameterId, collection), settings.Collection);

    public bool LocationScoped => settings.CurrentRegionOnly || regions.Count > 0 || expansions.Count > 0;
    public bool MatchesSpot(FishingSpotView spot, FishingRegionView region) =>
        FishLocationFilter.MatchesLocation(spot.Location, regions, expansions, settings.CurrentRegionOnly,
            region.State == FishingRegionState.Ready ? region.TerritoryId : null);
}
