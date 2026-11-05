using Dalamud.Plugin.Services;

namespace DuckbillFisher.Plugin;

internal enum BaitPreparationSource { Favorites, Catalog, Unused }
internal sealed record BaitPreparationEntry(FishingItemView Item, FishGuideRow[] Targets);
internal sealed record BaitPreparationView(int Session, int TargetCount, BaitPreparationEntry[] Entries, FishGuideRow[] Missing)
{
    public BaitPreparationSource Source { get; init; }
    public FishingItemView[] UnusedBaits { get; init; } = [];
}

/// <summary>Aggregates first baits outside Draw. Favorites and the full main query are independent scopes.</summary>
internal sealed class BaitPreparation : IDisposable
{
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly FishingRegionObserver region;
    private readonly CatalogQueryState query;
    private readonly IFramework framework;
    private readonly object gate = new();
    private BaitPreparationSource source;
    private BaitPreparationView view = new(-1, 0, [], []);
    private CatalogQuery? lastQuery;
    private IReadOnlyList<FishGuideRow>? lastRows;
    private IReadOnlySet<uint> fishItems = new HashSet<uint>();
    private FishingRegionView? lastRegion;
    private int lastPreference = -1, lastCollection = -1, lastSession = -1;
    private BaitPreparationSource lastSource;
    private bool disposed;
    private int lastForecast = -1;
    private DateTimeOffset nextWindowFilterUpdate, lastClock;

    public BaitPreparation(FishCatalog catalog, FishPreferences preferences, FishCollectionObserver collection,
        FishingRegionObserver region, CatalogQueryState query, IFramework framework)
    {
        this.catalog = catalog; this.preferences = preferences; this.collection = collection;
        this.region = region; this.query = query; this.framework = framework;
        catalog.RequestLoad();
        framework.Update += Update;
    }

    public BaitPreparationView View { get { lock (gate) return view; } }
    public BaitPreparationSource Source { get { lock (gate) return source; } }
    public void SetSource(BaitPreparationSource value) { lock (gate) source = value; }

    private void Update(IFramework _)
    {
        lock (gate)
        {
            if (disposed) return;
            var personal = preferences.View;
            if (personal.Session != lastSession) source = BaitPreparationSource.Favorites;
            var data = catalog.View;
            var observed = collection.View;
            var location = region.View;
            var current = query.Current;
            var now = DateTimeOffset.UtcNow;
            var timedFilterChanged = source != BaitPreparationSource.Favorites && CatalogWindowFilter.Enabled(current.Settings) &&
                (lastForecast != data.ForecastRevision || now >= nextWindowFilterUpdate || now < lastClock);
            lastClock = now;
            if (lastSession == personal.Session && lastPreference == personal.CatalogRevision && lastCollection == observed.Revision &&
                ReferenceEquals(lastRows, data.Rows) && lastRegion == location && lastQuery == current && lastSource == source && !timedFilterChanged) return;
            lastForecast = data.ForecastRevision;
            nextWindowFilterUpdate = CatalogWindowFilter.NextChange(current.Settings, data.Windows.Values, now);
            if (!ReferenceEquals(lastRows, data.Rows))
                fishItems = data.Rows.Where(row => row.ItemType == "Item").Select(row => row.ItemId).ToHashSet();
            lastSession = personal.Session; lastPreference = personal.CatalogRevision; lastCollection = observed.Revision;
            lastRows = data.Rows; lastRegion = location; lastQuery = current; lastSource = source;
            if (!personal.HasCharacter) { view = new(personal.Session, 0, [], []) { Source = source }; return; }
            var filter = new CatalogFilter(current.Settings);
            var targets = data.Rows.Where(row => source == BaitPreparationSource.Favorites
                ? personal.Favorites.Contains(row.FishParameterId)
                : current.Matches(row, filter, personal, observed, location, data, now)).ToArray();
            var entries = new Dictionary<uint, (FishingItemView Item, List<FishGuideRow> Targets)>();
            var missing = new List<FishGuideRow>();
            foreach (var row in targets)
            {
                var visited = new HashSet<uint>();
                var unresolved = false;
                AddRequirements(row.Details);
                if (unresolved) missing.Add(row);

                void AddRequirements(FishingDetailView? details)
                {
                    if (details is null) { unresolved = true; return; }
                    if (!visited.Add(details.Rule.ItemId)) return;
                    var first = details.BaitSteps.FirstOrDefault();
                    if (first is null || first.Length == 0 || first.Any(item => item.Id == 0 || fishItems.Contains(item.Id)))
                        unresolved = true;
                    else foreach (var item in first.DistinctBy(item => item.Id))
                    {
                        if (!entries.TryGetValue(item.Id, out var entry)) entry = (item, []);
                        if (!entry.Targets.Contains(row)) entry.Targets.Add(row);
                        entries[item.Id] = entry;
                    }
                    foreach (var prey in details.Rule.Predators)
                        AddRequirements(data.RelatedDetails.GetValueOrDefault(prey.ItemId));
                }
            }
            view = new(personal.Session, targets.Length, entries.Values.OrderBy(entry => entry.Item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new BaitPreparationEntry(entry.Item, entry.Targets.ToArray())).ToArray(), missing.OrderBy(fish => fish.DisplayName,
                        StringComparer.OrdinalIgnoreCase).ToArray())
            {
                Source = source,
                UnusedBaits = data.Baits.Where(item => !entries.ContainsKey(item.Id)).ToArray(),
            };
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            framework.Update -= Update;
            view = new(-1, 0, [], []);
        }
    }
}
