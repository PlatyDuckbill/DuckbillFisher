using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

// Compact UTC storage: the month retains intervals, not formatted labels or a UI row per occurrence.
internal readonly record struct CalendarOccurrence(long StartMilliseconds, long EndMilliseconds)
{
    public DateTimeOffset Start => DateTimeOffset.FromUnixTimeMilliseconds(StartMilliseconds);
    public DateTimeOffset? End => EndMilliseconds < 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(EndMilliseconds);
    public FishingWindow Window => new(FishingWindowKind.Upcoming, Start, End);
}
internal sealed record CalendarFishDay(ScheduleEntry Entry, CalendarOccurrence[] Windows);
internal sealed record CalendarDay(DateOnly Date, CalendarFishDay[] Fish);
internal sealed record ScheduleCalendarData(DateOnly Month, CalendarDay[] Days, int Unknown, int Always, int Unsupported, bool Incomplete);
internal readonly record struct CalendarDaySlot(ScheduleEntry Entry, CalendarOccurrence Occurrence);
internal sealed record CalendarDayView(DateOnly Date, CalendarDaySlot[] Slots, int FishCount);

internal static class SpawnScheduleCalendar
{
    internal const int MaxOccurrences = 1_000_000;
    internal const int MaxFishWindows = 4096;

    // Explicit civil-date conversion keeps UTC storage and local day boundaries distinct, including DST.
    internal static DateTimeOffset Midnight(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    internal static ScheduleCalendarData Calculate(DateOnly month,
        (FishGuideRow Fish, CollectionStatus Collection)[] targets, FishCatalogView catalog, bool fishEyes,
        TimeZoneInfo zone, CancellationToken token)
    {
        var count = DateTime.DaysInMonth(month.Year, month.Month);
        var start = Midnight(month, zone);
        var end = Midnight(month.AddMonths(1), zone);
        var days = Enumerable.Range(0, count).Select(_ => new Dictionary<uint, (ScheduleEntry Entry, List<CalendarOccurrence> Windows)>()).ToArray();
        var unknown = 0; var always = 0; var unsupported = 0; var stored = 0; var incomplete = false;
        foreach (var (fish, collection) in targets)
        {
            token.ThrowIfCancellationRequested();
            if (fish.IsOceanFishing || fish.ItemType != "Item" || fish.Details is null && catalog.SpearfishingItems.Contains(fish.ItemId))
            { unsupported++; continue; }
            if (fish.Details is not { Spot: not null }) { unknown++; continue; }
            var entry = new ScheduleEntry(fish, collection, ScheduleRowState.Timed, [], null, FishingForecastIssue.None,
                fish.Details.Rule is { Predators.Length: > 0, IntuitionSeconds: null });
            var fishWindows = 0;
            foreach (var window in catalog.Forecast.EnumerateCalendar(start, end, fish.ItemId, fishEyes, token))
            {
                token.ThrowIfCancellationRequested();
                if (window.Kind == FishingWindowKind.Unknown) { unknown++; break; }
                if (window.Kind == FishingWindowKind.Always) { always++; break; }
                if (window.Start is not { } windowStart) continue;
                if (++fishWindows > MaxFishWindows) { incomplete = true; break; }
                var clippedStart = windowStart < start ? start : windowStart;
                var clippedEnd = window.End is { } finish && finish < end ? finish : end;
                if (clippedEnd <= clippedStart) continue;
                var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clippedStart, zone).DateTime).DayNumber - month.DayNumber;
                var last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clippedEnd.AddTicks(-1), zone).DateTime).DayNumber - month.DayNumber;
                var occurrence = new CalendarOccurrence(windowStart.ToUnixTimeMilliseconds(), window.End?.ToUnixTimeMilliseconds() ?? -1);
                for (var day = Math.Max(0, first); day <= Math.Min(count - 1, last); day++)
                {
                    if (stored >= MaxOccurrences) { incomplete = true; break; }
                    if (!days[day].TryGetValue(fish.FishParameterId, out var value))
                        days[day][fish.FishParameterId] = value = (entry, []);
                    value.Windows.Add(occurrence);
                    stored++;
                }
                if (stored >= MaxOccurrences) { incomplete = true; break; }
            }
            if (stored >= MaxOccurrences) { incomplete = true; break; }
        }
        token.ThrowIfCancellationRequested();
        return new(month, days.Select((day, index) => new CalendarDay(month.AddDays(index),
            day.Values.Select(value => new CalendarFishDay(value.Entry, value.Windows.ToArray()))
                .OrderBy(value => value.Windows[0].StartMilliseconds).ThenBy(value => value.Entry.Fish.Name, StringComparer.OrdinalIgnoreCase).ToArray())).ToArray(),
            unknown, always, unsupported, incomplete);
    }
}
