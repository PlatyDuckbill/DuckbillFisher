namespace DuckbillFisher.Core;

public enum SpawnAlertAudience { AllFavorites, UncaughtOnly }

public sealed record SpawnAlertCandidate(uint FishId, bool Favorite, CollectionStatus Collection, FishingWindow Window,
    int? LeadMinutes = null);
public sealed record SpawnAlert(uint FishId, DateTimeOffset Start);
public sealed record SpawnAlertEvaluation(IReadOnlyList<SpawnAlert> Due, int Armed, bool Rebased);

/// <summary>Crosses advance-alert deadlines using explicit UTC and monotonic time inputs. Never catches up.</summary>
public sealed class SpawnAlertScheduler
{
    public const int DefaultLeadMinutes = 5;
    public const int MinLeadMinutes = 1;
    public const int MaxLeadMinutes = 60;
    private Dictionary<uint, (DateTimeOffset Start, int Lead)> armed = [];
    private DateTimeOffset? previousUtc;
    private TimeSpan previousElapsed;
    private int previousSession;
    private bool wasEnabled;

    public static bool Includes(SpawnAlertAudience audience, CollectionStatus collection) => audience switch
    {
        SpawnAlertAudience.AllFavorites => true,
        SpawnAlertAudience.UncaughtOnly => collection == CollectionStatus.Uncaught,
        _ => false,
    };

    public static FishingWindow? FindFutureWindow(DateTimeOffset now, int leadMinutes, FishingWindow? first,
        FishingRule rule, IReadOnlyList<WeatherChance> rates, long reservedStartUnixMilliseconds)
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
            var following = FishingAvailability.Find(end, rule, rates);
            if (following.Start <= window.Start || following.End <= end)
                return null;
            window = following;
        }
        return null;
    }

    public SpawnAlertEvaluation Evaluate(DateTimeOffset now, TimeSpan elapsed, int session, bool enabled,
        int leadMinutes, SpawnAlertAudience audience, IReadOnlyList<SpawnAlertCandidate> candidates)
    {
        var delta = elapsed - previousElapsed;
        var rebase = previousUtc is null || session != previousSession || enabled != wasEnabled ||
            delta <= TimeSpan.Zero || delta > TimeSpan.FromSeconds(10) ||
            Math.Abs(((now - previousUtc.Value) - delta).TotalSeconds) > 2;
        var due = new List<SpawnAlert>();
        var next = new Dictionary<uint, (DateTimeOffset Start, int Lead)>();
        if (enabled && leadMinutes is >= MinLeadMinutes and <= MaxLeadMinutes)
        {
            foreach (var fish in candidates)
            {
                if (fish.FishId == 0 || !fish.Favorite || !Includes(audience, fish.Collection) ||
                    fish.Window is not { Kind: FishingWindowKind.Upcoming, Start: { } start } || start <= now)
                    continue;
                var lead = fish.LeadMinutes ?? leadMinutes;
                if (lead is < MinLeadMinutes or > MaxLeadMinutes)
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
