using DuckbillFisher.Core;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class FishRecommendationTests
{
    // An ET midnight in a modern UTC date, with room for the calendar look-back.
    private static readonly DateTimeOffset Day = DateTimeOffset.FromUnixTimeSeconds(1_764_000_000);
    private static FishingRule Rule(uint id, double start = 0, double end = 24) => new()
    { ItemId = id, FishingSpotId = 1, StartHour = start, EndHour = end, BaitSteps = [[999]] };
    private static FishRecommendations Planner(params FishingRule[] rules) =>
        new(rules.Select(rule => new FishingForecastInput(rule, [new(1, 100)])));
    private static FishRecommendation Find(RecommendationResult result, uint id) =>
        Assert.Single(result.Ranked.Concat(result.Other), fish => fish.ItemId == id);

    [Fact]
    public void EstimatedLateCandidateStaysAccessibleButIsNotRanked()
    {
        // Only 125 seconds remain; even travel + the fast bait estimate takes 135 seconds.
        var target = Rule(1, 0, 1) with { BaitSteps = [[999], [3]] };
        var result = Planner(target, Rule(2), Rule(3)).Calculate(Day.AddSeconds(50), 1, [1, 2], true);
        Assert.DoesNotContain(result.Ranked, fish => fish.ItemId == 1);
        Assert.Contains(result.Other, fish => fish.ItemId == 1);
        Assert.Contains(result.Ranked, fish => fish.ItemId == 2);
        Assert.DoesNotContain(result.Now, fish => fish.ItemId == 1);
        Assert.Equal(RecommendationIssue.EstimatedLate, Assert.Single(result.Rare, fish => fish.ItemId == 1).Issue);
    }

    [Fact]
    public void AlwaysAvailableBaitDoesNotMislabelOwnFrequencyAsPrerequisiteFrequency()
    {
        var target = Rule(1, 10, 12) with { BaitSteps = [[999], [2]] };
        var fish = Find(Planner(target, Rule(2)).Calculate(Day.AddSeconds(120), 1, [1], true), 1);
        Assert.Equal(OpportunityBasis.OwnConditions, fish.Basis);
        Assert.Equal(1u, fish.BasisItemId);
    }

    [Fact]
    public void IntuitionTriggerCanWaitForTheLaterMoochOpportunity()
    {
        // The target opens at ET0, but its bait only opens at ET4 (700 real seconds).
        // Catching the trigger at ET0 wastes the 60-second buff; delay that catch instead.
        var target = Rule(1, 0, 12) with
        { Predators = [new(2, 1)], IntuitionSeconds = 60, BaitSteps = [[999], [3]] };
        var fish = Find(Planner(target, Rule(2), Rule(3, 4, 8)).Calculate(Day, 1, [1], true), 1);
        var plan = Assert.IsType<RecommendationPlan>(fish.Plan);
        Assert.NotNull(plan.FastFirstCast);
        Assert.True(plan.FastFirstCast >= Day.AddSeconds(700));
        var trigger = Assert.Single(plan.Steps, step => step.Role == PreparationRole.Trigger);
        Assert.True(plan.FastFirstCast < trigger.End.AddSeconds(60));
    }

    [Fact]
    public void TriggerBeforePreyClosesCanCarryIntoTargetOpening()
    {
        var target = Rule(1, 8, 10) with { Predators = [new(2, 1)], IntuitionSeconds = 300 };
        var fish = Find(Planner(target, Rule(2, 4, 7)).Calculate(Day.AddSeconds(300), 1, [1], true), 1);
        var plan = Assert.IsType<RecommendationPlan>(fish.Plan);
        var trigger = Assert.Single(plan.Steps, step => step.Role == PreparationRole.Trigger);
        Assert.True(trigger.End <= Day.AddSeconds(1225));
        Assert.Equal(Day.AddSeconds(1400), plan.FirstCast);
        Assert.True(plan.FirstCast < trigger.End.AddSeconds(300));
    }

    [Fact]
    public void RonkaSeparatesPreStockAndInWindowChainWithoutAwardingExtraStockPoints()
    {
        var target = Rule(33243, 10, 12) with { BaitSteps = [[999], [27490], [27491], [28071]] };
        var planner = Planner(target, Rule(27490), Rule(27491), Rule(28071, 10, 12) with { BigFish = true });
        var fish = Find(planner.Calculate(Day.AddSeconds(600), 1, [33243], true), 33243);
        var plan = Assert.IsType<RecommendationPlan>(fish.Plan);
        Assert.True(plan.Steps.Single(step => step.ItemId == 27491).End <= Day.AddSeconds(1750));
        Assert.True(plan.Steps.Single(step => step.ItemId == 28071).Start >= Day.AddSeconds(1750));
        Assert.True(plan.FirstCast > Day.AddSeconds(1750));
        Assert.Equal(2, plan.AdvanceMoochCount);
        var stored = Assert.Single(fish.Alternatives, alternative => alternative.Strategy == RecommendationStrategy.RonkaStored);
        Assert.NotNull(stored.OptionalStock);
        Assert.Equal(plan.Score, stored.Score, 6);
        var withoutStorage = Find(planner.Calculate(Day.AddSeconds(600), 1, [33243], false), 33243);
        Assert.Equal(plan.Score, withoutStorage.Plan!.Score, 6);
    }

    [Fact]
    public void LastIntuitionCatchAlsoProvidesTheSingleStepMoochBait()
    {
        var target = Rule(17592, 0, 3) with
        { BaitSteps = [[999], [12715]], Predators = [new(12715, 5)], IntuitionSeconds = 120 };
        var fish = Find(Planner(target, Rule(12715)).Calculate(Day, 1, [17592], true), 17592);
        var plan = Assert.IsType<RecommendationPlan>(fish.Plan);
        Assert.True(plan.SharedCatch);
        Assert.Equal(5, plan.Steps.Count(step => step.ItemId == 12715));
        Assert.Equal(PreparationRole.Trigger, plan.Steps[^1].Role);
    }

    [Fact]
    public void HorizonDoesNotSilentlyExpandAndUnknownIntuitionIsNotAssumed()
    {
        var upcoming = Rule(1, 23, 24);
        var unknown = Rule(2) with { Predators = [new(3, 1)] };
        var planner = Planner(upcoming, unknown, Rule(3));
        var shortResult = planner.Calculate(Day.AddSeconds(100), 1, [1, 2], true);
        Assert.Equal(RecommendationIssue.OutsideHorizon, Find(shortResult, 1).Issue);
        Assert.Equal(RecommendationIssue.IntuitionDurationUnknown, Find(shortResult, 2).Issue);
        Assert.Contains(planner.Calculate(Day.AddSeconds(100), 3, [1], true).Ranked, fish => fish.ItemId == 1);
    }

    [Fact]
    public void NowViewIncludesPreparationDueNowButNotAWindowThatOnlyOpensLater()
    {
        var direct = Rule(1, 10, 12);
        var prepare = Rule(2, 10, 12) with { Predators = [new(3, 10)], IntuitionSeconds = 360 };
        var result = Planner(direct, prepare, Rule(3)).Calculate(Day.AddSeconds(970), 1, [1, 2], true);
        Assert.DoesNotContain(result.Now, fish => fish.ItemId == 1);
        Assert.Contains(result.Now, fish => fish.ItemId == 2);
        Assert.Contains(result.Preparation, fish => fish.ItemId == 2);
        Assert.Contains(result.Rare, fish => fish.ItemId == 1);
    }

    [Fact]
    public void PreparationViewRetainsFutureWorkWithoutCallingItActionableNow()
    {
        var target = Rule(1, 20, 22) with { Predators = [new(2, 5)], IntuitionSeconds = 360 };
        var result = Planner(target, Rule(2)).Calculate(Day.AddSeconds(100), 1, [1], true);
        Assert.Empty(result.Now);
        var fish = Assert.Single(result.Preparation);
        Assert.True(fish.Plan!.RecommendedStart > Day.AddSeconds(400));
        Assert.True(fish.Plan.HasAdvancePreparation);
    }

    [Fact]
    public void PrerequisiteGapUsesThatPrerequisitesEndInsteadOfTheIntuitionExtension()
    {
        var target = Rule(1) with { Predators = [new(2, 1)], IntuitionSeconds = 350 };
        var fish = Find(Planner(target, Rule(2, 2, 4)).Calculate(Day.AddSeconds(120), 1, [1], true), 1);
        Assert.True(fish.OwnConditionsAlways);
        Assert.Equal(OpportunityBasis.PrerequisiteProxy, fish.Basis);
        Assert.Equal(Day.AddSeconds(4550), fish.NextOpportunity);
        Assert.Equal(3850.0 / 3600, fish.GapHours!.Value, 6);
    }

    [Fact]
    public void PreparationSortKeepsLargerDueWorkVisibleBeforeSmallerWork()
    {
        var large = Rule(1, 10, 12) with { Predators = [new(3, 10)], IntuitionSeconds = 360 };
        var small = Rule(2, 10, 12) with { Predators = [new(3, 6)], IntuitionSeconds = 360 };
        var result = Planner(large, small, Rule(3)).Calculate(Day.AddSeconds(970), 1, [2, 1], true);
        Assert.Equal(1u, result.Preparation[0].ItemId);
        Assert.Equal(2u, result.Preparation[1].ItemId);
        Assert.All(result.Preparation, fish => Assert.Contains(result.Now, current => current.ItemId == fish.ItemId));
    }

    [Fact]
    public void RareViewUsesFrequencyInsteadOfTargetNowScoreAndExcludesContinuousConditions()
    {
        var common = Rule(1, 0, 1);
        var rare = Rule(2, 0, 1) with { WeatherIds = [1], PreviousWeatherIds = [2] };
        var planner = new FishRecommendations(new[] { common, rare, Rule(3) }
            .Select(rule => new FishingForecastInput(rule, [new(1, 50), new(2, 50)])));
        var result = planner.Calculate(Day, 24, [1, 2, 3], true);
        Assert.Equal(2u, result.Rare[0].ItemId);
        Assert.Equal(1u, result.Rare[1].ItemId);
        Assert.True(result.Rare[0].OpportunityCount < result.Rare[1].OpportunityCount);
        Assert.DoesNotContain(result.Rare, fish => fish.ItemId == 3);
    }

    [Fact]
    public void RefreshUsesNewTimeAndCancelledCalculationDoesNotReturnAPartialList()
    {
        var planner = Planner(Rule(1, 10, 12));
        var first = planner.Calculate(Day.AddSeconds(100), 1, [1], true);
        var later = planner.Calculate(Day.AddSeconds(1500), 1, [1], true);
        Assert.Empty(first.Now);
        Assert.Single(later.Now);
        Assert.Equal(Day.AddSeconds(1500), later.At);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => planner.Calculate(Day, 3, [1], true, cancellation.Token));
    }

    [Fact]
    public void IntuitionWorkThatCanOnlyStartAtOpeningIsNotAdvancePreparation()
    {
        var target = Rule(1, 10, 12) with { Predators = [new(2, 1)], IntuitionSeconds = 360 };
        var result = Planner(target, Rule(2, 10, 12)).Calculate(Day.AddSeconds(120), 1, [1], true);
        var fish = Assert.Single(result.Ranked);
        Assert.NotNull(fish.Plan!.FirstCast);
        Assert.True(fish.Plan.Steps[0].Start >= Day.AddSeconds(1750));
        Assert.Equal(0, fish.Plan.AdvanceWorkMinutes);
        Assert.False(fish.Plan.HasAdvancePreparation);
        Assert.Empty(result.Preparation);
    }

    [Fact]
    public void WorkCrossingOpeningCountsOnlyItsAdvancePartAndDoesNotClaimACatchIsSecured()
    {
        var target = Rule(1, 10, 12) with { Predators = [new(2, 2)], IntuitionSeconds = 360 };
        // Arrival at 1720; the first catch completes at 1810. Target opens at 1750.
        var fish = Find(Planner(target, Rule(2)).Calculate(Day.AddSeconds(1600), 1, [1], true), 1);
        Assert.Equal(3, fish.Plan!.WorkMinutes);
        Assert.Equal(0.5, fish.Plan.AdvanceWorkMinutes, 6);
        Assert.Equal(0, fish.Plan.AdvanceIntuitionCount);
        Assert.True(fish.Plan.HasAdvancePreparation);
    }

    [Fact]
    public void UnrestrictedIntuitionTargetRetainsItsPreparationWithoutAnInventedOpening()
    {
        var target = Rule(1) with { Predators = [new(2, 3)], IntuitionSeconds = 360 };
        var result = Planner(target, Rule(2)).Calculate(Day, 1, [1], true);
        var fish = Assert.Single(result.Preparation);
        Assert.True(fish.OwnConditionsAlways);
        Assert.Null(fish.Window!.Start);
        Assert.Equal(4.5, fish.Plan!.AdvanceWorkMinutes);
        Assert.Equal(3, fish.Plan.AdvanceIntuitionCount);
    }

    [Fact]
    public void UpcomingBaitIsNotDescribedAsACurrentlyOpenPreparationOpportunity()
    {
        var target = Rule(1, 10, 12) with { BaitSteps = [[999], [2]] };
        var result = Planner(target, Rule(2, 8, 12)).Calculate(Day.AddSeconds(1300), 1, [1], true);
        var fish = Assert.Single(result.Now);
        Assert.NotNull(fish.Plan!.FirstCast);
        Assert.Null(fish.PreparationGateEnd);
        Assert.Equal(0u, fish.PreparationGateItem);
    }
}
