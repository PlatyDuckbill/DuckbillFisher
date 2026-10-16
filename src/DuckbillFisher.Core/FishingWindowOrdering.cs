namespace DuckbillFisher.Core;

public enum FishingWindowSortGroup { Open, Upcoming, Unknown, Always }

/// <summary>Orders normal time/weather windows without inferring personal readiness.</summary>
public static class FishingWindowOrdering
{
    // Absolute boundaries keep order stable as the displayed countdown ticks.
    // Missing boundaries remain unknown instead of becoming zero remaining time.
    public static (FishingWindowSortGroup Group, DateTimeOffset Time) Key(FishingWindow? window) =>
        window switch
        {
            { Kind: FishingWindowKind.Open, End: { } end } => (FishingWindowSortGroup.Open, end),
            { Kind: FishingWindowKind.Upcoming, Start: { } start } => (FishingWindowSortGroup.Upcoming, start),
            { Kind: FishingWindowKind.Always } => (FishingWindowSortGroup.Always, DateTimeOffset.MinValue),
            _ => (FishingWindowSortGroup.Unknown, DateTimeOffset.MinValue),
        };
}
