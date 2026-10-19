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
    private readonly ITextureProvider textures;
    private readonly Action showSpotFish;
    private readonly FishingHistoryPanel history;

    public CastingTimerWindow(FishingTracker tracker, ITextureProvider textures, Action<FishGuideRow> showDetail, Action showSpotFish)
        : base("DuckbillFisher###DuckbillFisher.Timer")
    {
        this.tracker = tracker; this.textures = textures; this.showSpotFish = showSpotFish;
        history = new(tracker, textures, showDetail);
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
        var spotLabel = spot is not null ? view.IsLastSpot ? UiText.Get(T.TimerLastSpot, spot.Name) : spot.Name
            : UiText.Get(view.HasSession ? T.DetailLocationUnknown : T.TimerSpotPending);
        var buttonSize = ImGui.CalcTextSize(UiText.Get(T.TimerSpotFishButton)).X + ImGui.GetStyle().FramePadding.X * 2;
        FishingHistoryPanel.Clipped(spotLabel, Math.Max(1, ImGui.GetContentRegionAvail().X - buttonSize - 10));
        UiWidgets.Tooltip(spotLabel);
        ImGui.SameLine();
        ImGui.BeginDisabled(spot is null);
        if (ImGui.Button(UiText.Label(T.TimerSpotFishButton, "TimerSpotFish"))) showSpotFish();
        ImGui.EndDisabled();

        var timed = view.Active ?? view.CurrentRecords.FirstOrDefault();
        var usesFishBait = timed?.BaitKind is FishingBaitKind.Mooch or FishingBaitKind.Swimbait;
        var bait = usesFishBait ? timed?.BaitItemId : view.Native?.EquippedBait;
        if (bait is { } id && view.Items.TryGetValue(id, out var item))
        {
            UiWidgets.GameIcon(textures, item.IconId, ImGui.GetTextLineHeightWithSpacing(), item.Name);
            ImGui.SameLine();
        }
        var baitLabel = bait is > 0 ? FishingTimeText.Item(view, bait) : usesFishBait
            ? UiText.Get(timed?.BaitKind == FishingBaitKind.Swimbait ? T.HistorySwimbaitUnknown : T.HistoryMoochUnknown)
            : UiText.Get(T.ConditionUnknown);
        ImGui.TextUnformatted(UiText.Get(usesFishBait ? T.TimerCastBait : T.TimerEquippedBait, baitLabel));
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
        var elapsed = timed is not null && FishingTimeText.DisplaySeconds(timed, view, tracker.Now) is { } seconds
            ? FishingTimeText.Elapsed(TimeSpan.FromSeconds(seconds)) : "—";
        ImGui.TextUnformatted(UiText.Get(T.TimerCastLine, new UiMessage(label), elapsed));
        var et = timed?.StartedUtc is { } utc ? FishingTimeText.Et(utc) : "—";
        ImGui.TextDisabled(UiText.Get(T.TimerCastEt, et));
        var sessionTime = view.SessionStartedUtc?.ToLocalTime().ToString("HH:mm:ss", UiText.Culture) ?? "—";
        var sessionElapsed = view.SessionStartedAt is { } sessionStart ? FishingTimeText.Elapsed(tracker.Now - sessionStart) : "—";
        ImGui.TextUnformatted(UiText.Get(T.TimerSessionLine, sessionTime, sessionElapsed));
        UiWidgets.Tooltip(UiText.Get(T.TimerStartUnknownHint));
        ImGui.Separator();
        ImGui.TextUnformatted(UiText.Get(T.TimerCurrentHistory, view.CurrentTrip?.Casts ?? 0));
        history.Draw(view, view.CurrentRecords, "TimerHistory");
    }

}
