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
    private bool showCalendarList = true;
    private float calendarShare = 0.58f;
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
        if (calendarMode) DrawMonthNavigation();
        else
        {
            DrawPeriods(personal, ref settings);
            var headerView = schedule.View;
            if (headerView.Session == personal.Session && headerView.State == ScheduleState.Ready && headerView.Calendar is null)
                DrawTimelineHeader(headerView);
        }
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
        var initial = settings;
        var width = ImGui.GetContentRegionAvail().X;
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var sourceWidth = ImGui.GetFontSize() * 172 / 14;
        var minimumSearch = ImGui.GetFontSize() * 140 / 14;
        var hideWidth = HideCaughtWidth();
        var optionsWidth = OptionsWidth();
        var eyesWidth = ImGui.GetFrameHeight();
        var filterCount = optionsWidth > 0 ? 3 : 2;
        var filterWidth = hideWidth + eyesWidth + optionsWidth;
        var tabsWidth = ViewTabsWidth();
        if (width >= tabsWidth + sourceWidth + minimumSearch + filterWidth + gap * (filterCount + 2))
            DrawToolbarRow(personal, ref settings, tabsWidth, sourceWidth, true, true);
        else if (width >= sourceWidth + minimumSearch + gap && width >= Math.Min(tabsWidth, ImGui.GetFontSize() * 12) + filterWidth + gap * filterCount)
        {
            DrawToolbarRow(personal, ref settings, width - filterWidth - gap * filterCount, 0, false, true);
            DrawToolbarRow(personal, ref settings, 0, sourceWidth, true, false);
        }
        else
        {
            DrawToolbarRow(personal, ref settings, width, 0, false, false);
            DrawToolbarRow(personal, ref settings, 0, Math.Min(sourceWidth, width * 0.43f), true, false);
            DrawToolbarRow(personal, ref settings, 0, 0, false, true);
        }
        if (settings != initial) preferences.SetSchedule(settings);
        ImGui.Separator();
    }

    private static float HideCaughtWidth() => ImGui.CalcTextSize(UiText.Get(T.CollectionHideCaught)).X +
        ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X;
    private float OptionsWidth() => calendarMode ? 0 : ImGui.CalcTextSize(UiText.Get(T.ScheduleOptions)).X +
        ImGui.GetStyle().FramePadding.X * 2;
    private static float ViewTabsWidth()
    {
        var title = Math.Max(ImGui.CalcTextSize(UiText.Get(T.ScheduleTimelineMode)).X,
            ImGui.CalcTextSize(UiText.Get(T.ScheduleCalendarMode)).X);
        return (title + ImGui.GetFontSize() + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetStyle().FramePadding.X * 2) * 2;
    }

    // Position a complete row explicitly: source/search cannot consume the space reserved for filters.
    // Groups keep normal button navigation and stable IDs when the row arrangement changes.
    private void DrawToolbarRow(FishPreferencesView personal, ref SpawnScheduleSettings settings,
        float tabsWidth, float sourceWidth, bool query, bool filters)
    {
        var origin = ImGui.GetCursorPos();
        var available = ImGui.GetContentRegionAvail().X;
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var height = ImGui.GetFrameHeight();
        var hideWidth = HideCaughtWidth();
        var optionsWidth = OptionsWidth();
        var eyesWidth = ImGui.GetFrameHeight();
        var filterCount = optionsWidth > 0 ? 3 : 2;
        var count = (tabsWidth > 0 ? 1 : 0) + (query ? 2 : 0) + (filters ? filterCount : 0);
        var searchWidth = Math.Max(1, available - tabsWidth - (query ? sourceWidth : 0) -
            (filters ? hideWidth + eyesWidth + optionsWidth : 0) - gap * (count - 1));
        var x = origin.X;
        ImGui.BeginGroup();
        if (tabsWidth > 0)
        {
            ImGui.SetCursorPos(new(x, origin.Y));
            DrawViewTabs(tabsWidth, height); x += tabsWidth + gap;
        }
        var y = origin.Y + (height - ImGui.GetFrameHeight()) / 2;
        if (query)
        {
            ImGui.SetCursorPos(new(x, y)); ImGui.SetNextItemWidth(sourceWidth);
            string[] sources = [UiText.Get(T.ScheduleFavorites), UiText.Get(T.ScheduleCatalog)];
            var source = (int)settings.Source;
            ImGui.BeginDisabled(!personal.CanEdit);
            if (ImGui.Combo("###ScheduleSource", ref source, sources, sources.Length))
                settings = settings with { Source = (SpawnScheduleSource)source };
            ImGui.EndDisabled();
            UiWidgets.Tooltip(UiText.Get(T.ScheduleSourceHint));
            x += sourceWidth + gap;
            ImGui.SetCursorPos(new(x, y)); ImGui.SetNextItemWidth(searchWidth);
            if (ImGui.InputTextWithHint("###ScheduleSearch", UiText.Get(T.SearchPlaceholder), ref search, 100))
                schedule.SetSearch(search);
            x += searchWidth + gap;
        }
        if (filters)
        {
            ImGui.SetCursorPos(new(x, y));
            ImGui.BeginDisabled(!personal.CanEdit);
            var hide = settings.HideCaught;
            if (ImGui.Checkbox(UiText.Label(T.CollectionHideCaught, "ScheduleHideCaught"), ref hide))
                settings = settings with { HideCaught = hide };
            ImGui.EndDisabled();
            UiWidgets.Tooltip(UiText.Get(T.ScheduleHideHint));
            x += hideWidth + gap;
            PositionFilter(eyesWidth);
            var data = catalog.View;
            if (UiWidgets.GameIconButton(textures, data.FishEyesSkill?.IconId ?? 0, "ScheduleFishEyes",
                UiText.Get(T.ScheduleFishEyesHint), data.FishEyesEnabled))
                catalog.SetFishEyes(!data.FishEyesEnabled);
            x += eyesWidth + gap;
            if (optionsWidth > 0)
            {
                PositionFilter(optionsWidth);
                DrawOptions(optionsWidth);
            }

            void PositionFilter(float itemWidth)
            {
                if (tabsWidth == 0 && !query && x + itemWidth > origin.X + available)
                {
                    x = origin.X;
                    y = origin.Y + height + ImGui.GetStyle().ItemSpacing.Y;
                    height += ImGui.GetFrameHeightWithSpacing();
                }
                ImGui.SetCursorPos(new(x, y));
            }
        }
        ImGui.EndGroup();
        ImGui.SetCursorPos(new(origin.X, origin.Y + height + ImGui.GetStyle().ItemSpacing.Y));
    }

    private void DrawOptions(float width)
    {
        if (ImGui.Button(UiText.Label(T.ScheduleOptions, "ScheduleOptions"), new Vector2(width, ImGui.GetFrameHeight())))
            ImGui.OpenPopup("ScheduleOptionsPopup");
        if (!ImGui.BeginPopup("ScheduleOptionsPopup")) return;
        ImGui.Checkbox(UiText.Label(T.ScheduleEmphasize, "ScheduleEmphasize"), ref emphasize);
        UiWidgets.Tooltip(UiText.Get(T.ScheduleConcurrentHint));
        ImGui.EndPopup();
    }

    private void DrawPeriods(FishPreferencesView personal, ref SpawnScheduleSettings settings)
    {
        var right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        ImGui.BeginDisabled(!personal.CanEdit);
        var first = true;
        foreach (var hours in Hours)
        {
            var label = UiText.Get(T.ScheduleHours, hours);
            if (!first) UiWidgets.FlowNext(ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2, right);
            first = false;
            if (Toggle(label, $"ScheduleHours{hours}", settings.Hours == hours))
            { settings = settings with { Hours = hours }; preferences.SetSchedule(settings); }
        }
        ImGui.EndDisabled();
    }

    private void DrawViewTabs(float width, float height)
    {
        var origin = ImGui.GetCursorScreenPos();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
        ImGui.BeginGroup();
        if (DrawViewTab(T.ScheduleTimelineMode, T.ScheduleTimelineHint, FontAwesomeIcon.ChartBar, "ScheduleTimelineMode", !calendarMode, width / 2, height))
        { calendarMode = false; schedule.ClearSelection(); }
        ImGui.SameLine(0, 0);
        if (DrawViewTab(T.ScheduleCalendarMode, T.ScheduleMonthHint, FontAwesomeIcon.CalendarAlt, "ScheduleCalendarMode", calendarMode, width / 2, height))
        { calendarMode = true; schedule.ClearSelection(); }
        ImGui.EndGroup();
        ImGui.PopStyleVar();
        var draw = ImGui.GetWindowDrawList();
        var border = ImGui.GetColorU32(new Vector4(0.54f, 0.67f, 0.82f, 1));
        draw.AddRect(origin, origin + new Vector2(width, height), border, 3);
        draw.AddLine(origin + new Vector2(width / 2, 0), origin + new Vector2(width / 2, height), border);
    }

    private static bool DrawViewTab(T title, T hint, FontAwesomeIcon icon, string id, bool active, float width, float height)
    {
        var origin = ImGui.GetCursorScreenPos();
        var fontSize = ImGui.GetFontSize();
        ImGui.PushStyleColor(ImGuiCol.Button, active ? new Vector4(0.20f, 0.40f, 0.56f, 1) : new Vector4(0.15f, 0.23f, 0.31f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.23f, 0.34f, 0.45f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.20f, 0.40f, 0.56f, 1));
        var clicked = ImGui.Button($"###{id}", new Vector2(width, height));
        ImGui.PopStyleColor(3);
        var draw = ImGui.GetWindowDrawList();
        var line = Math.Max(1, fontSize * 0.1f);
        if (active) draw.AddRectFilled(origin + new Vector2(0, height - line),
            origin + new Vector2(width, height), ImGui.GetColorU32(new Vector4(0.72f, 0.88f, 1, 1)));
        var padding = ImGui.GetStyle().FramePadding.X;
        var gap = ImGui.GetStyle().ItemInnerSpacing.X;
        var label = Fit(UiText.Get(title), width - padding * 2 - fontSize - gap);
        var textWidth = ImGui.CalcTextSize(label).X;
        var x = origin.X + Math.Max(padding, (width - fontSize - gap - textWidth) / 2);
        var textColor = ImGui.GetColorU32(active ? Vector4.One : new Vector4(0.86f, 0.91f, 0.96f, 1));
        draw.PushClipRect(origin, origin + new Vector2(width, height), true);
        // Use the current native font size and a single pass. Enlarging baked glyphs and overpainting
        // the title made the previous tabs heavy and could degrade glyph edges.
        ImGui.PushFont(UiBuilder.IconFont);
        draw.AddText(new(x, origin.Y + (height - ImGui.GetFontSize()) / 2), textColor, icon.ToIconString());
        ImGui.PopFont();
        draw.AddText(new(x + fontSize + gap, origin.Y + (height - fontSize) / 2), textColor, label);
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

    private void DrawTimelineHeader(SpawnScheduleView view)
    {
        var now = schedule.Now;
        var start = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() / 1800 * 1800);
        var summary = UiText.Get(T.ScheduleRangeSummary, start.ToLocalTime(), view.Through.ToLocalTime(), view.Timed.Length);
        var current = UiText.Get(T.ScheduleCurrentTime, now.ToLocalTime());
        var right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        var currentWidth = ImGui.CalcTextSize(current).X;
        UiWidgets.FlowNext(ImGui.CalcTextSize(summary).X + currentWidth + ImGui.GetStyle().ItemSpacing.X * 2, right);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(summary);
        UiWidgets.Tooltip(UiText.Get(T.ScheduleAxisHint, now.ToLocalTime()));
        UiWidgets.FlowNext(currentWidth, right);
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), right - currentWidth));
        ImGui.TextColored(ConcurrentColor, current);
        UiWidgets.Tooltip(UiText.Get(T.ScheduleAxisHint, now.ToLocalTime()));
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
        // Keep the current-time marker below tick text instead of striking through a nearby label.
        draw.AddLine(new(nowX, origin.Y + ImGui.GetTextLineHeight() - 4), new(nowX, origin.Y + ImGui.GetTextLineHeight()), ImGui.GetColorU32(ConcurrentColor), 2);
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
        var spot = entry.Fish.Details?.Spot ?? entry.Fish.Spots.FirstOrDefault();
        var zone = spot?.Zone ?? entry.Fish.Ocean?.Area ?? "";
        var location = spot is { Name.Length: > 0 }
            ? zone.Length > 0 ? UiText.Get(T.ScheduleLocation, spot.Name, zone) : spot.Name : zone;
        draw.AddText(textPos + new Vector2(0, ImGui.GetFontSize() * 1.03f), ImGui.GetColorU32(ImGuiCol.TextDisabled), Fit(location, width - iconSize - 6));
        draw.PopClipRect();
        ImGui.InvisibleButton("ScheduleFish", new Vector2(width, actualHeight));
        UiWidgets.Tooltip(UiText.Get(T.ScheduleNameTooltip, entry.Fish.DisplayName, location));
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
        var right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        ImGui.BeginGroup();
        ImGui.BeginDisabled(month.Year <= 1);
        if (ImGui.Button("<###CalendarPrevious")) ChangeMonth(month.AddMonths(-1));
        ImGui.EndDisabled(); ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(month.ToDateTime(TimeOnly.MinValue).ToString("Y", UiText.Culture));
        UiWidgets.Tooltip(UiText.Get(T.ScheduleCalendarHint));
        ImGui.SameLine(); ImGui.BeginDisabled(month.Year >= 9998);
        if (ImGui.Button(">###CalendarNext")) ChangeMonth(month.AddMonths(1));
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.Button(UiText.Label(T.ScheduleToday, "CalendarToday")))
        {
            selectedDate = DateOnly.FromDateTime(schedule.Now.LocalDateTime);
            month = new(selectedDate.Year, selectedDate.Month, 1); schedule.SelectDate(selectedDate);
        }
        ImGui.EndGroup();
        var toggleKey = showCalendarList ? T.ScheduleHideList : T.ScheduleShowList;
        UiWidgets.FlowNext(ImGui.CalcTextSize(UiText.Get(toggleKey)).X + ImGui.GetStyle().FramePadding.X * 2, right);
        if (ImGui.Button(UiText.Label(toggleKey, "CalendarListVisible"))) showCalendarList = !showCalendarList;
        UiWidgets.Tooltip(UiText.Get(T.ScheduleCalendarResizeHint));
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
        var available = Math.Max(1, ImGui.GetContentRegionAvail().Y);
        if (!showCalendarList)
        { DrawCalendarGridArea(calendar, available); return; }
        var handleHeight = Math.Max(6, ImGui.GetFontSize() * 0.6f);
        var usable = Math.Max(1, available - handleHeight - ImGui.GetStyle().ItemSpacing.Y * 2);
        var weeks = ((int)month.DayOfWeek + calendar.Days.Length + 6) / 7;
        var minimumGrid = Math.Min(usable * 0.6f, weeks * ImGui.GetFontSize() * 2 + ImGui.GetFrameHeight());
        var minimumList = Math.Min(usable * 0.3f, RowHeight * 2 + ImGui.GetFrameHeightWithSpacing() * 2);
        var gridHeight = Math.Clamp(usable * calendarShare, minimumGrid, Math.Max(minimumGrid, usable - minimumList));
        DrawCalendarGridArea(calendar, gridHeight);
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        ImGui.InvisibleButton("CalendarHeightDivider", new Vector2(width, handleHeight));
        if (ImGui.IsItemHovered() || ImGui.IsItemActive()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
        if (ImGui.IsItemActive())
            calendarShare = Math.Clamp((gridHeight + ImGui.GetIO().MouseDelta.Y) / usable, minimumGrid / usable, Math.Max(minimumGrid, usable - minimumList) / usable);
        UiWidgets.Tooltip(UiText.Get(T.ScheduleCalendarResizeHint));
        var draw = ImGui.GetWindowDrawList();
        var middle = origin.Y + handleHeight / 2;
        draw.AddLine(new(origin.X, middle), new(origin.X + width, middle), ImGui.GetColorU32(ImGuiCol.Separator));
        draw.AddRectFilled(new(origin.X + width * 0.45f, middle - 2), new(origin.X + width * 0.55f, middle + 2),
            ImGui.GetColorU32(ImGui.IsItemActive() || ImGui.IsItemHovered() ? ImGuiCol.CheckMark : ImGuiCol.TextDisabled), 2);
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
        DrawDayRows(day, Math.Max(1, ImGui.GetContentRegionAvail().Y));
    }

    private void DrawCalendarGridArea(ScheduleCalendarData calendar, float height)
    {
        var visible = ImGui.BeginChild("CalendarGridArea", new Vector2(0, height));
        if (visible) DrawMonthGrid(calendar, height - ImGui.GetStyle().WindowPadding.Y * 2);
        ImGui.EndChild();
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
