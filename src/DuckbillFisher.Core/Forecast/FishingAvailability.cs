namespace DuckbillFisher.Core;

public sealed record FishRequirement(uint ItemId, int Count);

public sealed record FishingRule
{
    public uint ItemId { get; init; }
    public uint FishParameterId { get; init; }
    public uint FishingSpotId { get; init; }
    public string SourceName { get; init; } = "";
    public double StartHour { get; init; }
    public double EndHour { get; init; }
    public uint[] WeatherIds { get; init; } = [];
    public uint[] PreviousWeatherIds { get; init; } = [];
    public uint[] BaitPath { get; init; } = [];
    public uint[][] BaitSteps { get; init; } = [];
    public string Hookset { get; init; } = "";
    public string Tug { get; init; } = "";
    public FishRequirement[] Predators { get; init; } = [];
    public double? IntuitionSeconds { get; init; }
    public bool FolkloreRequired { get; init; }
    public bool? FishEyesSupported { get; init; }
    public bool ConditionsKnown { get; init; } = true;
    public bool SnaggingRequired { get; init; }
    public string Lure { get; init; } = "";
    public bool BigFish { get; init; }
    public double Patch { get; init; }
    public string PatchVersion { get; init; } = "";
}

/// <summary>Source spearfishing facts kept separate from rod fishing and character log IDs.</summary>
public sealed record SpearfishingRule(uint ItemId, uint SpearfishingItemId, uint SourceSpotId,
    string SourceName, string Gig, bool ConditionsKnown, FishRequirement[] Predators);

public readonly record struct WeatherChance(uint WeatherId, int Chance);
public enum FishingWindowKind { Unknown, Always, Open, Upcoming, NotFound }
public enum FishingForecastBasis { TimeWeather, Intuition }
public enum FishingForecastIssue { None, PrerequisiteUnavailable, IntuitionDurationUnknown, NestedPrerequisiteUnsupported, FishEyesSupportUnknown }
public sealed record FishingPrerequisiteForecast(uint ItemId, int Count, FishingWindow Window);
public sealed record FishingForecastBreakdown(FishingWindow Target, IReadOnlyList<FishingPrerequisiteForecast> Prerequisites)
{
    public double IntuitionSeconds { get; init; }
    public bool DurationAssumed { get; init; }
}
public sealed record FishingWindow(FishingWindowKind Kind, DateTimeOffset? Start = null, DateTimeOffset? End = null)
{
    public FishingForecastBasis Basis { get; init; }
    public FishingForecastIssue Issue { get; init; }
    public FishingForecastBreakdown? Breakdown { get; init; }
}

internal readonly record struct AvailabilityRange(double Start, double End)
{
    public AvailabilityRange? Intersect(AvailabilityRange other)
    {
        var start = Math.Max(Start, other.Start);
        var end = Math.Min(End, other.End);
        return end > start ? new(start, end) : null;
    }
}

/// <summary>Normal time/weather windows only; personal conditions and Fish Eyes are not inferred.</summary>
public static class FishingAvailability
{
    public const int SearchDays = 30;
    // Include both partial weather periods at the edges; Find applies the exact UTC horizon.
    public const int SearchPeriods = (SearchDays * 86400 + (int)WeatherSeconds - 1) / (int)WeatherSeconds + 1;
    internal const long WeatherSeconds = 1400;
    internal const long DaySeconds = 4200;
    internal const double HourSeconds = 175;

    // Adapted from Carbuncle Plushy weather.js at the revision recorded in THIRD-PARTY-NOTICES.
    public static int ForecastTarget(long unixSeconds)
    {
        if (unixSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(unixSeconds));
        var bell = unixSeconds / 175;
        var increment = (bell + 8 - bell % 8) % 24;
        var basis = unchecked((uint)(unixSeconds / DaySeconds * 100 + increment));
        var first = (basis << 11) ^ basis;
        return (int)(((first >> 8) ^ first) % 100);
    }

    private static uint WeatherAt(long unixSeconds, IReadOnlyList<WeatherChance> rates)
    {
        var target = ForecastTarget(unixSeconds);
        var cumulative = 0;
        foreach (var rate in rates)
        {
            cumulative += rate.Chance;
            if (target < cumulative)
                return rate.WeatherId;
        }
        return 0;
    }

    public static FishingWindow Find(DateTimeOffset now, FishingRule rule, IReadOnlyList<WeatherChance> rates) =>
        Find(now, rule, rates, null);

    internal static bool IsAlways(FishingRule rule) => rule.StartHour == 0 && rule.EndHour == 24 &&
        rule.WeatherIds.Length == 0 && rule.PreviousWeatherIds.Length == 0;

    internal static bool IsValid(FishingRule rule, IReadOnlyList<WeatherChance> rates)
    {
        if (!rule.ConditionsKnown || rule.WeatherIds is null || rule.PreviousWeatherIds is null ||
            !double.IsFinite(rule.StartHour) || !double.IsFinite(rule.EndHour)
            || rule.StartHour < 0 || rule.StartHour >= 24 || rule.EndHour < 0 || rule.EndHour > 24
            || rule.StartHour == rule.EndHour)
            return false;
        var hasWeather = rule.WeatherIds.Length > 0 || rule.PreviousWeatherIds.Length > 0;
        if (hasWeather && (rates.Count == 0 || rates.Any(rate => rate.WeatherId == 0 || rate.Chance is <= 0 or > 100)
                || rates.Sum(rate => (long)rate.Chance) != 100 ||
                !HasForecastWeather(rule.WeatherIds) || !HasForecastWeather(rule.PreviousWeatherIds)))
            return false;
        return true;

        // Alternatives are OR conditions; absent special weather must not suppress supported alternatives.
        bool HasForecastWeather(uint[] alternatives) => alternatives.Length == 0 ||
            alternatives.Any(id => rates.Any(rate => rate.WeatherId == id));
    }

    internal static bool WeatherMatches(long period, FishingRule rule, IReadOnlyList<WeatherChance> rates) =>
        (rule.WeatherIds.Length == 0 || rule.WeatherIds.Contains(WeatherAt(period, rates))) &&
        (rule.PreviousWeatherIds.Length == 0 || rule.PreviousWeatherIds.Contains(WeatherAt(period - WeatherSeconds, rates)));

    internal static FishingWindow Find(DateTimeOffset now, FishingRule rule, IReadOnlyList<WeatherChance> rates,
        Func<AvailabilityRange, AvailabilityRange?>? constrain) =>
        Enumerate(now, now.AddDays(SearchDays), rule, rates, constrain, CancellationToken.None).FirstOrDefault()
            ?? new(FishingWindowKind.NotFound);

    // One scan, fixed preparation baseline, and complete UTC boundaries even past the visible range.
    // A continuous interval reaching the 30-day limit retains an unknown end.
    internal static IEnumerable<FishingWindow> Enumerate(DateTimeOffset now, DateTimeOffset through,
        FishingRule rule, IReadOnlyList<WeatherChance> rates, Func<AvailabilityRange, AvailabilityRange?>? constrain,
        CancellationToken cancellationToken, int searchDays = SearchDays)
    {
        var unix = now.ToUnixTimeMilliseconds() / 1000.0;
        if (unix < WeatherSeconds || !IsValid(rule, rates))
        {
            yield return new(FishingWindowKind.Unknown);
            yield break;
        }
        if (constrain is null && IsAlways(rule))
        {
            yield return new(FishingWindowKind.Always);
            yield break;
        }

        double? foundStart = null;
        double? foundEnd = null;
        var firstPeriod = (long)unix / WeatherSeconds * WeatherSeconds;
        var horizon = unix + searchDays * 86400;
        var visibleEnd = Math.Min(through.ToUnixTimeMilliseconds() / 1000.0, horizon);
        var periods = (searchDays * 86400 + (int)WeatherSeconds - 1) / (int)WeatherSeconds + 1;
        for (var index = 0; index < periods; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var period = firstPeriod + index * WeatherSeconds;
            if (period >= horizon || (period >= visibleEnd && foundEnd is null)) break;
            var periodEnd = Math.Min(period + WeatherSeconds, horizon);
            var weatherMatches = WeatherMatches(period, rule, rates);
            if (weatherMatches)
            {
                var day = period / DaySeconds * DaySeconds;
                for (var dayOffset = -1; dayOffset <= 1; dayOffset++)
                {
                    var start = day + dayOffset * DaySeconds + rule.StartHour * HourSeconds;
                    var end = day + dayOffset * DaySeconds + rule.EndHour * HourSeconds;
                    if (end <= start)
                        end += DaySeconds;
                    start = Math.Max(start, period);
                    end = Math.Min(end, periodEnd);
                    if (end <= start || end <= unix)
                        continue;
                    if (constrain is not null)
                    {
                        if (constrain(new(start, end)) is not { } accepted || accepted.End <= unix)
                            continue;
                        start = accepted.Start;
                        end = accepted.End;
                    }
                    if (foundEnd is null)
                    {
                        foundStart = start;
                        foundEnd = end;
                    }
                    else if (start <= foundEnd)
                        foundEnd = Math.Max(foundEnd.Value, end);
                    else
                    {
                        yield return Result(foundStart!.Value, foundEnd.Value);
                        if (start >= visibleEnd) yield break;
                        foundStart = start;
                        foundEnd = end;
                    }
                    if (foundStart >= visibleEnd) yield break;
                }
            }
            if (foundEnd is { } finish && finish < periodEnd)
            {
                yield return Result(foundStart!.Value, finish);
                foundStart = foundEnd = null;
            }
        }
        // A continuous window reaching the search limit has no confirmed end within that limit.
        if (foundStart is { } begin && begin < visibleEnd)
            yield return new(begin <= unix ? FishingWindowKind.Open : FishingWindowKind.Upcoming, FromSeconds(begin));

        FishingWindow Result(double start, double end) =>
            new(start <= unix ? FishingWindowKind.Open : FishingWindowKind.Upcoming, FromSeconds(start), FromSeconds(end));

    }

    private static DateTimeOffset FromSeconds(double seconds) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(seconds * 1000));
}
