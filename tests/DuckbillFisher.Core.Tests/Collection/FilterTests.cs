using DuckbillFisher.Core;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class FilterTests
{
    [Fact]
    public void RegionAndExpansionMustMatchTheSameLocation()
    {
        FishingLocation[] locations = [new(10, 1), new(20, 2)];
        Assert.False(FishLocationFilter.Matches(locations, new HashSet<uint> { 10 }, new HashSet<uint> { 2 }, false, null));
        Assert.True(FishLocationFilter.Matches(locations, new HashSet<uint> { 20 }, new HashSet<uint> { 2 }, true, 20));
        Assert.False(FishLocationFilter.Matches(locations, new HashSet<uint>(), new HashSet<uint>(), true, null));
        Assert.False(FishLocationFilter.Matches([new(0, null)], new HashSet<uint> { 10 }, new HashSet<uint>(), false, null));
        Assert.True(FishLocationFilter.Matches([], new HashSet<uint>(), new HashSet<uint>(), false, null));
    }

    [Fact]
    public void CollectionAndGradeFiltersPreserveUnknownRatherThanAssumingUncaught()
    {
        Assert.True(CollectionFilter.Matches(CollectionStatus.Unknown, CollectionVisibility.HideCaught));
        Assert.False(CollectionFilter.Matches(CollectionStatus.Unknown, CollectionVisibility.UncaughtOnly));
        Assert.True(CollectionFilter.Matches(CollectionStatus.Unknown, CollectionVisibility.UnknownOnly));
        Assert.False(CollectionFilter.Matches(CollectionStatus.Caught, CollectionVisibility.HideCaught));
        Assert.True(FishClassificationFilter.Matches(FishGrade.Legendary, FishTimeRestriction.Restricted, true, FishGradeVisibility.BigIncludingLegendary));
        Assert.False(FishClassificationFilter.Matches(FishGrade.Unknown, FishTimeRestriction.Restricted, true, FishGradeVisibility.LegendaryOnly));
        Assert.False(FishClassificationFilter.Matches(FishGrade.Big, FishTimeRestriction.Unknown, true, FishGradeVisibility.All));
    }

    [Fact]
    public void RemainingTimeOrderingUsesAbsoluteKnownBoundaries()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(100000);
        FishingWindow?[] windows = [new(FishingWindowKind.Always), null, new(FishingWindowKind.Upcoming, now.AddMinutes(10)),
            new(FishingWindowKind.Open, now, now.AddMinutes(20)), new(FishingWindowKind.Open, now, now.AddMinutes(5))];
        var sorted = windows.OrderBy(FishingWindowOrdering.Key).ToArray();
        Assert.Equal(now.AddMinutes(5), sorted[0]!.End);
        Assert.Equal(now.AddMinutes(20), sorted[1]!.End);
        Assert.Equal(FishingWindowKind.Upcoming, sorted[2]!.Kind);
        Assert.Null(sorted[3]);
        Assert.Equal(FishingWindowKind.Always, sorted[4]!.Kind);
        Assert.Equal(FishingWindowSortGroup.Unknown, FishingWindowOrdering.Key(new(FishingWindowKind.Open)).Group);
    }
}
