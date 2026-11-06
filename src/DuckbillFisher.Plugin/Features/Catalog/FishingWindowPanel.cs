using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Availability presentation only. UTC forecasts and personal readiness are not changed here.</summary>
internal static class FishingWindowPanel
{
    public static void Draw(FishingWindow? window, DateTimeOffset now, UiMessage? unavailableReason = null, bool intuitionRequired = false)
    {
        if (window?.Issue == FishingForecastIssue.FishEyesSupportUnknown)
            ImGui.TextWrapped(UiText.Get(T.ForecastFishEyesUnknown));
        if (intuitionRequired) ImGui.TextWrapped(IntuitionNotice(window));
        if (unavailableReason is { } reason && (window is null || window.Kind == FishingWindowKind.Unknown))
        {
            ImGui.TextDisabled(UiText.Get(T.ForecastUnavailable));
            UiWidgets.Tooltip(reason.Display);
            return;
        }
        var open = window?.Kind == FishingWindowKind.Open && (window.Start is null || window.Start <= now) &&
            (window.End is null || now < window.End);
        var text = open ? UiText.Get(T.WindowOpen) : window?.Kind switch
        {
            FishingWindowKind.Upcoming => UiText.Get(T.WindowUpcoming),
            FishingWindowKind.Always => UiText.Get(T.ForecastAlways),
            FishingWindowKind.NotFound => UiText.Get(T.ForecastNotFound, FishingAvailability.SearchDays),
            _ => UiText.Get(T.ForecastUnknown),
        };
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X - 24);
        var remaining = window is { Kind: FishingWindowKind.Open, End: not null } or { Kind: FishingWindowKind.Upcoming, Start: not null }
            ? FishRowRenderer.CompactWindow(window, now) : "";
        var range = Range(window, now);
        var line = remaining.Length > 0 && range.Length > 0 ? UiText.Get(T.WindowInline, remaining, range)
            : remaining.Length > 0 ? remaining : range;
        if (UiThemeScope.Current is not null)
        {
            var available = ImGui.GetContentRegionAvail().X;
            var stateWidth = ImGui.CalcTextSize(text).X;
            var timeWidth = ImGui.CalcTextSize(remaining).X;
            var inline = remaining.Length > 0 && stateWidth + timeWidth + ImGui.GetStyle().ItemSpacing.X * 2 <= available;
            if (open || window?.Kind == FishingWindowKind.Upcoming)
            {
                var statusHeight = ImGui.CalcTextSize(text, false, Math.Max(1, available)).Y;
                if (remaining.Length > 0 && !inline)
                    statusHeight += ImGui.GetStyle().ItemSpacing.Y + ImGui.CalcTextSize(remaining, false, Math.Max(1, available)).Y;
                DrawStateTint(ImGui.GetCursorScreenPos(), available, statusHeight, open ? UiThemeColors.Positive : UiThemeColors.Information);
            }
            // A state owns a whole line; never put it in a narrow, wrapping table column.
            ImGui.PushStyleColor(ImGuiCol.Text, open ? UiThemeColors.Positive
                : window?.Kind == FishingWindowKind.Upcoming ? UiThemeColors.Information : UiThemeColors.Secondary);
            if (stateWidth <= available) ImGui.TextUnformatted(text);
            else ImGui.TextWrapped(text);
            ImGui.PopStyleColor();
            if (remaining.Length > 0)
            {
                if (inline)
                    ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X * 2);
                ImGui.TextWrapped(remaining);
            }
            if (range.Length > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiThemeColors.Secondary);
                ImGui.TextWrapped(range);
                ImGui.PopStyleColor();
            }
            return;
        }
        ImGui.PushStyleColor(ImGuiCol.ChildBg, UiThemeScope.Current is not null
            ? UiThemeColors.Surface(open ? UiThemeColors.Positive : UiThemeColors.Information, .10f)
            : open ? new Vector4(0.10f, 0.40f, 0.25f, 0.32f) : new Vector4(0.18f, 0.23f, 0.29f, 0.40f));
        var height = ImGui.CalcTextSize(text, false, Math.Max(1, width - ImGui.GetFrameHeight())).Y + 16;
        if (line.Length > 0) height += ImGui.GetTextLineHeightWithSpacing();
        if (ImGui.BeginChild("Availability", new Vector2(0, height), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            UiWidgets.Icon(FontAwesomeIcon.Clock);
            ImGui.SameLine();
            ImGui.TextWrapped(text);
            if (line.Length > 0)
            {
                var screen = ImGui.GetCursorScreenPos();
                ImGui.PushClipRect(screen, screen + new Vector2(width, ImGui.GetTextLineHeight()), true);
                ImGui.TextUnformatted(line);
                ImGui.PopClipRect();
                if (window?.Start is { } start && window.End is { } end)
                    UiWidgets.Tooltip(UiText.Get(T.WindowInline, remaining,
                        UiText.Get(T.WindowLocalRange, start.ToLocalTime(), end.ToLocalTime())));
                else UiWidgets.Tooltip(line);
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private static void DrawStateTint(Vector2 start, float width, float height, Vector4 role)
    {
        // Paint behind the existing status lines; no child, padding or extra layout height.
        var draw = ImGui.GetWindowDrawList();
        var near = ImGui.GetColorU32(UiThemeColors.Surface(role, .16f));
        var far = ImGui.GetColorU32(UiThemeColors.Surface(role, .025f));
        var min = start - new Vector2(3, 1);
        var max = start + new Vector2(Math.Max(1, width), height + 1);
        draw.AddRectFilledMultiColor(min, max, near, far, far, near);
        role.W = .75f;
        draw.AddRectFilled(min, new Vector2(start.X - 1, max.Y), ImGui.GetColorU32(role));
    }

    public static string IntuitionNotice(FishingWindow? window) => UiText.Get(window?.Issue switch
    {
        FishingForecastIssue.IntuitionDurationUnknown => T.ForecastIntuitionDurationUnknown,
        FishingForecastIssue.PrerequisiteUnavailable or FishingForecastIssue.NestedPrerequisiteUnsupported => T.ForecastIntuitionPrerequisiteUnknown,
        FishingForecastIssue.FishEyesSupportUnknown => T.ForecastFishEyesUnknown,
        _ => T.ForecastIntuition,
    });

    public static bool Different(FishingWindow? normal, FishingWindow? fishEyes) =>
        normal?.Kind != fishEyes?.Kind || normal?.Start != fishEyes?.Start || normal?.End != fishEyes?.End || normal?.Issue != fishEyes?.Issue;

    public static void DrawBreakdown(FishingWindow? window, FishGuideRow fish, ITextureProvider textures, DateTimeOffset now, bool fishEyes)
    {
        if (window?.Breakdown is not { } breakdown || fish.Details is not { } details) return;
        if (!UiWidgets.Disclosure(UiText.Label(T.ForecastCalculation, "IntuitionCalculation"))) return;
        ImGui.TextDisabled(UiText.Get(fishEyes ? T.ForecastWithFishEyes : T.ForecastNormal));
        if (ImGui.BeginTable("IntuitionCalculationRows", 2,
            ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
        {
            ImGui.TableSetupColumn(UiText.Get(T.ForecastFishColumn), ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.TableSetupColumn(UiText.Get(T.ForecastTimeColumn), ImGuiTableColumnFlags.WidthStretch, 1.35f);
            ImGui.TableHeadersRow();
            FishRow(fish.IconId, fish.DisplayName, T.ForecastTargetRole, breakdown.Target);
            foreach (var prerequisite in breakdown.Prerequisites)
            {
                var item = details.Predators.FirstOrDefault(item => item.Id == prerequisite.ItemId);
                FishRow(item?.IconId ?? 0, UiText.Get(T.PredatorLabel, item?.Name ?? prerequisite.ItemId.ToString(), prerequisite.Count),
                    T.ForecastPrerequisiteRole, prerequisite.Window);
            }
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var accent = window.Kind == FishingWindowKind.Open ? new Vector4(0.55f, 0.88f, 0.68f, 1)
                : new Vector4(0.62f, 0.82f, 1, 1);
            if (UiThemeScope.Current is not null) accent = window.Kind == FishingWindowKind.Open ? UiThemeColors.Positive : UiThemeColors.Information;
            ImGui.PushStyleColor(ImGuiCol.Text, UiWidgets.ReadableColor(accent));
            UiWidgets.Icon(FontAwesomeIcon.Clock);
            ImGui.SameLine();
            ImGui.TextWrapped(UiText.Get(T.ForecastFinalRole));
            ImGui.TableNextColumn();
            ImGui.TextWrapped(FishRowRenderer.CompactWindow(window, now));
            ImGui.PopStyleColor();
            DrawRange(window, now);
            ImGui.EndTable();
        }
        UiWidgets.Icon(FontAwesomeIcon.HourglassHalf);
        ImGui.SameLine();
        ImGui.TextWrapped(UiText.Get(T.ForecastDurationUsed, breakdown.IntuitionSeconds));
        ImGui.PushStyleColor(ImGuiCol.Text, UiThemeScope.Current is not null ? UiThemeColors.Secondary
            : UiWidgets.ReadableColor(new Vector4(0.65f, 0.69f, 0.74f, 1)));
        ImGui.TextWrapped(UiText.Get(window.Start is null && window.Kind != FishingWindowKind.Always
            ? T.ForecastCalculationPendingHint : T.ForecastCalculationHint));
        ImGui.PopStyleColor();

        void FishRow(uint icon, string name, T role, FishingWindow availability)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.BeginGroup();
            UiWidgets.GameIcon(textures, icon, ImGui.GetTextLineHeightWithSpacing() * 1.65f, name);
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.PushStyleColor(ImGuiCol.Text, UiThemeScope.Current is not null ? UiThemeColors.Secondary
                : UiWidgets.ReadableColor(new Vector4(0.65f, 0.69f, 0.74f, 1)));
            ImGui.TextWrapped(UiText.Get(role));
            ImGui.PopStyleColor();
            ImGui.TextWrapped(name);
            ImGui.EndGroup();
            ImGui.EndGroup();
            ImGui.TableNextColumn();
            ImGui.TextWrapped(Describe(availability, now));
        }
    }

    private static void DrawRange(FishingWindow window, DateTimeOffset now)
    {
        var range = Range(window, now);
        if (range.Length > 0) ImGui.TextWrapped(range);
        if (window.Issue == FishingForecastIssue.FishEyesSupportUnknown) ImGui.TextWrapped(UiText.Get(T.ForecastFishEyesUnknown));
    }

    private static string Describe(FishingWindow window, DateTimeOffset now) => Range(window, now) is { Length: > 0 } range
        ? range : window.Issue == FishingForecastIssue.FishEyesSupportUnknown ? UiText.Get(T.ForecastFishEyesUnknown)
        : FishRowRenderer.FormatWindow(window, now);


    private static string Range(FishingWindow? window, DateTimeOffset now)
    {
        if (window?.Start is not { } start || window.End is not { } end) return "";
        var localStart = start.ToLocalTime();
        var localEnd = end.ToLocalTime();
        return localStart.Date == localEnd.Date
            ? UiText.Get(T.WindowSameDayRange, DateLabel(localStart, now), localEnd)
            : UiText.Get(T.WindowCrossDayRange, DateLabel(localStart, now), DateLabel(localEnd, now));
    }

    private static string DateLabel(DateTimeOffset value, DateTimeOffset now)
    {
        var days = (value.Date - now.ToLocalTime().Date).Days;
        return UiText.Get(days == 0 ? T.WindowToday : days == 1 ? T.WindowTomorrow : T.WindowOtherDay, value);
    }
}
