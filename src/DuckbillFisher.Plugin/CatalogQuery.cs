using System.Globalization;

namespace DuckbillFisher.Plugin;

/// <summary>Immutable main-list query shared with preparation; never shares mutable UI selections.</summary>
internal sealed record CatalogQuery(CatalogSettings Settings, string Search, bool ShowUnresolved, bool DetailsOnly)
{
    public bool Matches(FishGuideRow row, CatalogFilter filter, FishPreferencesView personal,
        FishCollectionView collection, FishingRegionView region, FishCatalogView catalog, DateTimeOffset now) =>
        (ShowUnresolved || row.IsDisplayable) && (!DetailsOnly || row.Details is not null) &&
        filter.Matches(row, personal, collection, region) && MatchesSearch(row, Search) &&
        CatalogWindowFilter.Matches(Settings, catalog.Windows.GetValueOrDefault(row.FishParameterId), now);

    public static bool MatchesSearch(FishGuideRow row, string term) =>
        term.Length == 0 || row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        row.Ocean?.Area.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
        row.Spots.Any(spot => spot.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            spot.Zone.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
        row.FishParameterId.ToString(CultureInfo.InvariantCulture) == term;
}

internal sealed class CatalogQueryState(CatalogSettings initial)
{
    private CatalogQuery current = new(initial, "", false, false);
    public CatalogQuery Current => Volatile.Read(ref current);
    public void Publish(CatalogQuery query)
    {
        var previous = Current;
        if (previous.Search == query.Search && previous.ShowUnresolved == query.ShowUnresolved &&
            previous.DetailsOnly == query.DetailsOnly && previous.Settings.Matches(query.Settings)) return;
        Volatile.Write(ref current, query);
    }
}
