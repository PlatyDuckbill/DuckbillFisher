using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

/// <summary>Only known boundaries exclude fish; thresholds use real minutes and the selected forecast.</summary>
internal static class CatalogWindowFilter
{
    public static bool Enabled(CatalogSettings settings) => settings.MinimumRemainingMinutes > 0 || settings.MaximumUpcomingMinutes > 0;

    public static bool Matches(CatalogSettings settings, FishingWindow? window, DateTimeOffset now) => window switch
    {
        { Kind: FishingWindowKind.Open, End: { } end } when settings.MinimumRemainingMinutes > 0 =>
            end - now >= TimeSpan.FromMinutes(settings.MinimumRemainingMinutes),
        { Kind: FishingWindowKind.Upcoming, Start: { } start } when settings.MaximumUpcomingMinutes > 0 =>
            start - now <= TimeSpan.FromMinutes(settings.MaximumUpcomingMinutes),
        _ => true,
    };

    public static DateTimeOffset NextChange(CatalogSettings settings, IEnumerable<FishingWindow> windows, DateTimeOffset now)
    {
        if (!Enabled(settings)) return DateTimeOffset.MaxValue;
        var next = now.AddSeconds(30);
        foreach (var window in windows)
        {
            var boundary = window switch
            {
                { Kind: FishingWindowKind.Open, End: { } end } when settings.MinimumRemainingMinutes > 0 =>
                    end.AddMinutes(-settings.MinimumRemainingMinutes).AddTicks(1),
                { Kind: FishingWindowKind.Upcoming, Start: { } start } when settings.MaximumUpcomingMinutes > 0 =>
                    start.AddMinutes(-settings.MaximumUpcomingMinutes),
                _ => DateTimeOffset.MaxValue,
            };
            if (boundary > now && boundary < next) next = boundary;
        }
        return next;
    }
}
