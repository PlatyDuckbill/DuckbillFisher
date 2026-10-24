using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Globalization;
using Dalamud.Interface.ImGuiFileDialog;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class SettingsBackupPanel(SettingsBackup backup, FishPreferences preferences) : IDisposable
{
    private readonly FileDialogManager dialogs = new();
    private bool includeSounds = true, disposed;
    private int session = -1;

    public void Draw(FishPreferencesView personal)
    {
        var status = backup.View;
        UiWidgets.Heading(T.BackupHeading);
        ImGui.BeginDisabled(!personal.CanEdit || status.Busy);
        ImGui.Checkbox(UiText.Label(T.BackupIncludeSounds, "BackupIncludeSounds"), ref includeSounds);
        ImGui.TextWrapped(UiText.Get(includeSounds ? T.BackupZipSummary : T.BackupJsonSummary));
        ImGui.Spacing();
        var actionWidth = Math.Max(1, (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2);
        if (UiWidgets.ActionButton(FontAwesomeIcon.FileExport, T.BackupExport, "ExportSettings", actionWidth))
        {
            var expectedSession = personal.Session;
            var withSounds = includeSounds;
            var extension = withSounds ? ".zip" : ".json";
            dialogs.SaveFileDialog(UiText.Get(T.BackupExport), extension,
                "DuckbillFisher-backup-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture), extension, (ok, path) =>
                {
                    if (disposed || !ok || preferences.View.Session != expectedSession) return;
                    if (Path.GetExtension(path).Length == 0) path += extension;
                    backup.Export(path, withSounds);
                });
        }
        ImGui.SameLine();
        if (UiWidgets.ActionButton(FontAwesomeIcon.FileImport, T.BackupImport, "ImportSettings", actionWidth))
        {
            var expectedSession = personal.Session;
            dialogs.OpenFileDialog(UiText.Get(T.BackupImport), ".zip,.json", (bool ok, string path) =>
            {
                if (!disposed && ok && preferences.View.Session == expectedSession) backup.Import(path);
            });
        }
        ImGui.EndDisabled();
        if (ImGui.CollapsingHeader(UiText.Label(T.BackupHelp, "BackupHelp")))
        {
            ImGui.TextWrapped(UiText.Get(T.BackupScope));
            ImGui.TextWrapped(UiText.Get(includeSounds ? T.BackupZipHint : T.BackupJsonHint));
        }
        if (status.Path is { } selected)
        {
            ImGui.TextWrapped(Path.GetFileName(selected));
            UiWidgets.Tooltip(selected);
        }
        if (status.Busy) ImGui.TextWrapped(UiText.Get(T.BackupWorking));
        if (status.Message is { } message) ImGui.TextWrapped(message.Display);
        if (!status.CanRestore) return;
        var created = status.CreatedUtc.GetValueOrDefault().ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        ImGui.TextWrapped(UiText.Get(T.BackupPreview, created, status.Favorites, status.Presets, status.Sounds));
        ImGui.TextWrapped(UiText.Get(T.BackupReplaceHint));
        ImGui.BeginDisabled(!personal.CanEdit || status.Busy);
        if (UiWidgets.ActionButton(FontAwesomeIcon.Check, T.BackupRestore, "ConfirmSettingsRestore", actionWidth)) backup.Restore();
        ImGui.SameLine();
        if (UiWidgets.ActionButton(FontAwesomeIcon.Times, T.BackupCancel, "CancelSettingsRestore", actionWidth)) backup.Cancel();
        ImGui.EndDisabled();
    }

    public void DrawDialogs()
    {
        var current = preferences.View.Session;
        if (session != current) { dialogs.Reset(); session = current; }
        dialogs.Draw();
    }
    public void CloseDialogs() => dialogs.Reset();
    public void Dispose() { disposed = true; dialogs.Reset(); }
}
