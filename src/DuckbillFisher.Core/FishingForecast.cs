using System.Collections.Frozen;

namespace DuckbillFisher.Core;

public sealed record FishingForecastInput(FishingRule Rule, IReadOnlyList<WeatherChance> WeatherRates);

/// <summary>Shared forecast for every supplied fish, including ordered Intuition prerequisites.
/// Predicts preparation opportunities, never a character's catches or active buff.</summary>
public sealed class FishingForecast
{
    private readonly FrozenDictionary<uint, FishingForecastInput> inputs;

    public FishingForecast(IEnumerable<FishingForecastInput> inputs) =>
        this.inputs = inputs.ToFrozenDictionary(input => input.Rule.ItemId);

    public FishTimeRestriction TimeRestriction(uint itemId)
    {
        if (!inputs.TryGetValue(itemId, out var input) || !FishingAvailability.IsValid(input.Rule, input.WeatherRates))
            return FishTimeRestriction.Unknown;
        if (!FishingAvailability.IsAlways(input.Rule)) return FishTimeRestriction.Restricted;
        var issue = Prerequisites(input.Rule, out var predators);
        if (predators.Any(predator => !FishingAvailability.IsAlways(predator.Rule)))
            return FishTimeRestriction.Restricted;
        return issue == FishingForecastIssue.None ? FishTimeRestriction.Unrestricted : FishTimeRestriction.Unknown;
    }

    public FishingWindow Find(DateTimeOffset now, uint itemId)
    {
        if (!inputs.TryGetValue(itemId, out var input)) return new(FishingWindowKind.Unknown);
        var rule = input.Rule;
        if (rule.Predators.Length == 0) return FishingAvailability.Find(now, rule, input.WeatherRates);
        var issue = Prerequisites(rule, out var predators);
        if (issue != FishingForecastIssue.None)
            return new(FishingWindowKind.Unknown) { Basis = FishingForecastBasis.Intuition, Issue = issue };
        // No duration is needed when every prerequisite is available at all times.
        if (predators.All(predator => FishingAvailability.IsAlways(predator.Rule)))
            return FishingAvailability.Find(now, rule, input.WeatherRates) with { Basis = FishingForecastBasis.Intuition };
        if (rule.IntuitionSeconds is not { } duration || !double.IsFinite(duration) || duration <= 0)
            return new(FishingWindowKind.Unknown) { Basis = FishingForecastBasis.Intuition, Issue = FishingForecastIssue.IntuitionDurationUnknown };
        var unix = now.ToUnixTimeMilliseconds() / 1000.0;
        if (unix < FishingAvailability.WeatherSeconds * 2 + duration)
            return new(FishingWindowKind.Unknown) { Basis = FishingForecastBasis.Intuition };
        return FishingAvailability.Find(now, rule, input.WeatherRates,
            range => Constrain(range, predators, duration, unix)) with { Basis = FishingForecastBasis.Intuition };
    }

    private FishingForecastIssue Prerequisites(FishingRule rule, out FishingForecastInput[] predators)
    {
        var result = new List<FishingForecastInput>();
        foreach (var requirement in rule.Predators)
        {
            if (!inputs.TryGetValue(requirement.ItemId, out var predator) ||
                !FishingAvailability.IsValid(predator.Rule, predator.WeatherRates))
            {
                predators = result.ToArray();
                return FishingForecastIssue.PrerequisiteUnavailable;
            }
            // No current record has nested Intuition; don't silently forecast a future unsupported chain.
            if (predator.Rule.Predators.Length > 0)
            {
                predators = result.ToArray();
                return FishingForecastIssue.NestedPrerequisiteUnsupported;
            }
            result.Add(predator);
        }
        predators = result.ToArray();
        return FishingForecastIssue.None;
    }

    // Adapted from Fish Tracker fishwatcher.js at the revision in THIRD-PARTY-NOTICES.
    // Preserve source order: prerequisites can be caught sequentially across adjacent periods
    // (Warden of the Seven Hues), rather than requiring every fish to overlap simultaneously.
    private static AvailabilityRange? Constrain(AvailabilityRange target, FishingForecastInput[] predators,
        double duration, double baseTime)
    {
        AvailabilityRange? overall = null;
        AvailabilityRange? accepted = null;
        var anyAlways = false;
        foreach (var predator in predators)
        {
            if (FishingAvailability.IsAlways(predator.Rule))
            {
                anyAlways = true;
                continue;
            }
            AvailabilityRange[] ranges = [];
            long period = 0;
            // Check the target period, the possible buff carry-over, and the previous weather period.
            foreach (var at in new[] { target.Start, target.Start - duration,
                         Math.Floor(target.Start / FishingAvailability.WeatherSeconds) * FishingAvailability.WeatherSeconds - FishingAvailability.WeatherSeconds })
            {
                period = (long)Math.Floor(at / FishingAvailability.WeatherSeconds) * FishingAvailability.WeatherSeconds;
                if (!FishingAvailability.WeatherMatches(period, predator.Rule, predator.WeatherRates)) continue;
                ranges = RangesDuring(period, predator.Rule);
                if (ranges.Length > 0) break;
            }
            var valid = false;
            foreach (var raw in ranges)
            {
                if (raw.End <= baseTime || raw.Intersect(new(period, period + FishingAvailability.WeatherSeconds)) is not { } range)
                    continue;
                valid = true;
                if (overall is not { } combined || accepted is not { } previous)
                    overall = accepted = range;
                else if (range.Intersect(previous) is { } reduced)
                {
                    accepted = reduced;
                    overall = new(Math.Min(combined.Start, range.Start), Math.Max(combined.End, range.End));
                }
                else if (range.Start > combined.End || range.End < combined.Start)
                    valid = false;
                else
                {
                    if (range.Start == combined.End) accepted = range;
                    overall = new(Math.Min(combined.Start, range.Start), Math.Max(combined.End, range.End));
                }
            }
            if (!valid) return null;
        }
        if (accepted is not { } prerequisite) return null;
        return target.Intersect(new(prerequisite.Start, (anyAlways ? target.End : prerequisite.End) + duration));
    }

    // Full daily ranges are intentional: the upstream carry-over lookup decides whether to
    // retry before clipping them to the weather period. All arithmetic here uses real UTC seconds.
    private static AvailabilityRange[] RangesDuring(long period, FishingRule rule)
    {
        if (rule.StartHour == 0 && rule.EndHour == 24)
            return [new(period, period + FishingAvailability.WeatherSeconds)];
        var day = period / FishingAvailability.DaySeconds * FishingAvailability.DaySeconds;
        var hour = (period - day) / FishingAvailability.HourSeconds;
        var start = day + rule.StartHour * FishingAvailability.HourSeconds;
        var end = day + rule.EndHour * FishingAvailability.HourSeconds;
        if (end < start)
        {
            end += FishingAvailability.DaySeconds;
            if (hour < rule.EndHour)
            {
                var previous = new AvailabilityRange(start - FishingAvailability.DaySeconds, end - FishingAvailability.DaySeconds);
                var endHour = (period + FishingAvailability.WeatherSeconds) % FishingAvailability.DaySeconds / FishingAvailability.HourSeconds;
                return endHour > rule.StartHour ? [previous, new(start, end)] : [previous];
            }
            return [new(start, end)];
        }
        return hour < rule.EndHour ? [new(start, end)] : [];
    }
}
