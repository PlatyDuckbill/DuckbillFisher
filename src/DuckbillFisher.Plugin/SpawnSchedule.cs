using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal enum ScheduleState { Waiting, Computing, Ready, Failed }
internal enum ScheduleRowState { Timed, Outside, Unknown, Always, Unsupported, Incomplete }
internal sealed record ScheduleEntry(FishGuideRow Fish, CollectionStatus Collection, ScheduleRowState State,
    FishingWindow[] Windows, FishingWindow? Next, FishingForecastIssue Issue, bool DurationAssumed);
internal sealed record SpawnScheduleView(int Session, DateTimeOffset At, DateTimeOffset Through, bool FishEyes,
    ScheduleState State, ScheduleEntry[] Timed, ScheduleEntry[] Other, int SourceCount, int TargetCount)
{
    public ScheduleSlot[] Slots { get; init; } = [];
    public ScheduleCalendarData? Calendar { get; init; }
}
internal sealed record ScheduleSlot(ScheduleEntry Entry, FishingWindow Window);
internal sealed record ScheduleCoincidence(string Name, DateTimeOffset Start, DateTimeOffset End);
internal sealed record ScheduleSelection(ScheduleEntry Entry, FishingWindow Window, bool Ended,
    IReadOnlyDictionary<uint, ScheduleCoincidence[]> Coincidences);

/// <summary>UI requests only; filters on Framework, managed forecast inputs on a cancellable worker.</summary>
internal sealed class SpawnSchedule : IDisposable
{
    internal const int MaxWindowsPerFish = 512;
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly FishingRegionObserver region;
    private readonly CatalogQueryState query;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly TimeProvider clock;
    private readonly object gate = new();
    private SpawnScheduleView view = Empty(-1);
    private CancellationTokenSource? cancellation;
    private Task<SpawnScheduleView?>? work;
    private RequestKey? lastKey;
    private long lastDraw;
    private bool active, disposed, catalogRequested;
    private string search = "";
    private DateTimeOffset lastClock;
    private DateTimeOffset nextBoundary = DateTimeOffset.MaxValue;
    private (uint FishId, DateTimeOffset Start)? selected;
    private ScheduleSelection? selection;
    private bool selectionChanged;
    private DateOnly? calendarMonth, calendarDate;
    private CalendarDayView? calendarDay;
    private bool calendarDateChanged;

    private sealed record RequestKey(int Session, int PersonalRevision, int CollectionRevision,
        IReadOnlyList<FishGuideRow> Rows, FishingRegionView? Region, CatalogQuery? Query,
        SpawnScheduleSettings Settings, bool FishEyes, string Search, DateOnly? Month, string Zone);

    public SpawnSchedule(FishCatalog catalog, FishPreferences preferences, FishCollectionObserver collection,
        FishingRegionObserver region, CatalogQueryState query, IFramework framework, IPluginLog log, TimeProvider? clock = null)
    {
        this.catalog = catalog; this.preferences = preferences; this.collection = collection;
        this.region = region; this.query = query; this.framework = framework; this.log = log;
        this.clock = clock ?? TimeProvider.System;
        framework.Update += Update;
    }

    public SpawnScheduleView View { get { lock (gate) return view; } }
    public DateTimeOffset Now => clock.GetUtcNow();
    public ScheduleSelection? Selection { get { lock (gate) return selection; } }
    public CalendarDayView? CalendarDay { get { lock (gate) return calendarDay; } }
    public void SetCalendarMonth(DateOnly? value)
    { lock (gate) if (!disposed) calendarMonth = value is { } month ? new(month.Year, month.Month, 1) : null; }
    public void SelectDate(DateOnly value)
    { lock (gate) if (!disposed) { calendarDate = value; calendarDateChanged = true; } }
    public void Select(uint fishId, DateTimeOffset start)
    { lock (gate) if (!disposed && selected != (fishId, start)) { selected = (fishId, start); selectionChanged = true; } }
    public void ClearSelection()
    { lock (gate) { selected = null; selection = null; selectionChanged = false; } }
    public void SetActive(bool value)
    {
        lock (gate)
        {
            if (disposed) return;
            active = value;
            if (value) lastDraw = clock.GetTimestamp();
            else { Cancel(); lastKey = null; view = Empty(preferences.View.Session); selection = null; selected = null; calendarDay = null; }
        }
    }
    public void SetSearch(string value) { lock (gate) if (!disposed) search = value.Trim(); }

    private static SpawnScheduleView Empty(int session) => new(session, default, default, false, ScheduleState.Waiting, [], [], 0, 0);
    private void Cancel() { cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null; work = null; }

    private void Update(IFramework _)
    {
        lock (gate)
        {
            if (disposed) return;
            if (!active || clock.GetElapsedTime(lastDraw) > TimeSpan.FromSeconds(2))
            {
                if (work is not null || lastKey is not null)
                { Cancel(); lastKey = null; view = Empty(preferences.View.Session); selection = null; selected = null; calendarDay = null; }
                return;
            }
            if (!catalogRequested) { catalogRequested = true; catalog.RequestLoad(); }
            var personal = preferences.View;
            var data = catalog.View;
            var observed = collection.View;
            var settings = personal.Schedule;
            var location = region.View;
            var current = query.Current;
            var key = new RequestKey(personal.Session, personal.CatalogRevision, observed.Revision, data.Rows,
                settings.Source == SpawnScheduleSource.Catalog && current.Settings.CurrentRegionOnly ? location : null,
                settings.Source == SpawnScheduleSource.Catalog ? current : null, settings, data.FishEyesEnabled, search,
                calendarMonth, TimeZoneInfo.Local.Id);
            var now = clock.GetUtcNow();
            var clockJump = lastClock != default && (now < lastClock || now - lastClock > TimeSpan.FromSeconds(5));
            lastClock = now;
            var boundary = now >= nextBoundary;
            if (lastKey != key || clockJump || key.Month is null && work is null && view.State == ScheduleState.Ready &&
                (now - view.At >= TimeSpan.FromSeconds(30) || boundary))
            {
                var inputsChanged = lastKey != key;
                Cancel(); lastKey = key;
                if (view.Session != personal.Session)
                { search = ""; key = key with { Search = "" }; lastKey = key; selected = null; }
                if (inputsChanged) { selection = null; calendarDay = null; }
                if (data.Rows.Count == 0) { view = Empty(personal.Session); lastKey = null; return; }
                var filter = new CatalogFilter(current.Settings);
                var source = data.Rows.Where(row => settings.Source == SpawnScheduleSource.Favorites
                    ? personal.Favorites.Contains(row.FishParameterId)
                    : current.Matches(row, filter, personal, observed, location)).ToArray();
                var targets = source.Where(row => CatalogQuery.MatchesSearch(row, key.Search))
                    .Select(row => (Fish: row, Collection: personal.Collection(row.FishParameterId, observed)))
                    .Where(target => !settings.HideCaught || target.Collection != CollectionStatus.Caught).ToArray();
                var through = now.AddHours(settings.Hours);
                var initial = new SpawnScheduleView(personal.Session, now, through, key.FishEyes,
                    ScheduleState.Computing, [], [], source.Length, targets.Length);
                // Keep the last valid display during periodic refreshes; input/clock changes hide stale results.
                if (inputsChanged || clockJump || view.State != ScheduleState.Ready) view = initial;
                cancellation = new();
                var token = cancellation.Token;
                var zone = TimeZoneInfo.Local;
                work = Task.Run(() =>
                {
                    try
                    {
                        return key.Month is { } month
                            ? initial with { State = ScheduleState.Ready, Calendar = SpawnScheduleCalendar.Calculate(month, targets, data, key.FishEyes, zone, token) }
                            : Calculate(initial, targets, data, token);
                    }
                    catch (OperationCanceledException) { return null; }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested) return null;
                        log.Error(ex, "Spawn schedule computation failed.");
                        return initial with { State = ScheduleState.Failed };
                    }
                });
            }
            if (work is { IsCompleted: true } completed)
            {
                var result = completed.GetAwaiter().GetResult();
                work = null; cancellation?.Dispose(); cancellation = null;
                if (result is not null)
                {
                    view = result; selectionChanged = true; calendarDateChanged = true;
                    nextBoundary = result.Timed.SelectMany(entry => entry.Windows)
                        .SelectMany(window => new[] { window.Start, window.End }).Where(at => at > result.At)
                        .Select(at => at!.Value).DefaultIfEmpty(DateTimeOffset.MaxValue).Min();
                }
            }
            if (selectionChanged && view.State == ScheduleState.Ready)
            { selectionChanged = false; RefreshSelection(); }
            if (calendarDateChanged && view.State == ScheduleState.Ready)
            { calendarDateChanged = false; RefreshCalendarDay(); }
        }
    }

    private void RefreshCalendarDay()
    {
        if (view.Calendar is not { } month) { calendarDay = null; return; }
        var date = calendarDate ?? DateOnly.FromDateTime(Now.LocalDateTime);
        var day = month.Days.FirstOrDefault(day => day.Date == date) ?? month.Days[0];
        calendarDate = day.Date;
        calendarDay = new(day.Date, day.Fish.SelectMany(fish => fish.Windows.Select(window => new CalendarDaySlot(fish.Entry, window)))
            .OrderBy(slot => slot.Occurrence.StartMilliseconds).ThenBy(slot => slot.Entry.Fish.Name, StringComparer.OrdinalIgnoreCase).ToArray(), day.Fish.Length);
    }

    private void RefreshSelection()
    {
        if (selected is not { } key) { selection = null; return; }
        var entry = view.Timed.FirstOrDefault(entry => entry.Fish.FishParameterId == key.FishId);
        var window = entry?.Windows.FirstOrDefault(window => window.Start == key.Start);
        if (entry is null || window is null)
        {
            if (selection is { } previous && previous.Entry.Fish.FishParameterId == key.FishId && previous.Window.End <= view.At)
                selection = previous with { Ended = true, Coincidences = new Dictionary<uint, ScheduleCoincidence[]>() };
            else selection = null;
            return;
        }
        var coincidences = new Dictionary<uint, ScheduleCoincidence[]>();
        foreach (var other in view.Timed.Where(other => other.Fish.FishParameterId != key.FishId))
        {
            var intersections = other.Windows.Select(otherWindow =>
            {
                var start = otherWindow.Start > window.Start ? otherWindow.Start!.Value : window.Start!.Value;
                var leftEnd = window.End ?? view.Through;
                var rightEnd = otherWindow.End ?? view.Through;
                return new ScheduleCoincidence(other.Fish.Name, start, leftEnd < rightEnd ? leftEnd : rightEnd);
            }).Where(range => range.End > range.Start).ToArray();
            if (intersections.Length > 0) coincidences.Add(other.Fish.FishParameterId, intersections);
        }
        selection = new(entry, window, false, coincidences);
    }

    private static SpawnScheduleView Calculate(SpawnScheduleView initial,
        (FishGuideRow Fish, CollectionStatus Collection)[] targets, FishCatalogView catalog, CancellationToken token)
    {
        var entries = new List<ScheduleEntry>();
        foreach (var (row, caught) in targets)
        {
            token.ThrowIfCancellationRequested();
            var windows = Array.Empty<FishingWindow>();
            var issue = FishingForecastIssue.None;
            ScheduleRowState state;
            if (row.IsOceanFishing || row.ItemType != "Item" || row.Details is null && catalog.SpearfishingItems.Contains(row.ItemId))
                state = ScheduleRowState.Unsupported;
            else if (row.Details is not { Spot: not null }) state = ScheduleRowState.Unknown;
            else
            {
                var found = catalog.Forecast.Enumerate(initial.At, initial.Through, row.ItemId, initial.FishEyes, token)
                    .Take(MaxWindowsPerFish + 1).ToArray();
                issue = found.FirstOrDefault()?.Issue ?? FishingForecastIssue.None;
                state = found.FirstOrDefault()?.Kind switch
                {
                    FishingWindowKind.Always => ScheduleRowState.Always,
                    FishingWindowKind.Unknown => ScheduleRowState.Unknown,
                    _ when found.Length > MaxWindowsPerFish => ScheduleRowState.Incomplete,
                    _ when found.Length == 0 => ScheduleRowState.Outside,
                    _ => ScheduleRowState.Timed,
                };
                windows = found.Where(window => window.Start is not null).Take(MaxWindowsPerFish).ToArray();
            }
            catalog.Windows.TryGetValue(row.FishParameterId, out var next);
            entries.Add(new(row, caught, state, windows, next, issue,
                row.Details?.Rule is { Predators.Length: > 0, IntuitionSeconds: null }));
        }
        var timed = entries.Where(entry => entry.Windows.Length > 0).OrderBy(entry => entry.Windows[0].Kind == FishingWindowKind.Open ? 0 : 1)
            .ThenBy(entry => entry.Windows[0].Kind == FishingWindowKind.Open ? entry.Windows[0].End ?? DateTimeOffset.MaxValue : entry.Windows[0].Start)
            .ThenBy(entry => entry.Fish.Name, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Fish.FishParameterId).ToArray();
        return initial with { State = ScheduleState.Ready, Timed = timed,
            Slots = timed.SelectMany(entry => entry.Windows.Select(window => new ScheduleSlot(entry, window))).ToArray(),
            Other = entries.Where(entry => entry.State != ScheduleRowState.Timed).OrderBy(entry => entry.Fish.Name, StringComparer.OrdinalIgnoreCase).ToArray() };
    }

    public void Dispose()
    {
        lock (gate) { disposed = true; Cancel(); lastKey = null; view = Empty(-1); }
        framework.Update -= Update;
    }
}
