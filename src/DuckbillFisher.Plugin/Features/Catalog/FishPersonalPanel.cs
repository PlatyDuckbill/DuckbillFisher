using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Shared manual editor. Drafts contain text only; every durable edit goes through preferences.</summary>
internal sealed class FishPersonalPanel(FishPreferences preferences)
{
    private const int MaxDrafts = 64;
    private readonly Dictionary<uint, NoteDraft> drafts = [];
    private int session = -1, restoreRevision = -1;
    public Action? ManagePresets { get; set; }

    public void Draw(uint fishId, FishPreferencesView personal, bool editable)
    {
        if (session != personal.Session || restoreRevision != personal.SettingsRestoreRevision)
        {
            drafts.Clear(); session = personal.Session; restoreRevision = personal.SettingsRestoreRevision;
        }
        var record = personal.PersonalRecords.GetValueOrDefault(fishId) ?? new();
        if (drafts.TryGetValue(fishId, out var existing)) existing.Sync(record.Note);
        var dirty = existing?.Dirty == true;
        var heading = UiText.Get(dirty ? T.FishPersonalHeadingDraft : record.Note.Length > 0
            ? T.FishPersonalHeadingNote : T.FishPersonalHeading, record.Attempts);
        var open = ImGui.CollapsingHeader(heading + "###FishPersonalRecord");
        UiWidgets.Tooltip(heading);
        if (!open) return;
        var canEdit = personal.CanEdit && editable;
        ImGui.TextDisabled(UiText.Get(T.FishPersonalScope));
        var draft = GetDraft(fishId, record.Note);
        if (draft is null) ImGui.TextWrapped(UiText.Get(T.FishNoteDraftLimit, MaxDrafts));
        else DrawNote(fishId, record, draft, personal, canEdit);
        ImGui.Spacing();
        var selected = personal.Timer.Presets.FirstOrDefault(preset => preset.Id == record.TimerPresetId);
        ImGui.BeginDisabled(!canEdit);
        if (ImGui.BeginTable("FishPersonalFields", 2, ImGuiTableFlags.SizingFixedFit))
        {
            var labelWidth = Math.Max(ImGui.CalcTextSize(UiText.Get(T.FishAttempts)).X, ImGui.CalcTextSize(UiText.Get(T.FishPreset)).X);
            ImGui.TableSetupColumn("Setting", ImGuiTableColumnFlags.WidthFixed, labelWidth);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(UiText.Get(T.FishAttempts));
            UiWidgets.Tooltip(UiText.Get(T.FishAttemptsHint, FishPersonalRecord.MaxAttempts));
            ImGui.TableNextColumn();
            var valueWidth = Math.Max(1, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() * 2 - ImGui.GetStyle().ItemSpacing.X * 2);
            ImGui.BeginDisabled(record.Attempts == 0);
            if (UiWidgets.IconButton(FontAwesomeIcon.Minus, "DecreaseAttempts", UiText.Get(T.FishAttemptsDecrease)))
                preferences.AdjustFishAttempts(fishId, -1, personal.Session, personal.SettingsRestoreRevision);
            ImGui.EndDisabled();
            ImGui.SameLine();
            var attempts = record.Attempts;
            ImGui.SetNextItemWidth(valueWidth);
            if (ImGui.InputInt("###FishAttempts", ref attempts, 0, 0))
                preferences.SetFishAttempts(fishId, Math.Clamp(attempts, 0, FishPersonalRecord.MaxAttempts), personal.Session, personal.SettingsRestoreRevision);
            ImGui.SameLine();
            ImGui.BeginDisabled(record.Attempts == FishPersonalRecord.MaxAttempts);
            if (UiWidgets.IconButton(FontAwesomeIcon.Plus, "IncreaseAttempts", UiText.Get(T.FishAttemptsIncrease)))
                preferences.AdjustFishAttempts(fishId, 1, personal.Session, personal.SettingsRestoreRevision);
            ImGui.EndDisabled();
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(UiText.Get(T.FishPreset));
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("###FishTimerPreset", selected?.Name ?? UiText.Get(T.FishPresetNone)))
            {
                if (ImGui.Selectable(UiText.Label(T.FishPresetNone, "NoFishPreset"), record.TimerPresetId is null))
                    preferences.SetFishTimerPreset(fishId, null, personal.Session, personal.SettingsRestoreRevision);
                foreach (var preset in personal.Timer.Presets)
                {
                    if (ImGui.Selectable("###FishPreset" + preset.Id.ToString("N"), preset.Id == record.TimerPresetId,
                        ImGuiSelectableFlags.None, new Vector2(0, ImGui.GetFrameHeight())))
                        preferences.SetFishTimerPreset(fishId, preset.Id, personal.Session, personal.SettingsRestoreRevision);
                    var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
                    var draw = ImGui.GetWindowDrawList();
                    draw.PushClipRect(min, max, true);
                    draw.AddText(min + new Vector2(4, ImGui.GetStyle().FramePadding.Y), ImGui.GetColorU32(ImGuiCol.Text), preset.Name);
                    draw.PopClipRect();
                    UiWidgets.Tooltip(preset.Name);
                }
                ImGui.EndCombo();
            }
            if (selected is not null) UiWidgets.Tooltip(selected.Name);
            ImGui.EndTable();
        }
        ImGui.EndDisabled();
        var actionWidth = Math.Max(1, (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2);
        ImGui.BeginDisabled(!canEdit || selected is null);
        if (UiWidgets.ActionButton(FontAwesomeIcon.Play, T.FishPresetApply, "ApplyFishPreset", actionWidth))
            preferences.ApplyFishTimerPreset(fishId, personal.Session, personal.SettingsRestoreRevision);
        UiWidgets.Tooltip(UiText.Get(T.FishPresetApplyHint));
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (UiWidgets.ActionButton(FontAwesomeIcon.Cog, T.FishPresetManage, "ManageFishPreset", actionWidth)) ManagePresets?.Invoke();
        if (personal.Timer.Presets.Count == 0) ImGui.TextWrapped(UiText.Get(T.FishPresetEmpty));
        if (personal.PersonalResult is { } result && result.FishId == fishId &&
            (result.Message.Key != T.FishPresetApplied || selected is not null && personal.Timer.Alerts.SequenceEqual(selected.Alerts)))
            ImGui.TextWrapped(result.Message.Display);
    }

    private void DrawNote(uint fishId, FishPersonalRecord record, NoteDraft draft, FishPreferencesView personal, bool canEdit)
    {
        ImGui.TextUnformatted(UiText.Get(T.FishNote));
        ImGui.BeginDisabled(!canEdit);
        if (ImGui.InputTextMultiline("###FishNote", ref draft.Text, FishPersonalRecord.MaxNoteLength * 4 + 1,
            new Vector2(-1, ImGui.GetTextLineHeightWithSpacing() * 4), ImGuiInputTextFlags.AllowTabInput)) draft.Submitted = false;
        var valid = FishPersonalRecord.ValidNote(draft.Text);
        var saveWidth = Math.Max(1, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeightWithSpacing());
        ImGui.BeginDisabled(!draft.Dirty || !valid);
        if (UiWidgets.ActionButton(FontAwesomeIcon.Save, T.FishNoteSave, "SaveFishNote", saveWidth))
        {
            preferences.SaveFishNote(fishId, draft.Text, draft.Source, personal.Session, personal.SettingsRestoreRevision);
            draft.Submitted = true;
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!draft.Dirty);
        if (UiWidgets.IconButton(FontAwesomeIcon.Undo, "RevertFishNote", UiText.Get(T.FishNoteRevert)))
        {
            draft.Text = draft.Source = record.Note;
            draft.Submitted = false;
        }
        ImGui.EndDisabled();
        ImGui.EndDisabled();
        if (!valid) ImGui.TextWrapped(UiText.Get(T.FishNoteInvalid, FishPersonalRecord.MaxNoteLength));
        else
        {
            ImGui.TextDisabled(UiText.Get(T.FishNoteLength, draft.Text.Length, FishPersonalRecord.MaxNoteLength));
            if (draft.Dirty || draft.Submitted)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(UiText.Get(draft.Dirty ? T.FishNoteUnsaved : T.FishNoteSaved));
            }
        }
        var conflictShown = personal.PersonalResult is { } result && result.FishId == fishId && result.Message.Key == T.FishNoteConflict;
        if (record.Note != draft.Source && record.Note != draft.Text && !conflictShown)
            ImGui.TextWrapped(UiText.Get(T.FishNoteConflict));
    }

    private NoteDraft? GetDraft(uint fishId, string saved)
    {
        if (!drafts.TryGetValue(fishId, out var draft))
        {
            if (drafts.Count >= MaxDrafts)
            {
                var clean = drafts.FirstOrDefault(pair => !pair.Value.Dirty);
                if (clean.Value is null) return null;
                drafts.Remove(clean.Key);
            }
            drafts[fishId] = draft = new(saved);
        }
        draft.Sync(saved);
        return draft;
    }

    private sealed class NoteDraft(string saved)
    {
        public string Text = saved;
        public string Source = saved;
        public bool Submitted;
        public bool Dirty => Text != Source;
        public void Sync(string saved) { if (!Dirty || Text == saved) Text = Source = saved; }
    }
}
