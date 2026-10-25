using Dalamud.Plugin.Services;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

internal sealed record RecommendationOptions(int Hours = 3, bool IncludeUnknown = false,
    bool FavoritesOnly = false, bool LegendaryOnly = false);
internal enum RecommendationState { Waiting, Computing, Ready, Failed, Unsupported }
internal sealed record RecommendationEntry(FishGuideRow Fish, CollectionStatus Collection, FishRecommendation Recommendation)
{
    public int FrequencyRank { get; init; }
    public int FrequencyComparisonCount { get; init; }
}
internal sealed record FishRecommendationView(int Session, RecommendationState State, DateTimeOffset At,
    RecommendationEntry[] Ranked, RecommendationEntry[] Other, int UnknownCount, int CaughtCount)
{
    public DateTimeOffset AnalysisStart { get; init; }
    public bool NeedsRefresh { get; init; }
    public int Generation { get; init; }
    public RecommendationEntry[] Now { get; init; } = [];
    public RecommendationEntry[] Preparation { get; init; } = [];
    public RecommendationEntry[] Rare { get; init; } = [];
}

/// <summary>Explicit snapshots: entering, changing options, or pressing refresh requests work.
/// Collection/time changes only flag staleness. Framework captures managed values; worker reads schedules.</summary>
internal sealed class FishRecommendationService : IDisposable
{
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly TimeProvider clock;
    private readonly object gate = new();
    private FishRecommendationView view = Empty(-1);
    private RecommendationOptions options = new();
    private Task<FishRecommendationView?>? work;
    private CancellationTokenSource? cancellation;
    private IReadOnlyList<FishGuideRow>? sourceRows;
    private FishRecommendations? engine;
    private bool active, disposed;
    private int requested, started = -1, collectionRevision, personalRevision;
    private long lastDraw;
    private DateTimeOffset lastUtc;

    public FishRecommendationService(FishCatalog catalog, FishPreferences preferences, FishCollectionObserver collection,
        IFramework framework, IPluginLog log, TimeProvider? clock = null)
    {
        this.catalog = catalog; this.preferences = preferences; this.collection = collection;
        this.framework = framework; this.log = log; this.clock = clock ?? TimeProvider.System;
        framework.Update += Update;
    }

    private static FishRecommendationView Empty(int session) => new(session, RecommendationState.Waiting, default, [], [], 0, 0);
    public FishRecommendationView View { get { lock (gate) return view; } }
    public DateTimeOffset Now => clock.GetUtcNow();
    public void SetActive(bool value)
    {
        lock (gate)
        {
            if (disposed) return;
            if (value) lastDraw = clock.GetTimestamp();
            if (active == value) return;
            active = value;
            if (value) requested++;
            else { cancellation?.Cancel(); view = Empty(preferences.View.Session); }
        }
    }
    public void Configure(RecommendationOptions value)
    { lock (gate) if (!disposed && options != value) { options = value; requested++; view = Empty(preferences.View.Session); } }
    public void Refresh() { lock (gate) if (!disposed) requested++; }

    private void Update(IFramework _)
    {
        lock (gate)
        {
            if (disposed) return;
            var personal = preferences.View;
            if (view.Session != personal.Session)
            { cancellation?.Cancel(); view = Empty(personal.Session); requested++; }
            if (!active || clock.GetElapsedTime(lastDraw) > TimeSpan.FromSeconds(2))
            { cancellation?.Cancel(); active = false; return; }
            catalog.RequestLoad();
            var data = catalog.View;
            if (data.Rows.Count == 0 || !personal.HasCharacter) return;
            if (!catalog.ConditionBuildMatches)
            { cancellation?.Cancel(); view = view with { State = RecommendationState.Unsupported }; return; }
            var observed = collection.View;
            var now = clock.GetUtcNow();
            if (lastUtc != default && (now < lastUtc || now - lastUtc > TimeSpan.FromSeconds(5)) ||
                view.At != default && now - view.At >= TimeSpan.FromMinutes(1) ||
                observed.Revision != collectionRevision || personal.CatalogRevision != personalRevision ||
                sourceRows is not null && !ReferenceEquals(sourceRows, data.Rows))
                view = view with { NeedsRefresh = true };
            lastUtc = now;
            if (started != requested) cancellation?.Cancel();
            if (work is { IsCompleted: true })
            {
                var result = work.GetAwaiter().GetResult();
                work = null; cancellation?.Dispose(); cancellation = null;
                if (result is not null && result.Session == personal.Session && result.Generation == requested)
                    view = result with { NeedsRefresh = observed.Revision != collectionRevision || personal.CatalogRevision != personalRevision ||
                        now - result.At >= TimeSpan.FromMinutes(1) };
            }
            // One worker owns the calendar cache; cancellation completes before the next worker starts.
            if (work is not null || started == requested) return;
            if (!ReferenceEquals(sourceRows, data.Rows))
            {
                sourceRows = data.Rows;
                var details = data.RelatedDetails.Values.Concat(data.Rows.Where(row => row.Details is not null).Select(row => row.Details!))
                    .Where(detail => detail.Spot is not null).GroupBy(detail => detail.Rule.ItemId).Select(group => group.First());
                engine = new(details.Select(detail => new FishingForecastInput(detail.Rule, detail.Spot!.WeatherRates)));
            }
            started = requested;
            collectionRevision = observed.Revision; personalRevision = personal.CatalogRevision;
            var settings = options;
            var source = data.Rows.Where(row => row.IsDisplayable && !row.IsOceanFishing && row.ItemType == "Item" &&
                row.Grade is FishGrade.Big or FishGrade.Legendary &&
                (!settings.FavoritesOnly || personal.Favorites.Contains(row.FishParameterId)) &&
                (!settings.LegendaryOnly || row.Grade == FishGrade.Legendary)).ToArray();
            var targets = source.Select(row => (Fish: row, Collection: personal.Collection(row.FishParameterId, observed))).ToArray();
            view = new(personal.Session, RecommendationState.Computing, now, [], [],
                targets.Count(value => value.Collection == CollectionStatus.Unknown), targets.Count(value => value.Collection == CollectionStatus.Caught))
            { Generation = requested };
            var initial = view;
            cancellation = new();
            var token = cancellation.Token;
            var planner = engine!;
            work = Task.Run(() =>
            {
                try
                {
                    var included = targets.Where(value => value.Collection == CollectionStatus.Uncaught ||
                        settings.IncludeUnknown && value.Collection == CollectionStatus.Unknown).ToDictionary(value => value.Fish.ItemId);
                    var result = planner.Calculate(now, settings.Hours, included.Keys, token);
                    RecommendationEntry Entry(FishRecommendation value) => new(included[value.ItemId].Fish, included[value.ItemId].Collection, value);
                    // Equal frequencies share a rank; these compare only the filtered horizon's rare candidates.
                    var frequencyRanks = result.Rare.Select((value, index) => (value.OpportunityCount, Rank: index + 1))
                        .GroupBy(value => value.OpportunityCount).ToDictionary(group => group.Key, group => group.First().Rank);
                    return initial with { State = RecommendationState.Ready, AnalysisStart = result.AnalysisStart,
                        Ranked = result.Ranked.Select(Entry).ToArray(), Other = result.Other.Select(Entry).ToArray(),
                        Now = result.Now.Select(Entry).ToArray(), Preparation = result.Preparation.Select(Entry).ToArray(),
                        Rare = result.Rare.Select(value => Entry(value) with
                        { FrequencyRank = frequencyRanks[value.OpportunityCount], FrequencyComparisonCount = result.Rare.Count }).ToArray() };
                }
                catch (OperationCanceledException) { return null; }
                catch (Exception ex)
                { log.Error(ex, "Could not calculate fresh-start fishing recommendations."); return initial with { State = RecommendationState.Failed }; }
            }, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        lock (gate) { disposed = true; cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null; view = Empty(-1); }
        framework.Update -= Update;
    }
}
