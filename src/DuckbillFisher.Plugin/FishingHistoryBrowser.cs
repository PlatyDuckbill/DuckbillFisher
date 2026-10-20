using System.Numerics;
using Dalamud.Bindings.ImGui;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Trip summaries and drill-down in one tab. Confirmed deletes carry their original character session.</summary>
internal sealed class FishingHistoryBrowser(FishingTracker tracker, FishCatalog catalog, FishingHistoryPanel rows)
{
    private Guid[] deleteIds = [];
    private int deleteSession;

    public void Draw(bool developerMode, Action? showTimer)
    {
        var view = tracker.View;
        if (view.SelectedTrip is { } selected)
        {
            if (ImGui.Button(UiText.Label(T.TripBack, "TripBack"))) tracker.SelectTrip(view.Session, null);
            ImGui.SameLine();
            ImGui.BeginDisabled(selected.IsActive || !view.HistoryWritable);
            if (ImGui.Button(UiText.Label(T.TripDelete, "DeleteSelectedTrip"))) Confirm(view.Session, [selected.Id]);
            ImGui.EndDisabled();
            UiWidgets.Tooltip(UiText.Get(T.TripActiveProtected));
            Diagnostics(developerMode);
            ImGui.TextWrapped(Location(view, selected));
            ImGui.TextColored(new Vector4(.58f, .82f, .92f, 1), UiText.Get(T.TripTotalDuration,
                FishingTimeText.TripDuration(selected, view, tracker.Now)));
            UiWidgets.Tooltip(UiText.Get(T.TripDurationHint));
            ImGui.TextWrapped(Summary(selected));
            if (selected.Casts > selected.Retained)
                ImGui.TextWrapped(UiText.Get(T.TripTruncated, selected.Retained, selected.Casts));
            rows.Draw(view, view.SelectedRecords, "SelectedTripRows", table: true);
        }
        else
        {
            if (ImGui.Button(UiText.Label(T.CastingTimerTitle, "OpenCastingTimer"))) showTimer?.Invoke();
            ImGui.SameLine();
            var closed = view.Trips.Where(t => !t.IsActive).Select(t => t.Id).ToArray();
            ImGui.BeginDisabled(closed.Length == 0 || !view.HistoryWritable);
            if (ImGui.Button(UiText.Label(T.TripDeleteAll, "DeleteCompletedTrips"))) Confirm(view.Session, closed);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextDisabled(UiText.Get(T.TripCapacity, view.Trips.Count, FishingAttempts.TripCapacity));
            Diagnostics(developerMode);
            if (view.Storage is FishingStorageState.ReadFailed or FishingStorageState.InvalidFile or FishingStorageState.WriteFailed)
                ImGui.TextWrapped(FishingHistoryPanel.StorageMessage(view));
            if (ImGui.BeginChild("TripSummaries"))
            {
                if (view.Trips.Count == 0) ImGui.TextWrapped(UiText.Get(view.State == FishingTrackerState.Loading ? T.HistoryLoading : T.TripEmpty));
                DrawSummaries(view);
            }
            ImGui.EndChild();
        }
        DrawConfirmation(view);
    }

    private void Diagnostics(bool enabled)
    {
        if (!enabled) return;
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label(T.CopyTimerReport, "CopyTimerReport"))) ImGui.SetClipboardText(tracker.CreateReport());
    }
    private void DrawSummaries(FishingTrackerView view)
    {
        if (!ImGui.BeginTable("FishingTrips", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp |
            ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn(UiText.Label(T.TripLocationColumn, "TripLocation"), ImGuiTableColumnFlags.WidthStretch, 3);
        ImGui.TableSetupColumn(UiText.Label(T.TripDateColumn, "TripDate"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 13);
        ImGui.TableSetupColumn(UiText.Label(T.TripDurationColumn, "TripDuration"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 8);
        ImGui.TableSetupColumn(UiText.Label(T.TripCastsColumn, "TripCasts"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 4);
        ImGui.TableSetupColumn(UiText.Label(T.TripCaughtColumn, "TripCaught"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 4);
        ImGui.TableSetupColumn(UiText.Label(T.TripStatusColumn, "TripStatus"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 6);
        ImGui.TableHeadersRow();
        foreach (var trip in view.Trips)
        {
            ImGui.PushID(trip.Id.ToString("N"));
            var height = ImGui.GetTextLineHeightWithSpacing() * 2 + 6;
            ImGui.TableNextRow(ImGuiTableRowFlags.None, height);
            ImGui.TableNextColumn();
            var origin = ImGui.GetCursorPos();
            if (ImGui.Selectable("###Trip", false, ImGuiSelectableFlags.SpanAllColumns,
                new Vector2(0, height - ImGui.GetStyle().CellPadding.Y * 2))) tracker.SelectTrip(view.Session, trip.Id);
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip(); ImGui.TextUnformatted(Location(view, trip)); ImGui.TextUnformatted(Summary(trip)); ImGui.EndTooltip();
            }
            var next = ImGui.GetCursorPos();
            ImGui.SetCursorPos(origin);
            var spot = trip.SpotId is { } spotId ? catalog.View.Spots.GetValueOrDefault(spotId)?.Name : null;
            FishingHistoryPanel.Clipped(spot ?? UiText.Get(T.DetailLocationUnknown), Math.Max(1, ImGui.GetContentRegionAvail().X));
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            FishingHistoryPanel.Clipped(view.TerritoryNames.GetValueOrDefault(trip.TerritoryId, ""), Math.Max(1, ImGui.GetContentRegionAvail().X));
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(next);
            ImGui.TableNextColumn();
            var date = (trip.FirstCastUtc ?? trip.StartedUtc)?.ToLocalTime();
            ImGui.TextUnformatted(date?.ToString("yyyy-MM-dd", UiText.Culture) ?? UiText.Get(T.HistoryStartUnknown));
            ImGui.TextDisabled(date?.ToString("HH:mm:ss", UiText.Culture) ?? "—");
            ImGui.TableNextColumn(); ImGui.TextUnformatted(FishingTimeText.TripDuration(trip, view, tracker.Now));
            UiWidgets.Tooltip(UiText.Get(T.TripDurationHint));
            ImGui.TableNextColumn(); ImGui.TextUnformatted(trip.Casts.ToString(UiText.Culture));
            ImGui.TableNextColumn(); ImGui.TextColored(new Vector4(.55f, .84f, .66f, 1), trip.Caught.ToString(UiText.Culture));
            ImGui.TableNextColumn(); ImGui.TextColored(trip.IsActive ? new Vector4(.58f, .78f, .94f, 1) : new Vector4(.7f, .72f, .76f, 1),
                UiText.Get(trip.IsActive ? T.TripInProgress : T.TripEnded));
            ImGui.PopID();
        }
        ImGui.EndTable();
    }
    private string Location(FishingTrackerView view, FishingTripSummary trip)
    {
        var zone = view.TerritoryNames.GetValueOrDefault(trip.TerritoryId, "");
        var spot = trip.SpotId is { } id ? catalog.View.Spots.GetValueOrDefault(id)?.Name : null;
        if (!string.IsNullOrWhiteSpace(spot)) return zone.Length > 0 && zone != spot ? UiText.Get(T.TripLocationTitle, zone, spot) : spot;
        return UiText.Get(T.TripLocationTitle, zone.Length > 0 ? zone : UiText.Get(T.DetailLocationUnknown), UiText.Get(T.DetailLocationUnknown));
    }
    private static string Date(FishingTripSummary trip) => (trip.FirstCastUtc ?? trip.StartedUtc)?.ToLocalTime().ToString("g", UiText.Culture) ?? UiText.Get(T.HistoryStartUnknown);
    private static string Summary(FishingTripSummary trip) => UiText.Get(T.TripSummary, Date(trip), trip.Casts, trip.Caught,
        trip.Failed, trip.Cancelled + trip.Interrupted, trip.Unknown, new UiMessage(trip.IsActive ? T.TripInProgress : T.TripEnded));
    private void Confirm(int session, Guid[] ids)
    {
        deleteSession = session;
        deleteIds = ids;
        ImGui.OpenPopup(UiText.Label(T.TripDeleteTitle, "TripDeleteConfirmation"));
    }
    private void DrawConfirmation(FishingTrackerView view)
    {
        var open = true;
        if (!ImGui.BeginPopupModal(UiText.Label(T.TripDeleteTitle, "TripDeleteConfirmation"), ref open, ImGuiWindowFlags.AlwaysAutoResize)) return;
        if (deleteSession != view.Session || deleteIds.Length == 0)
        { deleteIds = []; ImGui.CloseCurrentPopup(); ImGui.EndPopup(); return; }
        ImGui.TextUnformatted(UiText.Get(T.TripDeleteQuestion, deleteIds.Length));
        ImGui.TextDisabled(UiText.Get(T.TripActiveProtected));
        if (ImGui.Button(UiText.Label(T.Cancel, "CancelTripDelete")))
        { deleteIds = []; ImGui.CloseCurrentPopup(); }
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label(T.TripDelete, "ConfirmTripDelete")))
        { tracker.DeleteTrips(deleteSession, deleteIds); deleteIds = []; ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }
}
