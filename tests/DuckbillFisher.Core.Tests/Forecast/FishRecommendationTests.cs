using DuckbillFisher.Core;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class FishRecommendationTests
{
    private static readonly DateTimeOffset Day = DateTimeOffset.FromUnixTimeSeconds(1_764_000_000);
    private static FishingRule Rule(uint id, double start = 0, double end = 24) => new()
    { ItemId = id, FishingSpotId = 1, StartHour = start, EndHour = end, BaitSteps = [[999]] };
    private static FishRecommendations Browser(params FishingRule[] rules) =>
        new(rules.Select(rule => new FishingForecastInput(rule, [new(1, 75), new(2, 25)])));

    [Fact]
    public void ShortRemainingWindowIsKeptWithoutAnEstimatedPreparationExclusion()
    {
        var target = Rule(1, 0, 1) with { BaitSteps = [[999], [2], [3], [4]] };
        var result = Browser(target, Rule(2), Rule(3), Rule(4)).Calculate(Day.AddSeconds(50), 1, [1]);
        var fish = Assert.Single(result.Now);
        Assert.Equal(3, fish.MoochDepth);
        Assert.Equal(Day.AddSeconds(175), fish.Window.End);
        Assert.Single(result.Preparation);
        Assert.Empty(result.Other);
    }

    [Theory]
    [InlineData(1799, true)]
    [InlineData(1800, true)]
    [InlineData(1801, false)]
    public void TargetNowUsesAnInclusiveThirtyMinuteBoundary(int seconds, bool expected)
    {
        var result = Browser(Rule(1, seconds / 175.0, 12)).Calculate(Day, 1, [1]);
        Assert.Single(result.Ranked);
        Assert.Equal(expected, result.Now.Count == 1);
    }

    [Fact]
    public void SelectedRangeStillLimitsPreparationAndRarity()
    {
        var browser = Browser(Rule(1, 22, 23) with { BaitSteps = [[999], [2]] }, Rule(2));
        var shortRange = browser.Calculate(Day, 1, [1]);
        Assert.Empty(shortRange.Preparation);
        Assert.Empty(shortRange.Rare);
        Assert.Equal(RecommendationIssue.OutsideHorizon, Assert.Single(shortRange.Other).Issue);
        var longerRange = browser.Calculate(Day, 3, [1]);
        Assert.Single(longerRange.Preparation);
        Assert.Single(longerRange.Rare);
        Assert.Empty(longerRange.Now);
    }

    [Fact]
    public void PrepIncludesASingleMoochAndIntuitionWithoutInventingAWorkload()
    {
        var result = Browser(Rule(1), Rule(2) with { BaitSteps = [[999], [4]] },
            Rule(3) with { Predators = [new(4, 3)] }, Rule(4)).Calculate(Day, 3, [1, 2, 3]);
        Assert.Equal(new uint[] { 2, 3 }, result.Preparation.Select(value => value.ItemId));
        var intuition = Assert.Single(result.Preparation.Single(value => value.ItemId == 3).Preparation);
        Assert.Equal(3, intuition.Count);
        Assert.Equal(PreparationKind.Intuition, intuition.Kind);
        Assert.Equal(FishingWindowKind.Always, intuition.Window.Kind);
    }

    [Fact]
    public void RonkaAndAnEquivalentFishUseTheSameGenericRequirementRules()
    {
        var ordinary = Rule(1, 10, 12) with { BaitSteps = [[999], [2], [3], [4]] };
        var ronka = ordinary with { ItemId = 33243 };
        var result = Browser(ordinary, ronka, Rule(2), Rule(3), Rule(4, 10, 12)).Calculate(Day, 3, [1, 33243]);
        var first = result.Ranked.Single(value => value.ItemId == 1);
        var second = result.Ranked.Single(value => value.ItemId == 33243);
        Assert.Equal(first.Preparation, second.Preparation);
        Assert.Equal(first.OpportunityCount, second.OpportunityCount);
        Assert.Equal(first.Window, second.Window);
        Assert.Equal(2, result.Preparation.Count);
    }

    [Fact]
    public void MissingIntuitionDurationDoesNotHideKnownSchedulesAndCatchCounts()
    {
        var result = Browser(Rule(1, 10, 12) with { Predators = [new(2, 5)], IntuitionSeconds = null },
            Rule(2, 2, 3)).Calculate(Day, 3, [1]);
        var fish = Assert.Single(result.Ranked);
        Assert.Equal(5, fish.IntuitionCatchCount);
        Assert.Equal(RecommendationIssue.None, fish.Issue);
        Assert.Single(result.Preparation);
    }

    [Fact]
    public void UnrestrictedIntuitionShowsTheActualPreyIntervalWithoutBuffExtension()
    {
        var result = Browser(Rule(1) with { Predators = [new(2, 3)], IntuitionSeconds = 600 }, Rule(2, 13, 14))
            .Calculate(Day, 3, [1]);
        var fish = Assert.Single(result.Preparation);
        Assert.Equal(FishingWindowKind.Always, fish.Window.Kind);
        Assert.Null(fish.Window.Start);
        Assert.Equal(Day.AddSeconds(2275), fish.DisplayWindow.Start);
        Assert.Equal(Day.AddSeconds(2450), fish.DisplayWindow.End);
        Assert.Empty(result.Now);
        Assert.Equal(OpportunityBasis.PrerequisiteProxy, fish.Basis);
        Assert.Equal(2u, fish.BasisItemId);
    }

    [Fact]
    public void SeparatePreySchedulesAreShownWithoutClaimingTheyCanBeCompletedTogether()
    {
        var result = Browser(Rule(1) with { Predators = [new(2, 3), new(3, 5)] }, Rule(2, 4, 6), Rule(3, 12, 14))
            .Calculate(Day, 3, [1]);
        var fish = Assert.Single(result.Preparation);
        Assert.Equal(2, fish.Preparation.Count);
        Assert.Equal(3, fish.Preparation.Single(value => value.ItemId == 2).Count);
        Assert.Equal(5, fish.Preparation.Single(value => value.ItemId == 3).Count);
        Assert.True(fish.Preparation[0].Window.End < fish.Preparation[1].Window.Start);
        Assert.Equal(RecommendationIssue.None, fish.Issue);
    }

    [Fact]
    public void RestrictedTargetsUseTheirOwnFrequencyAndNextAppearance()
    {
        var target = Rule(1, 0, 12) with { BaitSteps = [[999], [2]], Predators = [new(3, 1)] };
        var result = Browser(target, Rule(2, 20, 21), Rule(3, 22, 23)).Calculate(Day, 3, [1]);
        var fish = Assert.Single(result.Ranked);
        var baseline = Assert.Single(Browser(target with { BaitSteps = [[999]], Predators = [] }).Calculate(Day, 3, [1]).Ranked);
        Assert.Equal(OpportunityBasis.OwnConditions, fish.Basis);
        Assert.Equal(1u, fish.BasisItemId);
        Assert.Equal(baseline.OpportunityCount, fish.OpportunityCount);
        Assert.Equal(Day.AddSeconds(4200), fish.NextOwnOpportunity);
    }

    [Fact]
    public void AlwaysAvailableFishHaveNoInventedRarity()
    {
        var result = Browser(Rule(1)).Calculate(Day, 3, [1]);
        Assert.Single(result.Now);
        Assert.Empty(result.Rare);
        Assert.Empty(result.Preparation);
        Assert.Equal(0, Assert.Single(result.Ranked).OpportunityCount);
    }

    [Fact]
    public void UnknownTargetIsExcludedButMissingPreyDoesNotBecomeAnEstimate()
    {
        var result = Browser(Rule(1) with { ConditionsKnown = false }, Rule(2, 10, 12) with { Predators = [new(3, 1)] })
            .Calculate(Day, 3, [1, 2]);
        Assert.Equal(RecommendationIssue.ConditionsUnknown, Assert.Single(result.Other).Issue);
        var fish = Assert.Single(result.Ranked);
        Assert.True(fish.PreparationUnknown);
        Assert.Equal(FishingWindowKind.Unknown, Assert.Single(fish.Preparation).Window.Kind);
    }

    [Fact]
    public void RarityUsesFewestContinuousConditionEpisodesFirst()
    {
        var result = Browser(Rule(1, 0, 1), Rule(2) with { WeatherIds = [2] }).Calculate(Day, 24, [1, 2]);
        Assert.Equal(2, result.Rare.Count);
        Assert.Equal(2u, result.Rare[0].ItemId);
        Assert.True(result.Rare[0].OpportunityCount < result.Rare[1].OpportunityCount);
    }

    [Fact]
    public void RefreshMovesPastAnEndedOccurrenceWithoutReusingItsCountdown()
    {
        var browser = Browser(Rule(1, 0, 1));
        var initial = Assert.Single(browser.Calculate(Day, 3, [1]).Now);
        var refreshed = Assert.Single(browser.Calculate(Day.AddSeconds(176), 3, [1]).Ranked);
        Assert.Equal(Day, initial.Window.Start);
        Assert.Equal(Day.AddSeconds(4200), refreshed.Window.Start);
    }

    [Fact]
    public void AnalysisDayIsUtcAndDuplicateTargetsAreNotRepeated()
    {
        var now = Day.ToOffset(TimeSpan.FromHours(9));
        var browser = Browser(Rule(1));
        var result = browser.Calculate(now, 24, [1, 1]);
        Assert.Single(result.Ranked);
        Assert.Equal(new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero), result.AnalysisStart);
        Assert.Equal(now.AddHours(24), result.Through);
        var later = browser.Calculate(now.AddDays(1), 24, [1]);
        Assert.Equal(result.AnalysisStart.AddDays(1), later.AnalysisStart);
    }

    [Fact]
    public void SharedMoochAndIntuitionFishKeepsItsIntuitionRoleAndRequiredCount()
    {
        var target = Rule(1) with { BaitSteps = [[999], [2]], Predators = [new(2, 5)] };
        var fish = Assert.Single(Browser(target, Rule(2, 4, 6)).Calculate(Day, 3, [1]).Preparation);
        Assert.Equal(2, fish.Preparation.Count);
        Assert.Equal(PreparationKind.Intuition, fish.PrimaryPreparation!.Kind);
        Assert.Equal(5, fish.PrimaryPreparation.Count);
        Assert.Equal(5, fish.IntuitionCatchCount);
    }

    [Fact]
    public void ContinuousWeatherConditionKeepsItsOpenWindowWithAnUnknownEnd()
    {
        var browser = new FishRecommendations([new(Rule(1) with { WeatherIds = [1] }, [new(1, 100)])]);
        var fish = Assert.Single(browser.Calculate(Day, 3, [1]).Now);
        Assert.Null(fish.Window.End);
        Assert.Equal(FishingWindowKind.Open, fish.Window.Kind);
        Assert.Equal(1, fish.OpportunityCount);
        Assert.False(fish.OwnConditionsAlways);
    }

    [Fact]
    public void CurrentWindowPreservesTheStartBeforeTheQueryBaseline()
    {
        var result = Browser(Rule(1, 23, 2)).Calculate(Day, 3, [1]);
        Assert.Equal(Day.AddSeconds(-175), Assert.Single(result.Now).Window.Start);
    }

    [Fact]
    public void CancelledQueryCannotReturnAPartialList()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => Browser(Rule(1)).Calculate(Day, 3, [1], cancellation.Token));
    }
}
