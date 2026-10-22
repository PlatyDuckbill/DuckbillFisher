using DuckbillFisher.Core;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class FishingObservationTests
{
    private static readonly FishingConditionSnapshot Idle = new(false, false, false);
    private static readonly FishingConditionSnapshot Fishing = new(true, true, false);

    [Fact]
    public void RecordsOnlyDuringSessionAndStopIsIdempotent()
    {
        var observation = new FishingObservation();
        observation.Record(TimeSpan.Zero, "before start", Idle);
        Assert.Empty(observation.GetView().Entries);

        observation.Start(TimeSpan.FromSeconds(10), Idle);
        observation.Record(TimeSpan.FromSeconds(12), "CHANGE Fishing=True", Fishing);
        observation.Stop(TimeSpan.FromSeconds(15), Idle);
        observation.Stop(TimeSpan.FromSeconds(16), Fishing);
        observation.Record(TimeSpan.FromSeconds(17), "after stop", Fishing);

        var view = observation.GetView();
        Assert.False(view.IsRecording);
        Assert.Equal(3, view.Entries.Count);
        Assert.Equal(new[] { 0d, 2d, 5d }, view.Entries.Select(e => e.Elapsed.TotalSeconds));
        Assert.Equal(Idle, view.Entries[0].Conditions);
        Assert.Equal(Fishing, view.Entries[1].Conditions);
        Assert.Equal(Idle, view.Entries[2].Conditions);
    }

    [Fact]
    public void RepeatedStartDoesNotResetActiveSession()
    {
        var observation = new FishingObservation();
        observation.Start(TimeSpan.FromSeconds(10), Fishing);
        observation.Start(TimeSpan.FromSeconds(20), Idle);
        observation.Record(TimeSpan.FromSeconds(21), "MARK", Fishing);

        var entries = observation.GetView().Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal(Fishing, entries[0].Conditions);
        Assert.Equal(TimeSpan.FromSeconds(11), entries[1].Elapsed);
    }

    [Fact]
    public void BoundedHistoryRetainsNewestEntriesAndOriginalElapsedTime()
    {
        var observation = new FishingObservation();
        observation.Start(TimeSpan.FromSeconds(100), Idle);
        for (var i = 1; i <= 205; i++)
            observation.Record(TimeSpan.FromSeconds(100 + i), $"event {i}", Fishing);
        observation.Stop(TimeSpan.FromSeconds(306), Idle);

        var view = observation.GetView();
        Assert.Equal(FishingObservation.Capacity, view.Entries.Count);
        Assert.Equal(7, view.DroppedCount);
        Assert.Equal(8, view.Entries[0].Sequence);
        Assert.Equal(TimeSpan.FromSeconds(7), view.Entries[0].Elapsed);
        Assert.Equal(207, view.Entries[^1].Sequence);
        Assert.Equal(TimeSpan.FromSeconds(206), view.Entries[^1].Elapsed);
        Assert.Equal("STOP (snapshot)", view.Entries[^1].Event);
    }

    [Fact]
    public void NewSessionClearsPreviousHistoryAndCounters()
    {
        var observation = new FishingObservation();
        observation.Start(TimeSpan.Zero, Idle);
        for (var i = 0; i < FishingObservation.Capacity; i++)
            observation.Record(TimeSpan.Zero, "old event", Idle);
        observation.Stop(TimeSpan.FromSeconds(1), Idle);
        observation.Start(TimeSpan.FromSeconds(50), Fishing);

        var view = observation.GetView();
        Assert.True(view.IsRecording);
        Assert.Equal(0, view.DroppedCount);
        var entry = Assert.Single(view.Entries);
        Assert.Equal(1, entry.Sequence);
        Assert.Equal(TimeSpan.Zero, entry.Elapsed);
        Assert.Equal(Fishing, entry.Conditions);
    }

    [Fact]
    public void PreviouslyPublishedViewDoesNotChangeWhenMoreEventsArrive()
    {
        var observation = new FishingObservation();
        observation.Start(TimeSpan.Zero, Idle);
        var initialView = observation.GetView();
        observation.Record(TimeSpan.FromMilliseconds(1), "CHANGE", Fishing);
        observation.Stop(TimeSpan.FromMilliseconds(2), Idle);

        Assert.True(initialView.IsRecording);
        Assert.Equal(Idle, Assert.Single(initialView.Entries).Conditions);
        Assert.Equal(3, observation.GetView().Entries.Count);
    }

    [Fact]
    public void SameTimestampAndRepeatedCallbacksRemainVisibleForDiagnosis()
    {
        var observation = new FishingObservation();
        observation.Start(TimeSpan.FromSeconds(1000), Idle);
        observation.Record(TimeSpan.FromSeconds(1001), "CHANGE Fishing=True", Fishing);
        observation.Record(TimeSpan.FromSeconds(1001), "CHANGE Fishing=True", Fishing);

        var entries = observation.GetView().Entries;
        Assert.Equal(3, entries.Count);
        Assert.Equal(entries[1].Elapsed, entries[2].Elapsed);
        Assert.Equal(entries[1].Event, entries[2].Event);
        Assert.Equal(3, entries[2].Sequence);
    }
}
