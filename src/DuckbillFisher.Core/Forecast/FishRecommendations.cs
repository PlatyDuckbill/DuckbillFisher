namespace DuckbillFisher.Core;

public enum RecommendationIssue { None, ConditionsUnknown, MissingPreparation, IntuitionDurationUnknown, NestedIntuition, AmbiguousPath, OutsideHorizon, NoFreshPath, EstimatedLate }
public enum RecommendationReadiness { Planned, EstimateOnly, Late }
public enum PreparationRole { Mooch, Intuition, Trigger, StoredMooch }
public enum RecommendationStrategy { Basic, RonkaStored }
public enum OpportunityBasis { OwnConditions, PrerequisiteProxy, Always }
public sealed record PreparationBudget(double ShortMinutes, double PlanningMinutes, double LongMinutes);
public sealed record OptionalStoredPreparation(uint ItemId, int ExtraCount, PreparationBudget Budget, DateTimeOffset? SuggestedStart);
public sealed record RecommendationStep(uint ItemId, int Count, PreparationRole Role, PreparationBudget Budget,
    DateTimeOffset Start, DateTimeOffset End);
public sealed record RecommendationPlan(RecommendationStrategy Strategy, DateTimeOffset RecommendedStart,
    DateTimeOffset? FirstCast, DateTimeOffset? FastFirstCast, DateTimeOffset? SlowFirstCast,
    IReadOnlyList<RecommendationStep> Steps, double WorkMinutes, double WaitMinutes, double Timeliness,
    double Room, double Score)
{
    public bool SharedCatch { get; init; }
    public uint StoredItemId { get; init; }
    public OptionalStoredPreparation? OptionalStock { get; init; }
    public RecommendationReadiness Readiness { get; init; }
    public double ShortWorkMinutes { get; init; }
    public double LongWorkMinutes { get; init; }
    public double AdvanceWorkMinutes { get; init; }
    public bool HasAdvancePreparation { get; init; }
    public int AdvanceMoochCount { get; init; }
    public int AdvanceIntuitionCount { get; init; }
}
public sealed record FishRecommendation(uint ItemId, RecommendationIssue Issue, FishingWindow? Window,
    RecommendationPlan? Plan, IReadOnlyList<RecommendationPlan> Alternatives, double Rarity, double NextGapScore,
    int OpportunityCount, OpportunityBasis Basis, uint BasisItemId, DateTimeOffset? NextOpportunity)
{
    public int OwnOpportunityCount { get; init; }
    public bool OwnConditionsAlways { get; init; }
    public int MoochDepth { get; init; }
    public int IntuitionCatchCount { get; init; }
    public uint PreparationGateItem { get; init; }
    public DateTimeOffset? PreparationGateEnd { get; init; }
    public double? GapHours { get; init; }
    // Target time/weather calendar, separate from the prerequisite used for rarity scoring.
    public DateTimeOffset? NextOwnOpportunity { get; init; }
}
public sealed record RecommendationResult(DateTimeOffset At, DateTimeOffset Through, DateTimeOffset AnalysisStart,
    IReadOnlyList<FishRecommendation> Ranked, IReadOnlyList<FishRecommendation> Other)
{
    public IReadOnlyList<FishRecommendation> Now { get; init; } = [];
    public IReadOnlyList<FishRecommendation> Preparation { get; init; } = [];
    public IReadOnlyList<FishRecommendation> Rare { get; init; } = [];
}

/// <summary>Fresh-start planning, with explicit design estimates, not catch probabilities.
/// Own condition calendars are independent of the existing Intuition forecast contract.</summary>
public sealed class FishRecommendations
{
    public const int StrategyRevision = 4;
    public const double TravelMinutes = 2;
    public const double ActionSoonMinutes = 5;
    private const double RegularCatchPlanningMinutes = 1.5;
    private const double PreparationWaitScaleMinutes = 60;
    private readonly IReadOnlyDictionary<uint, FishingForecastInput> inputs;
    // Owned by one background worker at a time. No UI/game objects or translated text.
    private readonly Dictionary<uint, FishingWindow[]> calendars = [];
    private DateTimeOffset analysisStart;
    private sealed record TaskStep(uint ItemId, PreparationRole Role, PreparationBudget Budget);
    private sealed record Strategy(RecommendationStrategy Kind, TaskStep[] Tasks, int TriggerIndex, bool SharedCatch, uint StoredItem);
    private sealed record Placement(DateTimeOffset First, DateTimeOffset Cast, RecommendationStep[] Steps,
        double Work, double Wait, DateTimeOffset? Triggered = null);

    public FishRecommendations(IEnumerable<FishingForecastInput> inputs) =>
        this.inputs = inputs.GroupBy(value => value.Rule.ItemId).ToDictionary(group => group.Key, group => group.First());

    public RecommendationResult Calculate(DateTimeOffset now, int hours, IEnumerable<uint> itemIds,
        bool allowStorage, CancellationToken token = default)
    {
        if (hours is not (1 or 3 or 6 or 12 or 24)) throw new ArgumentOutOfRangeException(nameof(hours));
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        if (day != analysisStart) { calendars.Clear(); analysisStart = day; }
        var through = now.AddHours(hours);
        var results = new List<FishRecommendation>();
        var nowCandidates = new List<FishRecommendation>();
        var preparationCandidates = new List<FishRecommendation>();
        var rareCandidates = new List<FishRecommendation>();
        foreach (var id in itemIds.Distinct())
        {
            token.ThrowIfCancellationRequested();
            results.Add(CalculateFish(id, now, through, allowStorage, token, out var candidates));
            var viable = candidates.Where(value => value.Issue == RecommendationIssue.None).ToArray();
            var actionable = viable.Where(value => value.Plan!.RecommendedStart <= now.AddMinutes(ActionSoonMinutes)).ToArray();
            if (actionable.Length > 0) nowCandidates.Add(Best(actionable));
            var prepare = OrderPreparation(viable.Where(value => IsAdvancePreparationCandidate(value, now)), now).ToArray();
            if (prepare.Length > 0) preparationCandidates.Add(prepare[0]);
            // A rarity view orders known restricted conditions; it does not invent a rarity cutoff.
            // Prefer the earliest reachable occurrence, retaining an estimated-late one only if none fits.
            var rare = (viable.Length > 0 ? viable : candidates).Where(value => value.OpportunityCount > 0)
                .OrderBy(value => value.Window?.Start ?? now).ThenBy(value => value.Plan!.Readiness)
                .ThenBy(value => value.Plan!.Strategy).FirstOrDefault();
            if (rare is not null) rareCandidates.Add(rare);
        }
        return new(now, through, analysisStart,
            results.Where(value => value.Issue == RecommendationIssue.None && value.Plan is not null).OrderByDescending(value => value.Plan!.Score)
                .ThenBy(value => value.Plan!.RecommendedStart).ThenBy(value => value.ItemId).ToArray(),
            results.Where(value => value.Issue != RecommendationIssue.None || value.Plan is null).OrderBy(value => value.Issue).ThenBy(value => value.ItemId).ToArray())
        {
            Now = nowCandidates.OrderByDescending(value => value.Plan!.Score).ThenBy(value => value.ItemId).ToArray(),
            Preparation = OrderPreparation(preparationCandidates, now).ToArray(),
            Rare = rareCandidates.OrderBy(value => value.OpportunityCount).ThenByDescending(value => value.GapHours ?? 0)
                .ThenBy(value => value.Window?.Start).ThenBy(value => value.ItemId).ToArray(),
        };
    }

    private static bool IsAdvancePreparationCandidate(FishRecommendation value, DateTimeOffset now)
    {
        var plan = value.Plan!;
        if (!plan.HasAdvancePreparation) return false;
        if (!value.OwnConditionsAlways && !(value.Window?.Start > now)) return false;
        // Merely starting a catch before opening is not an item secured in advance.
        if (plan.AdvanceIntuitionCount + plan.AdvanceMoochCount == 0) return false;
        if (plan.AdvanceIntuitionCount > 0 || plan.AdvanceMoochCount >= 2) return true;
        var opening = value.Window?.Start;
        // A single ordinary mooch fish normally belongs in target-now. Keep it here
        // if it closes by the time the target opens, or the catch itself needs extra work.
        return (value.PreparationGateEnd is { } gate && opening is { } start && gate <= start) ||
            plan.Steps.Any(step => (value.OwnConditionsAlways || step.End <= opening) &&
                step.Budget.PlanningMinutes > RegularCatchPlanningMinutes);
    }

    private static IOrderedEnumerable<FishRecommendation> OrderPreparation(IEnumerable<FishRecommendation> candidates, DateTimeOffset now) =>
        // Smoothly discount work that starts later. The old due-within-five-minutes
        // bucket put a tiny imminent catch ahead of substantial preparation.
        candidates.OrderByDescending(value => value.Plan!.AdvanceWorkMinutes /
                (1 + Math.Max(0, (value.Plan.RecommendedStart - now).TotalMinutes) / PreparationWaitScaleMinutes))
            .ThenByDescending(value => value.Plan!.AdvanceWorkMinutes)
            .ThenBy(value => value.Plan!.RecommendedStart).ThenBy(value => value.ItemId);

    private FishingWindow[] Calendar(uint id, CancellationToken token)
    {
        if (calendars.TryGetValue(id, out var cached)) return cached;
        if (!inputs.TryGetValue(id, out var input)) return [new(FishingWindowKind.Unknown)];
        // Include one day of look-back for reverse preparation and the complete 30-day analysis.
        var found = FishingAvailability.Enumerate(analysisStart.AddDays(-1), analysisStart.AddDays(30),
            input.Rule, input.WeatherRates, null, token, 31).ToArray();
        calendars.Add(id, found);
        return found;
    }

    private FishRecommendation CalculateFish(uint id, DateTimeOffset now, DateTimeOffset through,
        bool allowStorage, CancellationToken token, out FishRecommendation[] candidates)
    {
        candidates = [];
        FishRecommendation Other(RecommendationIssue issue, FishingWindow? next = null) =>
            new(id, issue, next, null, [], 0, 0, 0, OpportunityBasis.OwnConditions, id, null);
        if (!inputs.TryGetValue(id, out var input) || !FishingAvailability.IsValid(input.Rule, input.WeatherRates))
            return Other(RecommendationIssue.ConditionsUnknown);
        var rule = input.Rule;
        if (rule.Predators.Length > 0 && !(rule.IntuitionSeconds is > 0))
            return Other(RecommendationIssue.IntuitionDurationUnknown);
        var strategies = Strategies(rule, allowStorage, out var issue);
        if (issue != RecommendationIssue.None) return Other(issue);
        var dependentIds = strategies.SelectMany(value => value.Tasks).Select(value => value.ItemId).Append(id).Distinct().ToArray();
        if (dependentIds.Any(item => Calendar(item, token).Any(value => value.Kind == FishingWindowKind.Unknown)))
            return Other(RecommendationIssue.MissingPreparation);

        // A clearly labelled single-condition proxy, never a claimed full Intuition frequency.
        // Use the least frequent required calendar; count merged own-condition episodes, not buff fragments.
        var basisId = id;
        var basis = FishingAvailability.IsAlways(rule) ? OpportunityBasis.Always : OpportunityBasis.OwnConditions;
        var count = Count(id, token);
        foreach (var dependency in dependentIds)
        {
            var dependencyCount = Count(dependency, token);
            if (dependencyCount >= count) continue;
            count = dependencyCount; basisId = dependency;
            basis = dependency == id ? OpportunityBasis.OwnConditions : OpportunityBasis.PrerequisiteProxy;
        }
        if (count == int.MaxValue) { count = 0; basis = OpportunityBasis.Always; }
        var rarity = count == 0 ? 0 : Math.Clamp(Math.Log10(1 + 30.0 / count) / Math.Log10(31), 0, 1);
        var occurrences = Occurrences(rule, now, through, strategies, token).ToArray();
        if (occurrences.Length == 0)
        {
            if (!FishingAvailability.IsAlways(rule) && Calendar(id, token).Any(window => window.Start < through && window.End is null))
                return Other(RecommendationIssue.ConditionsUnknown);
            return Other(RecommendationIssue.OutsideHorizon, Calendar(id, token).FirstOrDefault(window => window.Start > through));
        }
        var choices = new List<(FishingWindow Window, RecommendationPlan Plan, double NextScore, DateTimeOffset? Next)>();
        foreach (var window in occurrences)
        {
            token.ThrowIfCancellationRequested();
            var start = window.Start is { } s && s > now ? s : now;
            var end = window.End ?? through.AddDays(1);
            foreach (var strategy in strategies)
            {
                // Zero-work placement distinguishes a closed prerequisite from an estimated late attempt.
                var possible = Forward(strategy, rule, now, start, end, -1, null, token);
                if (possible is null) continue;
                // Establish the first reachable target cast from NOW, then move flexible work later.
                // Starting the reference route yesterday would falsely pre-fill earlier Intuition catches.
                var reference = Forward(strategy, rule, now.AddMinutes(TravelMinutes), start, end, 1, null, token);
                var earliestFast = Forward(strategy, rule, now.AddMinutes(TravelMinutes), start, end, 0, null, token);
                var desiredCast = reference?.Cast ?? earliestFast?.Cast ?? start;
                var reversed = Reverse(strategy, desiredCast, 1, token);
                var recommended = reversed?.First.AddMinutes(-TravelMinutes) ?? start.AddMinutes(-TravelMinutes);
                var planned = Forward(strategy, rule, now.AddMinutes(TravelMinutes), start, end, 1, reversed, token) ?? reference;
                var fast = Forward(strategy, rule, now.AddMinutes(TravelMinutes), start, end, 0, Reverse(strategy, desiredCast, 0, token), token) ?? earliestFast;
                var slow = Forward(strategy, rule, now.AddMinutes(TravelMinutes), start, end, 2, Reverse(strategy, desiredCast, 2, token), token)
                    ?? Forward(strategy, rule, now.AddMinutes(TravelMinutes), start, end, 2, null, token);
                var t = 1 / (1 + Math.Max(0, (recommended - now).TotalMinutes) / 20);
                var q = planned is not null ? slow is not null ? 1 : 0.75 : fast is not null || slow is not null ? 0.5 : 0;
                var shown = planned ?? fast ?? slow ?? reversed;
                if (shown is null) continue; // A zero-work feasibility sketch is never a preparation estimate.
                var advance = AdvancePreparation(rule, window, shown.Steps);
                var (next, gap) = NextOpportunity(basisId, basis, window, shown.Steps, token);
                var nextScore = gap is { } hours ? Math.Clamp(Math.Log10(1 + hours) / Math.Log10(73), 0, 1) : 0;
                var plan = new RecommendationPlan(strategy.Kind, recommended, planned?.Cast, fast?.Cast, slow?.Cast,
                    shown.Steps, shown.Work, shown.Wait, t, q, 40 * t + 30 * rarity + 20 * q + 10 * nextScore)
                { SharedCatch = strategy.SharedCatch, StoredItemId = strategy.StoredItem,
                    OptionalStock = OptionalStock(strategy, desiredCast, token),
                    Readiness = planned is not null ? RecommendationReadiness.Planned : q > 0 ? RecommendationReadiness.EstimateOnly : RecommendationReadiness.Late,
                    ShortWorkMinutes = strategy.Tasks.Sum(step => step.Budget.ShortMinutes),
                    LongWorkMinutes = strategy.Tasks.Sum(step => step.Budget.LongMinutes),
                    AdvanceWorkMinutes = advance.Minutes, HasAdvancePreparation = advance.Minutes > 0,
                    AdvanceMoochCount = advance.MoochCount, AdvanceIntuitionCount = advance.IntuitionCount };
                choices.Add((window, plan, nextScore, next));
            }
        }
        if (choices.Count == 0) return Other(RecommendationIssue.NoFreshPath, occurrences[0]);
        candidates = choices.Select(choice =>
        {
            var gate = choice.Plan.Steps.Where(step => step.Start <= now.AddMinutes(ActionSoonMinutes + TravelMinutes))
                .Select(step => (step.ItemId, Window: Calendar(step.ItemId, token)
                    .FirstOrDefault(value => value.Start <= step.Start && value.End > step.Start)))
                .Where(value => value.Window?.Start <= now && value.Window.End > now)
                .OrderBy(value => value.Window!.End).FirstOrDefault();
            var (_, gap) = NextOpportunity(basisId, basis, choice.Window, choice.Plan.Steps, token);
            return new FishRecommendation(id, choice.Plan.Readiness == RecommendationReadiness.Late ? RecommendationIssue.EstimatedLate : RecommendationIssue.None,
                choice.Window, choice.Plan, choices.Where(value => value.Window == choice.Window && value.Plan != choice.Plan)
                    .GroupBy(value => value.Plan.Strategy).Select(group => group.OrderByDescending(value => value.Plan.Score).First().Plan).ToArray(),
                rarity, choice.NextScore, count, basis, basisId, choice.Next)
            {
                OwnOpportunityCount = FishingAvailability.IsAlways(rule) ? 0 : Count(id, token),
                OwnConditionsAlways = FishingAvailability.IsAlways(rule), MoochDepth = Path(rule)!.Length,
                IntuitionCatchCount = rule.Predators.Sum(prey => prey.Count),
                PreparationGateItem = gate.ItemId, PreparationGateEnd = gate.Window?.End, GapHours = gap,
                NextOwnOpportunity = FishingAvailability.IsAlways(rule) ? null
                    : NextOpportunity(id, OpportunityBasis.OwnConditions, choice.Window, choice.Plan.Steps, token).Next,
            };
        }).ToArray();
        var feasible = candidates.Where(value => value.Issue == RecommendationIssue.None).ToArray();
        return Best(feasible.Length > 0 ? feasible : candidates);
    }

    private static FishRecommendation Best(FishRecommendation[] choices)
    {
        var bestScore = choices.Max(value => value.Plan!.Score);
        return choices.Where(value => value.Plan!.Score >= bestScore - 3).OrderBy(value => value.Plan!.Readiness)
            .ThenBy(value => value.Plan!.WaitMinutes).ThenBy(value => value.Plan!.WorkMinutes)
            .ThenBy(value => value.Plan!.Strategy).ThenBy(value => value.Window?.Start).First();
    }

    private static (double Minutes, int MoochCount, int IntuitionCount) AdvancePreparation(
        FishingRule rule, FishingWindow window, IReadOnlyList<RecommendationStep> steps)
    {
        // An unrestricted Intuition target has no opening to prepare before; its prerequisites
        // are the preparation itself. Restricted targets count only work before their opening.
        var unrestrictedIntuition = FishingAvailability.IsAlways(rule) && rule.Predators.Length > 0;
        var minutes = 0.0;
        var mooch = 0; var intuition = 0;
        foreach (var step in steps)
        {
            var fraction = unrestrictedIntuition ? 1 : window.Start is { } opening && step.End > step.Start
                ? Math.Clamp((opening - step.Start).TotalSeconds / (step.End - step.Start).TotalSeconds, 0, 1) : 0;
            minutes += step.Budget.PlanningMinutes * fraction;
            // A catch completed after opening is not described as bait/prey already secured.
            if (fraction < 1) continue;
            if (step.Role is PreparationRole.Intuition or PreparationRole.Trigger) intuition += step.Count;
            else mooch += step.Count;
        }
        return (minutes, mooch, intuition);
    }

    private (DateTimeOffset? Next, double? GapHours) NextOpportunity(uint basisId, OpportunityBasis basis,
        FishingWindow targetWindow, IReadOnlyList<RecommendationStep> steps, CancellationToken token)
    {
        if (basis == OpportunityBasis.Always) return (null, null);
        var calendar = Calendar(basisId, token);
        var gate = targetWindow;
        if (basis == OpportunityBasis.PrerequisiteProxy)
        {
            var step = steps.LastOrDefault(step => step.ItemId == basisId);
            gate = step is null ? targetWindow : calendar.FirstOrDefault(window => window.Start <= step.Start && window.End > step.Start) ?? targetWindow;
        }
        if (gate.End is not { } end) return (null, null);
        var next = calendar.FirstOrDefault(window => window.Start >= end)?.Start;
        return (next, next is { } at ? Math.Max(0, (at - end).TotalHours) : null);
    }

    private int Count(uint id, CancellationToken token)
    {
        var calendar = Calendar(id, token);
        if (calendar.Any(window => window.Kind == FishingWindowKind.Always)) return int.MaxValue;
        // Count a continuous episode crossing the analysis boundary once as well.
        return calendar.Count(window => window.Start < analysisStart.AddDays(30) &&
            (window.End > analysisStart || window.Start is not null && window.End is null));
    }

    private IEnumerable<FishingWindow> Occurrences(FishingRule rule, DateTimeOffset now, DateTimeOffset through,
        Strategy[] strategies, CancellationToken token)
    {
        if (!FishingAvailability.IsAlways(rule))
        {
            foreach (var window in Calendar(rule.ItemId, token).Where(value => value.End > now && value.Start < through)) yield return window;
            yield break;
        }
        var gates = strategies.SelectMany(value => value.Tasks).Select(value => value.ItemId).Distinct()
            .Where(id => !FishingAvailability.IsAlways(inputs[id].Rule)).ToArray();
        if (gates.Length == 0) { yield return new(FishingWindowKind.Always); yield break; }
        // These are preparation opportunities, not a global spawn timer. Include trigger carry-over explicitly.
        var intervals = gates.SelectMany(id => Calendar(id, token)).Where(value => value.Start < through &&
                value.End?.AddSeconds(rule.IntuitionSeconds ?? 0) > now)
            .Select(value => new FishingWindow(FishingWindowKind.Upcoming, value.Start, value.End?.AddSeconds(rule.IntuitionSeconds ?? 0))
                { Basis = FishingForecastBasis.Intuition }).OrderBy(value => value.Start).ToArray();
        FishingWindow? merged = null;
        foreach (var interval in intervals)
        {
            if (merged is not null && interval.Start <= merged.End)
                merged = merged with { End = interval.End > merged.End ? interval.End : merged.End };
            else { if (merged is not null) yield return merged; merged = interval; }
        }
        if (merged is not null) yield return merged;
    }

    private Strategy[] Strategies(FishingRule rule, bool storage, out RecommendationIssue issue)
    {
        issue = RecommendationIssue.None;
        var path = Path(rule);
        if (path is null) { issue = RecommendationIssue.AmbiguousPath; return []; }
        foreach (var id in path.Concat(rule.Predators.Select(value => value.ItemId)))
        {
            if (!inputs.TryGetValue(id, out var input)) { issue = RecommendationIssue.MissingPreparation; return []; }
            if (input.Rule.Predators.Length > 0) { issue = RecommendationIssue.NestedIntuition; return []; }
            if (input.Rule.FishingSpotId != rule.FishingSpotId &&
                (input.Rule.WeatherIds.Length > 0 || input.Rule.PreviousWeatherIds.Length > 0))
            { issue = RecommendationIssue.MissingPreparation; return []; }
        }
        if (rule.Predators.Length > 3 || rule.Predators.Any(prey => prey.Count is < 1 or > 100))
        { issue = RecommendationIssue.MissingPreparation; return []; }
        var result = new List<Strategy>();
        foreach (var order in Orders(rule.Predators))
        {
            var tasks = new List<TaskStep>();
            var trigger = -1;
            var shared = order.Length > 0 && path.Length > 0 && order[^1].ItemId == path[^1];
            // Reusing the final predator as target bait is only modelled for a verified single-step path.
            if (shared && path.Length != 1) continue;
            foreach (var predator in order)
            {
                var prey = inputs[predator.ItemId].Rule;
                var preyPath = Path(prey);
                if (preyPath is null || preyPath.Any(id => !inputs.ContainsKey(id) || inputs[id].Rule.Predators.Length > 0 ||
                    rule.Predators.Any(requirement => requirement.ItemId == id)))
                { issue = RecommendationIssue.MissingPreparation; return []; }
                for (var number = 0; number < predator.Count; number++)
                {
                    tasks.AddRange(preyPath.Select(id => Step(id, PreparationRole.Mooch)));
                    tasks.Add(Step(predator.ItemId, PreparationRole.Intuition));
                }
                trigger = tasks.Count - 1;
            }
            if (trigger >= 0) tasks[trigger] = tasks[trigger] with { Role = PreparationRole.Trigger };
            if (!shared) tasks.AddRange(path.Select(id => Step(id, PreparationRole.Mooch)));
            result.Add(new(RecommendationStrategy.Basic, tasks.ToArray(), trigger, shared, 0));
            // Explicit user-proposed Ronka strategy only; never infer storage eligibility for every bait fish.
            if (storage && rule.ItemId == 33243 && path.SequenceEqual(new uint[] { 27490, 27491, 28071 }) && trigger < 0)
            {
                var stored = tasks.ToArray();
                stored[1] = stored[1] with { Role = PreparationRole.StoredMooch };
                result.Add(new(RecommendationStrategy.RonkaStored, stored, -1, false, 27491));
            }
        }
        if (result.Count == 0) issue = RecommendationIssue.MissingPreparation;
        return result.ToArray();
    }

    private static uint[]? Path(FishingRule rule)
    {
        if (rule.BaitSteps.Skip(1).Any(step => step.Length != 1)) return null;
        return rule.BaitSteps.Length > 0 ? rule.BaitSteps.Skip(1).Select(step => step[0]).ToArray() : rule.BaitPath.Skip(1).ToArray();
    }

    private TaskStep Step(uint id, PreparationRole role)
    {
        // Design-estimate v1. Wide ranges are not empirical percentiles or bait bite times.
        // Rare predecessor work has a separate budget; availability is placed per stage, never depth * minutes.
        var budget = inputs[id].Rule.BigFish ? new PreparationBudget(0.5, 6, 15) : new PreparationBudget(0.25, RegularCatchPlanningMinutes, 4);
        if (id == 20040) budget = new(0.5, 6, 15); // Sculptor: reviewed difficult prerequisite, despite its regular grade.
        // Ronka's final mooch stage has only ET10-12. Separate provisional planning budget;
        // the long bound may still consume most of that opportunity. No measured bite-time claim.
        if (id == 28071) budget = new(0.25, 1, 4);
        return new(id, role, budget);
    }

    private static IEnumerable<FishRequirement[]> Orders(FishRequirement[] items)
    {
        if (items.Length == 0) { yield return []; yield break; }
        for (var index = 0; index < items.Length; index++)
            foreach (var rest in Orders(items.Where((_, i) => i != index).ToArray())) yield return [items[index], .. rest];
    }

    private OptionalStoredPreparation? OptionalStock(Strategy strategy, DateTimeOffset cast, CancellationToken token)
    {
        if (strategy.Kind != RecommendationStrategy.RonkaStored) return null;
        // One optional spare, only if the player has a free storage slot. No effect on the first-cast score.
        var prefix = strategy.Tasks.Take(2).ToArray();
        var extra = strategy with { Tasks = [.. prefix, .. strategy.Tasks] };
        var placement = Reverse(extra, cast, 1, token);
        return new(strategy.StoredItem, 1, new(prefix.Sum(step => step.Budget.ShortMinutes),
            prefix.Sum(step => step.Budget.PlanningMinutes), prefix.Sum(step => step.Budget.LongMinutes)),
            placement?.First.AddMinutes(-TravelMinutes));
    }

    private static double Minutes(TaskStep step, int scenario) => scenario switch
    { -1 => 0, 0 => step.Budget.ShortMinutes, 1 => step.Budget.PlanningMinutes, _ => step.Budget.LongMinutes };

    private Placement? Reverse(Strategy strategy, DateTimeOffset cast, int scenario, CancellationToken token)
    {
        var next = cast;
        var steps = new RecommendationStep[strategy.Tasks.Length];
        var work = 0.0;
        for (var index = steps.Length - 1; index >= 0; index--)
        {
            var task = strategy.Tasks[index];
            var duration = TimeSpan.FromMinutes(Minutes(task, scenario));
            var range = Calendar(task.ItemId, token).LastOrDefault(window => window.Kind == FishingWindowKind.Always ||
                window.Start <= next - duration && (window.End is null || window.End >= window.Start + duration));
            if (range is null) return null;
            var end = range.End is { } endAt && endAt <= next ? duration == TimeSpan.Zero ? endAt.AddMilliseconds(-1) : endAt : next;
            var start = end - duration;
            if (range.Start > start) return null;
            steps[index] = new(task.ItemId, 1, task.Role, task.Budget, start, end);
            work += duration.TotalMinutes; next = start;
        }
        return new(next, cast, steps, work, Math.Max(0, (cast - next).TotalMinutes - work));
    }

    private Placement? Forward(Strategy strategy, FishingRule target, DateTimeOffset arrival,
        DateTimeOffset opening, DateTimeOffset closing, int scenario, Placement? intended, CancellationToken token)
    {
        if (strategy.TriggerIndex < 0)
            return PlaceForward(strategy, target, arrival, opening, closing, scenario, intended, null, token);
        var lifetime = target.IntuitionSeconds!.Value;
        if (strategy.Tasks.Skip(strategy.TriggerIndex + 1).Sum(step => Minutes(step, scenario)) * 60 >= lifetime) return null;
        DateTimeOffset? triggerNotBefore = null;
        // If a later bait gate consumes the buff, keep the accumulated catches and defer the trigger.
        // Every repair advances the trigger. Bound work and observe cancellation on each pass.
        for (var retry = 0; retry < 128; retry++)
        {
            token.ThrowIfCancellationRequested();
            var placed = PlaceForward(strategy, target, arrival, opening, closing, scenario, intended, triggerNotBefore, token);
            if (placed is null) return null;
            if (placed.Triggered is not { } triggered || placed.Cast < triggered.AddSeconds(lifetime)) return placed;
            triggerNotBefore = placed.Cast.AddSeconds(-lifetime).AddMilliseconds(1)
                .AddMinutes(-Minutes(strategy.Tasks[strategy.TriggerIndex], scenario));
        }
        return null;
    }

    private Placement? PlaceForward(Strategy strategy, FishingRule target, DateTimeOffset arrival,
        DateTimeOffset opening, DateTimeOffset closing, int scenario, Placement? intended,
        DateTimeOffset? triggerNotBefore, CancellationToken token)
    {
        var cursor = arrival;
        if (scenario == 1 && intended?.First > cursor) cursor = intended.First;
        var steps = new List<RecommendationStep>();
        DateTimeOffset? triggered = null;
        var work = 0.0;
        for (var index = 0; index < strategy.Tasks.Length; index++)
        {
            var task = strategy.Tasks[index];
            var duration = TimeSpan.FromMinutes(Minutes(task, scenario));
            var taskRule = inputs[task.ItemId].Rule;
            if (!FishingAvailability.IsAlways(target) && taskRule.StartHour == target.StartHour &&
                taskRule.EndHour == target.EndHour && taskRule.WeatherIds.SequenceEqual(target.WeatherIds) &&
                taskRule.PreviousWeatherIds.SequenceEqual(target.PreviousWeatherIds) && cursor < opening)
                cursor = opening;
            // Keep the final trigger close to the target; accumulated prerequisites remain separate.
            if (index == strategy.TriggerIndex)
            {
                var after = strategy.Tasks.Skip(index + 1).Sum(step => Minutes(step, scenario));
                var finish = opening.AddMinutes(-after);
                var lastBeforeOpening = Reverse(new(strategy.Kind, [task], 0, false, 0), finish, scenario, token);
                var desired = intended?.Steps.ElementAtOrDefault(index)?.Start ?? lastBeforeOpening?.First ?? finish - duration;
                // A final catch just before the prey closes may carry Intuition into the target opening.
                // Do not demand that the prey is still available at the target's exact start.
                var earliestTriggerStart = opening.AddSeconds(-target.IntuitionSeconds!.Value).AddMilliseconds(1) - duration;
                if (desired < earliestTriggerStart) desired = earliestTriggerStart;
                if (triggerNotBefore > desired) desired = triggerNotBefore.Value;
                if (desired > cursor) cursor = desired;
            }
            DateTimeOffset? start = null;
            foreach (var window in Calendar(task.ItemId, token))
            {
                var candidate = window.Start is { } from && from > cursor ? from : cursor;
                if (window.Kind == FishingWindowKind.Always || window.End > candidate && candidate + duration <= window.End)
                { start = candidate; break; }
            }
            if (start is null || start >= closing) return null;
            cursor = start.Value + duration;
            steps.Add(new(task.ItemId, 1, task.Role, task.Budget, start.Value, cursor));
            work += duration.TotalMinutes;
            if (index == strategy.TriggerIndex) triggered = cursor;
        }
        var cast = cursor > opening ? cursor : opening;
        if (cast >= closing) return null;
        return new(steps.Count > 0 ? steps[0].Start : cast, cast, steps.ToArray(), work,
            Math.Max(0, (cast - (steps.Count > 0 ? steps[0].Start : cast)).TotalMinutes - work), triggered);
    }
}
