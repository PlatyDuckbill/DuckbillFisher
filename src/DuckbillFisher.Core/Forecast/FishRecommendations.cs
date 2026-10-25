namespace DuckbillFisher.Core;

public enum RecommendationIssue { None, ConditionsUnknown, MissingPreparation, OutsideHorizon }
public enum OpportunityBasis { OwnConditions, PrerequisiteProxy, Always }
public enum PreparationKind { Mooch, Intuition }
public sealed record PreparationCondition(uint ItemId, int Count, PreparationKind Kind, FishingWindow Window);
public sealed record FishRecommendation(uint ItemId, RecommendationIssue Issue, FishingWindow Window,
    int OpportunityCount, OpportunityBasis Basis, uint BasisItemId, DateTimeOffset? NextOpportunity)
{
    public bool OwnConditionsAlways { get; init; }
    public int MoochDepth { get; init; }
    public int IntuitionCatchCount { get; init; }
    public uint[][] MoochPath { get; init; } = [];
    public IReadOnlyList<PreparationCondition> Preparation { get; init; } = [];
    public PreparationCondition? PrimaryPreparation { get; init; }
    public DateTimeOffset? NextOwnOpportunity { get; init; }
    public bool PreparationUnknown => Preparation.Any(value => value.Window.Kind == FishingWindowKind.Unknown);
    // An actual prerequisite's own calendar, never a synthesized target/Intuition interval.
    public FishingWindow DisplayWindow => OwnConditionsAlways && PrimaryPreparation is { } primary ? primary.Window : Window;
}
public sealed record RecommendationResult(DateTimeOffset At, DateTimeOffset Through, DateTimeOffset AnalysisStart,
    IReadOnlyList<FishRecommendation> Ranked, IReadOnlyList<FishRecommendation> Other)
{
    public IReadOnlyList<FishRecommendation> Now { get; init; } = [];
    public IReadOnlyList<FishRecommendation> Preparation { get; init; } = [];
    public IReadOnlyList<FishRecommendation> Rare { get; init; } = [];
}

/// <summary>Schedule and requirement browsing only. No catch-duration, route, stock or score estimates.</summary>
public sealed class FishRecommendations
{
    public const int SoonMinutes = 30;
    private readonly IReadOnlyDictionary<uint, FishingForecastInput> inputs;
    // One background worker owns this cache. Refreshes within the UTC day reuse condition calendars.
    private readonly Dictionary<uint, FishingWindow[]> calendars = [];
    private DateTimeOffset analysisStart;

    public FishRecommendations(IEnumerable<FishingForecastInput> inputs) =>
        this.inputs = inputs.GroupBy(value => value.Rule.ItemId).ToDictionary(group => group.Key, group => group.First());

    public RecommendationResult Calculate(DateTimeOffset now, int hours, IEnumerable<uint> itemIds,
        CancellationToken token = default)
    {
        if (hours is not (1 or 3 or 6 or 12 or 24)) throw new ArgumentOutOfRangeException(nameof(hours));
        token.ThrowIfCancellationRequested();
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        if (day != analysisStart) { calendars.Clear(); analysisStart = day; }
        var through = now.AddHours(hours);
        var results = new List<FishRecommendation>();
        foreach (var id in itemIds.Distinct())
        {
            token.ThrowIfCancellationRequested();
            results.Add(Describe(id, now, through, token));
        }
        var included = Order(results.Where(value => value.Issue == RecommendationIssue.None), now).ToArray();
        return new(now, through, analysisStart, included,
            results.Where(value => value.Issue != RecommendationIssue.None).OrderBy(value => value.Issue).ThenBy(value => value.ItemId).ToArray())
        {
            Now = Order(included.Where(value => InRange(value.DisplayWindow, now, now.AddMinutes(SoonMinutes))), now).ToArray(),
            Preparation = Order(included.Where(value => value.MoochDepth > 0 || value.IntuitionCatchCount > 0), now).ToArray(),
            Rare = included.Where(value => value.OpportunityCount > 0).OrderBy(value => value.OpportunityCount)
                .ThenBy(value => value.DisplayWindow.Start ?? now).ThenBy(value => value.ItemId).ToArray(),
        };
    }

    private FishRecommendation Describe(uint id, DateTimeOffset now, DateTimeOffset through, CancellationToken token)
    {
        var own = Current(id, now, token);
        FishRecommendation Unknown() => new(id, RecommendationIssue.ConditionsUnknown, own, 0, OpportunityBasis.OwnConditions, id, null);
        if (own.Kind == FishingWindowKind.Unknown || !inputs.TryGetValue(id, out var input)) return Unknown();
        var rule = input.Rule;
        var path = rule.BaitSteps.Length > 0 ? rule.BaitSteps.Skip(1).ToArray()
            : rule.BaitPath.Skip(1).Select(item => new[] { item }).ToArray();
        var preparation = path.SelectMany(step => step).Distinct()
            .Select(item => new PreparationCondition(item, 1, PreparationKind.Mooch, Current(item, now, token)))
            .Concat(rule.Predators.Select(prey => new PreparationCondition(prey.ItemId, prey.Count,
                PreparationKind.Intuition, Current(prey.ItemId, now, token)))).ToArray();
        var always = own.Kind == FishingWindowKind.Always;
        var restricted = preparation.Where(value => value.Window.Kind is FishingWindowKind.Open or FishingWindowKind.Upcoming).ToArray();
        var primary = restricted.Where(value => InRange(value.Window, now, through))
            .OrderBy(value => Count(value.ItemId, token)).ThenBy(value => value.Window.Start).ThenBy(value => value.ItemId)
                .ThenByDescending(value => value.Kind == PreparationKind.Intuition).FirstOrDefault()
            ?? restricted.OrderBy(value => value.Window.Start).ThenBy(value => value.ItemId)
                .ThenByDescending(value => value.Kind == PreparationKind.Intuition).FirstOrDefault()
            ?? preparation.OrderBy(value => value.Window.Kind == FishingWindowKind.Unknown)
                .ThenByDescending(value => value.Kind == PreparationKind.Intuition).FirstOrDefault();
        var display = always && primary is not null ? primary.Window : own;
        var issue = display.Kind == FishingWindowKind.Unknown ? RecommendationIssue.MissingPreparation
            : !InRange(display, now, through) ? RecommendationIssue.OutsideHorizon : RecommendationIssue.None;
        var basisId = always && primary is not null && primary.Window.Kind != FishingWindowKind.Always ? primary.ItemId : id;
        var basis = always ? basisId != id ? OpportunityBasis.PrerequisiteProxy : OpportunityBasis.Always : OpportunityBasis.OwnConditions;
        var nextOwn = Next(id, own, token);
        return new(id, issue, own, Count(basisId, token), basis, basisId,
            always && primary is not null ? Next(primary.ItemId, primary.Window, token) : nextOwn)
        {
            OwnConditionsAlways = always, MoochDepth = path.Length, MoochPath = path,
            IntuitionCatchCount = rule.Predators.Sum(prey => prey.Count), Preparation = preparation,
            PrimaryPreparation = primary, NextOwnOpportunity = nextOwn,
        };
    }

    private static IOrderedEnumerable<FishRecommendation> Order(IEnumerable<FishRecommendation> values, DateTimeOffset now) =>
        values.OrderBy(value => value.DisplayWindow.Kind == FishingWindowKind.Always ? 2 : value.DisplayWindow.Start > now ? 1 : 0)
            .ThenBy(value => value.DisplayWindow.Start > now ? value.DisplayWindow.Start : value.DisplayWindow.End ?? DateTimeOffset.MaxValue)
            .ThenBy(value => value.ItemId);

    private static bool InRange(FishingWindow window, DateTimeOffset now, DateTimeOffset through) =>
        window.Kind == FishingWindowKind.Always || window.Kind is FishingWindowKind.Open or FishingWindowKind.Upcoming
            && (window.End is null || window.End > now) && window.Start <= through;

    private FishingWindow Current(uint id, DateTimeOffset now, CancellationToken token)
    {
        var calendar = Calendar(id, token);
        var found = calendar.FirstOrDefault(window => window.Kind is FishingWindowKind.Always or FishingWindowKind.Unknown
            || window.End is null || window.End > now);
        return found is null ? new(FishingWindowKind.NotFound)
            : found.Kind is FishingWindowKind.Always or FishingWindowKind.Unknown ? found
            : found with { Kind = found.Start > now ? FishingWindowKind.Upcoming : FishingWindowKind.Open };
    }

    private int Count(uint id, CancellationToken token) => Calendar(id, token).Count(window =>
        window.Kind is not (FishingWindowKind.Always or FishingWindowKind.Unknown) &&
        window.Start < analysisStart.AddDays(30) && (window.End is null || window.End > analysisStart));

    private DateTimeOffset? Next(uint id, FishingWindow window, CancellationToken token) => window.End is { } end
        ? Calendar(id, token).FirstOrDefault(value => value.Start >= end)?.Start : null;

    private FishingWindow[] Calendar(uint id, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (calendars.TryGetValue(id, out var cached)) return cached;
        if (!inputs.TryGetValue(id, out var input)) return [new(FishingWindowKind.Unknown)];
        // Preserve an occurrence crossing UTC midnight and cover a 24-hour query late in the day.
        // Only intervals overlapping the 30-day analysis period contribute to the count.
        var found = FishingAvailability.Enumerate(analysisStart.AddDays(-1), analysisStart.AddDays(31), input.Rule,
            input.WeatherRates, null, token, 32).ToArray();
        calendars.Add(id, found);
        return found;
    }
}
