namespace DuckbillFisher.Core;

public enum RecommendationIssue { None, ConditionsUnknown, MissingPreparation, OutsideHorizon }
public enum OpportunityBasis { OwnConditions, PrerequisiteProxy, Always }
public enum PreparationKind { Mooch, Intuition }
public sealed record PreparationCondition(uint ItemId, int Count, PreparationKind Kind, FishingWindow Window)
{
    public int OpportunityCount { get; init; }
    public DateTimeOffset? NextOpportunity { get; init; }
}
public sealed record FishRecommendation(uint ItemId, RecommendationIssue Issue, FishingWindow Window,
    int OpportunityCount, OpportunityBasis Basis, uint BasisItemId, DateTimeOffset? NextOpportunity)
{
    public bool OwnConditionsAlways { get; init; }
    public int MoochDepth { get; init; }
    public int IntuitionCatchCount { get; init; }
    public IReadOnlyList<IReadOnlyList<uint>> MoochPath { get; init; } = [];
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
        var facts = new List<FishRecommendation>();
        foreach (var id in itemIds.Distinct())
        {
            token.ThrowIfCancellationRequested();
            facts.Add(Describe(id, now, token));
        }
        return FishRecommendationSelection.Select(now, through, analysisStart, facts);
    }

    private FishRecommendation Describe(uint id, DateTimeOffset now, CancellationToken token)
    {
        var own = Current(id, now, token);
        FishRecommendation Unknown() => new(id, RecommendationIssue.ConditionsUnknown, own, 0, OpportunityBasis.OwnConditions, id, null);
        if (own.Kind == FishingWindowKind.Unknown || !inputs.TryGetValue(id, out var input)) return Unknown();
        var rule = input.Rule;
        var path = rule.BaitSteps.Length > 0 ? rule.BaitSteps.Skip(1).ToArray()
            : rule.BaitPath.Skip(1).Select(item => new[] { item }).ToArray();
        PreparationCondition Requirement(uint item, int count, PreparationKind kind)
        {
            var window = Current(item, now, token);
            return new(item, count, kind, window)
            { OpportunityCount = Count(item, token), NextOpportunity = Next(item, window, token) };
        }
        var preparation = path.SelectMany(step => step).Distinct()
            .Select(item => Requirement(item, 1, PreparationKind.Mooch))
            .Concat(rule.Predators.Select(prey => Requirement(prey.ItemId, prey.Count, PreparationKind.Intuition))).ToArray();
        var always = own.Kind == FishingWindowKind.Always;
        var nextOwn = Next(id, own, token);
        return new(id, RecommendationIssue.None, own, Count(id, token),
            always ? OpportunityBasis.Always : OpportunityBasis.OwnConditions, id, nextOwn)
        {
            OwnConditionsAlways = always, MoochDepth = path.Length,
            MoochPath = Array.AsReadOnly(path.Select(step => (IReadOnlyList<uint>)Array.AsReadOnly(step.ToArray())).ToArray()),
            IntuitionCatchCount = rule.Predators.Sum(prey => prey.Count), Preparation = Array.AsReadOnly(preparation),
            NextOwnOpportunity = nextOwn,
        };
    }

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
