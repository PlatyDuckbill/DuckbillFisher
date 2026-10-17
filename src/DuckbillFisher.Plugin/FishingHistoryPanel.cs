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
        var tenths = Math.Max(0, (long)(value.TotalMilliseconds / 100));
        return string.Create(CultureInfo.InvariantCulture, $"{tenths / 600:00}:{tenths % 600 / 10d:00.0}");
    }
    public static string Item(FishingTrackerView view, uint? id) => id is not > 0 ? UiText.Get(T.ConditionUnknown) :
        view.Items.TryGetValue(id.Value, out var item) && item.Name.Length > 0 ? item.Name : UiText.Get(T.HistoryItemUnknown, id.Value);
}

/// <summary>Shared recent/full history. Constant-height entries and cached session separators.</summary>
internal sealed class FishingHistoryPanel(FishingTracker tracker, ITextureProvider textures, Action<FishGuideRow> showDetail)
{
    private IReadOnlyList<FishingAttempt>? lastRecords;
    private Entry[] entries = [];
    private int lastLimit;

    public void Draw(FishingTrackerView view, int limit, string id)
    {
        if (!ReferenceEquals(lastRecords, view.Records) || lastLimit != limit)
        {
            lastRecords = view.Records;
            lastLimit = limit;
            var result = new List<Entry>();
            Guid? session = null;
            foreach (var record in view.Records.Take(limit))
            {
                if (session != record.SessionId) { result.Add(new(record, true)); session = record.SessionId; }
                result.Add(new(record, false));
            }
            entries = result.ToArray();
        }
        if (view.Storage is FishingStorageState.ReadFailed or FishingStorageState.InvalidFile or FishingStorageState.WriteFailed)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, .72f, .3f, 1));
            ImGui.TextWrapped(UiText.Get(T.HistoryStorageError));
            ImGui.PopStyleColor();
        }
        if (ImGui.BeginChild(id))
        {
            if (view.Records.Count == 0)
                ImGui.TextWrapped(UiText.Get(view.State == FishingTrackerState.Loading ? T.HistoryLoading : T.HistoryEmpty));
            var height = ImGui.GetTextLineHeightWithSpacing() * 2 + 8;
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                clipper.Begin(entries.Length, height + ImGui.GetStyle().ItemSpacing.Y);
                while (clipper.Step())
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) DrawEntry(entries[i], view, height);
            }
            finally { clipper.Destroy(); }
        }
        ImGui.EndChild();
    }

    private void DrawEntry(Entry entry, FishingTrackerView view, float height)
    {
        var row = entry.Record;
        ImGui.PushID($"{row.Id:N}-{entry.Header}");
        var start = ImGui.GetCursorPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (entry.Header)
        {
            ImGui.TextDisabled(UiText.Get(T.HistorySession,
                row.SessionStartedUtc?.ToLocalTime().ToString("g", UiText.Culture) ?? UiText.Get(T.HistoryStartUnknown)));
            ImGui.Separator();
            ImGui.SetCursorPos(start);
            ImGui.Dummy(new Vector2(width, height));
            ImGui.PopID();
            return;
        }
        var clicked = ImGui.Selectable("###Attempt", false, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var next = ImGui.GetCursorPos();
        var line = ImGui.GetTextLineHeightWithSpacing();
        var icon = line * 2;
        var itemId = row.FishItemId ?? row.BaitItemId;
        var iconId = itemId is { } value && view.Items.TryGetValue(value, out var item) ? item.IconId : 0;
        ImGui.SetCursorPos(start + new Vector2(4, 4));
        UiWidgets.GameIcon(textures, iconId, icon, FishingTimeText.Item(view, itemId));
        var textX = icon + 12;
        var textWidth = Math.Max(1, width - textX - 4);
        var time = row.StartedUtc?.ToLocalTime().ToString("HH:mm:ss", UiText.Culture) ?? "—";
        var timeWidth = Math.Min(ImGui.CalcTextSize(time).X, textWidth * .35f);
        var outcome = UiText.Get(Outcome(row.Outcome));
        var result = row.FishItemId is > 0
            ? row.Quantity is > 1
                ? UiText.Get(T.HistoryFishQuantityOutcome, FishingTimeText.Item(view, row.FishItemId), row.Quantity.Value, outcome)
                : UiText.Get(T.HistoryFishOutcome, FishingTimeText.Item(view, row.FishItemId), outcome)
            : outcome;
        ImGui.SetCursorPos(start + new Vector2(textX, 4));
        Clipped(result, Math.Max(1, textWidth - timeWidth - 8));
        ImGui.SetCursorPos(start + new Vector2(width - timeWidth - 4, 4));
        Clipped(time, timeWidth);
        var duration = row.Id == view.Active?.Id && view.CastStartedAt is { } at
            ? (tracker.Now - at).TotalSeconds : row.DurationSeconds;
        var elapsed = duration is { } seconds ? UiText.Get(T.HistorySeconds, seconds) : UiText.Get(T.ConditionUnknown);
        var bait = row.BaitItemId is not null ? FishingTimeText.Item(view, row.BaitItemId) : UiText.Get(row.BaitKind switch
            { FishingBaitKind.Mooch => T.HistoryMoochUnknown, FishingBaitKind.Swimbait => T.HistorySwimbaitUnknown, _ => T.ConditionUnknown });
        var et = row.StartedUtc is { } utc ? FishingTimeText.Et(utc) : "—";
        ImGui.SetCursorPos(start + new Vector2(textX, 4 + line));
        Clipped(UiText.Get(T.HistoryTiming, bait, elapsed, et), textWidth);
        ImGui.SetCursorPos(next);
        if (hovered)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(result);
            ImGui.TextUnformatted(UiText.Get(T.HistoryTiming, bait, elapsed, et));
            if (row.StartedUtc is { } castUtc)
                ImGui.TextUnformatted(UiText.Get(T.HistoryCastAt, castUtc.ToLocalTime().ToString("G", UiText.Culture)));
            ImGui.TextUnformatted(UiText.Get(T.HistoryReason, UiText.Get(Reason(row.EndReason))));
            if (row.BiteSeconds is { } bite) ImGui.TextUnformatted(UiText.Get(T.HistoryBiteTime, bite));
            if (row.HookSeconds is { } hook) ImGui.TextUnformatted(UiText.Get(T.HistoryHookTime, hook));
            ImGui.EndTooltip();
        }
        if (clicked && row.FishItemId is { } fish && tracker.FishForItem(fish) is { } target) showDetail(target);
        ImGui.PopID();
    }

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
        FishingEndReason.ManualRetrieve => T.HistoryCancelled, FishingEndReason.RodFolded => T.HistoryRodFolded,
        FishingEndReason.Logout => T.HistoryLogout, FishingEndReason.Travel => T.HistoryTravel,
        FishingEndReason.Reload => T.HistoryReload, FishingEndReason.ObservationGap => T.HistoryGap,
        FishingEndReason.ReadUnavailable or FishingEndReason.UnsupportedMode => T.HistoryUnavailable,
        _ => T.HistoryUnknown,
    };
    private sealed record Entry(FishingAttempt Record, bool Header);
}
