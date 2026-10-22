using DuckbillFisher.Core;
using DuckbillFisher.Plugin;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class CatalogWindowFilterTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(100000);

    [Theory]
    [InlineData(299, false)]
    [InlineData(300, true)]
    [InlineData(301, true)]
    public void RemainingThresholdIncludesExactBoundary(int remaining, bool expected) =>
        Assert.Equal(expected, CatalogWindowFilter.Matches(new() { MinimumRemainingMinutes = 5 },
            new(FishingWindowKind.Open, Now.AddMinutes(-1), Now.AddSeconds(remaining)), Now));

    [Theory]
    [InlineData(299, false)]
    [InlineData(300, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void UpcomingRangeIncludesBothBoundaries(int seconds, bool expected) =>
        Assert.Equal(expected, CatalogWindowFilter.Matches(new() { MinimumUpcomingMinutes = 5, MaximumUpcomingMinutes = 10 },
            new(FishingWindowKind.Upcoming, Now.AddSeconds(seconds)), Now));

    [Fact]
    public void IndependentBoundsAndUnknownWindowsKeepTheirMeaning()
    {
        var upcoming = new FishingWindow(FishingWindowKind.Upcoming, Now.AddMinutes(20));
        Assert.True(CatalogWindowFilter.Matches(new() { MinimumUpcomingMinutes = 5 }, upcoming, Now));
        Assert.False(CatalogWindowFilter.Matches(new() { MaximumUpcomingMinutes = 10 }, upcoming, Now));
        Assert.True(CatalogWindowFilter.Matches(new(), upcoming, Now));
        var strict = new CatalogSettings { MinimumRemainingMinutes = 5, MinimumUpcomingMinutes = 30, MaximumUpcomingMinutes = 10 };
        foreach (var window in new FishingWindow?[] { null, new(FishingWindowKind.Unknown), new(FishingWindowKind.NotFound),
            new(FishingWindowKind.Always), new(FishingWindowKind.Open) })
            Assert.True(CatalogWindowFilter.Matches(strict, window, Now));
        Assert.False(CatalogWindowFilter.Matches(strict, upcoming, Now));
    }

    [Fact]
    public void NextChangeMatchesInclusiveEntryAndExclusiveExit()
    {
        var upcoming = new FishingWindow(FishingWindowKind.Upcoming, Now.AddMinutes(10).AddSeconds(5));
        Assert.Equal(Now.AddSeconds(5), CatalogWindowFilter.NextChange(new() { MaximumUpcomingMinutes = 10 }, [upcoming], Now));
        var open = new FishingWindow(FishingWindowKind.Open, Now.AddMinutes(-1), Now.AddMinutes(5).AddSeconds(7));
        Assert.Equal(Now.AddSeconds(7).AddTicks(1),
            CatalogWindowFilter.NextChange(new() { MinimumRemainingMinutes = 5 }, [open], Now));
        Assert.Equal(DateTimeOffset.MaxValue, CatalogWindowFilter.NextChange(new(), [upcoming, open], Now));
    }
}
