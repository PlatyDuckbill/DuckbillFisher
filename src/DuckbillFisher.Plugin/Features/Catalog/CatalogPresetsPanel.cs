using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class CatalogPresetsPanel(FishPreferences preferences)
{
    private string name = "";
    private string? savingName;
    private Guid deleting;

    public CatalogSettings? Draw(FishPreferencesView view, CatalogSettings current)
    {
        if (savingName is not null && view.CatalogPresets.Any(value => value.Name == savingName))
        {
            name = "";
            savingName = null;
        }
        if (ImGui.Button(UiText.Label(T.PresetButton, "CatalogPresets"))) ImGui.OpenPopup("CatalogPresetsPopup");
        UiWidgets.Tooltip(UiText.Get(T.PresetScope));
        ImGui.SetNextWindowSizeConstraints(new Vector2(ImGui.GetFontSize() * 22, 0),
            new Vector2(ImGui.GetFontSize() * 30, ImGui.GetMainViewport().WorkSize.Y * 0.8f));
        if (!ImGui.BeginPopup("CatalogPresetsPopup")) return null;
        CatalogSettings? selected = null;
        ImGui.TextDisabled(UiText.Get(T.PresetCount, view.CatalogPresets.Count, CatalogPreset.MaxCount));
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("###PresetName", UiText.Get(T.PresetName), ref name, 128);
        var trimmed = name.Trim();
        var duplicate = view.CatalogPresets.Any(value => string.Equals(value.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        var full = view.CatalogPresets.Count >= CatalogPreset.MaxCount;
        ImGui.BeginDisabled(!view.CanEdit || !CatalogPreset.ValidName(trimmed) || duplicate || full);
        if (ImGui.Button(UiText.Label(T.PresetSaveCurrent, "SaveCatalogPreset")))
        {
            preferences.SaveCatalogPreset(trimmed, current);
            savingName = trimmed;
        }
        ImGui.EndDisabled();
        UiWidgets.Tooltip(UiText.Get(T.PresetScope));
        if (full) ImGui.TextWrapped(UiText.Get(T.PresetLimit, CatalogPreset.MaxCount));
        else if (duplicate) ImGui.TextWrapped(UiText.Get(T.PresetDuplicateName));
        else if (trimmed.Length > 0 && !CatalogPreset.ValidName(trimmed))
            ImGui.TextWrapped(UiText.Get(T.PresetInvalidName, CatalogPreset.MaxNameLength));
        if (view.CatalogPresetError is { } error) ImGui.TextWrapped(error.Display);
        if (view.Error is { } storageError) ImGui.TextWrapped(storageError.Display);
        ImGui.Separator();
        var requestDelete = false;
        if (view.CatalogPresets.Count == 0) ImGui.TextDisabled(UiText.Get(T.PresetEmpty));
        if (ImGui.BeginTable("SavedCatalogPresets", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
            foreach (var preset in view.CatalogPresets)
            {
                ImGui.PushID(preset.Id.ToString("N"));
                ImGui.TableNextColumn();
                if (ImGui.Selectable("###LoadPreset", current.Matches(preset.Settings), ImGuiSelectableFlags.None,
                    new Vector2(0, ImGui.GetFrameHeight())))
                {
                    selected = preset.Settings.Copy();
                    ImGui.CloseCurrentPopup();
                }
                // User names are literal text, never ImGui labels or identifiers.
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                ImGui.GetWindowDrawList().PushClipRect(min, max, true);
                ImGui.GetWindowDrawList().AddText(min + new Vector2(4, ImGui.GetStyle().FramePadding.Y),
                    ImGui.GetColorU32(ImGuiCol.Text), preset.Name);
                ImGui.GetWindowDrawList().PopClipRect();
                UiWidgets.Tooltip(preset.Name);
                ImGui.TableNextColumn();
                ImGui.BeginDisabled(!view.CanEdit);
                if (UiWidgets.IconButton(FontAwesomeIcon.Trash, "DeletePreset", UiText.Get(T.PresetDelete)))
                {
                    deleting = preset.Id;
                    requestDelete = true;
                }
                ImGui.EndDisabled();
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (requestDelete) ImGui.OpenPopup("DeleteCatalogPreset");
        if (ImGui.BeginPopup("DeleteCatalogPreset"))
        {
            var preset = view.CatalogPresets.FirstOrDefault(value => value.Id == deleting);
            if (preset is null) ImGui.CloseCurrentPopup();
            else
            {
                ImGui.TextWrapped(UiText.Get(T.PresetDeleteQuestion, preset.Name));
                ImGui.BeginDisabled(!view.CanEdit);
                if (ImGui.Button(UiText.Label(T.PresetDelete, "ConfirmDeletePreset")))
                {
                    preferences.DeleteCatalogPreset(preset.Id);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button(UiText.Label(T.Cancel, "CancelDeletePreset"))) ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        ImGui.EndPopup();
        return selected;
    }
}
