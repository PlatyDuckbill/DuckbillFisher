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
    private readonly Action toggleSpotFish;
    private readonly FishingHistoryPanel history;
    private readonly FishingBuffObserver buffs;
    public Action? ShowSettings { get; set; }

    public CastingTimerWindow(FishingTracker tracker, FishingBuffObserver buffs, ITextureProvider textures, Action<FishGuideRow> showDetail, Action toggleSpotFish)
        : base("DuckbillFisher###DuckbillFisher.Timer")
    {
        this.tracker = tracker; this.textures = textures; this.toggleSpotFish = toggleSpotFish;
        this.buffs = buffs;
        history = new(tracker, textures, showDetail);
        Size = new Vector2(440, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(340, 340), MaximumSize = new Vector2(float.MaxValue) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        BgAlpha = UiWidgets.BackgroundAlpha;
    }

    public override void PreDraw() => WindowName = $"{UiText.Get(T.CastingTimerTitle)}###DuckbillFisher.Timer";
    public override void OnOpen() => buffs.SetActive(true);
    public override void OnClose() => buffs.SetActive(false);
    public override void Draw()
    {
        var view = tracker.View;
        var retained = !view.HasSession && view.CurrentTrip is not null && view.TimerRetainedUntil is not null;
        if (retained && view.TimerRetainedUntil is { } expires)
        {
            ImGui.TextColored(new Vector4(.60f, .76f, .84f, 1), UiText.Get(T.TimerRetained,
                FishRowRenderer.Countdown(expires > tracker.Now ? expires - tracker.Now : TimeSpan.Zero, compact: false)));
            UiWidgets.Tooltip(UiText.Get(T.TimerRetentionHint));
        }
        var spot = view.Spot;
        var spotLabel = UiWidgets.SpotLabel(view);
        var fishLabel = UiText.Get(T.TimerSpotFishButton);
        var buttonSize = ImGui.GetFrameHeight() + ImGui.CalcTextSize(fishLabel).X + ImGui.GetStyle().FramePadding.X * 2;
        var controlsWidth = buttonSize + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X * 2;
        FishingHistoryPanel.Clipped(spotLabel, Math.Max(1, ImGui.GetContentRegionAvail().X - controlsWidth));
        UiWidgets.Tooltip(spotLabel);
        ImGui.SameLine();
        ImGui.BeginDisabled(spot is null);
        if (ImGui.Button("###TimerSpotFish", new Vector2(buttonSize, ImGui.GetFrameHeight()))) toggleSpotFish();
        var buttonOrigin = ImGui.GetItemRectMin() + ImGui.GetStyle().FramePadding;
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(ImGuiCol.Text);
        draw.AddText(UiBuilder.IconFont, ImGui.GetFontSize(), buttonOrigin, color, FontAwesomeIcon.Fish.ToIconString());
        draw.AddText(buttonOrigin + new Vector2(ImGui.GetFrameHeight(), 0), color, fishLabel);
        ImGui.EndDisabled();
        UiWidgets.Tooltip(spot is null ? UiText.Get(T.TimerSpotUnavailableHint) : fishLabel);
        ImGui.SameLine();
        if (UiWidgets.IconButton(FontAwesomeIcon.Stopwatch, "OpenTimerSettings", UiText.Get(T.TimerAlertSettings))) ShowSettings?.Invoke();

        var timed = view.Active ?? view.CurrentRecords.FirstOrDefault();
        var usesFishBait = timed?.BaitKind is FishingBaitKind.Mooch or FishingBaitKind.Swimbait;
        var bait = usesFishBait || retained ? timed?.BaitItemId : view.Native?.EquippedBait;
        if (bait is { } id && view.Items.TryGetValue(id, out var item))
        {
            UiWidgets.GameIcon(textures, item.IconId, ImGui.GetTextLineHeightWithSpacing(), item.Name);
            ImGui.SameLine();
        }
        var baitLabel = bait is > 0 ? FishingTimeText.Item(view, bait) : usesFishBait
            ? UiText.Get(timed?.BaitKind == FishingBaitKind.Swimbait ? T.HistorySwimbaitUnknown : T.HistoryMoochUnknown)
            : UiText.Get(T.ConditionUnknown);
        ImGui.TextUnformatted(UiText.Get(usesFishBait || retained ? T.TimerCastBait : T.TimerEquippedBait, baitLabel));
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
        if (retained) label = T.TimerRecentCast;
        var elapsed = timed is not null && FishingTimeText.DisplaySeconds(timed, view, tracker.Now) is { } seconds
            ? FishingTimeText.Elapsed(TimeSpan.FromSeconds(seconds)) : "—";
        ImGui.TextUnformatted(UiText.Get(T.TimerCastLine, new UiMessage(label), elapsed));
        DrawLure();
        var et = timed?.StartedUtc is { } utc ? FishingTimeText.Et(utc) : "—";
        ImGui.TextDisabled(UiText.Get(T.TimerCastEt, et));
        DrawBuffs();
        var sessionTime = (view.CurrentTrip?.FirstCastUtc ?? view.SessionStartedUtc)?.ToLocalTime().ToString("HH:mm:ss", UiText.Culture) ?? "—";
        var sessionElapsed = view.CurrentTrip is { } trip ? FishingTimeText.TripDuration(trip, view, tracker.Now) : "—";
        ImGui.TextUnformatted(UiText.Get(retained ? T.TimerCompletedSessionLine : T.TimerSessionLine, sessionTime, sessionElapsed));
        UiWidgets.Tooltip(UiText.Get(T.TripDurationHint));
        ImGui.Separator();
        ImGui.TextUnformatted(UiText.Get(T.TimerCurrentHistory, view.CurrentTrip?.Casts ?? 0));
        history.Draw(view, view.CurrentRecords, "TimerHistory");
    }

    private void DrawLure()
    {
        var sample = buffs.View;
        if (!sample.LureReady)
            ImGui.TextDisabled(UiText.Get(T.TimerLureUnknown));
        else if (sample.Lure is { } lure)
        {
            UiWidgets.GameIcon(textures, lure.Icon, ImGui.GetTextLineHeightWithSpacing(), lure.Name);
            ImGui.SameLine();
            var elapsed = lure.StartedAt is { } start ? FishingTimeText.Elapsed(buffs.Now - start) : UiText.Get(T.ConditionUnknown);
            ImGui.TextWrapped(UiText.Get(T.TimerLureElapsed, lure.Name, elapsed, lure.Stacks));
        }
        else ImGui.TextDisabled(UiText.Get(T.TimerLureInactive));
        UiWidgets.Tooltip(UiText.Get(T.TimerLureHint));
    }

    private void DrawBuffs()
    {
        var sample = buffs.View;
        var now = buffs.Now;
        var right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        var intuitionLabel = BuffLabel(T.TimerIntuition, sample.IntuitionEnd);
        DrawBuff(T.TimerPatience, sample.PatienceIcon, sample.PatienceEnd);
        UiWidgets.FlowNext(ImGui.GetTextLineHeightWithSpacing() + ImGui.CalcTextSize(intuitionLabel).X + ImGui.GetStyle().ItemSpacing.X, right);
        DrawBuff(T.TimerIntuition, sample.IntuitionIcon, sample.IntuitionEnd);

        string BuffLabel(T key, TimeSpan? end)
        {
            var active = sample.Ready && end > now;
            var seconds = active ? (long)Math.Ceiling((end!.Value - now).TotalSeconds) : 0;
            var remaining = !sample.Ready ? UiText.Get(T.ConditionUnknown) : active
                ? UiText.Get(T.TimerBuffTime, seconds / 60, seconds % 60)
                : UiText.Get(T.TimerBuffInactive);
            return UiText.Get(T.TimerBuffRemaining, new UiMessage(key), remaining);
        }

        void DrawBuff(T key, uint icon, TimeSpan? end)
        {
            var active = sample.Ready && end > now;
            ImGui.BeginGroup();
            if (icon != 0)
            {
                UiWidgets.GameIcon(textures, icon, ImGui.GetTextLineHeightWithSpacing(), UiText.Get(key));
                ImGui.SameLine();
            }
            var text = BuffLabel(key, end);
            if (active) ImGui.TextColored(new Vector4(.46f, .88f, .70f, 1), text);
            else ImGui.TextDisabled(text);
            ImGui.EndGroup();
            UiWidgets.Tooltip(UiText.Get(T.TimerBuffHint));
        }
    }
}
