using DuckbillFisher.Core;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class FishingForecastTests
{
    private static readonly DateTimeOffset Day = DateTimeOffset.FromUnixTimeSeconds(42000);
    private static FishingRule Rule(uint id, double start = 0, double end = 24) =>
        new() { ItemId = id, StartHour = start, EndHour = end };
    private static FishingForecast Forecast(params FishingRule[] rules) =>
        new(rules.Select(rule => new FishingForecastInput(rule, [new(1, 100)])));

    [Theory]
    [InlineData(true, FishingWindowKind.Always)]
    [InlineData(false, FishingWindowKind.Upcoming)]
    [InlineData(null, FishingWindowKind.Unknown)]
    public void FishEyesOnlyRemovesExplicitlySupportedTimeLimits(bool? support, FishingWindowKind expected)
    {
        var rule = Rule(1, 10, 12) with { FishEyesSupported = support };
        var forecast = Forecast(rule);
        Assert.Equal(FishingWindowKind.Upcoming, forecast.Find(Day, 1).Kind);
        var actual = forecast.Find(Day, 1, fishEyes: true);
        Assert.Equal(expected, actual.Kind);
        Assert.Equal(support is null ? FishingForecastIssue.FishEyesSupportUnknown : FishingForecastIssue.None, actual.Issue);
        Assert.Equal(10, rule.StartHour);
        Assert.Equal(12, rule.EndHour);
    }

    [Fact]
    public void FishEyesPreservesWeatherAndDoesNotRequireSupportForUntimedFish()
    {
        var forecast = Forecast(Rule(1, 10, 12) with { FishEyesSupported = true, WeatherIds = [1] }, Rule(2));
        var window = forecast.Find(Day, 1, true);
        Assert.Equal(FishingWindowKind.Open, window.Kind);
        Assert.NotEqual(FishingWindowKind.Always, window.Kind);
        Assert.Null(window.End);
        Assert.Equal(FishingWindowKind.Always, forecast.Find(Day, 2, true).Kind);
    }

    [Theory]
    [InlineData(null, 1925, true)]
    [InlineData(350d, 2100, false)]
    public void IntuitionExtendsPrerequisiteByDurationWithoutChangingSource(double? seconds, int expectedEnd, bool assumed)
    {
        var target = Rule(1, 10, 12) with { Predators = [new(2, 3)], IntuitionSeconds = seconds };
        var forecast = Forecast(target, Rule(2, 8, 10));
        var actual = forecast.Find(Day.AddSeconds(100), 1);
        Assert.Equal(FishingWindowKind.Upcoming, actual.Kind);
        Assert.Equal(Day.AddSeconds(1750), actual.Start);
        Assert.Equal(Day.AddSeconds(expectedEnd), actual.End);
        Assert.Equal(FishingForecastBasis.Intuition, actual.Basis);
        var breakdown = Assert.IsType<FishingForecastBreakdown>(actual.Breakdown);
        Assert.Equal(assumed, breakdown.DurationAssumed);
        Assert.Equal(seconds ?? 175, breakdown.IntuitionSeconds);
        var prerequisite = Assert.Single(breakdown.Prerequisites);
        Assert.Equal(3, prerequisite.Count);
        Assert.Equal(Day.AddSeconds(1400), prerequisite.Window.Start);
        Assert.Equal(seconds, target.IntuitionSeconds);
    }

    [Fact]
    public void AdjacentPrerequisitesCanBePreparedSequentially()
    {
        var target = Rule(1, 12, 14) with { Predators = [new(2, 1), new(3, 1)], IntuitionSeconds = 175 };
        var window = Forecast(target, Rule(2, 8, 10), Rule(3, 10, 12)).Find(Day.AddSeconds(100), 1);
        Assert.Equal(Day.AddSeconds(2100), window.Start);
        Assert.Equal(Day.AddSeconds(2275), window.End);
        Assert.Equal(2, window.Breakdown!.Prerequisites.Count);
    }

    [Fact]
    public void ValidSeparatedPrerequisitesProduceNotFoundRatherThanUnknown()
    {
        var target = Rule(1, 14, 15) with { Predators = [new(2, 1), new(3, 1)], IntuitionSeconds = 175 };
        var actual = Forecast(target, Rule(2, 8, 9), Rule(3, 12, 13)).Find(Day, 1);
        Assert.Equal(FishingWindowKind.NotFound, actual.Kind);
        Assert.Equal(FishingForecastIssue.None, actual.Issue);
    }

    [Fact]
    public void AlwaysAvailablePrerequisitesNeedNoDurationAndRetainTargetTimeLimit()
    {
        var target = Rule(1, 10, 12) with { Predators = [new(2, 1)] };
        var forecast = Forecast(target, Rule(2));
        var window = forecast.Find(Day, 1);
        Assert.Equal(Day.AddSeconds(1750), window.Start);
        Assert.Equal(Day.AddSeconds(2100), window.End);
        Assert.Equal(FishTimeRestriction.Restricted, forecast.TimeRestriction(1));
        Assert.Equal(FishTimeRestriction.Unrestricted, forecast.TimeRestriction(2));
    }

    [Fact]
    public void MissingInvalidAndNestedPrerequisitesKeepDistinctUnknownReasons()
    {
        var target = Rule(1) with { Predators = [new(2, 1)] };
        Assert.Equal(FishingForecastIssue.PrerequisiteUnavailable, Forecast(target).Find(Day, 1).Issue);
        Assert.Equal(FishingForecastIssue.PrerequisiteUnavailable,
            Forecast(target, Rule(2) with { ConditionsKnown = false }).Find(Day, 1).Issue);
        Assert.Equal(FishingForecastIssue.NestedPrerequisiteUnsupported,
            Forecast(target, Rule(2) with { Predators = [new(3, 1)] }, Rule(3)).Find(Day, 1).Issue);
        Assert.Equal(FishingForecastIssue.FishEyesSupportUnknown,
            Forecast(target, Rule(2, 10, 12)).Find(Day, 1, true).Issue);
        Assert.Equal(FishingWindowKind.Unknown, Forecast(target).Find(Day, 99).Kind);
        Assert.Equal(FishTimeRestriction.Unknown, Forecast(target).TimeRestriction(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidIntuitionDurationCannotProduceAWindow(double seconds)
    {
        var forecast = Forecast(Rule(1) with { Predators = [new(2, 1)], IntuitionSeconds = seconds }, Rule(2, 10, 12));
        var actual = forecast.Find(Day, 1);
        Assert.Equal(FishingWindowKind.Unknown, actual.Kind);
        Assert.Equal(FishingForecastIssue.IntuitionDurationUnknown, actual.Issue);
    }

    [Fact]
    public void EnumerationAgreesWithFindAndKeepsCompleteEndPastVisibleRange()
    {
        var forecast = Forecast(Rule(1, 10, 12));
        var found = forecast.Find(Day, 1);
        Assert.Equal(found, Assert.Single(forecast.Enumerate(Day, Day.AddSeconds(1800), 1)));
        Assert.Empty(forecast.Enumerate(Day, Day.AddSeconds(1700), 1));
        var next = forecast.Find(found.End!.Value, 1);
        Assert.Equal(Day.AddSeconds(4200 + 1750), next.Start);
    }

    [Fact]
    public void CalendarAcceptsFullMonthWhileNormalSearchRejectsBeyondThirtyDays()
    {
        var forecast = Forecast(Rule(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => forecast.Enumerate(Day, Day.AddDays(31), 1).ToArray());
        Assert.Equal(FishingWindowKind.Always,
            Assert.Single(forecast.EnumerateCalendar(Day, Day.AddDays(31), 1)).Kind);
        Assert.Throws<ArgumentOutOfRangeException>(() => forecast.EnumerateCalendar(Day, Day.AddDays(33), 1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => forecast.Enumerate(Day, Day, 1).ToArray());
        Assert.Throws<OperationCanceledException>(() =>
            forecast.EnumerateCalendar(Day, Day.AddDays(1), 1, cancellationToken: new(true)).ToArray());
    }
}
