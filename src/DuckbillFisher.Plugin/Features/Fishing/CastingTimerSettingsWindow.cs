using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiFileDialog;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class CastingTimerSettingsWindow(FishPreferences preferences, SpawnAlertOutput output, FishingSoundNotifications fishingSounds,
    ManagedSoundLibrary soundLibrary)
    : Window("DuckbillFisher###DuckbillFisher.TimerSettings"), IDisposable
{
    private readonly FileDialogManager soundFiles = new();
    private int dialogSession = -1;
    private bool disposed;
    private CastingTimerSettings draft = new();
    private CastingTimerSettings? lastSaved;
    private Guid? selectedPreset;
    private string presetName = "";
    private SettingsTab selectedTab;
    private bool showPresets;
    private PresetOverwrite? overwrite;
    private UiMessage? presetError;
    private int draftSession = -1, draftRestoreRevision = -1;
    private enum SettingsTab { CastAlerts, FishingSounds }

    public void OpenPresets() { selectedTab = SettingsTab.CastAlerts; showPresets = true; IsOpen = true; }

    public override void PreDraw()
    {
        if (showPresets) ImGui.SetNextWindowFocus();
        WindowName = $"{UiText.Get(T.TimerAlertSettings)}###DuckbillFisher.TimerSettings";
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(410, 360), MaximumSize = new Vector2(float.MaxValue) };
        Size = new Vector2(580, 580);
        SizeCondition = ImGuiCond.FirstUseEver;
        BgAlpha = UiWidgets.BackgroundAlpha;
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public override void OnClose()
    {
        soundFiles.Reset();
        overwrite = null;
        if (lastSaved is not null && draft.IsValid() && !draft.Matches(lastSaved)) preferences.SetTimer(draft, draftSession, draftRestoreRevision);
    }

    public override void Draw()
    {
        var personal = preferences.View;
        var contextChanged = draftSession != personal.Session || draftRestoreRevision != personal.SettingsRestoreRevision;
        if (contextChanged) { overwrite = null; presetError = null; }
        if (dialogSession != personal.Session) { soundFiles.Reset(); overwrite = null; presetError = null; dialogSession = personal.Session; }
        if (lastSaved is null || contextChanged || !personal.Timer.Matches(lastSaved))
        {
            lastSaved = personal.Timer.Copy();
            draft = personal.Timer.Copy();
        }
        draftSession = personal.Session; draftRestoreRevision = personal.SettingsRestoreRevision;
        var changed = false;
        DrawNavigation();
        // Each tab owns its scrolling; navigation remains visible.
        var contentId = selectedTab == SettingsTab.CastAlerts ? "CastAlertContent" : "FishingSoundContent";
        if (ImGui.BeginChild(contentId))
        {
            if (showPresets && selectedTab == SettingsTab.CastAlerts) { ImGui.SetScrollY(0); showPresets = false; }
            if (personal.Error is { } error) ImGui.TextWrapped(error.Display);
            ImGui.BeginDisabled(!personal.CanEdit);
            changed |= selectedTab == SettingsTab.CastAlerts ? DrawCastAlerts(personal) : DrawFishingSounds(personal);
            ImGui.EndDisabled();
        }
        ImGui.EndChild();
        if (changed && draft.IsValid()) preferences.SetTimer(draft, personal.Session, personal.SettingsRestoreRevision);
        soundFiles.Draw();
    }

    private void DrawNavigation()
    {
        if (ImGui.BeginChild("TimerSettingsNavigation", new Vector2(0, ImGui.GetFrameHeightWithSpacing()), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (ImGui.BeginTabBar("TimerSettingsTabs", ImGuiTabBarFlags.FittingPolicyScroll))
            {
                NavigationTab(SettingsTab.CastAlerts, T.TimerAlertTab, "CastAlertsTab", FontAwesomeIcon.Stopwatch);
                NavigationTab(SettingsTab.FishingSounds, T.FishingSoundsHeading, "FishingSoundsTab", FontAwesomeIcon.VolumeUp);
                ImGui.EndTabBar();
            }
        }
        ImGui.EndChild();
    }

    private void NavigationTab(SettingsTab page, T label, string id, FontAwesomeIcon icon)
    {
        var fontSize = ImGui.GetFontSize();
        var gap = fontSize + ImGui.GetStyle().ItemInnerSpacing.X;
        var padding = new string(' ', Math.Max(1, (int)MathF.Ceiling(gap / Math.Max(1, ImGui.CalcTextSize(" ").X))));
        var flags = showPresets && page == SettingsTab.CastAlerts ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        var active = ImGui.BeginTabItem($"{padding}{UiText.Label(label, id)}", flags);
        if (ImGui.IsItemVisible())
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var draw = ImGui.GetWindowDrawList();
            draw.PushClipRect(min, max, true);
            draw.AddText(UiBuilder.IconFont, fontSize,
                min + new Vector2(ImGui.GetStyle().FramePadding.X, Math.Max(0, (max.Y - min.Y - fontSize) / 2)),
                ImGui.GetColorU32(active ? ImGuiCol.Text : ImGuiCol.TextDisabled), icon.ToIconString());
            draw.PopClipRect();
        }
        if (active) { selectedTab = page; ImGui.EndTabItem(); }
    }

    private bool DrawCastAlerts(FishPreferencesView personal)
    {
        var channel = Math.Max(0, Array.IndexOf(ChatChannelChoices.All, (Dalamud.Game.Text.XivChatType)draft.AlertChannelId));
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 14);
        var names = ChatChannelChoices.Names;
        var changed = ImGui.Combo(UiText.Label(T.AlertChannel, "TimerChatChannel"), ref channel, names, names.Length);
        UiWidgets.Tooltip(UiText.Get(T.TimerAlertDeliveryHint));
        if (changed) draft = draft with { AlertChannelId = (ushort)ChatChannelChoices.All[channel] };
        ImGui.TextWrapped(UiText.Get(T.TimerAlertHint));
        changed |= DrawPresets(personal);
        SectionHeading(FontAwesomeIcon.Bell, T.TimerAlertRulesHeading, draft.Alerts.Count, CastingTimerSettings.MaxAlerts,
            new(T.TimerAlertLimit, CastingTimerSettings.MaxAlerts, CastingTimerSettings.MaxSeconds));
        ImGui.BeginDisabled(draft.Alerts.Count >= CastingTimerSettings.MaxAlerts);
        if (ActionButton(FontAwesomeIcon.Plus, T.TimerAlertAdd, "AddTimerAlert"))
        {
            draft.Alerts.Add(new(Guid.NewGuid(), 10));
            changed = true;
        }
        if (draft.Alerts.Count >= CastingTimerSettings.MaxAlerts)
            UiWidgets.Tooltip(UiText.Get(T.TimerAlertLimit, CastingTimerSettings.MaxAlerts, CastingTimerSettings.MaxSeconds));
        ImGui.EndDisabled();
        if (draft.Alerts.Count == 0) ImGui.TextWrapped(UiText.Get(T.TimerAlertEmpty));
        // Rule groups scroll vertically on narrow screens without hiding columns.
        for (var index = 0; index < draft.Alerts.Count; index++)
        {
            var rule = draft.Alerts[index];
            ImGui.PushID(rule.Id.ToString("N"));
            var enabled = rule.Enabled;
            changed |= ImGui.Checkbox(UiText.Label(T.TimerAlertEnabled, "Enabled"), ref enabled);
            ImGui.SameLine();
            var name = rule.Name;
            ImGui.SetNextItemWidth(Math.Max(80, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeightWithSpacing()));
            ImGui.InputTextWithHint("###AlertName", UiText.Get(T.TimerAlertName), ref name, CastingTimerSettings.MaxNameLength + 1);
            changed |= ImGui.IsItemDeactivatedAfterEdit();
            ImGui.SameLine();
            var remove = UiWidgets.IconButton(FontAwesomeIcon.Minus, "RemoveTimerAlert", UiText.Get(T.TimerAlertRemove));
            var seconds = rule.Seconds;
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 8);
            changed |= ImGui.InputInt(UiText.Label(T.TimerAlertSeconds, "Seconds"), ref seconds);
            UiWidgets.Tooltip(UiText.Get(T.TimerAlertSecondsRange, CastingTimerSettings.MaxSeconds));
            var sound = rule.Sound;
            var soundNames = Enumerable.Range(0, 17).Select(value => value == 0
                ? UiText.Get(T.AlertSoundNone) : UiText.Get(T.AlertSoundNumber, value)).ToArray();
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
            changed |= ImGui.Combo(UiText.Label(T.AlertSound, "Sound"), ref sound, soundNames, soundNames.Length);
            ImGui.SameLine();
            ImGui.BeginDisabled(!personal.HasCharacter || sound == 0);
            if (UiWidgets.IconButton(FontAwesomeIcon.VolumeUp, "PreviewTimerSound", UiText.Get(T.AlertSoundPreview)))
                output.RequestPreview(sound, personal.Session);
            ImGui.EndDisabled();
            draft.Alerts[index] = rule with { Name = name, Enabled = enabled, Seconds = Math.Clamp(seconds, 1, CastingTimerSettings.MaxSeconds), Sound = sound };
            ImGui.Separator();
            ImGui.PopID();
            if (!remove) continue;
            draft.Alerts.RemoveAt(index--);
            changed = true;
        }
        if (output.PreviewResult is { } preview) ImGui.TextWrapped(preview.Display);
        return changed;
    }

    private bool DrawFishingSounds(FishPreferencesView personal)
    {
        var changed = false;
        HintBullet(T.FishingSoundsHint);
        HintBullet(T.FishingSoundLifecycleHint);
        foreach (var kind in Enum.GetValues<FishingSoundKind>())
        {
            ImGui.PushID((int)kind);
            var rule = FishingSoundNotifications.Settings(draft, kind);
            var slot = kind switch
            {
                FishingSoundKind.Bite => SoundSlot.Bite,
                FishingSoundKind.BigFish => SoundSlot.BigFish,
                _ => SoundSlot.LegendaryFish,
            };
            var title = kind switch
            {
                FishingSoundKind.Bite => T.FishingSoundBite,
                FishingSoundKind.BigFish => T.FishingSoundBigFish,
                _ => T.FishingSoundLegendaryFish,
            };
            SectionHeading(kind switch
            {
                FishingSoundKind.Bite => FontAwesomeIcon.Fish,
                FishingSoundKind.BigFish => FontAwesomeIcon.Trophy,
                _ => FontAwesomeIcon.Crown,
            }, title);
            HintBullet(kind switch
            {
                FishingSoundKind.Bite => T.FishingSoundBiteHint,
                FishingSoundKind.BigFish => T.FishingSoundBigFishHint,
                _ => T.FishingSoundLegendaryFishHint,
            });
            var sound = rule.Sound;
            var names = Enumerable.Range(0, CustomAlertSound.Choice + 1).Select(value => value == 0
                ? UiText.Get(T.AlertSoundNone) : value == CustomAlertSound.Choice ? UiText.Get(T.CustomSound)
                : UiText.Get(T.AlertSoundNumber, value)).ToArray();
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
            changed |= ImGui.Combo(UiText.Label(T.AlertSound, "FishingSoundChoice"), ref sound, names, names.Length);
            ImGui.SameLine();
            ImGui.BeginDisabled(!personal.HasCharacter || sound == 0 || soundLibrary.Busy(slot));
            if (UiWidgets.IconButton(FontAwesomeIcon.VolumeUp, "PreviewFishingSound", UiText.Get(T.AlertSoundPreview)))
                fishingSounds.Preview(kind, personal.Session);
            ImGui.EndDisabled();
            var firstOnly = rule.FirstCatchOnly;
            if (kind != FishingSoundKind.Bite)
            {
                changed |= ImGui.Checkbox(UiText.Label(T.FishingSoundFirstCatchOnly, "FirstCatchOnly"), ref firstOnly);
                UiWidgets.Tooltip(UiText.Get(T.FishingSoundFirstCatchHint));
            }
            if (sound == CustomAlertSound.Choice)
            {
                if (rule.CustomPath.Length > 0)
                {
                    ImGui.TextWrapped(UiText.Get(T.FishingSoundFile, Path.GetFileName(rule.CustomPath)));
                    UiWidgets.Tooltip(rule.CustomPath);
                }
                ImGui.BeginDisabled(soundLibrary.Busy(slot));
                if (ActionButton(FontAwesomeIcon.FolderOpen, T.CustomSoundChoose, "ChooseFishingSound"))
                {
                    var expectedSession = personal.Session;
                    soundFiles.OpenFileDialog(UiText.Get(T.CustomSoundChoose), ".wav,.WAV", (bool ok, string path) =>
                    {
                        if (!disposed && ok && preferences.View.Session == expectedSession)
                            soundLibrary.ImportFile(slot, path, expectedSession);
                    });
                }
                ImGui.EndDisabled();
                ImGui.SameLine();
                ImGui.BeginDisabled(soundLibrary.Busy(slot));
                if (UiWidgets.IconButton(FontAwesomeIcon.SyncAlt, "ReloadFishingSound", UiText.Get(T.CustomSoundReload)))
                    fishingSounds.Reload(kind, personal.Session);
                ImGui.EndDisabled();
                ImGui.SameLine();
                if (UiWidgets.IconButton(FontAwesomeIcon.Stop, "StopFishingSound", UiText.Get(T.CustomSoundStop)))
                    fishingSounds.Stop(kind, personal.Session);
                if (soundLibrary.Message(slot) is { } importMessage) ImGui.TextWrapped(importMessage.Display);
                ImGui.TextWrapped(UiText.Get(fishingSounds.CustomSound(kind).State switch
                {
                    CustomSoundState.Ready => T.CustomSoundReady,
                    CustomSoundState.Loading => T.CustomSoundLoading,
                    CustomSoundState.NoFile => T.CustomSoundNoFile,
                    CustomSoundState.FileUnavailable => T.CustomSoundFileUnavailable,
                    CustomSoundState.InvalidFile => T.CustomSoundInvalid,
                    CustomSoundState.LimitExceeded => T.CustomSoundLimit,
                    CustomSoundState.AudioUnavailable => T.CustomSoundUnavailable,
                    _ => T.CustomSoundLoginRequired,
                }));
            }
            ReplaceSound(kind, rule with { Sound = sound, FirstCatchOnly = firstOnly });
            ImGui.PopID();
        }
        if (Enum.GetValues<FishingSoundKind>().Any(kind => FishingSoundNotifications.Settings(draft, kind).Sound == CustomAlertSound.Choice))
        {
            SectionHeading(FontAwesomeIcon.InfoCircle, T.FishingSoundWaveHelp);
            HintBullet(T.FishingSoundWaveLimits, CustomAlertSound.MaxSeconds, CustomAlertSound.MaxMegabytes);
            HintBullet(T.ManagedSoundHint);
        }
        if (fishingSounds.PreviewResult is { } preview) ImGui.TextWrapped(preview.Display);
        return changed;
    }

    private void ReplaceSound(FishingSoundKind kind, FishingSoundSettings value) => draft = kind switch
    {
        FishingSoundKind.Bite => draft with { BiteSound = value },
        FishingSoundKind.BigFish => draft with { BigFishSound = value },
        _ => draft with { LegendaryFishSound = value },
    };

    public void Dispose() { disposed = true; overwrite = null; soundFiles.Reset(); }

    private bool DrawPresets(FishPreferencesView personal)
    {
        var changed = false;
        SectionHeading(FontAwesomeIcon.Save, T.TimerAlertPresets, draft.Presets.Count, CastingTimerSettings.MaxPresets,
            new(T.TimerAlertPresetHint, CastingTimerSettings.MaxPresets));
        var selected = draft.Presets.FirstOrDefault(preset => preset.Id == selectedPreset);
        ImGui.SetNextItemWidth(Math.Max(80, ImGui.GetContentRegionAvail().X - ActionWidth(T.TimerAlertLoadPreset) -
            ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X * 2));
        if (ImGui.BeginCombo("###TimerPresets", selected?.Name ?? UiText.Get(T.TimerAlertSelectPreset)))
        {
            foreach (var preset in draft.Presets)
            {
                ImGui.PushID(preset.Id.ToString("N"));
                if (ImGui.Selectable("###SelectPreset", preset.Id == selectedPreset, ImGuiSelectableFlags.None,
                    new Vector2(0, ImGui.GetFrameHeight())))
                {
                    selectedPreset = preset.Id;
                    presetName = preset.Name;
                }
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var draw = ImGui.GetWindowDrawList();
                draw.PushClipRect(min, max, true);
                draw.AddText(min + new Vector2(4, ImGui.GetStyle().FramePadding.Y), ImGui.GetColorU32(ImGuiCol.Text), preset.Name);
                draw.PopClipRect();
                UiWidgets.Tooltip(preset.Name);
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(selected is null);
        if (ActionButton(FontAwesomeIcon.FolderOpen, T.TimerAlertLoadPreset, "LoadTimerPreset") && selected is not null)
        {
            draft = draft with { Alerts = [.. selected.Alerts] };
            changed = true;
        }
        UiWidgets.Tooltip(UiText.Get(T.TimerAlertPresetReplaceHint));
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(selected is null);
        if (UiWidgets.IconButton(FontAwesomeIcon.Minus, "DeleteTimerPreset", UiText.Get(T.PresetDelete)))
        {
            draft.Presets.RemoveAll(preset => preset.Id == selectedPreset);
            selectedPreset = null;
            changed = true;
        }
        ImGui.EndDisabled();
        ImGui.SetNextItemWidth(Math.Max(80, ImGui.GetContentRegionAvail().X - ActionWidth(T.TimerAlertSavePreset) - ImGui.GetStyle().ItemSpacing.X));
        ImGui.InputTextWithHint("###TimerPresetName", UiText.Get(T.PresetName), ref presetName, CastingTimerSettings.MaxNameLength + 1);
        var name = presetName.Trim();
        var existing = draft.Presets.FindIndex(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        ImGui.SameLine();
        ImGui.BeginDisabled(!CastingTimerSettings.ValidName(name) || name.Length == 0 ||
            existing < 0 && draft.Presets.Count >= CastingTimerSettings.MaxPresets);
        if (ActionButton(FontAwesomeIcon.Save, T.TimerAlertSavePreset, "SaveTimerPreset"))
        {
            presetError = null;
            if (existing >= 0)
            {
                var source = draft.Presets[existing];
                overwrite = new(source.Copy(), new(source.Id, name, [.. draft.Alerts]),
                    personal.Session, personal.SettingsRestoreRevision);
                ImGui.OpenPopup(UiText.Label(T.TimerPresetOverwriteTitle, "TimerPresetOverwrite"));
            }
            else
            {
                var preset = new CastingTimerPreset(Guid.NewGuid(), name, [.. draft.Alerts]);
                draft.Presets.Add(preset);
                selectedPreset = preset.Id;
                changed = true;
            }
        }
        if (existing < 0 && draft.Presets.Count >= CastingTimerSettings.MaxPresets)
            UiWidgets.Tooltip(UiText.Get(T.TimerAlertPresetHint, CastingTimerSettings.MaxPresets));
        ImGui.EndDisabled();
        changed |= DrawOverwriteConfirmation();
        if (presetError is { } error) ImGui.TextWrapped(error.Display);
        return changed;
    }

    private bool DrawOverwriteConfirmation()
    {
        var open = true;
        if (!ImGui.BeginPopupModal(UiText.Label(T.TimerPresetOverwriteTitle, "TimerPresetOverwrite"), ref open,
            ImGuiWindowFlags.AlwaysAutoResize)) return false;
        var request = overwrite;
        var personal = preferences.View;
        if (request is null || request.Session != personal.Session || request.RestoreRevision != personal.SettingsRestoreRevision || !personal.CanEdit)
        { overwrite = null; ImGui.CloseCurrentPopup(); ImGui.EndPopup(); return false; }
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetFontSize() * 24);
        ImGui.TextUnformatted(UiText.Get(T.TimerPresetOverwriteQuestion, request.Replacement.Name));
        ImGui.PopTextWrapPos();
        var changed = false;
        if (ImGui.Button(UiText.Label(T.Cancel, "CancelTimerPresetOverwrite")))
        { overwrite = null; ImGui.CloseCurrentPopup(); }
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label(T.TimerPresetOverwrite, "ConfirmTimerPresetOverwrite")))
        {
            var index = draft.Presets.FindIndex(preset => preset.Id == request.Source.Id);
            if (index < 0 || draft.Presets[index].Name != request.Source.Name ||
                !draft.Presets[index].Alerts.SequenceEqual(request.Source.Alerts))
                presetError = new(T.TimerPresetChanged);
            else
            {
                draft.Presets[index] = request.Replacement.Copy();
                selectedPreset = request.Replacement.Id;
                changed = true;
            }
            overwrite = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
        return changed;
    }

    private static void HintBullet(T key, params object[] args)
    {
        ImGui.Bullet();
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped(UiText.Get(key, args));
        ImGui.EndGroup();
    }

    private static void SectionHeading(FontAwesomeIcon icon, T key, int? count = null, int maximum = 0, UiMessage? hint = null)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        UiWidgets.Icon(icon);
        ImGui.SameLine();
        ImGui.TextUnformatted(UiText.Get(key));
        if (count is { } current)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(UiText.Get(T.TimerSettingsCount, current, maximum));
            if (hint is not null) UiWidgets.Tooltip(hint.Display);
        }
        ImGui.Spacing();
    }

    private static float ActionWidth(T key) => ImGui.GetFrameHeight() + ImGui.CalcTextSize(UiText.Get(key)).X +
        ImGui.GetStyle().FramePadding.X * 2;

    private static bool ActionButton(FontAwesomeIcon icon, T key, string id) => UiWidgets.ActionButton(icon, key, id);
    private sealed record PresetOverwrite(CastingTimerPreset Source, CastingTimerPreset Replacement, int Session, int RestoreRevision);
}
