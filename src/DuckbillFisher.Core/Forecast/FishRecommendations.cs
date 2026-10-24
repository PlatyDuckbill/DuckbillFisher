namespace DuckbillFisher.Core;

public enum RecommendationIssue { None, ConditionsUnknown, MissingPreparation, IntuitionDurationUnknown, NestedIntuition, AmbiguousPath, OutsideHorizon, NoFreshPath }
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
}
public sealed record FishRecommendation(uint ItemId, RecommendationIssue Issue, FishingWindow? Window,
    RecommendationPlan? Plan, IReadOnlyList<RecommendationPlan> Alternatives, double Rarity, double NextGapScore,
    int OpportunityCount, OpportunityBasis Basis, uint BasisItemId, DateTimeOffset? NextOpportunity);
public sealed record RecommendationResult(DateTimeOffset At, DateTimeOffset Through, DateTimeOffset AnalysisStart,
    IReadOnlyList<FishRecommendation> Ranked, IReadOnlyList<FishRecommendation> Other);

/// <summary>Fresh-start planning, with explicit design estimates, not catch probabilities.
/// Own condition calendars are independent of the existing Intuition forecast contract.</summary>
public sealed class FishRecommendations
{
    public const int StrategyRevision = 1;
    public const double TravelMinutes = 2;
    private readonly IReadOnlyDictionary<uint, FishingForecastInput> inputs;
    // Owned by one background worker at a time. No UI/game objects or translated text.
    private readonly Dictionary<uint, FishingWindow[]> calendars = [];
    private DateTimeOffset analysisStart;
    private sealed record TaskStep(uint ItemId, PreparationRole Role, PreparationBudget Budget);
    private sealed record Strategy(RecommendationStrategy Kind, TaskStep[] Tasks, int TriggerIndex, bool SharedCatch, uint StoredItem);
    private sealed record Placement(DateTimeOffset First, DateTimeOffset Cast, RecommendationStep[] Steps,
        double Work, double Wait);

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
        foreach (var id in itemIds.Distinct())
        {
            token.ThrowIfCancellationRequested();
            results.Add(CalculateFish(id, now, through, allowStorage, token));
        }
        return new(now, through, analysisStart,
            results.Where(value => value.Plan is not null).OrderByDescending(value => value.Plan!.Score)
                .ThenBy(value => value.Plan!.RecommendedStart).ThenBy(value => value.ItemId).ToArray(),
            results.Where(value => value.Plan is null).OrderBy(value => value.Issue).ThenBy(value => value.ItemId).ToArray());
    }

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
        bool allowStorage, CancellationToken token)
    {
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
        else if (dependentIds.Length > 1) basis = OpportunityBasis.PrerequisiteProxy;
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
            var next = basis == OpportunityBasis.Always ? null : Calendar(basisId, token)
                .FirstOrDefault(value => value.Start >= (window.End ?? start.AddSeconds(1)))?.Start;
            var nextScore = next is { } nextAt
                ? Math.Clamp(Math.Log10(1 + Math.Max(0, (nextAt - end).TotalHours)) / Math.Log10(73), 0, 1) : 0;
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
                var planned = Forward(strategy, rule, now.AddMinutes(TravelMinutes), start, end, 1, reversed, token);
                var fast = Forward(strategy, rule, now.AddMinutes(TravelMinutes), start, end, 0, Reverse(strategy, desiredCast, 0, token), token) ?? earliestFast;
                var slow = Forward(strategy, rule, now.AddMinutes(TravelMinutes), start, end, 2, Reverse(strategy, desiredCast, 2, token), token)
                    ?? Forward(strategy, rule, now.AddMinutes(TravelMinutes), start, end, 2, null, token);
                var t = 1 / (1 + Math.Max(0, (recommended - now).TotalMinutes) / 20);
                var q = slow is not null ? 1 : fast is not null ? 0.5 : 0;
                var shown = planned ?? fast ?? reversed ?? possible;
                var plan = new RecommendationPlan(strategy.Kind, recommended, planned?.Cast, fast?.Cast, slow?.Cast,
                    shown.Steps, shown.Work, shown.Wait, t, q, 40 * t + 30 * rarity + 20 * q + 10 * nextScore)
                { SharedCatch = strategy.SharedCatch, StoredItemId = strategy.StoredItem,
                    OptionalStock = OptionalStock(strategy, desiredCast, token) };
                choices.Add((window, plan, nextScore, next));
            }
        }
        if (choices.Count == 0) return Other(RecommendationIssue.NoFreshPath, occurrences[0]);
        var bestScore = choices.Max(value => value.Plan.Score);
        var best = choices.Where(value => value.Plan.Score >= bestScore - 3)
            .OrderBy(value => value.Plan.WaitMinutes).ThenBy(value => value.Plan.WorkMinutes)
            .ThenBy(value => value.Plan.Strategy).ThenBy(value => value.Window.Start).First();
        return new(id, RecommendationIssue.None, best.Window, best.Plan,
            choices.Where(value => value.Window == best.Window && value.Plan != best.Plan)
                .GroupBy(value => value.Plan.Strategy).Select(group => group.OrderByDescending(value => value.Plan.Score).First().Plan).ToArray(),
            rarity, best.NextScore, count, basis, basisId, best.Next);
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
        var budget = inputs[id].Rule.BigFish ? new PreparationBudget(0.5, 6, 15) : new PreparationBudget(0.25, 1.5, 4);
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
            var end = range.End is { } endAt && endAt <= next ? endAt.AddMilliseconds(-1) : next;
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
        if (cast >= closing || triggered is { } trigger && cast >= trigger.AddSeconds(target.IntuitionSeconds!.Value)) return null;
        return new(steps.Count > 0 ? steps[0].Start : cast, cast, steps.ToArray(), work,
            Math.Max(0, (cast - (steps.Count > 0 ? steps[0].Start : cast)).TotalMinutes - work));
    }
}
