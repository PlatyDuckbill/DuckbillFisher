using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Availability presentation only. UTC forecasts and personal readiness are not changed here.</summary>
internal static class FishingWindowPanel
{
    public static void Draw(FishingWindow? window, DateTimeOffset now, UiMessage? unavailableReason = null, bool intuitionRequired = false)
    {
        ImGui.TextWrapped(UiText.Get(T.ForecastBasis));
        UiWidgets.Tooltip(UiText.Get(T.ForecastScope, FishingAvailability.SearchDays));
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
        ImGui.PushStyleColor(ImGuiCol.ChildBg, open ? new Vector4(0.10f, 0.40f, 0.25f, 0.32f) : new Vector4(0.18f, 0.23f, 0.29f, 0.40f));
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X - 24);
        var remaining = window is { Kind: FishingWindowKind.Open, End: not null } or { Kind: FishingWindowKind.Upcoming, Start: not null }
            ? FishRowRenderer.CompactWindow(window, now) : "";
        var range = Range(window, now);
        var line = remaining.Length > 0 && range.Length > 0 ? UiText.Get(T.WindowInline, remaining, range)
            : remaining.Length > 0 ? remaining : range;
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

    public static string IntuitionNotice(FishingWindow? window) => UiText.Get(window?.Issue switch
    {
        FishingForecastIssue.IntuitionDurationUnknown => T.ForecastIntuitionDurationUnknown,
        FishingForecastIssue.PrerequisiteUnavailable or FishingForecastIssue.NestedPrerequisiteUnsupported => T.ForecastIntuitionPrerequisiteUnknown,
        _ => T.ForecastIntuition,
    });

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
