using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiFileDialog;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class CastingTimerSettingsWindow(FishPreferences preferences, SpawnAlertOutput output, FishingSoundNotifications fishingSounds)
    : Window("DuckbillFisher###DuckbillFisher.TimerSettings"), IDisposable
{
    private readonly FileDialogManager soundFiles = new();
    private (FishingSoundKind Kind, int Session, string Path)? selectedFile;
    private int dialogSession = -1;
    private bool disposed;
    private CastingTimerSettings draft = new();
    private CastingTimerSettings? lastSaved;
    private Guid? selectedPreset;
    private string presetName = "";
    public Action? ShowOutputSettings { get; set; }

    public override void PreDraw()
    {
        WindowName = $"{UiText.Get(T.TimerAlertSettings)}###DuckbillFisher.TimerSettings";
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(410, 360), MaximumSize = new Vector2(float.MaxValue) };
        Size = new Vector2(580, 580);
        SizeCondition = ImGuiCond.FirstUseEver;
        BgAlpha = UiWidgets.BackgroundAlpha;
    }

    public override void OnClose()
    {
        soundFiles.Reset();
        selectedFile = null;
        if (lastSaved is not null && draft.IsValid() && !draft.Matches(lastSaved)) preferences.SetTimer(draft);
    }

    public override void Draw()
    {
        var personal = preferences.View;
        if (dialogSession != personal.Session) { soundFiles.Reset(); selectedFile = null; dialogSession = personal.Session; }
        if (lastSaved is null || !personal.Timer.Matches(lastSaved))
        {
            lastSaved = personal.Timer.Copy();
            draft = personal.Timer.Copy();
        }
        ImGui.TextWrapped(UiText.Get(T.TimerAlertHint));
        if (ImGui.Button(UiText.Label(T.TimerAlertOutputSettings, "TimerAlertOutputSettings"))) ShowOutputSettings?.Invoke();
        if (personal.Error is { } error) ImGui.TextWrapped(error.Display);
        ImGui.BeginDisabled(!personal.CanEdit);
        var changed = DrawFishingSounds(personal);
        ImGui.Separator();
        changed |= DrawPresets();
        ImGui.Separator();
        ImGui.TextDisabled(UiText.Get(T.TimerAlertLimit, CastingTimerSettings.MaxAlerts, CastingTimerSettings.MaxSeconds));
        ImGui.BeginDisabled(draft.Alerts.Count >= CastingTimerSettings.MaxAlerts);
        if (UiWidgets.IconButton(FontAwesomeIcon.Plus, "AddTimerAlert", UiText.Get(T.TimerAlertAdd)))
        {
            draft.Alerts.Add(new(Guid.NewGuid(), 10));
            changed = true;
        }
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
        ImGui.EndDisabled();
        if (output.PreviewResult is { } preview) ImGui.TextWrapped(preview.Display);
        if (changed && draft.IsValid()) preferences.SetTimer(draft);
        soundFiles.Draw();
    }

    private bool DrawFishingSounds(FishPreferencesView personal)
    {
        var changed = false;
        if (selectedFile is { } file)
        {
            selectedFile = null;
            if (personal.CanEdit && file.Session == personal.Session)
            {
                ReplaceSound(file.Kind, FishingSoundNotifications.Settings(draft, file.Kind) with { CustomPath = file.Path });
                changed = true;
            }
        }
        if (!ImGui.CollapsingHeader(UiText.Label(T.FishingSoundsHeading, "FishingSounds"))) return changed;
        ImGui.TextWrapped(UiText.Get(T.FishingSoundsHint));
        foreach (var kind in Enum.GetValues<FishingSoundKind>())
        {
            ImGui.PushID((int)kind);
            var rule = FishingSoundNotifications.Settings(draft, kind);
            ImGui.TextUnformatted(UiText.Get(kind switch
            {
                FishingSoundKind.Bite => T.FishingSoundBite,
                FishingSoundKind.BigFish => T.FishingSoundBigFish,
                _ => T.FishingSoundLegendaryFish,
            }));
            var sound = rule.Sound;
            var names = Enumerable.Range(0, CustomAlertSound.Choice + 1).Select(value => value == 0
                ? UiText.Get(T.AlertSoundNone) : value == CustomAlertSound.Choice ? UiText.Get(T.CustomSound)
                : UiText.Get(T.AlertSoundNumber, value)).ToArray();
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
            changed |= ImGui.Combo(UiText.Label(T.AlertSound, "FishingSoundChoice"), ref sound, names, names.Length);
            ImGui.BeginDisabled(!personal.HasCharacter || sound == 0);
            if (ImGui.Button(UiText.Label(T.AlertSoundPreview, "PreviewFishingSound"))) fishingSounds.Preview(kind, personal.Session);
            ImGui.EndDisabled();
            var firstOnly = rule.FirstCatchOnly;
            if (kind != FishingSoundKind.Bite)
                changed |= ImGui.Checkbox(UiText.Label(T.FishingSoundFirstCatchOnly, "FirstCatchOnly"), ref firstOnly);
            if (sound == CustomAlertSound.Choice)
            {
                if (rule.CustomPath.Length > 0) ImGui.TextWrapped(rule.CustomPath);
                if (ImGui.Button(UiText.Label(T.CustomSoundChoose, "ChooseFishingSound")))
                {
                    var expectedSession = personal.Session;
                    soundFiles.OpenFileDialog(UiText.Get(T.CustomSoundChoose), ".wav,.WAV", (bool ok, string path) =>
                    {
                        if (!disposed && ok && preferences.View.Session == expectedSession)
                            selectedFile = (kind, expectedSession, path);
                    });
                }
                if (ImGui.Button(UiText.Label(T.CustomSoundReload, "ReloadFishingSound"))) fishingSounds.Reload(kind, personal.Session);
                ImGui.SameLine();
                if (ImGui.Button(UiText.Label(T.CustomSoundStop, "StopFishingSound"))) fishingSounds.Stop(kind, personal.Session);
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
                ImGui.TextWrapped(UiText.Get(T.CustomSoundHint, CustomAlertSound.MaxSeconds, CustomAlertSound.MaxMegabytes));
            }
            ReplaceSound(kind, rule with { Sound = sound, FirstCatchOnly = firstOnly });
            ImGui.Separator();
            ImGui.PopID();
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

    public void Dispose() { disposed = true; selectedFile = null; soundFiles.Reset(); }

    private bool DrawPresets()
    {
        var changed = false;
        ImGui.TextUnformatted(UiText.Get(T.TimerAlertPresets));
        var selected = draft.Presets.FirstOrDefault(preset => preset.Id == selectedPreset);
        ImGui.SetNextItemWidth(Math.Max(80, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeightWithSpacing()));
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
        if (UiWidgets.IconButton(FontAwesomeIcon.Minus, "DeleteTimerPreset", UiText.Get(T.PresetDelete)))
        {
            draft.Presets.RemoveAll(preset => preset.Id == selectedPreset);
            selectedPreset = null;
            changed = true;
        }
        ImGui.EndDisabled();
        selected = draft.Presets.FirstOrDefault(preset => preset.Id == selectedPreset);
        ImGui.BeginDisabled(selected is null);
        if (ImGui.Button(UiText.Label(T.TimerAlertLoadPreset, "LoadTimerPreset")) && selected is not null)
        {
            draft = draft with { Alerts = [.. selected.Alerts] };
            changed = true;
        }
        ImGui.EndDisabled();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("###TimerPresetName", UiText.Get(T.PresetName), ref presetName, CastingTimerSettings.MaxNameLength + 1);
        var name = presetName.Trim();
        var existing = draft.Presets.FindIndex(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        ImGui.BeginDisabled(!CastingTimerSettings.ValidName(name) || name.Length == 0 ||
            existing < 0 && draft.Presets.Count >= CastingTimerSettings.MaxPresets);
        if (ImGui.Button(UiText.Label(T.TimerAlertSavePreset, "SaveTimerPreset")))
        {
            var preset = new CastingTimerPreset(existing >= 0 ? draft.Presets[existing].Id : Guid.NewGuid(), name, [.. draft.Alerts]);
            if (existing >= 0) draft.Presets[existing] = preset;
            else draft.Presets.Add(preset);
            selectedPreset = preset.Id;
            changed = true;
        }
        ImGui.EndDisabled();
        UiWidgets.Tooltip(UiText.Get(T.TimerAlertPresetHint, CastingTimerSettings.MaxPresets));
        return changed;
    }
}
