namespace DuckbillFisher.Core;

public sealed record CastTimerAlert(Guid Id, int Seconds, bool Enabled = true, string Name = "", int Sound = 1);

/// <summary>Crossed cast deadlines only. All times are supplied by the caller's monotonic clock.</summary>
public sealed class CastTimerAlerts
{
    private Guid? cast;
    private int session = -1;
    private TimeSpan previousNow, previousElapsed;
    private TimeSpan? previousStart;
    private readonly HashSet<Guid> delivered = [];

    public IReadOnlyList<CastTimerAlert> Evaluate(int currentSession, Guid? currentCast, TimeSpan? startedAt,
        TimeSpan now, IReadOnlyList<CastTimerAlert> alerts)
    {
        var elapsed = startedAt is { } start ? now - start : TimeSpan.Zero;
        if (currentSession != session || currentCast != cast) delivered.Clear();
        if (currentSession != session || currentCast != cast || currentCast is null || startedAt is null || startedAt != previousStart ||
            now < previousNow || now - previousNow > TimeSpan.FromSeconds(5) || elapsed < previousElapsed)
        {
            session = currentSession; cast = currentCast; previousNow = now; previousElapsed = elapsed; previousStart = startedAt;
            return [];
        }
        var due = alerts.Where(alert => alert.Enabled && !delivered.Contains(alert.Id) && alert.Seconds > previousElapsed.TotalSeconds &&
            alert.Seconds <= elapsed.TotalSeconds).OrderBy(alert => alert.Seconds).ThenBy(alert => alert.Id).ToArray();
        foreach (var alert in due) delivered.Add(alert.Id);
        previousNow = now; previousElapsed = elapsed;
        return due;
    }
}
