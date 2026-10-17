namespace DuckbillFisher.Core;

public enum FishingOutcome { Pending, Caught, Failed, Cancelled, Interrupted, Unknown }
public enum FishingEndReason { None, CatchLog, ManualRetrieve, RodFolded, NativeFinished, NextCast, Logout, Travel, Reload, ObservationGap, ReadUnavailable, UnsupportedMode }
public enum FishingBaitKind { Unknown, Equipped, Mooch, Swimbait }

public sealed record FishingAttempt(Guid Id, Guid SessionId, DateTimeOffset? SessionStartedUtc,
    uint TerritoryId, uint? SpotId, uint? BaitItemId, FishingBaitKind BaitKind, DateTimeOffset? StartedUtc,
    FishingOutcome Outcome = FishingOutcome.Pending, FishingEndReason EndReason = FishingEndReason.None,
    DateTimeOffset? EndedUtc = null, double? DurationSeconds = null, uint? FishItemId = null, int? Quantity = null,
    double? BiteSeconds = null, double? HookSeconds = null);

/// <summary>Single-writer cast/session history. Inputs are copied observations and caller-controlled times.</summary>
public sealed class FishingAttempts
{
    public const int Capacity = 1000;
    public const int RecentCapacity = 100;
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
    public IReadOnlyList<FishingAttempt> Records => snapshot;

    public void Restore(IEnumerable<FishingAttempt> saved)
    {
        records.Clear();
        records.AddRange(saved.Take(Capacity).Select(row => row.Outcome == FishingOutcome.Pending
            ? row with { Outcome = FishingOutcome.Interrupted, EndReason = FishingEndReason.Reload } : row));
        Active = null;
        sessionId = null;
        sessionUtc = null;
        CastStartedAt = SessionStartedAt = null;
        Publish();
    }

    public void Start(TimeSpan now, DateTimeOffset utc, uint territory, uint? spot, uint? bait,
        FishingBaitKind baitKind, bool sessionStartKnown, bool castStartKnown = true)
    {
        if (Active is not null) End(now, utc, FishingOutcome.Unknown, FishingEndReason.NextCast);
        if (sessionId is null)
        {
            sessionId = Guid.NewGuid();
            sessionUtc = sessionStartKnown ? utc : null;
            SessionStartedAt = sessionStartKnown ? now : null;
        }
        CastStartedAt = castStartKnown ? now : null;
        Active = new(Guid.NewGuid(), sessionId.Value, sessionUtc, territory, spot, bait, baitKind,
            castStartKnown ? utc : null);
        records.Insert(0, Active);
        if (records.Count > Capacity) records.RemoveAt(records.Count - 1);
        Publish();
    }

    public void SetSpot(uint spot)
    {
        if (Active is null || Active.SpotId is not null || spot == 0) return;
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
        Publish();
    }

    public void EndSession(TimeSpan now, DateTimeOffset utc, FishingEndReason reason)
    {
        End(now, utc, FishingOutcome.Interrupted, reason);
        if (sessionId is null) return;
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
