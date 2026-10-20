using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal static class FishingTimeText
{
    public static string Et(DateTimeOffset utc)
    {
        var minutes = (utc.ToUnixTimeMilliseconds() % 4_200_000 + 4_200_000) % 4_200_000 * 1440 / 4_200_000;
        return string.Create(CultureInfo.InvariantCulture, $"{minutes / 60:00}:{minutes % 60:00}");
    }
    public static string Elapsed(TimeSpan value)
    {
        if (value.TotalDays >= 1) return FishRowRenderer.Countdown(value, compact: false);
        var tenths = Math.Max(0, (long)(value.TotalMilliseconds / 100));
        return string.Create(CultureInfo.InvariantCulture, $"{tenths / 600:00}:{tenths % 600 / 10d:00.0}");
    }
    public static string Item(FishingTrackerView view, uint? id) => id is not > 0 ? UiText.Get(T.ConditionUnknown) :
        view.Items.TryGetValue(id.Value, out var item) && item.Name.Length > 0 ? item.Name : UiText.Get(T.HistoryItemUnknown, id.Value);
    public static double? DisplaySeconds(FishingAttempt row, FishingTrackerView view, TimeSpan now) =>
        row.HookSeconds ?? (row.Id == view.Active?.Id && view.CastStartedAt is { } start ? (now - start).TotalSeconds : row.DurationSeconds);
    public static string TripDuration(FishingTripSummary trip, FishingTrackerView view, TimeSpan now)
    {
        var seconds = trip.IsActive && trip.Id == view.CurrentTrip?.Id && view.TripStartedAt is { } monotonic
            ? Math.Max(0, (now - monotonic).TotalSeconds) : trip.DurationSeconds;
        if (seconds is null && trip.EndedUtc is { } end && (trip.FirstCastUtc ?? trip.StartedUtc) is { } start && end >= start)
            seconds = (end - start).TotalSeconds;
        return seconds is { } duration && double.IsFinite(duration) && duration >= 0 && duration < TimeSpan.MaxValue.TotalSeconds
            ? FishRowRenderer.Countdown(TimeSpan.FromSeconds(duration), compact: false) : UiText.Get(T.ConditionUnknown);
    }
}

/// <summary>Shared current-trip/detail rows. No synthetic group headers or reserved separator rows.</summary>
internal sealed class FishingHistoryPanel(FishingTracker tracker, ITextureProvider textures, Action<FishGuideRow> showDetail)
{
    public void Draw(FishingTrackerView view, IReadOnlyList<FishingAttempt> records, string id, bool table = false)
    {
        if (view.Storage is FishingStorageState.ReadFailed or FishingStorageState.InvalidFile or FishingStorageState.WriteFailed || view.DetailError && table)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, .72f, .3f, 1));
            ImGui.TextWrapped(StorageMessage(view));
            ImGui.PopStyleColor();
        }
        if (ImGui.BeginChild(id))
        {
            if (records.Count == 0)
                ImGui.TextWrapped(UiText.Get(view.DetailLoading || view.State == FishingTrackerState.Loading ? T.HistoryLoading :
                    table ? T.HistoryEmpty : T.TimerCurrentEmpty));
            if (table && ImGui.GetContentRegionAvail().X > ImGui.GetFontSize() * 42)
                DrawTable(view, records);
            else
            {
                var height = ImGui.GetTextLineHeightWithSpacing() * 2 + 8;
                var clipper = ImGui.ImGuiListClipper();
                try
                {
                    clipper.Begin(records.Count, height + ImGui.GetStyle().ItemSpacing.Y);
                    while (clipper.Step())
                        for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) DrawCompact(records[i], view, height);
                }
                finally { clipper.Destroy(); }
            }
        }
        ImGui.EndChild();
    }

    internal static string StorageMessage(FishingTrackerView view) =>
        view.Storage == FishingStorageState.WriteFailed && view.HistoryWritable && !view.DetailError
            ? UiText.Get(T.HistoryWriteRetry, (int)FishingHistoryStore.RetryInterval.TotalSeconds)
            : UiText.Get(T.HistoryStorageError);

    private void DrawTable(FishingTrackerView view, IReadOnlyList<FishingAttempt> records)
    {
        if (!ImGui.BeginTable("TripAttempts", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp |
            ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn(UiText.Label(T.TripResultColumn, "Result"), ImGuiTableColumnFlags.WidthStretch, 2);
        ImGui.TableSetupColumn(UiText.Label(T.TripBaitColumn, "Bait"), ImGuiTableColumnFlags.WidthStretch, 2);
        ImGui.TableSetupColumn(UiText.Label(T.TripCastColumn, "CastAt"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 7);
        ImGui.TableSetupColumn(UiText.Label(T.TripEtColumn, "CastEt"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 5);
        ImGui.TableSetupColumn(UiText.Label(T.TripHookColumn, "CastElapsed"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 7);
        ImGui.TableSetupColumn(UiText.Label(T.TripResultElapsedColumn, "ResultElapsed"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 7);
        ImGui.TableHeadersRow();
        var height = ImGui.GetFrameHeight() + 6;
        var clipper = ImGui.ImGuiListClipper();
        try
        {
            clipper.Begin(records.Count, height);
            while (clipper.Step())
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var row = records[i];
                    ImGui.PushID(row.Id.ToString("N"));
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, height);
                    ImGui.TableNextColumn();
                    var origin = ImGui.GetCursorPos();
                    var clicked = ImGui.Selectable("###Attempt", false,
                        ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap, new Vector2(0, height - ImGui.GetStyle().CellPadding.Y * 2));
                    var hovered = ImGui.IsItemHovered();
                    var next = ImGui.GetCursorPos();
                    ImGui.SetCursorPos(origin);
                    DrawItemCell(view, row.Outcome == FishingOutcome.Caught ? row.FishItemId : null, Result(row, view), OutcomeColor(row.Outcome));
                    ImGui.SetCursorPos(next);
                    ImGui.TableNextColumn(); DrawItemCell(view, row.BaitItemId, Bait(row, view));
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(row.StartedUtc?.ToLocalTime().ToString("HH:mm:ss", UiText.Culture) ?? "—");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(row.StartedUtc is { } utc ? FishingTimeText.Et(utc) : "—");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(row.HookSeconds is { } hook ? UiText.Get(T.HistorySeconds, hook)
                        : row.Id == view.Active?.Id ? Duration(row, view) : UiText.Get(T.ConditionUnknown));
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(row.DurationSeconds is { } total
                        ? UiText.Get(T.HistorySeconds, total) : UiText.Get(T.ConditionUnknown));
                    if (hovered) Tooltip(row, view);
                    OpenResult(clicked, row);
                    ImGui.PopID();
                }
        }
        finally { clipper.Destroy(); }
        ImGui.EndTable();
    }

    private void DrawCompact(FishingAttempt row, FishingTrackerView view, float height)
    {
        ImGui.PushID(row.Id.ToString("N"));
        var start = ImGui.GetCursorPos();
        var width = ImGui.GetContentRegionAvail().X;
        var clicked = ImGui.Selectable("###Attempt", false, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var next = ImGui.GetCursorPos();
        var line = ImGui.GetTextLineHeightWithSpacing();
        if (row.MoochSourceId is not null)
        {
            var screen = ImGui.GetItemRectMin();
            ImGui.GetWindowDrawList().AddRectFilled(screen, screen + new Vector2(3, height), ImGui.GetColorU32(new Vector4(.32f, .68f, .72f, .9f)));
        }
        var icon = line * 2;
        var itemId = row.Outcome == FishingOutcome.Caught ? row.FishItemId : null;
        var iconId = itemId is { } value && view.Items.TryGetValue(value, out var item) ? item.IconId : 0;
        ImGui.SetCursorPos(start + new Vector2(4, 4));
        if (itemId is > 0) UiWidgets.GameIcon(textures, iconId, icon, FishingTimeText.Item(view, itemId));
        var textX = icon + 12;
        var textWidth = Math.Max(1, width - textX - 4);
        var time = row.StartedUtc?.ToLocalTime().ToString("HH:mm:ss", UiText.Culture) ?? "—";
        var timeWidth = Math.Min(ImGui.CalcTextSize(time).X, textWidth * .35f);
        ImGui.SetCursorPos(start + new Vector2(textX, 4));
        ImGui.PushStyleColor(ImGuiCol.Text, OutcomeColor(row.Outcome));
        Clipped(Result(row, view), Math.Max(1, textWidth - timeWidth - 8));
        ImGui.PopStyleColor();
        ImGui.SetCursorPos(start + new Vector2(width - timeWidth - 4, 4));
        Clipped(time, timeWidth);
        ImGui.SetCursorPos(start + new Vector2(textX, 4 + line));
        var baitIconSize = ImGui.GetTextLineHeight();
        if (row.BaitItemId is { } baitId && view.Items.TryGetValue(baitId, out var bait))
        {
            UiWidgets.GameIcon(textures, bait.IconId, baitIconSize, Bait(row, view));
            ImGui.SameLine(0, 4);
            textWidth = Math.Max(1, textWidth - baitIconSize - 4);
        }
        Clipped(UiText.Get(T.HistoryTiming, Bait(row, view), Duration(row, view),
            row.StartedUtc is { } utc ? FishingTimeText.Et(utc) : "—"), textWidth);
        ImGui.SetCursorPos(next);
        if (hovered) Tooltip(row, view);
        OpenResult(clicked, row);
        ImGui.PopID();
    }
    private string Duration(FishingAttempt row, FishingTrackerView view) =>
        FishingTimeText.DisplaySeconds(row, view, tracker.Now) is { } seconds ? UiText.Get(T.HistorySeconds, seconds) : UiText.Get(T.ConditionUnknown);
    private static string Bait(FishingAttempt row, FishingTrackerView view)
    {
        if (row.BaitItemId is null) return UiText.Get(row.BaitKind switch
        { FishingBaitKind.Mooch => T.HistoryMoochUnknown, FishingBaitKind.Swimbait => T.HistorySwimbaitUnknown, _ => T.ConditionUnknown });
        var name = FishingTimeText.Item(view, row.BaitItemId);
        return row.MoochSourceId is not null ? UiText.Get(T.HistoryMoochLinkedBait, name) : row.BaitKind switch
        {
            FishingBaitKind.Mooch => UiText.Get(T.HistoryMoochBait, name),
            FishingBaitKind.Swimbait => UiText.Get(T.HistorySwimbaitBait, name),
            _ => name,
        };
    }
    private static string Result(FishingAttempt row, FishingTrackerView view)
    {
        var outcome = UiText.Get(Outcome(row.Outcome));
        return row.FishItemId is > 0 ? row.Quantity is > 1
            ? UiText.Get(T.HistoryFishQuantityOutcome, FishingTimeText.Item(view, row.FishItemId), row.Quantity.Value, outcome)
            : UiText.Get(T.HistoryFishOutcome, FishingTimeText.Item(view, row.FishItemId), outcome) : outcome;
    }
    private void Tooltip(FishingAttempt row, FishingTrackerView view)
    {
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(Result(row, view));
        if (row.MoochSourceId is not null) ImGui.TextUnformatted(UiText.Get(T.TimerMoochLink, FishingTimeText.Item(view, row.BaitItemId)));
        if (row.StartedUtc is { } castUtc) ImGui.TextUnformatted(UiText.Get(T.HistoryCastAt, castUtc.ToLocalTime().ToString("G", UiText.Culture)));
        ImGui.TextUnformatted(UiText.Get(T.HistoryTiming, Bait(row, view), Duration(row, view),
            row.StartedUtc is { } utc ? FishingTimeText.Et(utc) : "—"));
        if (row.DurationSeconds is { } total) ImGui.TextUnformatted(UiText.Get(T.HistoryResultTime, total));
        if (row.HookSeconds is { } hook) ImGui.TextUnformatted(UiText.Get(T.HistoryHookTime, hook));
        if (row.BiteSeconds is { } bite) ImGui.TextUnformatted(UiText.Get(T.HistoryBiteTime, bite));
        ImGui.TextUnformatted(UiText.Get(T.HistoryReason, UiText.Get(Reason(row.EndReason))));
        ImGui.EndTooltip();
    }
    private void OpenResult(bool clicked, FishingAttempt row)
    { if (clicked && row.FishItemId is { } fish && tracker.FishForItem(fish) is { } target) showDetail(target); }
    private void DrawItemCell(FishingTrackerView view, uint? itemId, string text, Vector4? color = null)
    {
        if (itemId is { } id && view.Items.TryGetValue(id, out var item))
        {
            UiWidgets.GameIcon(textures, item.IconId, ImGui.GetTextLineHeight(), item.Name);
            ImGui.SameLine(0, 4);
        }
        if (color is { } tint) ImGui.PushStyleColor(ImGuiCol.Text, tint);
        Clipped(text, Math.Max(1, ImGui.GetContentRegionAvail().X));
        if (color is not null) ImGui.PopStyleColor();
        UiWidgets.Tooltip(text);
    }
    private static Vector4 OutcomeColor(FishingOutcome outcome) => outcome switch
    {
        FishingOutcome.Caught => new(.55f, .84f, .66f, 1),
        FishingOutcome.Failed => new(.94f, .65f, .48f, 1),
        FishingOutcome.Pending => new(.58f, .78f, .94f, 1),
        _ => new(.70f, .72f, .76f, 1),
    };
    internal static void Clipped(string text, float width)
    {
        var position = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(), position, ImGui.GetColorU32(ImGuiCol.Text), text,
            0, new Vector4(position.X, position.Y, position.X + width, position.Y + ImGui.GetTextLineHeight()));
        ImGui.Dummy(new Vector2(width, ImGui.GetTextLineHeight()));
    }
    private static T Outcome(FishingOutcome value) => value switch
    {
        FishingOutcome.Pending => T.HistoryPending, FishingOutcome.Caught => T.HistoryCaught,
        FishingOutcome.Failed => T.HistoryFailed, FishingOutcome.Cancelled => T.HistoryCancelled,
        FishingOutcome.Interrupted => T.HistoryInterrupted, _ => T.HistoryUnknown,
    };
    private static T Reason(FishingEndReason value) => value switch
    {
        FishingEndReason.None => T.HistoryPending, FishingEndReason.CatchLog => T.HistoryCaught,
        FishingEndReason.FailureLog => T.HistoryFailed,
        FishingEndReason.ManualRetrieve => T.HistoryCancelled, FishingEndReason.RodFolded => T.HistoryRodFolded,
        FishingEndReason.Logout => T.HistoryLogout, FishingEndReason.Travel => T.HistoryTravel,
        FishingEndReason.Reload => T.HistoryReload, FishingEndReason.ObservationGap => T.HistoryGap,
        FishingEndReason.ReadUnavailable or FishingEndReason.UnsupportedMode => T.HistoryUnavailable,
        _ => T.HistoryUnknown,
    };
}
