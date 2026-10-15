namespace DuckbillFisher.Core;

public sealed record FishingConditionSnapshot(bool Fishing, bool ExecutingGatheringAction, bool Casting);

public sealed record FishingObservationEntry(
    long Sequence, TimeSpan Elapsed, string Event, FishingConditionSnapshot Conditions);

public sealed record FishingObservationView(
    bool IsRecording, long DroppedCount, IReadOnlyList<FishingObservationEntry> Entries);

/// <summary>
/// Bounded diagnostic history, with no interpretation of the flags as fishing actions.
/// The caller supplies monotonic timestamps and serializes access.
/// </summary>
public sealed class FishingObservation
{
    public const int Capacity = 200;
    private readonly Queue<FishingObservationEntry> entries = new();
    private TimeSpan startedAt;
    private long sequence;
    private long droppedCount;

    public bool IsRecording { get; private set; }

    public void Start(TimeSpan timestamp, FishingConditionSnapshot conditions)
    {
        if (IsRecording)
            return;

        entries.Clear();
        sequence = 0;
        droppedCount = 0;
        startedAt = timestamp;
        IsRecording = true;
        Record(timestamp, "START (snapshot)", conditions);
    }

    public void Record(TimeSpan timestamp, string description, FishingConditionSnapshot conditions)
    {
        if (!IsRecording)
            return;

        if (entries.Count == Capacity)
        {
            entries.Dequeue();
            droppedCount++;
        }

        entries.Enqueue(new(++sequence, timestamp - startedAt, description, conditions));
    }

    public void Stop(TimeSpan timestamp, FishingConditionSnapshot conditions)
    {
        if (!IsRecording)
            return;

        Record(timestamp, "STOP (snapshot)", conditions);
        IsRecording = false;
    }

    public FishingObservationView GetView() => new(
        IsRecording, droppedCount, Array.AsReadOnly(entries.ToArray()));
}
