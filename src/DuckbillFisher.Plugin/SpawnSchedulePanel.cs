using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Schedule snapshots only. No forecast computation, sheet reads or whole-catalog filtering in Draw.</summary>
internal sealed class SpawnSchedulePanel(SpawnSchedule schedule, FishCatalog catalog, FishPreferences preferences,
    ITextureProvider textures, Action<FishGuideRow> showDetail)
{
    private static readonly int[] Hours = [1, 3, 6, 12, 24];
    private static readonly Vector4 OpenColor = new(0.30f, 0.71f, 0.56f, 1);
    private static readonly Vector4 UpcomingColor = new(0.34f, 0.60f, 0.84f, 1);
    private static readonly Vector4 ConcurrentColor = new(0.94f, 0.70f, 0.32f, 1);
    private string search = "";
    private int session = -1;
    private bool emphasize = true;
    private DateTimeOffset axisStart, axisEnd;
    private string? candidatePopup;
    private uint candidateFish;
    private FishingWindow[] candidates = [];

    public void SetActive(bool value) => schedule.SetActive(value);

    public void Draw()
    {
        schedule.SetActive(true);
        var personal = preferences.View;
        if (session != personal.Session)
        { session = personal.Session; search = ""; schedule.SetSearch(""); candidatePopup = null; }
        if (!ImGui.BeginChild("ScheduleContent")) { ImGui.EndChild(); return; }
        var settings = personal.Schedule;
        ImGui.TextUnformatted(UiText.Get(T.ScheduleTitle));
        DrawControls(personal, ref settings);
        if (personal.Error is { } error) ImGui.TextWrapped(error.Display);
        if (catalog.View.Status.Key != T.CatalogLoaded) ImGui.TextWrapped(catalog.View.Status.Display);
        var view = schedule.View;
        if (view.Session != personal.Session || view.State != ScheduleState.Ready)
        {
            ImGui.TextWrapped(UiText.Get(view.State == ScheduleState.Failed ? T.ScheduleFailed : T.ScheduleComputing));
            ImGui.EndChild(); return;
        }
        var now = schedule.Now;
        axisStart = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() / 1800 * 1800);
        axisEnd = view.Through;
        ImGui.TextWrapped(UiText.Get(T.ScheduleRange, axisStart.ToLocalTime(), axisEnd.ToLocalTime(), now.ToLocalTime().ToString("zzz", UiText.Culture)));
        ImGui.TextDisabled(UiText.Get(T.ScheduleCounts, view.SourceCount, view.TargetCount, view.Timed.Length));
        if (view.TargetCount == 0)
        {
            ImGui.TextWrapped(UiText.Get(view.SourceCount == 0
                ? settings.Source == SpawnScheduleSource.Favorites ? T.ScheduleNoFavorites : T.ScheduleNoCatalog
                : T.ScheduleNoMatches));
            if (search.Length > 0 && ImGui.SmallButton(UiText.Label(T.ScheduleClearSearch, "ClearScheduleSearch")))
            { search = ""; schedule.SetSearch(""); }
        }
        var selected = schedule.Selection;
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 47;
        if (view.Timed.Length > 0)
        {
            var height = Math.Clamp(ImGui.GetContentRegionAvail().Y * 0.42f, ImGui.GetFontSize() * 6, ImGui.GetFontSize() * 22);
            if (narrow) DrawList(view, selected, now, height);
            else DrawTimeline(view, selected, now, height);
        }
        DrawSelection(selected, now);
        ImGui.TextWrapped(UiText.Get(T.ScheduleComparisonHint));
        DrawOther(view, settings);
        ImGui.EndChild();
    }

    private void DrawControls(FishPreferencesView personal, ref SpawnScheduleSettings settings)
    {
        var right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        string[] sources = [UiText.Get(T.ScheduleFavorites), UiText.Get(T.ScheduleCatalog)];
        var source = (int)settings.Source;
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 13);
        var changed = false;
        ImGui.BeginDisabled(!personal.CanEdit);
        if (ImGui.Combo("###ScheduleSource", ref source, sources, sources.Length))
        { settings = settings with { Source = (SpawnScheduleSource)source }; changed = true; }
        ImGui.EndDisabled();
        UiWidgets.Tooltip(UiText.Get(T.ScheduleSourceHint));
        UiWidgets.FlowNext(ImGui.GetFontSize() * 14, right);
        ImGui.SetNextItemWidth(Math.Min(ImGui.GetFontSize() * 16, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputTextWithHint("###ScheduleSearch", UiText.Get(T.SearchPlaceholder), ref search, 100)) schedule.SetSearch(search);
        ImGui.BeginDisabled(!personal.CanEdit);
        foreach (var hours in Hours)
        {
            var label = UiText.Get(T.ScheduleHours, hours);
            UiWidgets.FlowNext(ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2, right);
            var active = hours == settings.Hours;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
            if (ImGui.Button($"{label}###ScheduleHours{hours}")) { settings = settings with { Hours = hours }; changed = true; }
            if (active) ImGui.PopStyleColor();
        }
        ImGui.NewLine();
        var hide = settings.HideCaught;
        if (ImGui.Checkbox(UiText.Label(T.CollectionHideCaught, "ScheduleHideCaught"), ref hide))
        { settings = settings with { HideCaught = hide }; changed = true; }
        ImGui.EndDisabled();
        UiWidgets.Tooltip(UiText.Get(T.ScheduleHideHint));
        UiWidgets.FlowNext(ImGui.CalcTextSize(UiText.Get(T.ScheduleEmphasize)).X + ImGui.GetFrameHeight() * 1.5f, right);
        ImGui.Checkbox(UiText.Label(T.ScheduleEmphasize, "ScheduleEmphasize"), ref emphasize);
        UiWidgets.Tooltip(UiText.Get(T.ScheduleConcurrentHint));
        var data = catalog.View;
        UiWidgets.FlowNext(ImGui.GetFrameHeight(), right);
        if (UiWidgets.GameIconButton(textures, data.FishEyesSkill?.IconId ?? 0, "ScheduleFishEyes",
            UiText.Get(T.ScheduleFishEyesHint), data.FishEyesEnabled)) catalog.SetFishEyes(!data.FishEyesEnabled);
        UiWidgets.FlowNext(ImGui.GetFontSize() * 15, right);
        ImGui.TextUnformatted(UiText.Get(data.FishEyesEnabled ? T.ForecastWithFishEyes : T.ForecastNormal));
        if (changed) preferences.SetSchedule(settings);
        ImGui.Separator();
    }

    private void DrawTimeline(SpawnScheduleView view, ScheduleSelection? selected, DateTimeOffset now, float height)
    {
        var rowHeight = ImGui.GetFontSize() * 3.5f;
        if (!ImGui.BeginTable("ScheduleTimeline", 2, ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerH |
                ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg, new Vector2(0, height))) return;
        ImGui.TableSetupColumn(UiText.Get(T.ScheduleFishColumn), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 13);
        ImGui.TableSetupColumn("###ScheduleTimeColumn", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, ImGui.GetTextLineHeightWithSpacing() * 2.3f);
        ImGui.TableNextColumn(); ImGui.TextUnformatted(UiText.Get(T.ScheduleFishColumn));
        ImGui.TableNextColumn(); DrawAxis(now);
        var clipper = ImGui.ImGuiListClipper();
        try
        {
            clipper.Begin(view.Timed.Length, rowHeight);
            while (clipper.Step())
                for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; index++)
                {
                    var entry = view.Timed[index];
                    ImGui.PushID((int)entry.Fish.FishParameterId);
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                    ImGui.TableNextColumn(); DrawName(entry, rowHeight);
                    ImGui.TableNextColumn(); DrawTrack(entry, selected, now, rowHeight);
                    ImGui.PopID();
                }
        }
        finally { clipper.Destroy(); }
        ImGui.EndTable();
        ImGui.TextDisabled(UiText.Get(T.ScheduleLegend));
    }

    private void DrawAxis(DateTimeOffset now)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var draw = ImGui.GetWindowDrawList();
        var step = TickMinutes();
        for (var tick = axisStart; tick <= axisEnd; tick = tick.AddMinutes(step))
        {
            var text = tick.ToLocalTime().ToString("HH:mm", UiText.Culture);
            var x = Math.Clamp(X(tick, width) - ImGui.CalcTextSize(text).X / 2, 0, Math.Max(0, width - ImGui.CalcTextSize(text).X));
            draw.AddText(origin + new Vector2(x, ImGui.GetTextLineHeightWithSpacing()), ImGui.GetColorU32(ImGuiCol.TextDisabled), text);
        }
        var current = UiText.Get(T.ScheduleNow, now.ToLocalTime());
        draw.AddText(origin + new Vector2(Math.Clamp(X(now, width), 0, Math.Max(0, width - ImGui.CalcTextSize(current).X)), 0),
            ImGui.GetColorU32(ConcurrentColor), current);
        ImGui.Dummy(new Vector2(width, ImGui.GetTextLineHeightWithSpacing() * 2));
    }

    private void DrawName(ScheduleEntry entry, float height)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + new Vector2(width, height), true);
        draw.AddText(origin, ImGui.GetColorU32(ImGuiCol.Text), entry.Fish.DisplayName);
        var zone = entry.Fish.Details?.Spot?.Zone ?? entry.Fish.Spots.FirstOrDefault()?.Zone ?? entry.Fish.Ocean?.Area ?? "";
        draw.AddText(origin + new Vector2(0, ImGui.GetTextLineHeightWithSpacing()), ImGui.GetColorU32(ImGuiCol.TextDisabled), zone);
        var y = origin.Y + ImGui.GetTextLineHeightWithSpacing() * 2;
        var x = origin.X;
        if (entry.Fish.Details?.Rule.Predators.Length > 0)
        {
            var text = UiText.Get(entry.DurationAssumed ? T.ScheduleIntuitionAssumed : T.ScheduleIntuition);
            draw.AddText(new Vector2(x, y), ImGui.GetColorU32(ConcurrentColor), text);
            x += ImGui.CalcTextSize(text).X + ImGui.GetStyle().ItemSpacing.X;
        }
        if (entry.Collection == CollectionStatus.Unknown)
            draw.AddText(new Vector2(x, y), ImGui.GetColorU32(ImGuiCol.TextDisabled), UiText.Get(T.CollectionUnknown));
        draw.PopClipRect();
        ImGui.InvisibleButton("ScheduleFishName", new Vector2(width, Math.Max(1, height - ImGui.GetStyle().CellPadding.Y * 2)));
        UiWidgets.Tooltip(UiText.Get(T.ScheduleNameTooltip, entry.Fish.DisplayName, zone));
        if (ImGui.IsItemClicked()) showDetail(entry.Fish);
    }

    private void DrawTrack(ScheduleEntry entry, ScheduleSelection? selected, DateTimeOffset now, float height)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var actualHeight = Math.Max(1, height - ImGui.GetStyle().CellPadding.Y * 2);
        ImGui.InvisibleButton("ScheduleTrack", new Vector2(width, actualHeight));
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked();
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + new Vector2(width, actualHeight), true);
        for (var tick = axisStart; tick <= axisEnd; tick = tick.AddMinutes(TickMinutes()))
        {
            var x = origin.X + X(tick, width);
            draw.AddLine(new Vector2(x, origin.Y), new Vector2(x, origin.Y + actualHeight), ImGui.GetColorU32(ImGuiCol.Separator));
        }
        var hits = new List<FishingWindow>();
        foreach (var window in entry.Windows)
        {
            if (window.Start is not { } start || window.End <= now) continue;
            var end = window.End ?? axisEnd;
            if (start >= axisEnd || end <= axisStart) continue;
            var left = Math.Clamp(X(start, width), 0, width);
            var right = Math.Clamp(X(end, width), 0, width);
            var top = origin.Y + actualHeight * 0.34f;
            var bottom = origin.Y + actualHeight * 0.72f;
            var a = new Vector2(origin.X + left, top);
            var b = new Vector2(origin.X + Math.Min(width, Math.Max(right, left + 3)), bottom);
            draw.AddRectFilled(a, b, ImGui.GetColorU32(start <= now ? OpenColor : UpcomingColor), 2);
            var status = UiText.Get(start <= now ? T.WindowOpen : T.WindowUpcoming);
            if (b.X - a.X > ImGui.CalcTextSize(status).X + 8)
                draw.AddText(a + new Vector2(4, Math.Max(0, (b.Y - a.Y - ImGui.GetFontSize()) / 2)),
                    ImGui.GetColorU32(new Vector4(0.04f, 0.09f, 0.12f, 1)), status);
            if (selected?.Entry.Fish.FishParameterId == entry.Fish.FishParameterId && selected.Window.Start == start)
                draw.AddRect(a - new Vector2(2), b + new Vector2(2), ImGui.GetColorU32(ImGuiCol.Text), 2);
            if (emphasize && selected is not null && selected.Entry.Fish.FishParameterId != entry.Fish.FishParameterId &&
                selected.Coincidences.TryGetValue(entry.Fish.FishParameterId, out var coincidences))
                foreach (var range in coincidences)
                {
                    var overlapLeft = origin.X + Math.Clamp(X(range.Start > start ? range.Start : start, width), left, right);
                    var overlapRight = origin.X + Math.Clamp(X(range.End < end ? range.End : end, width), left, right);
                    for (var x = overlapLeft; x < overlapRight; x += 6)
                        draw.AddLine(new Vector2(x, bottom), new Vector2(Math.Min(x + 5, overlapRight), top), ImGui.GetColorU32(ConcurrentColor), 2);
                }
            if (start < axisStart) draw.AddText(a - new Vector2(0, ImGui.GetFontSize()), ImGui.GetColorU32(ImGuiCol.Text), "←");
            if (end > axisEnd || window.End is null)
                draw.AddText(new Vector2(Math.Max(origin.X, b.X - ImGui.GetFontSize()), top - ImGui.GetFontSize()), ImGui.GetColorU32(ImGuiCol.Text), "→");
            var mouse = ImGui.GetMousePos();
            if (hovered && mouse.X >= a.X - 4 && mouse.X <= b.X + 4 && mouse.Y >= top - 5 && mouse.Y <= bottom + 5)
                hits.Add(window);
        }
        var nowX = origin.X + Math.Clamp(X(now, width), 0, width);
        draw.AddLine(new Vector2(nowX, origin.Y), new Vector2(nowX, origin.Y + actualHeight), ImGui.GetColorU32(ConcurrentColor));
        draw.PopClipRect();
        if (hits.Count == 1)
        {
            ImGui.SetTooltip(UiText.Get(T.ScheduleWindowTooltip, entry.Fish.DisplayName, Range(hits[0]), FishRowRenderer.FormatWindow(hits[0], now)));
            if (clicked) schedule.Select(entry.Fish.FishParameterId, hits[0].Start!.Value);
        }
        else if (hits.Count > 1)
        {
            if (clicked)
            {
                candidatePopup = "ScheduleCandidates"; candidateFish = entry.Fish.FishParameterId;
                candidates = hits.ToArray(); ImGui.OpenPopup(candidatePopup);
            }
        }
        if (candidatePopup is not null && candidateFish == entry.Fish.FishParameterId && ImGui.BeginPopup(candidatePopup))
        {
            // List the row's real intervals; never let widened hit areas silently choose one.
            foreach (var window in candidates)
                if (ImGui.Selectable($"{Range(window)}###Candidate{window.Start?.UtcTicks}"))
                { schedule.Select(entry.Fish.FishParameterId, window.Start!.Value); candidatePopup = null; }
            ImGui.EndPopup();
        }
    }

    private void DrawList(SpawnScheduleView view, ScheduleSelection? selected, DateTimeOffset now, float height)
    {
        ImGui.TextDisabled(UiText.Get(T.ScheduleTimeList));
        if (ImGui.BeginChild("ScheduleTimeList", new Vector2(0, height), true))
        {
            var clipper = ImGui.ImGuiListClipper();
            var rowHeight = ImGui.GetTextLineHeightWithSpacing() * 4;
            try
            {
                clipper.Begin(view.Slots.Length, rowHeight);
                while (clipper.Step())
                    for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; index++)
                {
                    var (entry, window) = view.Slots[index];
                    ImGui.PushID(index);
                    var y = ImGui.GetCursorPosY();
                    ImGui.TextUnformatted(entry.Fish.DisplayName);
                    if (entry.Collection == CollectionStatus.Unknown)
                    { ImGui.SameLine(); ImGui.TextDisabled(UiText.Get(T.CollectionUnknown)); }
                    var active = selected?.Entry.Fish.FishParameterId == entry.Fish.FishParameterId && selected.Window.Start == window.Start;
                    if (ImGui.Selectable($"{UiText.Get(T.ScheduleListWindow, Range(window), new UiMessage(window.Start <= now ? T.WindowOpen : T.WindowUpcoming))}###Window{window.Start?.UtcTicks}", active))
                        schedule.Select(entry.Fish.FishParameterId, window.Start!.Value);
                    if (entry.DurationAssumed) ImGui.TextUnformatted(UiText.Get(T.ScheduleIntuitionAssumed));
                    else if (entry.Collection == CollectionStatus.Unknown) ImGui.TextDisabled(UiText.Get(T.CollectionUnknown));
                    ImGui.SetCursorPosY(y + rowHeight);
                    ImGui.PopID();
                }
            }
            finally { clipper.Destroy(); }
        }
        ImGui.EndChild();
    }

    private void DrawSelection(ScheduleSelection? selected, DateTimeOffset now)
    {
        ImGui.Separator();
        if (selected is null) { ImGui.TextWrapped(UiText.Get(T.ScheduleChoose)); return; }
        ImGui.TextWrapped(UiText.Get(T.ScheduleSelected, selected.Entry.Fish.DisplayName, Range(selected.Window)));
        if (ImGui.SmallButton(UiText.Label(T.ScheduleOpenDetail, "ScheduleOpenDetail"))) showDetail(selected.Entry.Fish);
        ImGui.TextWrapped(UiText.Get(selected.Ended ? T.ScheduleEnded : T.ScheduleRemaining,
            FishRowRenderer.FormatWindow(selected.Window with { Kind = selected.Window.Start <= now ? FishingWindowKind.Open : FishingWindowKind.Upcoming }, now)));
        if (selected.Entry.DurationAssumed)
            ImGui.TextWrapped(UiText.Get(T.ForecastDurationAssumed, FishingForecast.DefaultIntuitionSeconds));
        if (selected.Window.Start is { } start)
            ImGui.TextDisabled(UiText.Get(T.ScheduleEt, Et(start), selected.Window.End is { } end ? Et(end) : UiText.Get(T.ConditionUnknown)));
        if (selected.Coincidences.Count == 0) ImGui.TextWrapped(UiText.Get(T.ScheduleNoConcurrent));
        else
        {
            ImGui.TextUnformatted(UiText.Get(T.ScheduleConcurrent, selected.Coincidences.Count));
            if (ImGui.BeginChild("ScheduleConcurrentList", new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 2.5f)))
            {
                var clipper = ImGui.ImGuiListClipper();
                var rowHeight = ImGui.GetTextLineHeightWithSpacing() * 4;
                try
                {
                    clipper.Begin(selected.Lines.Length, rowHeight);
                    while (clipper.Step())
                        for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; index++)
                        {
                            var range = selected.Lines[index];
                            var y = ImGui.GetCursorPosY();
                            var text = UiText.Get(T.ScheduleConcurrentRow, range.Name, range.Start.ToLocalTime(), range.End.ToLocalTime(),
                                FishRowRenderer.Countdown(range.End - range.Start, false));
                            ImGui.TextWrapped(text);
                            UiWidgets.Tooltip(text);
                            ImGui.SetCursorPosY(y + rowHeight);
                        }
                }
                finally { clipper.Destroy(); }
            }
            ImGui.EndChild();
        }
    }

    private void DrawOther(SpawnScheduleView view, SpawnScheduleSettings settings)
    {
        ImGui.Separator();
        ImGui.TextUnformatted(UiText.Get(T.ScheduleOtherHeading, view.Other.Length));
        if (view.Other.Length == 0) { ImGui.TextWrapped(UiText.Get(T.ScheduleNoOther)); return; }
        if (!ImGui.BeginTable("ScheduleOther", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH |
                ImGuiTableFlags.ScrollY, new Vector2(0, ImGui.GetFontSize() * 10))) return;
        ImGui.TableSetupColumn(UiText.Get(T.ScheduleFishColumn), ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn(UiText.Get(T.ScheduleReasonColumn), ImGuiTableColumnFlags.WidthStretch, 1.8f);
        ImGui.TableSetupColumn("###ScheduleOtherAction", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 3.5f);
        ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
        var clipper = ImGui.ImGuiListClipper();
        var rowHeight = ImGui.GetFontSize() * 5;
        try
        {
            clipper.Begin(view.Other.Length, rowHeight);
            while (clipper.Step())
                for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; index++)
                {
                    var entry = view.Other[index];
                    ImGui.PushID((int)entry.Fish.FishParameterId);
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight); ImGui.TableNextColumn();
                    DrawName(entry, rowHeight);
                    ImGui.TableNextColumn();
                    var reason = Reason(entry, settings.Hours);
                    if (ImGui.BeginChild("ScheduleReason", new Vector2(0, rowHeight - ImGui.GetStyle().CellPadding.Y * 2), false,
                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                    {
                        ImGui.TextWrapped(reason);
                        UiWidgets.Tooltip(reason);
                        if (entry.State == ScheduleRowState.Outside && entry.Next?.Start is { } next && next >= view.Through)
                            ImGui.TextWrapped(UiText.Get(T.ScheduleNext, next.ToLocalTime()));
                    }
                    ImGui.EndChild();
                    ImGui.TableNextColumn();
                    if (ImGui.SmallButton(UiText.Label(T.ScheduleDetail, "ScheduleOtherDetail"))) showDetail(entry.Fish);
                    ImGui.PopID();
                }
        }
        finally { clipper.Destroy(); }
        ImGui.EndTable();
    }

    private static string Reason(ScheduleEntry entry, int hours) => entry.State switch
    {
        ScheduleRowState.Outside => entry.Next?.Kind == FishingWindowKind.NotFound
            ? UiText.Get(T.ForecastNotFound, FishingAvailability.SearchDays) : UiText.Get(T.ScheduleOutside, hours),
        ScheduleRowState.Always => UiText.Get(T.ScheduleAlways),
        ScheduleRowState.Unsupported => UiText.Get(entry.Fish.IsOceanFishing ? T.ScheduleOceanUnsupported : T.ScheduleUnsupported),
        ScheduleRowState.Incomplete => UiText.Get(T.ScheduleIncomplete, SpawnSchedule.MaxWindowsPerFish),
        _ => entry.Issue == FishingForecastIssue.None ? UiText.Get(T.ForecastUnknown)
            : FishingWindowPanel.IntuitionNotice(new(FishingWindowKind.Unknown) { Issue = entry.Issue }),
    };

    private int TickMinutes() => (axisEnd - axisStart).TotalHours <= 1.5 ? 15 : (axisEnd - axisStart).TotalHours <= 6.5 ? 60 : (axisEnd - axisStart).TotalHours <= 12.5 ? 120 : 240;
    private float X(DateTimeOffset value, float width) => (float)((value - axisStart).TotalSeconds / Math.Max(1, (axisEnd - axisStart).TotalSeconds)) * width;
    private static string Et(DateTimeOffset value)
    {
        var minutes = (long)(value.ToUnixTimeMilliseconds() / 1000.0 / 175 * 60) % 1440;
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }
    private static string Range(FishingWindow window) => window.Start is { } start && window.End is { } end
        ? UiText.Get(T.ScheduleWindowRange, start.ToLocalTime(), end.ToLocalTime())
        : window.Start is { } begin ? UiText.Get(T.ScheduleWindowUnknownEnd, begin.ToLocalTime()) : UiText.Get(T.ForecastUnknown);
}
