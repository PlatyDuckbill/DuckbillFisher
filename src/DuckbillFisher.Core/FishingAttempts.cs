namespace DuckbillFisher.Core;

public enum FishingOutcome { Pending, Caught, Failed, Cancelled, Interrupted, Unknown }
public enum FishingEndReason { None, CatchLog, ManualRetrieve, RodFolded, NativeFinished, NextCast, Logout, Travel, Reload, ObservationGap, ReadUnavailable, UnsupportedMode }
public enum FishingBaitKind { Unknown, Equipped, Mooch, Swimbait }

public sealed record FishingAttempt(Guid Id, Guid SessionId, DateTimeOffset? SessionStartedUtc,
    uint TerritoryId, uint? SpotId, uint? BaitItemId, FishingBaitKind BaitKind, DateTimeOffset? StartedUtc,
    FishingOutcome Outcome = FishingOutcome.Pending, FishingEndReason EndReason = FishingEndReason.None,
    DateTimeOffset? EndedUtc = null, double? DurationSeconds = null, uint? FishItemId = null, int? Quantity = null,
    double? BiteSeconds = null, double? HookSeconds = null);

public sealed record FishingTripSummary(Guid Id, long Sequence, uint TerritoryId, uint? SpotId,
    DateTimeOffset? StartedUtc, DateTimeOffset? EndedUtc, bool IsActive, int Casts, int Retained,
    int Caught, int Failed, int Cancelled, int Interrupted, int Unknown, FishingEndReason EndReason)
{
    public DateTimeOffset? FirstCastUtc { get; init; }
}

/// <summary>Single-writer cast/session history. Inputs are copied observations and caller-controlled times.</summary>
public sealed class FishingAttempts
{
    public const int Capacity = 2000;
    public const int TripCapacity = 100;
    private readonly List<FishingAttempt> records = [];
    private IReadOnlyList<FishingAttempt> snapshot = Array.Empty<FishingAttempt>();
    private Guid? sessionId;
    private DateTimeOffset? sessionUtc;
    public int Revision { get; private set; }
    public FishingAttempt? Active { get; private set; }
    public TimeSpan? CastStartedAt { get; private set; }
    public TimeSpan? SessionStartedAt { get; private set; }
    public DateTimeOffset? SessionStartedUtc => sessionUtc;
    public bool HasSession => sessionId is not null;
    public FishingTripSummary? Trip { get; private set; }
    public IReadOnlyList<FishingAttempt> Records => snapshot;

    public void SetSequence(long sequence)
    {
        if (Trip is null || Trip.Sequence != 0) return;
        Trip = Trip with { Sequence = sequence };
        Publish();
    }

    public void Start(TimeSpan now, DateTimeOffset utc, uint territory, uint? spot, uint? bait,
        FishingBaitKind baitKind, bool sessionStartKnown, bool castStartKnown = true)
    {
        if (Active is not null) End(now, utc, FishingOutcome.Unknown, FishingEndReason.NextCast);
        if (sessionId is null)
        {
            records.Clear();
            sessionId = Guid.NewGuid();
            sessionUtc = sessionStartKnown ? utc : null;
            SessionStartedAt = sessionStartKnown ? now : null;
            Trip = new(sessionId.Value, 0, territory, spot, sessionUtc, null, true, 0, 0, 0, 0, 0, 0, 0, FishingEndReason.None);
            Trip = Trip with { FirstCastUtc = castStartKnown ? utc : null };
        }
        CastStartedAt = castStartKnown ? now : null;
        Active = new(Guid.NewGuid(), sessionId.Value, sessionUtc, territory, spot, bait, baitKind,
            castStartKnown ? utc : null);
        records.Insert(0, Active);
        if (records.Count > Capacity) records.RemoveAt(records.Count - 1);
        Trip = Trip! with { Casts = Trip!.Casts + 1, Retained = records.Count, SpotId = Trip.SpotId ?? spot };
        Publish();
    }

    public void SetSpot(uint spot)
    {
        if (Active is null || Active.SpotId is not null || spot == 0) return;
        if (Trip is not null && Trip.SpotId is null) Trip = Trip with { SpotId = spot };
        ReplaceActive(Active with { SpotId = spot });
    }

    public void Bite(TimeSpan now)
    {
        if (Active is { BiteSeconds: null } && CastStartedAt is { } start)
            ReplaceActive(Active with { BiteSeconds = Math.Max(0, (now - start).TotalSeconds) });
    }

    public void Hook(TimeSpan now)
    {
        if (Active is { HookSeconds: null } && CastStartedAt is { } start)
            ReplaceActive(Active with { HookSeconds = Math.Max(0, (now - start).TotalSeconds) });
    }

    public void End(TimeSpan now, DateTimeOffset utc, FishingOutcome outcome, FishingEndReason reason,
        uint? fish = null, int? quantity = null)
    {
        if (Active is null) return;
        records[0] = Active with { Outcome = outcome, EndReason = reason, EndedUtc = utc,
            DurationSeconds = CastStartedAt is { } start ? Math.Max(0, (now - start).TotalSeconds) : null,
            FishItemId = fish, Quantity = quantity };
        Active = null;
        CastStartedAt = null;
        if (Trip is not null)
            Trip = Trip with
            {
                Caught = Trip.Caught + (outcome == FishingOutcome.Caught ? 1 : 0),
                Failed = Trip.Failed + (outcome == FishingOutcome.Failed ? 1 : 0),
                Cancelled = Trip.Cancelled + (outcome == FishingOutcome.Cancelled ? 1 : 0),
                Interrupted = Trip.Interrupted + (outcome == FishingOutcome.Interrupted ? 1 : 0),
                Unknown = Trip.Unknown + (outcome == FishingOutcome.Unknown ? 1 : 0),
            };
        Publish();
    }

    public void EndSession(TimeSpan now, DateTimeOffset utc, FishingEndReason reason)
    {
        End(now, utc, FishingOutcome.Interrupted, reason);
        if (sessionId is null) return;
        if (Trip is not null) Trip = Trip with { IsActive = false, EndedUtc = utc, EndReason = reason };
        sessionId = null;
        sessionUtc = null;
        SessionStartedAt = null;
        Publish();
    }

    private void ReplaceActive(FishingAttempt value)
    {
        Active = value;
        records[0] = value;
        Publish();
    }
    private void Publish()
    {
        snapshot = Array.AsReadOnly(records.ToArray());
        Revision++;
    }
}
