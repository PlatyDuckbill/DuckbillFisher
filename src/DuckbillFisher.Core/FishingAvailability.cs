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
public sealed record FishingWindow(FishingWindowKind Kind, DateTimeOffset? Start = null, DateTimeOffset? End = null);

/// <summary>Normal time/weather windows only; personal conditions and Fish Eyes are not inferred.</summary>
public static class FishingAvailability
{
    public const int SearchDays = 30;
    // Include both partial weather periods at the edges; Find applies the exact UTC horizon.
    public const int SearchPeriods = (SearchDays * 86400 + (int)WeatherSeconds - 1) / (int)WeatherSeconds + 1;
    private const long WeatherSeconds = 1400;
    private const long DaySeconds = 4200;
    private const double HourSeconds = 175;

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

    public static FishingWindow Find(DateTimeOffset now, FishingRule rule, IReadOnlyList<WeatherChance> rates)
    {
        var unix = now.ToUnixTimeMilliseconds() / 1000.0;
        if (!rule.ConditionsKnown || rule.WeatherIds is null || rule.PreviousWeatherIds is null ||
            unix < WeatherSeconds || !double.IsFinite(rule.StartHour) || !double.IsFinite(rule.EndHour)
            || rule.StartHour < 0 || rule.StartHour >= 24 || rule.EndHour < 0 || rule.EndHour > 24
            || rule.StartHour == rule.EndHour)
            return new(FishingWindowKind.Unknown);
        var hasWeather = rule.WeatherIds.Length > 0 || rule.PreviousWeatherIds.Length > 0;
        if (hasWeather && (rates.Count == 0 || rates.Any(rate => rate.WeatherId == 0 || rate.Chance is <= 0 or > 100)
                || rates.Sum(rate => (long)rate.Chance) != 100 ||
                !HasForecastWeather(rule.WeatherIds) || !HasForecastWeather(rule.PreviousWeatherIds)))
            return new(FishingWindowKind.Unknown);
        if (!hasWeather && rule.StartHour == 0 && rule.EndHour == 24)
            return new(FishingWindowKind.Always);

        double? foundStart = null;
        double? foundEnd = null;
        var firstPeriod = (long)unix / WeatherSeconds * WeatherSeconds;
        var horizon = unix + SearchDays * 86400;
        for (var index = 0; index < SearchPeriods; index++)
        {
            var period = firstPeriod + index * WeatherSeconds;
            if (period >= horizon) break;
            var periodEnd = Math.Min(period + WeatherSeconds, horizon);
            var weatherMatches = !hasWeather ||
                ((rule.WeatherIds.Length == 0 || rule.WeatherIds.Contains(WeatherAt(period, rates))) &&
                 (rule.PreviousWeatherIds.Length == 0 || rule.PreviousWeatherIds.Contains(WeatherAt(period - WeatherSeconds, rates))));
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
                    if (foundEnd is null)
                    {
                        foundStart = start;
                        foundEnd = end;
                    }
                    else if (start <= foundEnd)
                        foundEnd = Math.Max(foundEnd.Value, end);
                    else
                        return Result(foundStart!.Value, foundEnd.Value);
                }
            }
            if (foundEnd is { } finish && finish < periodEnd)
                return Result(foundStart!.Value, finish);
        }
        // A continuous window reaching the search limit has no confirmed end within that limit.
        return foundStart is { } begin
            ? new(begin <= unix ? FishingWindowKind.Open : FishingWindowKind.Upcoming, FromSeconds(begin))
            : new(FishingWindowKind.NotFound);

        FishingWindow Result(double start, double end) =>
            new(start <= unix ? FishingWindowKind.Open : FishingWindowKind.Upcoming, FromSeconds(start), FromSeconds(end));

        // Alternatives are OR conditions. A special weather absent from the normal rate table
        // must not suppress the other supported alternatives (e.g. Raincaller: rain or snow).
        bool HasForecastWeather(uint[] alternatives) => alternatives.Length == 0 ||
            alternatives.Any(id => rates.Any(rate => rate.WeatherId == id));
    }

    private static DateTimeOffset FromSeconds(double seconds) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(seconds * 1000));
}
