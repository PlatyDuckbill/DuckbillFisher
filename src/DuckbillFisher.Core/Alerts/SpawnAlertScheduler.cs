namespace DuckbillFisher.Core;

public enum SpawnAlertAudience { AllFavorites, UncaughtOnly }

public sealed record SpawnAlertCandidate(uint FishId, bool Favorite, CollectionStatus Collection, FishingWindow Window,
    int? LeadMinutes = null);
public sealed record SpawnAlert(uint FishId, DateTimeOffset Start);
public sealed record SpawnAlertEvaluation(IReadOnlyList<SpawnAlert> Due, int Armed, bool Rebased);

/// <summary>Crosses advance-alert deadlines using explicit UTC and monotonic time inputs. Never catches up.</summary>
public sealed class SpawnAlertScheduler
{
    public const int DefaultLeadMinutes = 0;
    public const int MinLeadMinutes = 0;
    public const int MaxLeadMinutes = 120;
    private Dictionary<uint, (DateTimeOffset Start, int Lead)> armed = [];
    private DateTimeOffset? previousUtc;
    private TimeSpan previousElapsed;
    private int previousSession;
    private bool wasEnabled;

    /// <summary>Shared opening key for favorite-zero and current-hole notices.
    /// The caller owns per-character persistence; advance reservations use a separate start key.</summary>
    public static IReadOnlyList<(uint FishId, DateTimeOffset End)> UnseenOpenings(
        IReadOnlyDictionary<uint, long>? previous, IReadOnlyList<(uint FishId, DateTimeOffset End)> occurrences) =>
        occurrences.Where(value => value.End.ToUnixTimeMilliseconds() >
            (previous?.GetValueOrDefault(value.FishId) ?? 0)).ToArray();

    public static bool Includes(SpawnAlertAudience audience, CollectionStatus collection) => audience switch
    {
        SpawnAlertAudience.AllFavorites => true,
        SpawnAlertAudience.UncaughtOnly => collection == CollectionStatus.Uncaught,
        _ => false,
    };

    public static FishingWindow? FindFutureWindow(DateTimeOffset now, int leadMinutes, FishingWindow? first,
        FishingRule rule, IReadOnlyList<WeatherChance> rates, long reservedStartUnixMilliseconds) =>
        FindFutureWindow(now, leadMinutes, first, reservedStartUnixMilliseconds,
            at => FishingAvailability.Find(at, rule, rates));

    public static FishingWindow? FindFutureWindow(DateTimeOffset now, int leadMinutes, FishingWindow? first,
        long reservedStartUnixMilliseconds, Func<DateTimeOffset, FishingWindow> find)
    {
        if (leadMinutes is < MinLeadMinutes or > MaxLeadMinutes)
            return null;
        var window = first;
        // Advance beyond open/too-close windows with the existing formula. Bound the work per plan.
        for (var index = 0; index < 64 && window is not null; index++)
        {
            if (window.Kind is not (FishingWindowKind.Open or FishingWindowKind.Upcoming))
                return null;
            if (window.Start is { } start && start.AddMinutes(-leadMinutes) > now &&
                start.ToUnixTimeMilliseconds() > reservedStartUnixMilliseconds)
                return window with { Kind = FishingWindowKind.Upcoming };
            if (window.End is not { } end || end <= now && index > 0)
                return null;
            var following = find(end);
            if (following.Start <= window.Start || following.End <= end)
                return null;
            window = following;
        }
        return null;
    }

    // Planning and delivery must discard the same old timeline, before choosing the next window.
    // Inspecting this does not advance the scheduler's baseline.
    public bool RequiresRebase(DateTimeOffset now, TimeSpan elapsed, int session, bool enabled)
    {
        var delta = elapsed - previousElapsed;
        return previousUtc is null || session != previousSession || enabled != wasEnabled ||
            delta <= TimeSpan.Zero || delta > TimeSpan.FromSeconds(10) ||
            Math.Abs(((now - previousUtc.Value) - delta).TotalSeconds) > 2;
    }

    public SpawnAlertEvaluation Evaluate(DateTimeOffset now, TimeSpan elapsed, int session, bool enabled,
        int leadMinutes, SpawnAlertAudience audience, IReadOnlyList<SpawnAlertCandidate> candidates)
    {
        var rebase = RequiresRebase(now, elapsed, session, enabled);
        var due = new List<SpawnAlert>();
        var next = new Dictionary<uint, (DateTimeOffset Start, int Lead)>();
        if (enabled && leadMinutes is >= MinLeadMinutes and <= MaxLeadMinutes)
        {
            foreach (var fish in candidates)
            {
                if (fish.FishId == 0 || !fish.Favorite || !Includes(audience, fish.Collection) ||
                    fish.Window is not { Kind: FishingWindowKind.Upcoming, Start: { } start })
                    continue;
                var lead = fish.LeadMinutes ?? leadMinutes;
                if (lead is < MinLeadMinutes or > MaxLeadMinutes)
                    continue;
                // A zero-minute deadline crosses the opening itself. Only an already armed,
                // still-open window may be delivered; positive leads must remain before opening.
                if (start <= now && (lead != 0 || fish.Window.End is not { } end || end <= now))
                    continue;
                var deadline = start.AddMinutes(-lead);
                if (deadline > now)
                    next[fish.FishId] = (start, lead);
                else if (!rebase && armed.TryGetValue(fish.FishId, out var reserved) && reserved == (start, lead) &&
                         previousUtc < deadline)
                    due.Add(new(fish.FishId, start));
            }
        }
        armed = next;
        previousUtc = now;
        previousElapsed = elapsed;
        previousSession = session;
        wasEnabled = enabled;
        return new(due, armed.Count, rebase);
    }
}
