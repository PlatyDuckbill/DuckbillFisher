using DuckbillFisher.Core;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class SpawnAlertSchedulerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(100000);
    private static readonly SpawnAlertCandidate Fish = new(1, true, CollectionStatus.Uncaught,
        new(FishingWindowKind.Upcoming, Now.AddMinutes(5).AddSeconds(1), Now.AddMinutes(10)));
    private static SpawnAlertEvaluation Evaluate(SpawnAlertScheduler scheduler, int seconds,
        int session = 1, bool enabled = true, SpawnAlertAudience audience = SpawnAlertAudience.AllFavorites,
        params SpawnAlertCandidate[] candidates) =>
        scheduler.Evaluate(Now.AddSeconds(seconds), TimeSpan.FromSeconds(seconds + 100), session, enabled, 5, audience,
            candidates.Length == 0 ? [Fish] : candidates);

    [Fact]
    public void FiresOnceAtDeadlineAndDoesNotCatchUpOnStartup()
    {
        var scheduler = new SpawnAlertScheduler();
        Assert.True(Evaluate(scheduler, 0).Rebased);
        Assert.Equal(new SpawnAlert(1, Fish.Window.Start!.Value), Assert.Single(Evaluate(scheduler, 1).Due));
        Assert.Empty(Evaluate(scheduler, 2).Due);
        Assert.Empty(Evaluate(new(), 1).Due);
        Assert.Empty(Evaluate(new(), 3).Due);
    }

    [Theory]
    [InlineData(SpawnAlertAudience.AllFavorites, CollectionStatus.Unknown, true)]
    [InlineData(SpawnAlertAudience.AllFavorites, CollectionStatus.Caught, true)]
    [InlineData(SpawnAlertAudience.UncaughtOnly, CollectionStatus.Uncaught, true)]
    [InlineData(SpawnAlertAudience.UncaughtOnly, CollectionStatus.Unknown, false)]
    [InlineData(SpawnAlertAudience.UncaughtOnly, CollectionStatus.Caught, false)]
    public void AudienceUsesFinalCollectionState(SpawnAlertAudience audience, CollectionStatus collection, bool shouldFire)
    {
        var scheduler = new SpawnAlertScheduler();
        var candidate = Fish with { Collection = collection };
        Evaluate(scheduler, 0, audience: audience, candidates: [candidate]);
        Assert.Equal(shouldFire, Evaluate(scheduler, 1, audience: audience, candidates: [candidate]).Due.Count == 1);
    }

    [Fact]
    public void NonfavoritesAndDisabledAlertsNeverFire()
    {
        var scheduler = new SpawnAlertScheduler();
        Evaluate(scheduler, 0, candidates: [Fish with { Favorite = false }]);
        Assert.Empty(Evaluate(scheduler, 1, candidates: [Fish with { Favorite = false }]).Due);
        Evaluate(scheduler, 2, enabled: false);
        Assert.Empty(Evaluate(scheduler, 3, enabled: true).Due);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void InvalidGlobalAndIndividualLeadTimesAreIgnored(int lead)
    {
        var scheduler = new SpawnAlertScheduler();
        Assert.Equal(0, scheduler.Evaluate(Now, TimeSpan.FromSeconds(100), 1, true, lead,
            SpawnAlertAudience.AllFavorites, [Fish]).Armed);
        Assert.Equal(0, Evaluate(new(), 0, candidates: [Fish with { LeadMinutes = lead }]).Armed);
    }

    [Fact]
    public void IndividualLeadCrossesItsOwnDeadlineAndChangedLeadDoesNotDeliverOldReservation()
    {
        var scheduler = new SpawnAlertScheduler();
        var candidate = Fish with { LeadMinutes = 1, Window = Fish.Window with { Start = Now.AddSeconds(61) } };
        Evaluate(scheduler, 0, candidates: [candidate]);
        Assert.Single(Evaluate(scheduler, 1, candidates: [candidate]).Due);
        scheduler = new();
        Evaluate(scheduler, 0);
        Assert.Empty(Evaluate(scheduler, 1, candidates: [Fish with { LeadMinutes = 6 }]).Due);
    }

    [Fact]
    public void CharacterChangeAndLongGapDiscardOldDeadlines()
    {
        var scheduler = new SpawnAlertScheduler();
        Evaluate(scheduler, 0);
        Assert.True(Evaluate(scheduler, 1, session: 2).Rebased);
        Assert.Empty(Evaluate(scheduler, 2, session: 2).Due);
        scheduler = new();
        Evaluate(scheduler, 0);
        var resumed = Evaluate(scheduler, 11);
        Assert.True(resumed.Rebased);
        Assert.Empty(resumed.Due);
    }

    [Theory]
    [InlineData(-30, 1)]
    [InlineData(30, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void ClockDiscontinuityRebasesBeforeDelivery(int utcSeconds, int elapsedSeconds)
    {
        var scheduler = new SpawnAlertScheduler();
        Evaluate(scheduler, 0);
        var result = scheduler.Evaluate(Now.AddSeconds(utcSeconds), TimeSpan.FromSeconds(100 + elapsedSeconds),
            1, true, 5, SpawnAlertAudience.AllFavorites, [Fish]);
        Assert.True(result.Rebased);
        Assert.Empty(result.Due);
    }

    [Fact]
    public void PlanningRebaseCheckDoesNotAdvanceDeliveryBaseline()
    {
        var scheduler = new SpawnAlertScheduler();
        Evaluate(scheduler, 0);
        Assert.False(scheduler.RequiresRebase(Now.AddSeconds(1), TimeSpan.FromSeconds(101), 1, true));
        Assert.False(scheduler.RequiresRebase(Now.AddSeconds(1), TimeSpan.FromSeconds(101), 1, true));
        Assert.Single(Evaluate(scheduler, 1).Due);
    }

    [Fact]
    public void FutureSearchSkipsReservedAndTooCloseWindowsAndStopsNonprogressingFinder()
    {
        var first = new FishingWindow(FishingWindowKind.Open, Now.AddMinutes(-1), Now.AddMinutes(1));
        var future = new FishingWindow(FishingWindowKind.Upcoming, Now.AddMinutes(20), Now.AddMinutes(21));
        Assert.Equal(future, SpawnAlertScheduler.FindFutureWindow(Now, 5, first, 0, _ => future));
        Assert.Null(SpawnAlertScheduler.FindFutureWindow(Now, 5, future,
            future.Start!.Value.ToUnixTimeMilliseconds(), _ => future));
        Assert.Null(SpawnAlertScheduler.FindFutureWindow(Now, 5, first, 0, _ => first));
        Assert.Null(SpawnAlertScheduler.FindFutureWindow(Now, 5,
            new(FishingWindowKind.Open, Now.AddMinutes(-1)), 0, _ => throw new InvalidOperationException()));
    }
}
