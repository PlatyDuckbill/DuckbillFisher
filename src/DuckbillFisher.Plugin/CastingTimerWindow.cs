using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class CastingTimerWindow : Window
{
    private readonly FishingTracker tracker;
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly ITextureProvider textures;
    private readonly Action<FishGuideRow> showDetail;
    private readonly FishingHistoryPanel history;
    private readonly FishRowRenderer rows;
    private bool hideCaught = true;

    public CastingTimerWindow(FishingTracker tracker, FishCatalog catalog, FishPreferences preferences,
        FishCollectionObserver collection, ITextureProvider textures, FishingMapService maps, Action<FishGuideRow> showDetail)
        : base("DuckbillFisher###DuckbillFisher.Timer")
    {
        this.tracker = tracker; this.catalog = catalog; this.preferences = preferences;
        this.collection = collection; this.textures = textures; this.showDetail = showDetail;
        history = new(tracker, textures, showDetail);
        rows = new(textures, maps);
        Size = new Vector2(440, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(340, 340), MaximumSize = new Vector2(float.MaxValue) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        BgAlpha = UiWidgets.BackgroundAlpha;
    }

    public override void PreDraw() => WindowName = $"{UiText.Get(T.CastingTimerTitle)}###DuckbillFisher.Timer";
    public override void Draw()
    {
        var view = tracker.View;
        var spot = view.Spot;
        var spotLabel = spot?.Name ?? UiText.Get(view.HasSession ? T.DetailLocationUnknown : T.TimerSpotPending);
        var buttonSize = ImGui.GetFrameHeight();
        FishingHistoryPanel.Clipped(spotLabel, Math.Max(1, ImGui.GetContentRegionAvail().X - buttonSize - 10));
        UiWidgets.Tooltip(spotLabel);
        ImGui.SameLine();
        ImGui.BeginDisabled(spot is null);
        if (UiWidgets.IconButton(FontAwesomeIcon.Fish, "TimerSpotFish", UiText.Get(T.TimerSpotFish))) ImGui.OpenPopup("TimerSpotFishPopup");
        ImGui.EndDisabled();
        DrawFishPopup(view);

        var bait = view.Native?.EquippedBait;
        if (bait is { } id && view.Items.TryGetValue(id, out var item))
        {
            UiWidgets.GameIcon(textures, item.IconId, ImGui.GetTextLineHeightWithSpacing(), item.Name);
            ImGui.SameLine();
        }
        ImGui.TextUnformatted(UiText.Get(T.TimerEquippedBait, FishingTimeText.Item(view, bait)));
        var phase = view.Native?.Phase;
        var label = view.State switch
        {
            FishingTrackerState.Waiting => T.TimerLoginRequired,
            FishingTrackerState.Loading => T.HistoryLoading,
            FishingTrackerState.OceanUnsupported => T.TimerOceanUnsupported,
            FishingTrackerState.Unavailable => T.TimerUnavailable,
            _ => phase switch { 1 => T.TimerCasting, 12 => T.TimerWaiting, 5 => T.TimerBite, 6 => T.TimerHook,
                2 => T.TimerReturning, 3 => T.TimerStopping, 4 => T.TimerRodReady, 0 => T.TimerIdle, _ => T.TimerObserving },
        };
        var elapsed = view.CastStartedAt is { } started ? FishingTimeText.Elapsed(tracker.Now - started) : "—";
        ImGui.TextUnformatted(UiText.Get(T.TimerCastLine, new UiMessage(label), elapsed));
        var et = view.Active?.StartedUtc is { } utc ? FishingTimeText.Et(utc) : "—";
        ImGui.TextDisabled(UiText.Get(T.TimerCastEt, et));
        var sessionTime = view.SessionStartedUtc?.ToLocalTime().ToString("HH:mm:ss", UiText.Culture) ?? "—";
        var sessionElapsed = view.SessionStartedAt is { } sessionStart ? FishingTimeText.Elapsed(tracker.Now - sessionStart) : "—";
        ImGui.TextUnformatted(UiText.Get(T.TimerSessionLine, sessionTime, sessionElapsed));
        UiWidgets.Tooltip(UiText.Get(T.TimerStartUnknownHint));
        ImGui.Separator();
        ImGui.TextUnformatted(UiText.Get(T.HistoryRecent, Math.Min(view.Records.Count, FishingAttempts.RecentCapacity)));
        history.Draw(view, FishingAttempts.RecentCapacity, "TimerHistory");
    }

    private void DrawFishPopup(FishingTrackerView view)
    {
        var viewport = ImGui.GetMainViewport().WorkSize;
        ImGui.SetNextWindowSize(new Vector2(Math.Min(560, viewport.X * .85f), Math.Min(600, viewport.Y * .75f)), ImGuiCond.Appearing);
        if (!ImGui.BeginPopup("TimerSpotFishPopup", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) return;
        ImGui.TextUnformatted(view.Spot?.Name ?? UiText.Get(T.TimerSpotPending));
        if (ImGui.Button(UiText.Label(hideCaught ? T.TimerHideCaught : T.TimerAllFish, "TimerCollectionFilter"))) hideCaught = !hideCaught;
        var matches = hideCaught ? view.RemainingSpotFish : view.AllSpotFish;
        ImGui.SameLine();
        ImGui.TextDisabled(UiText.Get(T.TimerFishCount, matches.Length));
        if (ImGui.BeginChild("SpotFishRows"))
        {
            if (matches.Length == 0) ImGui.TextWrapped(UiText.Get(T.TimerNoFish));
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                var height = FishRowRenderer.GetHeight(ImGui.GetContentRegionAvail().X);
                clipper.Begin(matches.Length, height + ImGui.GetStyle().ItemSpacing.Y);
                var personal = preferences.View;
                var observed = collection.View;
                var snapshot = catalog.View;
                while (clipper.Step())
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        var fish = matches[i];
                        var forecast = snapshot.Windows.GetValueOrDefault(fish.FishParameterId);
                        var otherRegion = fish.Details?.Spot is { } source && source.TerritoryId != view.Spot?.TerritoryId;
                        if (rows.Draw(fish, height, false, UiText.Get(FishRowRenderer.CollectionKey(personal, observed, fish.FishParameterId)),
                            view.Spot?.Name ?? "", forecast, DateTimeOffset.UtcNow, otherRegion, personal.Favorites.Contains(fish.FishParameterId),
                            compact: true, spot: view.Spot, canOpenMap: personal.HasCharacter))
                        { showDetail(fish); ImGui.CloseCurrentPopup(); }
                    }
            }
            finally { clipper.Destroy(); }
        }
        ImGui.EndChild();
        ImGui.EndPopup();
    }
}
