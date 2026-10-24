using System.Text.Json;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

internal sealed record SettingsBackupView(bool Busy, string? Path, UiMessage? Message,
    DateTimeOffset? CreatedUtc, int Favorites, int Presets, int Sounds, bool CanRestore)
{
    public int PersonalRecords { get; init; }
    public int SourceSettingsVersion { get; init; }
}

internal sealed class SettingsBackup : IDisposable
{
    private readonly FishPreferences preferences;
    private readonly ManagedSoundFiles files;
    private readonly ManagedSoundLibrary library;
    private readonly IFramework framework;
    private readonly object gate = new();
    private CancellationTokenSource cancellation = new();
    private Task? work;
    private PreparedSettingsBackup? prepared;
    private SettingsBackupView view = new(false, null, null, null, 0, 0, 0, false);
    private int generation, session;
    private bool disposed;
    public Action<int>? Restored { get; set; }

    public SettingsBackup(FishPreferences preferences, ManagedSoundFiles files, ManagedSoundLibrary library, IFramework framework)
    {
        this.preferences = preferences;
        this.files = files;
        this.library = library;
        this.framework = framework;
        session = preferences.View.Session;
        framework.Update += OnUpdate;
    }
    public SettingsBackupView View
    {
        get { lock (gate) return view with { Busy = work is { IsCompleted: false }, CanRestore = prepared is not null }; }
    }

    public void Export(string path, bool includeSounds)
    {
        var snapshot = preferences.ExportSettings();
        if (snapshot is null) return;
        Start(path, async (version, token) =>
        {
            await library.PauseAsync();
            try
            {
                await SettingsBackupFile.WriteAsync(path, snapshot, includeSounds, token);
                Set(version, new(false, path, new(TextKey.BackupExported), null, 0, 0, 0, false));
            }
            finally { library.Resume(); }
        });
    }

    public void Import(string path) => Start(path, async (version, token) =>
    {
        var result = await SettingsBackupFile.ReadAsync(path, token);
        lock (gate)
        {
            if (disposed || version != generation) return;
            prepared = result;
            view = new(false, path, null, result.Document.CreatedUtc, result.Document.Settings.Favorites.Count,
                result.Document.Settings.CatalogPresets.Count, result.Sounds.Count, true)
            {
                PersonalRecords = result.Document.Settings.PersonalRecords.Count,
                SourceSettingsVersion = result.Document.SettingsVersion,
            };
        }
    });

    public void Restore()
    {
        PreparedSettingsBackup backup;
        int expectedSession;
        string? selectedPath;
        lock (gate)
        {
            if (disposed || work is { IsCompleted: false } || prepared is null) return;
            backup = prepared;
            expectedSession = session;
            selectedPath = view.Path;
        }
        Start(selectedPath, async (version, token) =>
        {
            var previous = new Dictionary<SoundSlot, byte[]?>();
            var written = new List<SoundSlot>();
            var saved = false;
            await library.PauseAsync();
            try
            {
                foreach (var slot in backup.Sounds.Keys) previous[slot] = await files.PreviousAsync(slot, token);
                foreach (var (slot, bytes) in backup.Sounds)
                {
                    var destination = await files.StoreAsync(slot, bytes, token);
                    written.Add(slot);
                    ManagedSoundFiles.Select(backup.Document.Settings, slot, destination);
                }
                token.ThrowIfCancellationRequested();
                var committed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (!preferences.RestoreSettings(backup.Document.Settings, expectedSession, token, success =>
                    {
                        if (success)
                        {
                            lock (gate) if (!disposed) Restored?.Invoke(expectedSession);
                        }
                        committed.TrySetResult(success);
                    })) throw new BackupException(BackupIssue.FileAccess);
                // Preferences always completes the request on Framework or Dispose, even after cancellation.
                saved = await committed.Task;
                if (!saved) throw new BackupException(BackupIssue.FileAccess);
                Set(version, new(false, selectedPath, new(TextKey.BackupRestored), null, 0, 0, 0, false));
            }
            finally
            {
                var rollbackFailed = false;
                if (!saved)
                    foreach (var slot in written)
                        try { await files.RollbackAsync(slot, previous[slot]); }
                        catch (Exception error) when (IsFileError(error)) { rollbackFailed = true; }
                library.Resume();
                if (rollbackFailed) throw new BackupException(BackupIssue.RollbackFailed);
            }
        });
    }

    private void Start(string? path, Func<int, CancellationToken, Task> action)
    {
        lock (gate)
        {
            if (disposed || work is { IsCompleted: false }) return;
            prepared = null;
            var version = ++generation;
            var token = cancellation.Token;
            view = new(true, path, null, null, 0, 0, 0, false);
            work = Task.Run(async () =>
            {
                try { await action(version, token); }
                catch (OperationCanceledException) { }
                catch (Exception error) when (error is BackupException or JsonException || IsFileError(error))
                {
                    var issue = error is BackupException failure ? failure.Issue :
                        error is JsonException or InvalidDataException ? BackupIssue.Invalid : BackupIssue.FileAccess;
                    Set(version, new(false, path, new(issue switch
                    {
                        BackupIssue.Unsupported => TextKey.BackupUnsupported,
                        BackupIssue.Invalid => TextKey.BackupInvalid,
                        BackupIssue.RollbackFailed => TextKey.BackupRollbackFailed,
                        _ => TextKey.BackupFileError,
                    }), null, 0, 0, 0, false));
                }
            });
        }
    }

    private static bool IsFileError(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or System.Security.SecurityException;
    private void Set(int version, SettingsBackupView value)
    {
        lock (gate) if (!disposed && version == generation) view = value;
    }
    public void Cancel()
    {
        lock (gate)
        {
            ++generation;
            cancellation.Cancel(); cancellation.Dispose(); cancellation = new();
            prepared = null;
            view = new(false, null, null, null, 0, 0, 0, false);
        }
    }
    private void OnUpdate(IFramework _)
    {
        var current = preferences.View.Session;
        lock (gate)
        {
            if (disposed || current == session) return;
            Cancel();
            session = current;
        }
    }
    public void Dispose()
    {
        framework.Update -= OnUpdate;
        Task? pending;
        lock (gate)
        {
            disposed = true; generation++;
            cancellation.Cancel(); cancellation.Dispose();
            prepared = null; Restored = null;
            pending = work;
        }
        preferences.CancelSettingsRestore();
        pending?.GetAwaiter().GetResult();
    }
}
