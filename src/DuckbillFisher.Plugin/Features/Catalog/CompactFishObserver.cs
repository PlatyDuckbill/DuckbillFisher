using System.Diagnostics;
using System.Globalization;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record CompactFishEntry(FishGuideRow Fish, FishingWindow Window, FishingSpotView? Spot, bool UsesOtherRegion);
internal sealed record CompactFishView(CompactFishEntry[] Open, CompactFishEntry[] Upcoming, UiMessage Status)
{
    public static CompactFishView Empty { get; } = new([], [], new(T.CatalogWaiting));
}

/// <summary>Filters existing forecasts on Framework.Update. No sheet access or forecasts in Draw.</summary>
internal sealed class CompactFishObserver : IDisposable
{
    private static readonly StringComparer Names = StringComparer.Create(CultureInfo.GetCultureInfo("ko-KR"), true);
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly IFramework framework;
    private readonly FishCollectionObserver collection;
    private readonly FishingRegionObserver region;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private CompactFishView view = CompactFishView.Empty;
    private TimeSpan nextUpdate;
    private volatile bool active;

    public CompactFishObserver(FishCatalog catalog, FishPreferences preferences, IFramework framework,
        FishCollectionObserver collection, FishingRegionObserver region)
    {
        this.catalog = catalog;
        this.preferences = preferences;
        this.framework = framework;
        this.collection = collection;
        this.region = region;
        framework.Update += OnUpdate;
    }

    public CompactFishView View => Volatile.Read(ref view);
    public void SetActive(bool value)
    {
        if (value) Volatile.Write(ref view, CompactFishView.Empty);
        active = value;
        if (value) catalog.RequestLoad();
    }

    private void OnUpdate(IFramework _)
    {
        if (!active || clock.Elapsed < nextUpdate) return;
        nextUpdate = clock.Elapsed + TimeSpan.FromSeconds(1);
        var source = catalog.View;
        var personal = preferences.View;
        var filter = new CatalogFilter(personal.Catalog with { FavoritesOnly = personal.Compact.FavoritesOnly });
        var observed = collection.View;
        var location = region.View;
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddMinutes(personal.Compact.UpcomingMinutes);
        var open = new List<CompactFishEntry>();
        var upcoming = new List<CompactFishEntry>();
        foreach (var fish in source.Rows)
        {
            if (!fish.IsDisplayable || fish.IsOceanFishing || !filter.Matches(fish, personal, observed, location) ||
                !source.Windows.TryGetValue(fish.FishParameterId, out var window) ||
                !CatalogWindowFilter.Matches(personal.Catalog, window, now)) continue;
            var spot = filter.LocationScoped ? fish.Spots.FirstOrDefault(spot => filter.MatchesSpot(spot, location)) : fish.Spots.FirstOrDefault();
            var usesOtherRegion = filter.LocationScoped && spot is not null && fish.Details?.Spot is { } recommended &&
                recommended.TerritoryId != spot.TerritoryId;
            if (source.FishEyesEnabled && window.Kind == FishingWindowKind.Always &&
                source.NormalWindows.GetValueOrDefault(fish.FishParameterId)?.Kind != FishingWindowKind.Always)
                open.Add(new(fish, window, spot, usesOtherRegion));
            else if (window.Kind == FishingWindowKind.Open && (window.Start is null || window.Start <= now) &&
                (window.End is null || now < window.End)) open.Add(new(fish, window, spot, usesOtherRegion));
            else if (window.Kind == FishingWindowKind.Upcoming && window.Start is { } start && now < start && start <= cutoff)
                upcoming.Add(new(fish, window, spot, usesOtherRegion));
        }
        Volatile.Write(ref view, new(open.OrderBy(entry => entry.Window.End ?? DateTimeOffset.MaxValue)
                .ThenBy(entry => entry.Fish.Name, Names).ThenBy(entry => entry.Fish.FishParameterId).ToArray(),
            upcoming.OrderBy(entry => entry.Window.Start).ThenBy(entry => entry.Fish.Name, Names)
                .ThenBy(entry => entry.Fish.FishParameterId).ToArray(), source.Status));
    }

    public void Dispose()
    {
        active = false;
        framework.Update -= OnUpdate;
    }
}
