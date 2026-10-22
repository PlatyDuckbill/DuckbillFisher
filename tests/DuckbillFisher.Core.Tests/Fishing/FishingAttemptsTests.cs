using DuckbillFisher.Core;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class FishingAttemptsTests
{
    private static readonly DateTimeOffset Utc = DateTimeOffset.FromUnixTimeSeconds(100000);
    private static void Start(FishingAttempts history, int seconds = 0, bool known = true, uint? spot = null) =>
        history.Start(TimeSpan.FromSeconds(seconds), Utc.AddSeconds(seconds), 10, spot, 100, FishingBaitKind.Equipped, known, known);

    [Fact]
    public void UsesMonotonicDurationAndPreservesFirstBiteAndHook()
    {
        var history = new FishingAttempts();
        Start(history);
        var original = history.Records;
        history.Bite(TimeSpan.FromSeconds(3));
        history.Bite(TimeSpan.FromSeconds(4));
        history.Hook(TimeSpan.FromSeconds(5));
        history.Hook(TimeSpan.FromSeconds(6));
        history.End(TimeSpan.FromSeconds(8), Utc.AddHours(-1), FishingOutcome.Caught, FishingEndReason.CatchLog, 200, 1);
        var row = Assert.Single(history.Records);
        Assert.Equal(3, row.BiteSeconds);
        Assert.Equal(5, row.HookSeconds);
        Assert.Equal(8, row.DurationSeconds);
        Assert.Equal(200u, row.FishItemId);
        Assert.Null(history.Active);
        Assert.Equal(FishingOutcome.Pending, Assert.Single(original).Outcome);
        Assert.Null(original[0].BiteSeconds);
    }

    [Fact]
    public void UnknownStartDoesNotInventTiming()
    {
        var history = new FishingAttempts();
        Start(history, known: false);
        history.Bite(TimeSpan.FromSeconds(3));
        history.Hook(TimeSpan.FromSeconds(4));
        history.EndSession(TimeSpan.FromSeconds(5), Utc.AddSeconds(5), FishingEndReason.Reload);
        var row = Assert.Single(history.Records);
        Assert.Null(row.StartedUtc);
        Assert.Null(row.SessionStartedUtc);
        Assert.Null(row.BiteSeconds);
        Assert.Null(row.HookSeconds);
        Assert.Null(row.DurationSeconds);
        Assert.Null(history.Trip!.DurationSeconds);
    }

    [Theory]
    [InlineData(FishingOutcome.Caught)]
    [InlineData(FishingOutcome.Failed)]
    [InlineData(FishingOutcome.Cancelled)]
    [InlineData(FishingOutcome.Interrupted)]
    [InlineData(FishingOutcome.Unknown)]
    public void RepeatedEndCannotDoubleCountAnyOutcome(FishingOutcome outcome)
    {
        var history = new FishingAttempts();
        Start(history);
        history.End(TimeSpan.FromSeconds(1), Utc, outcome, FishingEndReason.NativeFinished);
        var revision = history.Revision;
        history.End(TimeSpan.FromSeconds(2), Utc, outcome, FishingEndReason.NativeFinished);
        Assert.Equal(revision, history.Revision);
        Assert.Equal(1, history.Trip!.Caught + history.Trip.Failed + history.Trip.Cancelled +
            history.Trip.Interrupted + history.Trip.Unknown);
        Assert.Equal(outcome, Assert.Single(history.Records).Outcome);
    }

    [Fact]
    public void NextCastClosesPendingAttemptAsUnknownWithinSameTrip()
    {
        var history = new FishingAttempts();
        Start(history);
        var first = history.Active!;
        Start(history, 10);
        Assert.Equal(first.SessionId, history.Active!.SessionId);
        Assert.Equal(2, history.Trip!.Casts);
        Assert.Equal(1, history.Trip.Unknown);
        Assert.Equal(FishingEndReason.NextCast, history.Records[1].EndReason);
        Assert.Equal(10, history.Records[1].DurationSeconds);
    }

    [Fact]
    public void EndSessionIsIdempotentAndNextSessionClearsOldRecords()
    {
        var history = new FishingAttempts();
        Start(history);
        var first = history.Active!.SessionId;
        history.EndSession(TimeSpan.FromSeconds(5), Utc.AddSeconds(5), FishingEndReason.Logout);
        var revision = history.Revision;
        history.EndSession(TimeSpan.FromSeconds(6), Utc.AddSeconds(6), FishingEndReason.Logout);
        Assert.Equal(revision, history.Revision);
        Assert.False(history.HasSession);
        Assert.Equal(1, history.Trip!.Interrupted);
        Assert.Equal(5, history.Trip.DurationSeconds);
        Start(history, 10);
        Assert.NotEqual(first, history.Active!.SessionId);
        Assert.Single(history.Records);
        Assert.Equal(1, history.Trip!.Casts);
    }

    [Fact]
    public void CapacityBoundsRetainedRowsWithoutLosingTotalCasts()
    {
        var history = new FishingAttempts();
        for (var index = 0; index < FishingAttempts.Capacity + 1; index++) Start(history, index);
        Assert.Equal(2000, history.Records.Count);
        Assert.Equal(2001, history.Trip!.Casts);
        Assert.Equal(2000, history.Trip.Retained);
        Assert.Equal(2000, history.Trip.Unknown);
        Assert.Equal(history.Active, history.Records[0]);
    }

    [Fact]
    public void LateSpotIdentificationUpdatesOnlyMatchingTripAndUnidentifiedAttempt()
    {
        var history = new FishingAttempts();
        Start(history);
        var attempt = history.Active!.Id;
        history.End(TimeSpan.FromSeconds(1), Utc, FishingOutcome.Caught, FishingEndReason.CatchLog);
        Assert.False(history.SetSpot(attempt, 11, 3));
        Assert.False(history.SetSpot(attempt, 10, 0));
        Assert.False(history.SetSpot(Guid.NewGuid(), 10, 3));
        Assert.True(history.SetSpot(attempt, 10, 3));
        Assert.False(history.SetSpot(attempt, 10, 4));
        Assert.Equal(3u, history.Trip!.SpotId);
        Assert.Equal(3u, Assert.Single(history.Records).SpotId);
        history.EndSession(TimeSpan.FromSeconds(2), Utc, FishingEndReason.Logout);
        Start(history, 3);
        Assert.False(history.SetSpot(attempt, 10, 5));
    }

    [Theory]
    [InlineData(FishingBaitKind.Mooch, true)]
    [InlineData(FishingBaitKind.Swimbait, false)]
    public void OnlyMoochLinksMatchingImmediatelyPreviousCatch(FishingBaitKind kind, bool linked)
    {
        var history = new FishingAttempts();
        Start(history);
        var source = history.Active!.Id;
        history.End(TimeSpan.FromSeconds(1), Utc, FishingOutcome.Caught, FishingEndReason.CatchLog, 200);
        Start(history, 2);
        Assert.True(history.SetCastBait(200, kind));
        Assert.Equal(linked ? source : (Guid?)null, history.Active!.MoochSourceId);
        Assert.False(history.SetCastBait(201, FishingBaitKind.Mooch));
    }

    [Fact]
    public void BaitObservationCannotRewriteAfterBiteOrLinkAcrossFailedCatch()
    {
        var history = new FishingAttempts();
        Start(history);
        history.End(TimeSpan.FromSeconds(1), Utc, FishingOutcome.Failed, FishingEndReason.FailureLog, 200);
        Start(history, 2);
        Assert.True(history.SetCastBait(200, FishingBaitKind.Mooch));
        Assert.Null(history.Active!.MoochSourceId);
        Start(history, 3);
        history.Bite(TimeSpan.FromSeconds(4));
        Assert.False(history.SetCastBait(200, FishingBaitKind.Mooch));
        Assert.Equal(FishingBaitKind.Equipped, history.Active!.BaitKind);
    }
}
