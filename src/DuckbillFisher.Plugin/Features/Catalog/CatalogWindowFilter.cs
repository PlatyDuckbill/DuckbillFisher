using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

/// <summary>Only known boundaries exclude fish; thresholds use real minutes and the selected forecast.</summary>
internal static class CatalogWindowFilter
{
    public static bool Enabled(CatalogSettings settings) => settings.MinimumRemainingMinutes > 0 ||
        settings.MinimumUpcomingMinutes is not null || settings.MaximumUpcomingMinutes is not null;

    public static bool Matches(CatalogSettings settings, FishingWindow? window, DateTimeOffset now) => window switch
    {
        { Kind: FishingWindowKind.Open, End: { } end } when settings.MinimumRemainingMinutes > 0 =>
            end - now >= TimeSpan.FromMinutes(settings.MinimumRemainingMinutes),
        { Kind: FishingWindowKind.Upcoming, Start: { } start } =>
            (settings.MinimumUpcomingMinutes is not { } min || start - now >= TimeSpan.FromMinutes(min)) &&
            (settings.MaximumUpcomingMinutes is not { } max || start - now <= TimeSpan.FromMinutes(max)),
        _ => true,
    };

    public static DateTimeOffset NextChange(CatalogSettings settings, IEnumerable<FishingWindow> windows, DateTimeOffset now)
    {
        if (!Enabled(settings)) return DateTimeOffset.MaxValue;
        var next = now.AddSeconds(30);
        foreach (var window in windows)
        {
            if (window is { Kind: FishingWindowKind.Open, End: { } end } && settings.MinimumRemainingMinutes > 0)
                Include(end.AddMinutes(-settings.MinimumRemainingMinutes).AddTicks(1));
            if (window is { Kind: FishingWindowKind.Upcoming, Start: { } start })
            {
                if (settings.MinimumUpcomingMinutes is { } min) Include(start.AddMinutes(-min).AddTicks(1));
                if (settings.MaximumUpcomingMinutes is { } max) Include(start.AddMinutes(-max));
            }
        }
        return next;

        void Include(DateTimeOffset boundary) { if (boundary > now && boundary < next) next = boundary; }
    }
}
