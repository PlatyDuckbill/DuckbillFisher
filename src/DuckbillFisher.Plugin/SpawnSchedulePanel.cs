using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Compact scheduling only: cached intervals, explicit map/detail clicks, and civil-month navigation.</summary>
internal sealed class SpawnSchedulePanel(SpawnSchedule schedule, FishCatalog catalog, FishPreferences preferences,
    ITextureProvider textures, Action<FishGuideRow> showDetail, FishingMapService maps)
{
    private static readonly int[] Hours = [1, 3, 6, 12, 24];
    private static readonly Vector4 OpenColor = new(0.30f, 0.71f, 0.56f, 1);
    private static readonly Vector4 UpcomingColor = new(0.34f, 0.60f, 0.84f, 1);
    private static readonly Vector4 ConcurrentColor = new(0.94f, 0.70f, 0.32f, 1);
    private string search = "";
    private int session = -1;
    private bool emphasize = true, calendarMode, otherOpen;
    private DateOnly month, selectedDate;
    private DateTimeOffset axisStart, axisEnd;
    private (uint FishId, DateTimeOffset Start)? hoveredWindow;
    private float RowHeight => Math.Max(ImGui.GetFontSize() * 2.15f, ImGui.GetFrameHeight()) + ImGui.GetStyle().CellPadding.Y * 2;

    public void SetActive(bool value) => schedule.SetActive(value);

    public void Draw()
    {
        schedule.SetActive(true);
        var personal = preferences.View;
        if (session != personal.Session)
        {
            session = personal.Session; search = ""; calendarMode = false; otherOpen = false;
            selectedDate = DateOnly.FromDateTime(schedule.Now.LocalDateTime);
            month = new(selectedDate.Year, selectedDate.Month, 1);
            schedule.SetSearch(""); schedule.SelectDate(selectedDate);
        }
        var background = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg]; background.W = 0.97f;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, background);
        var visible = ImGui.BeginChild("ScheduleContent", Vector2.Zero, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleColor();
        if (!visible)
        { ImGui.EndChild(); return; }
        var settings = personal.Schedule;
        DrawControls(personal, ref settings);
        schedule.SetCalendarMonth(calendarMode ? month : null);
        if (personal.Error is { } error) ImGui.TextWrapped(error.Display);
        if (catalog.View.Status.Key != T.CatalogLoaded) ImGui.TextWrapped(catalog.View.Status.Display);
        var view = schedule.View;
        if (view.Session != personal.Session || view.State != ScheduleState.Ready ||
            (calendarMode ? view.Calendar?.Month != month : view.Calendar is not null))
        {
            ImGui.TextWrapped(UiText.Get(view.State == ScheduleState.Failed ? T.ScheduleFailed : T.ScheduleComputing));
            ImGui.EndChild(); return;
        }
        if (calendarMode) DrawCalendar(view, settings);
        else DrawTimelinePage(view, settings);
        ImGui.EndChild();
    }

    private void DrawControls(FishPreferencesView personal, ref SpawnScheduleSettings settings)
    {
        DrawViewTabs();
        var right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        string[] sources = [UiText.Get(T.ScheduleFavorites), UiText.Get(T.ScheduleCatalog)];
        var source = (int)settings.Source;
        var changed = false;
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 11);
        ImGui.BeginDisabled(!personal.CanEdit);
        if (ImGui.Combo("###ScheduleSource", ref source, sources, sources.Length))
        { settings = settings with { Source = (SpawnScheduleSource)source }; changed = true; }
        ImGui.EndDisabled();
        UiWidgets.Tooltip(UiText.Get(T.ScheduleSourceHint));
        UiWidgets.FlowNext(ImGui.GetFontSize() * 10, right);
        ImGui.SetNextItemWidth(Math.Max(ImGui.GetFontSize() * 9, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputTextWithHint("###ScheduleSearch", UiText.Get(T.SearchPlaceholder), ref search, 100)) schedule.SetSearch(search);
        ImGui.BeginDisabled(!personal.CanEdit);
        if (!calendarMode)
        {
            var first = true;
            foreach (var hours in Hours)
            {
                var label = UiText.Get(T.ScheduleHours, hours);
                if (!first) UiWidgets.FlowNext(ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2, right);
                first = false;
                if (Toggle(label, $"ScheduleHours{hours}", settings.Hours == hours))
                { settings = settings with { Hours = hours }; changed = true; }
            }
            UiWidgets.FlowNext(ImGui.GetFontSize() * 11, right);
        }
        var hide = settings.HideCaught;
        if (ImGui.Checkbox(UiText.Label(T.CollectionHideCaught, "ScheduleHideCaught"), ref hide))
        { settings = settings with { HideCaught = hide }; changed = true; }
        ImGui.EndDisabled();
        UiWidgets.Tooltip(UiText.Get(T.ScheduleHideHint));
        if (!calendarMode)
        {
            UiWidgets.FlowNext(ImGui.CalcTextSize(UiText.Get(T.ScheduleEmphasize)).X + ImGui.GetFrameHeight() * 1.5f, right);
            ImGui.Checkbox(UiText.Label(T.ScheduleEmphasize, "ScheduleEmphasize"), ref emphasize);
            UiWidgets.Tooltip(UiText.Get(T.ScheduleConcurrentHint));
        }
        UiWidgets.FlowNext(ImGui.GetFrameHeight(), right);
        var data = catalog.View;
        if (UiWidgets.GameIconButton(textures, data.FishEyesSkill?.IconId ?? 0, "ScheduleFishEyes", UiText.Get(T.ScheduleFishEyesHint), data.FishEyesEnabled))
            catalog.SetFishEyes(!data.FishEyesEnabled);
        if (changed) preferences.SetSchedule(settings);
        ImGui.Separator();
        if (calendarMode) DrawMonthNavigation();
    }

    private void DrawViewTabs()
    {
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var width = Math.Max(1, (ImGui.GetContentRegionAvail().X - gap) / 2);
        var height = ImGui.GetTextLineHeightWithSpacing() * 2 + ImGui.GetStyle().FramePadding.Y * 2 + 3;
        if (DrawViewTab(T.ScheduleTimelineMode, T.ScheduleTimelineHint, FontAwesomeIcon.ChartBar, "ScheduleTimelineMode", !calendarMode, width, height))
        { calendarMode = false; schedule.ClearSelection(); }
        ImGui.SameLine(0, gap);
        if (DrawViewTab(T.ScheduleCalendarMode, T.ScheduleMonthHint, FontAwesomeIcon.CalendarAlt, "ScheduleCalendarMode", calendarMode, width, height))
        { calendarMode = true; schedule.ClearSelection(); }
        ImGui.Spacing();
    }

    private static bool DrawViewTab(T title, T hint, FontAwesomeIcon icon, string id, bool active, float width, float height)
    {
        var origin = ImGui.GetCursorScreenPos();
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)(active ? ImGuiCol.Header : ImGuiCol.FrameBg)]);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderHovered]);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
        var clicked = ImGui.Button($"###{id}", new Vector2(width, height));
        ImGui.PopStyleColor(3);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRect(origin, origin + new Vector2(width, height), ImGui.GetColorU32(active ? ImGuiCol.CheckMark : ImGuiCol.Border), 3);
        if (active) draw.AddRectFilled(origin + new Vector2(0, height - 3), origin + new Vector2(width, height), ImGui.GetColorU32(ImGuiCol.CheckMark));
        draw.PushClipRect(origin, origin + new Vector2(width, height - 3), true);
        var padding = ImGui.GetStyle().FramePadding + new Vector2(5, 0);
        ImGui.PushFont(UiBuilder.IconFont);
        var iconText = icon.ToIconString();
        var iconWidth = ImGui.CalcTextSize(iconText).X;
        draw.AddText(origin + padding, ImGui.GetColorU32(ImGuiCol.Text), iconText);
        ImGui.PopFont();
        var titlePos = origin + padding + new Vector2(iconWidth + 7, 0);
        var label = Fit(UiText.Get(title), width - (titlePos.X - origin.X) - padding.X);
        draw.AddText(titlePos, ImGui.GetColorU32(ImGuiCol.Text), label);
        if (active) draw.AddText(titlePos + new Vector2(0.5f, 0), ImGui.GetColorU32(ImGuiCol.Text), label);
        draw.AddText(origin + padding + new Vector2(0, ImGui.GetTextLineHeightWithSpacing()), ImGui.GetColorU32(ImGuiCol.TextDisabled),
            Fit(UiText.Get(hint), width - padding.X * 2));
        draw.PopClipRect();
        UiWidgets.Tooltip(UiText.Get(hint));
        return clicked;
    }

    private static bool Toggle(T text, string id, bool active) => Toggle(UiText.Get(text), id, active);
    private static bool Toggle(string text, string id, bool active)
    {
        if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
        var clicked = ImGui.Button($"{text}###{id}");
        if (active) ImGui.PopStyleColor();
        return clicked;
    }

    private void DrawTimelinePage(SpawnScheduleView view, SpawnScheduleSettings settings)
    {
        var now = schedule.Now;
        axisStart = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() / 1800 * 1800);
        axisEnd = view.Through;
        ImGui.TextDisabled(UiText.Get(T.ScheduleCompactRange, axisStart.ToLocalTime(), axisEnd.ToLocalTime(), view.Timed.Length));
        UiWidgets.Tooltip(UiText.Get(T.ScheduleCounts, view.SourceCount, view.TargetCount, view.Timed.Length));
        var selected = schedule.Selection;
        hoveredWindow = null;
        var reserve = ImGui.GetFrameHeightWithSpacing();
        if (otherOpen) reserve += Math.Min(ImGui.GetFontSize() * 9, ImGui.GetContentRegionAvail().Y * 0.30f);
        var graphHeight = Math.Max(ImGui.GetFrameHeight() * 2, ImGui.GetContentRegionAvail().Y - reserve);
        if (view.Timed.Length > 0)
        {
            if (ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 37) DrawTimeList(view, graphHeight);
            else DrawTimeline(view, selected, now, graphHeight);
        }
        else
        {
            ImGui.TextWrapped(UiText.Get(view.TargetCount == 0 ? view.SourceCount == 0
                ? settings.Source == SpawnScheduleSource.Favorites ? T.ScheduleNoFavorites : T.ScheduleNoCatalog
                : T.ScheduleNoMatches : T.ScheduleNoTimed));
            if (search.Length > 0 && ImGui.SmallButton(UiText.Label(T.ScheduleClearSearch, "ClearScheduleSearch")))
            { search = ""; schedule.SetSearch(""); }
        }
        if (emphasize && hoveredWindow is { } hovered) schedule.Select(hovered.FishId, hovered.Start);
        else schedule.ClearSelection();
        DrawOther(view, settings);
    }

    private void DrawTimeline(SpawnScheduleView view, ScheduleSelection? selected, DateTimeOffset now, float height)
    {
        if (!ImGui.BeginTable("ScheduleTimeline", 3, ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerH |
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg, new Vector2(0, height))) return;
        ImGui.TableSetupColumn(UiText.Get(T.ScheduleFishColumn), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 14);
        ImGui.TableSetupColumn("###ScheduleTimeColumn", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("###ScheduleMapColumn", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() + 6);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, ImGui.GetTextLineHeightWithSpacing());
        ImGui.TableNextColumn(); ImGui.TextUnformatted(UiText.Get(T.ScheduleFishColumn));
        ImGui.TableNextColumn(); DrawAxis(now);
        ImGui.TableNextColumn();
        var rowHeight = RowHeight;
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
                    ImGui.TableNextColumn(); DrawFish(entry, rowHeight);
                    ImGui.TableNextColumn(); DrawTrack(entry, selected, now, rowHeight);
                    ImGui.TableNextColumn(); DrawPin(entry.Fish);
                    ImGui.PopID();
                }
        }
        finally { clipper.Destroy(); }
        ImGui.EndTable();
    }

    private void DrawAxis(DateTimeOffset now)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var draw = ImGui.GetWindowDrawList();
        for (var tick = axisStart; tick <= axisEnd; tick = tick.AddMinutes(TickMinutes()))
        {
            var text = tick.ToLocalTime().ToString("HH:mm", UiText.Culture);
            var x = Math.Clamp(X(tick, width) - ImGui.CalcTextSize(text).X / 2, 0, Math.Max(0, width - ImGui.CalcTextSize(text).X));
            draw.AddText(origin + new Vector2(x, 0), ImGui.GetColorU32(ImGuiCol.TextDisabled), text);
        }
        var nowX = origin.X + Math.Clamp(X(now, width), 0, width);
        draw.AddLine(new(nowX, origin.Y), new(nowX, origin.Y + ImGui.GetTextLineHeight()), ImGui.GetColorU32(ConcurrentColor), 2);
        ImGui.Dummy(new Vector2(width, ImGui.GetTextLineHeight()));
    }

    private void DrawFish(ScheduleEntry entry, float height)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var actualHeight = Math.Max(1, height - ImGui.GetStyle().CellPadding.Y * 2);
        var iconSize = Math.Min(actualHeight, ImGui.GetFontSize() * 1.75f);
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + new Vector2(width, actualHeight), true);
        DrawIcon(entry.Fish, origin + new Vector2(0, (actualHeight - iconSize) / 2), iconSize);
        var textPos = origin + new Vector2(iconSize + 6, 0);
        draw.AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), Fit(entry.Fish.DisplayName, width - iconSize - 6));
        var zone = entry.Fish.Details?.Spot?.Zone ?? entry.Fish.Spots.FirstOrDefault()?.Zone ?? entry.Fish.Ocean?.Area ?? "";
        draw.AddText(textPos + new Vector2(0, ImGui.GetFontSize() * 1.03f), ImGui.GetColorU32(ImGuiCol.TextDisabled), Fit(zone, width - iconSize - 6));
        draw.PopClipRect();
        ImGui.InvisibleButton("ScheduleFish", new Vector2(width, actualHeight));
        UiWidgets.Tooltip(UiText.Get(T.ScheduleNameTooltip, entry.Fish.DisplayName, zone));
        if (ImGui.IsItemClicked()) showDetail(entry.Fish);
    }

    private void DrawIcon(FishGuideRow fish, Vector2 position, float size)
    {
        if (fish.IconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(fish.IconId), out var texture) && texture.TryGetWrap(out var wrap, out _))
            ImGui.GetWindowDrawList().AddImage(wrap.Handle, position, position + new Vector2(size));
        else ImGui.GetWindowDrawList().AddText(position, ImGui.GetColorU32(ImGuiCol.TextDisabled), "?");
    }

    private void DrawPin(FishGuideRow fish)
    {
        var spot = fish.Details?.Spot ?? fish.Spots.FirstOrDefault();
        ImGui.BeginDisabled(spot is null || !preferences.View.HasCharacter);
        if (UiWidgets.MapPinButton("ScheduleMap", spot is null ? UiText.Get(T.MapUnavailable) : UiText.Get(T.ScheduleMapTooltip, spot.Zone, spot.Name), ImGui.GetFrameHeight()) && spot is not null)
            maps.Request(spot);
        ImGui.EndDisabled();
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
        if (hits.Count > 0)
        {
            ImGui.SetTooltip(UiText.Get(T.ScheduleTrackTooltip, entry.Fish.DisplayName, string.Join(Environment.NewLine, hits.Select(Range))));
            // Widened hit areas may cover several real intervals. Show all times without choosing an arbitrary one.
            if (hits.Count == 1) hoveredWindow = (entry.Fish.FishParameterId, hits[0].Start!.Value);
            if (clicked) showDetail(entry.Fish);
        }
    }


    private void DrawTimeList(SpawnScheduleView view, float height)
    {
        if (!ImGui.BeginTable("ScheduleNarrowList", 3, ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg, new Vector2(0, height))) return;
        ImGui.TableSetupColumn("###Fish", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("###Time", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 12);
        ImGui.TableSetupColumn("###Map", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() + 6);
        var clipper = ImGui.ImGuiListClipper(); var rowHeight = RowHeight;
        try
        {
            clipper.Begin(view.Slots.Length, rowHeight);
            while (clipper.Step()) for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var (entry, window) = view.Slots[i];
                ImGui.PushID(i); ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                ImGui.TableNextColumn(); DrawFish(entry, rowHeight);
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{TimeRange(window)}###ScheduleInterval")) showDetail(entry.Fish);
                UiWidgets.Tooltip(UiText.Get(T.ScheduleTrackTooltip, entry.Fish.DisplayName, Range(window)));
                ImGui.TableNextColumn(); DrawPin(entry.Fish); ImGui.PopID();
            }
        }
        finally { clipper.Destroy(); }
        ImGui.EndTable();
    }

    private void DrawOther(SpawnScheduleView view, SpawnScheduleSettings settings)
    {
        otherOpen = ImGui.CollapsingHeader($"{UiText.Get(T.ScheduleOtherHeading, view.Other.Length)}###ScheduleHiddenFish");
        if (!otherOpen) return;
        if (view.Other.Length == 0) { ImGui.TextDisabled(UiText.Get(T.ScheduleNoOther)); return; }
        if (!ImGui.BeginTable("ScheduleOther", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(0, Math.Max(ImGui.GetFrameHeight() * 2, ImGui.GetContentRegionAvail().Y)))) return;
        ImGui.TableSetupColumn("###Fish", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("###Reason", ImGuiTableColumnFlags.WidthStretch, 1.5f);
        ImGui.TableSetupColumn("###Map", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() + 6);
        var clipper = ImGui.ImGuiListClipper(); var rowHeight = RowHeight;
        try
        {
            clipper.Begin(view.Other.Length, rowHeight);
            while (clipper.Step()) for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var entry = view.Other[i];
                ImGui.PushID(i); ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                ImGui.TableNextColumn(); DrawFish(entry, rowHeight);
                ImGui.TableNextColumn();
                var reason = Reason(entry, settings.Hours);
                ImGui.TextUnformatted(Fit(reason, ImGui.GetContentRegionAvail().X)); UiWidgets.Tooltip(reason);
                ImGui.TableNextColumn(); DrawPin(entry.Fish); ImGui.PopID();
            }
        }
        finally { clipper.Destroy(); }
        ImGui.EndTable();
    }

    private void DrawMonthNavigation()
    {
        ImGui.BeginDisabled(month.Year <= 1);
        if (ImGui.SmallButton("<###CalendarPrevious")) ChangeMonth(month.AddMonths(-1));
        ImGui.EndDisabled(); ImGui.SameLine();
        ImGui.TextUnformatted(month.ToDateTime(TimeOnly.MinValue).ToString("Y", UiText.Culture));
        UiWidgets.Tooltip(UiText.Get(T.ScheduleCalendarHint));
        ImGui.SameLine(); ImGui.BeginDisabled(month.Year >= 9998);
        if (ImGui.SmallButton(">###CalendarNext")) ChangeMonth(month.AddMonths(1));
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.SmallButton(UiText.Label(T.ScheduleToday, "CalendarToday")))
        {
            selectedDate = DateOnly.FromDateTime(schedule.Now.LocalDateTime);
            month = new(selectedDate.Year, selectedDate.Month, 1); schedule.SelectDate(selectedDate);
        }
    }

    private void ChangeMonth(DateOnly value)
    {
        month = value; selectedDate = value; schedule.SelectDate(value);
    }

    private void DrawCalendar(SpawnScheduleView view, SpawnScheduleSettings settings)
    {
        var calendar = view.Calendar!;
        if (calendar.Incomplete)
        { ImGui.TextColored(ConcurrentColor, UiText.Get(T.ScheduleCalendarPartial)); UiWidgets.Tooltip(UiText.Get(T.ScheduleCalendarLimit, SpawnScheduleCalendar.MaxOccurrences, SpawnScheduleCalendar.MaxFishWindows)); }
        var available = ImGui.GetContentRegionAvail().Y;
        var gridHeight = Math.Max(ImGui.GetFontSize() * 9, available * 0.58f);
        DrawMonthGrid(calendar, gridHeight);
        var day = schedule.CalendarDay;
        if (day is null || day.Date != selectedDate)
        { ImGui.TextDisabled(UiText.Get(T.ScheduleComputing)); return; }
        ImGui.TextUnformatted(UiText.Get(T.ScheduleCalendarDay, day.Date.ToDateTime(TimeOnly.MinValue), day.FishCount, day.Slots.Length));
        if (day.Slots.Length == 0)
        {
            var message = view.TargetCount == 0 ? view.SourceCount == 0
                ? settings.Source == SpawnScheduleSource.Favorites ? T.ScheduleNoFavorites : T.ScheduleNoCatalog
                : T.ScheduleNoMatches : calendar.Incomplete ? T.ScheduleCalendarPartial : T.ScheduleCalendarEmpty;
            ImGui.TextDisabled(UiText.Get(message)); return;
        }
        DrawDayRows(day, Math.Max(ImGui.GetFrameHeight() * 2, ImGui.GetContentRegionAvail().Y));
    }

    private void DrawMonthGrid(ScheduleCalendarData calendar, float height)
    {
        var offset = (int)month.DayOfWeek;
        var weeks = (offset + calendar.Days.Length + 6) / 7;
        var cellHeight = Math.Max(ImGui.GetFontSize() * 2, (height - ImGui.GetFrameHeight()) / weeks);
        var today = DateOnly.FromDateTime(schedule.Now.LocalDateTime);
        if (!ImGui.BeginTable("ScheduleMonthGrid", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchSame)) return;
        var weekdays = UiText.Culture.DateTimeFormat.AbbreviatedDayNames;
        for (var column = 0; column < 7; column++) ImGui.TableSetupColumn($"{weekdays[column]}###ScheduleWeekday{column}");
        ImGui.TableHeadersRow();
        for (var week = 0; week < weeks; week++)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None, cellHeight);
            for (var column = 0; column < 7; column++)
            {
                ImGui.TableNextColumn();
                var index = week * 7 + column - offset;
                if (index < 0 || index >= calendar.Days.Length) continue;
                var day = calendar.Days[index];
                ImGui.PushID(day.Date.DayNumber);
                var origin = ImGui.GetCursorScreenPos();
                var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
                var actualHeight = Math.Max(1, cellHeight - ImGui.GetStyle().CellPadding.Y * 2);
                if (ImGui.InvisibleButton("CalendarDate", new Vector2(width, actualHeight)))
                { selectedDate = day.Date; schedule.SelectDate(day.Date); }
                var hovered = ImGui.IsItemHovered();
                string? previewTooltip = null;
                var draw = ImGui.GetWindowDrawList();
                if (day.Date == selectedDate || ImGui.IsItemHovered())
                    draw.AddRectFilled(origin, origin + new Vector2(width, actualHeight), ImGui.GetColorU32(day.Date == selectedDate ? ImGuiCol.Header : ImGuiCol.HeaderHovered), 2);
                draw.PushClipRect(origin, origin + new Vector2(width, actualHeight), true);
                draw.AddText(origin + new Vector2(3, 0), ImGui.GetColorU32(day.Date == today ? ConcurrentColor : ImGui.GetStyle().Colors[(int)ImGuiCol.Text]), day.Date.Day.ToString(UiText.Culture));
                var small = Math.Max(12, ImGui.GetFontSize() * 0.85f);
                var lineHeight = small + 3;
                // Keep a first time/name line even in six-week months; the remaining count shares the date header.
                var lines = Math.Max(0, (int)((actualHeight - ImGui.GetTextLineHeightWithSpacing()) / lineHeight));
                var shown = Math.Min(lines, day.Fish.Length);
                for (var i = 0; i < shown; i++)
                {
                    var fish = day.Fish[i];
                    var first = fish.Windows[0].Start;
                    var clipped = first.LocalDateTime.Date < day.Date.ToDateTime(TimeOnly.MinValue).Date;
                    var label = UiText.Get(T.ScheduleCalendarEntry, clipped ? day.Date.ToDateTime(TimeOnly.MinValue) : first.LocalDateTime, fish.Entry.Fish.DisplayName);
                    draw.AddText(ImGui.GetFont(), small, origin + new Vector2(3, ImGui.GetTextLineHeightWithSpacing() + i * lineHeight),
                        ImGui.GetColorU32(ImGuiCol.Text), Fit(label, width - 6, small / ImGui.GetFontSize()));
                    var mouseY = ImGui.GetMousePos().Y - origin.Y - ImGui.GetTextLineHeightWithSpacing();
                    if (hovered && mouseY >= i * lineHeight && mouseY < (i + 1) * lineHeight)
                        previewTooltip = UiText.Get(T.ScheduleSimpleTooltip, fish.Entry.Fish.DisplayName, Range(fish.Windows[0].Window));
                }
                if (day.Fish.Length > shown)
                {
                    var more = UiText.Get(T.ScheduleCalendarMore, day.Fish.Length - shown);
                    var size = ImGui.CalcTextSize(more).X * small / ImGui.GetFontSize();
                    draw.AddText(ImGui.GetFont(), small, origin + new Vector2(Math.Max(ImGui.GetFontSize() * 1.5f, width - size - 3), 1),
                        ImGui.GetColorU32(ImGuiCol.TextDisabled), more);
                }
                draw.PopClipRect();
                UiWidgets.Tooltip(previewTooltip ?? UiText.Get(T.ScheduleCalendarDateHint, day.Date.ToDateTime(TimeOnly.MinValue), day.Fish.Length));
                ImGui.PopID();
            }
        }
        ImGui.EndTable();
    }

    private void DrawDayRows(CalendarDayView day, float height)
    {
        if (!ImGui.BeginTable("ScheduleCalendarDayList", 4, ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg, new Vector2(0, height))) return;
        ImGui.TableSetupColumn(UiText.Get(T.ScheduleCalendarTime), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 12);
        ImGui.TableSetupColumn(UiText.Get(T.ScheduleFishColumn), ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("###Map", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() + 6);
        ImGui.TableSetupColumn("###Detail", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize(UiText.Get(T.ScheduleDetail)).X + 12);
        ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
        var clipper = ImGui.ImGuiListClipper(); var rowHeight = RowHeight;
        var start = SpawnScheduleCalendar.Midnight(day.Date, TimeZoneInfo.Local);
        var end = SpawnScheduleCalendar.Midnight(day.Date.AddDays(1), TimeZoneInfo.Local);
        try
        {
            clipper.Begin(day.Slots.Length, rowHeight);
            while (clipper.Step()) for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var slot = day.Slots[i]; var window = slot.Occurrence.Window;
                var clipped = window with { Start = window.Start < start ? start : window.Start, End = window.End is { } finish && finish < end ? finish : end };
                ImGui.PushID(i); ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(TimeRange(clipped)); UiWidgets.Tooltip(Range(window));
                ImGui.TableNextColumn(); DrawFish(slot.Entry, rowHeight);
                ImGui.TableNextColumn(); DrawPin(slot.Entry.Fish);
                ImGui.TableNextColumn(); if (ImGui.SmallButton(UiText.Label(T.ScheduleDetail, "CalendarFishDetail"))) showDetail(slot.Entry.Fish);
                ImGui.PopID();
            }
        }
        finally { clipper.Destroy(); }
        ImGui.EndTable();
    }

    private static string Fit(string value, float width, float scale = 1)
    {
        if (ImGui.CalcTextSize(value).X * scale <= width) return value;
        var length = value.Length;
        while (length > 0 && ImGui.CalcTextSize(value[..length] + "…").X * scale > width) length--;
        if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
        return value[..length] + "…";
    }
    private static string Reason(ScheduleEntry entry, int hours) => entry.State switch
    {
        ScheduleRowState.Outside => UiText.Get(T.ScheduleOutside, hours),
        ScheduleRowState.Always => UiText.Get(T.ScheduleAlways),
        ScheduleRowState.Unsupported => UiText.Get(entry.Fish.IsOceanFishing ? T.ScheduleOceanUnsupported : T.ScheduleUnsupported),
        ScheduleRowState.Incomplete => UiText.Get(T.ScheduleIncomplete, SpawnSchedule.MaxWindowsPerFish),
        _ => entry.Issue == FishingForecastIssue.None ? UiText.Get(T.ForecastUnknown)
            : FishingWindowPanel.IntuitionNotice(new(FishingWindowKind.Unknown) { Issue = entry.Issue }),
    };
    private int TickMinutes() => (axisEnd - axisStart).TotalHours <= 1.5 ? 15 : (axisEnd - axisStart).TotalHours <= 6.5 ? 30 : (axisEnd - axisStart).TotalHours <= 12.5 ? 120 : 240;
    private float X(DateTimeOffset value, float width) => (float)((value - axisStart).TotalSeconds / Math.Max(1, (axisEnd - axisStart).TotalSeconds)) * width;
    private static string TimeRange(FishingWindow window) => window.Start is { } start && window.End is { } end
        ? UiText.Get(T.ScheduleClockRange, start.ToLocalTime(), end.ToLocalTime())
        : window.Start is { } begin ? UiText.Get(T.ScheduleClockUnknownEnd, begin.ToLocalTime()) : UiText.Get(T.ConditionUnknown);
    private static string Range(FishingWindow window) => window.Start is { } start && window.End is { } end
        ? UiText.Get(T.ScheduleWindowRange, start.ToLocalTime(), end.ToLocalTime())
        : window.Start is { } begin ? UiText.Get(T.ScheduleWindowUnknownEnd, begin.ToLocalTime()) : UiText.Get(T.ForecastUnknown);
}
