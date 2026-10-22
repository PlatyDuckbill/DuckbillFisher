using System.Text.Json;
using DuckbillFisher.Core;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class FishingAvailabilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ReferenceFixture Reference = Read<ReferenceFixture>("forecast-reference.json");
    private static readonly RuleDataset Dataset = Read<RuleDataset>("fishing.kr.v1.json");
    private static readonly WeatherChance[] CertainWeather = [new(1, 100)];

    public static IEnumerable<object[]> WeatherTargets => Reference.Weather.Targets.Select((target, index) =>
        new object[] { Reference.Weather.FirstPeriodSeconds + (index - 1) * 1400L, target });

    public static IEnumerable<object?[]> ReferenceWindows => Reference.Cases.Select(sample => new object?[]
    {
        sample.FishParameterId, sample.Now, sample.Kind, sample.Start, sample.End,
    });

    [Fact]
    public void ReferenceCoversEveryPinnedSpotAndMatchesItsSourceRevision()
    {
        Assert.Equal(Dataset.SourceRevision, Reference.SourceRevision);
        Assert.Equal(Dataset.GameBuild, Reference.GameBuild);
        Assert.Equal(10, Reference.Spots.Length);
        Assert.Equal(Reference.Spots.Select(spot => spot.FishParameterId).Order(),
            Reference.Cases.Select(sample => sample.FishParameterId).Distinct().Order());
        Assert.All(Reference.Spots, spot =>
            Assert.Single(Dataset.Fish, rule => rule.FishParameterId == spot.FishParameterId));
    }

    [Theory]
    [MemberData(nameof(WeatherTargets))]
    public void MatchesPinnedJavaScriptAtWeatherPeriodBoundaries(long period, int expected)
    {
        Assert.Equal(expected, FishingAvailability.ForecastTarget(period));
        Assert.Equal(expected, FishingAvailability.ForecastTarget(period + 1));
        Assert.Equal(expected, FishingAvailability.ForecastTarget(period + 1399));
    }

    [Theory]
    [MemberData(nameof(ReferenceWindows))]
    public void MatchesIndependentEventSweep(uint fishId, long now, string kind, long? start, long? end)
    {
        var rule = Dataset.Fish.Single(rule => rule.FishParameterId == fishId);
        var rates = Reference.Spots.Single(spot => spot.FishParameterId == fishId).Rates;
        var actual = FishingAvailability.Find(DateTimeOffset.FromUnixTimeMilliseconds(now), rule, rates);

        Assert.Equal(Enum.Parse<FishingWindowKind>(kind), actual.Kind);
        Assert.Equal(start, actual.Start?.ToUnixTimeMilliseconds());
        Assert.Equal(end, actual.End?.ToUnixTimeMilliseconds());
    }

    // Synthetic ET day 10: directly calculated boundaries, independent of source fixtures.
    [Theory]
    [InlineData(9, 14, 1574999, FishingWindowKind.Upcoming, 1575000, 2450000)]
    [InlineData(9, 14, 1575000, FishingWindowKind.Open, 1575000, 2450000)]
    [InlineData(9, 14, 2450000, FishingWindowKind.Upcoming, 5775000, 6650000)]
    [InlineData(18, 2, 4199999, FishingWindowKind.Open, 3150000, 4550000)]
    [InlineData(18, 2, 4200000, FishingWindowKind.Open, 4200000, 4550000)]
    [InlineData(18, 2, 4550000, FishingWindowKind.Upcoming, 7350000, 8750000)]
    [InlineData(7.5, 8.5, 1312500, FishingWindowKind.Open, 1312500, 1487500)]
    public void UsesInclusiveStartExclusiveEndAndHandlesMidnight(
        double startHour, double endHour, long relativeNow, FishingWindowKind kind, long start, long end)
    {
        const long day = 42000000;
        var actual = FishingAvailability.Find(DateTimeOffset.FromUnixTimeMilliseconds(day + relativeNow),
            new FishingRule { StartHour = startHour, EndHour = endHour }, []);

        Assert.Equal(kind, actual.Kind);
        // Start can be clipped to the current weather period, including fractional ET hours.
        var expectedStart = Math.Max(day + start, (day + relativeNow) / 1400000 * 1400000);
        Assert.Equal(expectedStart, actual.Start?.ToUnixTimeMilliseconds());
        Assert.Equal(day + end, actual.End?.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void OffsetDoesNotChangeForecastAndPersonalRequirementsDoNotGateWeatherWindow()
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(1792083059);
        var rule = new FishingRule { StartHour = 9, EndHour = 14 };
        var expected = FishingAvailability.Find(time, rule, []);
        Assert.Equal(expected, FishingAvailability.Find(time.ToOffset(TimeSpan.FromHours(9)), rule, []));
        Assert.Equal(expected, FishingAvailability.Find(time, rule with
        {
            Predators = [new FishRequirement(4913, 3)], IntuitionSeconds = 350, FolkloreRequired = true,
        }, []));
    }

    [Fact]
    public void DistinguishesAlwaysUnsupportedWeatherAndUnconfirmedEndAtHorizon()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(42001);
        var rule = new FishingRule { StartHour = 0, EndHour = 24 };
        Assert.Equal(new FishingWindow(FishingWindowKind.Always), FishingAvailability.Find(now, rule, []));
        Assert.Equal(new FishingWindow(FishingWindowKind.Unknown), FishingAvailability.Find(now,
            rule with { WeatherIds = [2] }, CertainWeather));
        var continuous = FishingAvailability.Find(now, rule with { WeatherIds = [1], PreviousWeatherIds = [1] }, CertainWeather);
        Assert.Equal(FishingWindowKind.Open, continuous.Kind);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(42000), continuous.Start);
        Assert.Null(continuous.End);
    }


    [Theory]
    [InlineData(-1, 14)]
    [InlineData(24, 24)]
    [InlineData(9, 25)]
    [InlineData(9, -1)]
    [InlineData(9, 9)]
    [InlineData(double.NaN, 14)]
    [InlineData(9, double.PositiveInfinity)]
    public void InvalidTimeConditionsStayUnknown(double start, double end)
    {
        Assert.Equal(new FishingWindow(FishingWindowKind.Unknown), FishingAvailability.Find(
            DateTimeOffset.FromUnixTimeSeconds(42000), new FishingRule { StartHour = start, EndHour = end }, []));
    }

    [Fact]
    public void MissingOrMalformedWeatherRatesStayUnknown()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(42000);
        foreach (var rule in new[]
        {
            new FishingRule { StartHour = 0, EndHour = 24, WeatherIds = [1] },
            new FishingRule { StartHour = 0, EndHour = 24, PreviousWeatherIds = [1] },
        })
        foreach (WeatherChance[] rates in new WeatherChance[][]
        {
            [], [new(1, 99)], [new(0, 100)], [new(1, 100), new(2, 0)], [new(1, 101), new(2, -1)],
        })
            Assert.Equal(new FishingWindow(FishingWindowKind.Unknown), FishingAvailability.Find(now, rule, rates));
        Assert.Throws<ArgumentOutOfRangeException>(() => FishingAvailability.ForecastTarget(-1));
        Assert.Equal(FishingWindowKind.Unknown, FishingAvailability.Find(DateTimeOffset.UnixEpoch,
            new FishingRule { StartHour = 0, EndHour = 24 }, []).Kind);
    }

    [Fact]
    public void UnsupportedWeatherAlternativeDoesNotMaskSupportedAlternative()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(42000);
        var rule = new FishingRule { StartHour = 9, EndHour = 14, WeatherIds = [1] };
        var expected = FishingAvailability.Find(now, rule, CertainWeather);
        Assert.Equal(expected, FishingAvailability.Find(now, rule with { WeatherIds = [1, 999] }, CertainWeather));
        Assert.Equal(FishingWindowKind.Unknown,
            FishingAvailability.Find(now, rule with { WeatherIds = [999] }, CertainWeather).Kind);
        Assert.Equal(FishingWindowKind.Unknown,
            FishingAvailability.Find(now, rule with { ConditionsKnown = false }, CertainWeather).Kind);
    }

    private static T Read<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name)), JsonOptions)
        ?? throw new InvalidDataException(name);

    private sealed record RuleDataset(string SourceRevision, string GameBuild, FishingRule[] Fish);
    private sealed record ReferenceFixture(string SourceRevision, string GameBuild, WeatherFixture Weather,
        SpotFixture[] Spots, WindowFixture[] Cases);
    private sealed record WeatherFixture(long FirstPeriodSeconds, int[] Targets);
    private sealed record SpotFixture(uint FishParameterId, WeatherChance[] Rates);
    private sealed record WindowFixture(uint FishParameterId, long Now, string Kind, long? Start, long? End);
}
