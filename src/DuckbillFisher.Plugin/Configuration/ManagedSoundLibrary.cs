using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

/// <summary>Bounded imports on workers; preference changes and playback reloads stay on Framework.</summary>
internal sealed class ManagedSoundLibrary : IDisposable
{
    private readonly FishPreferences preferences;
    private readonly ManagedSoundFiles files;
    private readonly IFramework framework;
    private readonly object gate = new();
    private readonly Dictionary<SoundSlot, Import> imports = [];
    private readonly Dictionary<SoundSlot, string> attempted = [];
    private readonly Dictionary<SoundSlot, UiMessage> messages = [];
    private CancellationTokenSource cancellation = new();
    private int session = -1, restoreRevision = -1;
    private bool disposed;
    private bool paused;
    private Task draining = Task.CompletedTask;
    public Action<SoundSlot, int>? Reload { get; set; }

    public ManagedSoundLibrary(FishPreferences preferences, ManagedSoundFiles files, IFramework framework)
    {
        this.preferences = preferences;
        this.files = files;
        this.framework = framework;
        framework.Update += OnUpdate;
    }

    public UiMessage? Message(SoundSlot slot) { lock (gate) return messages.GetValueOrDefault(slot); }
    public bool Busy(SoundSlot slot) { lock (gate) return paused || !draining.IsCompleted || imports.ContainsKey(slot); }
    public Task PauseAsync()
    {
        lock (gate)
        {
            paused = true;
            cancellation.Cancel(); cancellation.Dispose(); cancellation = new();
            var pending = imports.ToArray();
            var previous = draining;
            draining = Task.Run(async () => { await previous; await DrainAsync(pending); });
            imports.Clear(); messages.Clear();
            return draining;
        }
    }
    public void Resume() { lock (gate) { if (disposed) return; paused = false; attempted.Clear(); } }

    public void ImportFile(SoundSlot slot, string source, int expectedSession)
    {
        var personal = preferences.View;
        if (!personal.CanEdit || personal.Session != expectedSession || !ManagedSoundFiles.UsesCustomSound(personal, slot)) return;
        lock (gate)
        {
            if (disposed || paused || !draining.IsCompleted || session != expectedSession || imports.ContainsKey(slot)) return;
            var request = new Import(ManagedSoundFiles.Selected(personal, slot), expectedSession, personal.SettingsRestoreRevision);
            imports[slot] = request;
            messages[slot] = new(TextKey.ManagedSoundCopying);
            attempted[slot] = request.Previous;
            var token = cancellation.Token;
            request.Work = Task.Run(() => CopyAsync(slot, source, token));
        }
    }

    private async Task<CopyResult> CopyAsync(SoundSlot slot, string source, CancellationToken token)
    {
        byte[]? previous = null;
        var written = false;
        try
        {
            var bytes = await ManagedSoundFiles.ReadAsync(source, token).ConfigureAwait(false);
            previous = await files.PreviousAsync(slot, token).ConfigureAwait(false);
            await files.StoreAsync(slot, bytes, token).ConfigureAwait(false);
            written = true;
            token.ThrowIfCancellationRequested();
            return new(true, previous);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException or OperationCanceledException)
        {
            if (written) return await UndoAsync(slot, previous);
            return new(false, null);
        }
    }

    private async Task<CopyResult> UndoAsync(SoundSlot slot, byte[]? previous)
    {
        try { await files.RollbackAsync(slot, previous); return new(false, null); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException) { return new(false, null, true); }
    }

    private async Task DrainAsync(KeyValuePair<SoundSlot, Import>[] pending)
    {
        foreach (var (slot, request) in pending)
        {
            var result = await request.Work;
            if (result.Success) result = await UndoAsync(slot, result.Previous);
            if (result.RollbackFailed)
                lock (gate) if (!disposed) messages[slot] = new(TextKey.BackupRollbackFailed);
        }
    }

    private void OnUpdate(IFramework _)
    {
        var personal = preferences.View;
        if (!personal.CanEdit) return;
        List<(SoundSlot Slot, Import Request)> completed;
        lock (gate)
        {
            if (disposed || paused) return;
            if (session != personal.Session || restoreRevision != personal.SettingsRestoreRevision)
            {
                cancellation.Cancel();
                cancellation.Dispose();
                cancellation = new();
                var pending = imports.ToArray();
                var previous = draining;
                draining = Task.Run(async () => { await previous; await DrainAsync(pending); });
                imports.Clear(); attempted.Clear(); messages.Clear();
                session = personal.Session; restoreRevision = personal.SettingsRestoreRevision;
            }
            if (!draining.IsCompleted) return;
            completed = imports.Where(pair => pair.Value.Work.IsCompleted).Select(pair => (pair.Key, pair.Value)).ToList();
        }
        foreach (var (slot, request) in completed)
        {
            var copied = request.Work.GetAwaiter().GetResult();
            bool applied;
            lock (gate)
            {
                // Pause/reset must not roll back a file while Framework is committing its preference.
                if (disposed || paused || !imports.TryGetValue(slot, out var current) || !ReferenceEquals(current, request)) continue;
                applied = copied.Success && preferences.SetManagedSoundPath(slot, files.FilePath(slot), request.Previous,
                    request.Session, request.RestoreRevision);
                if (copied.Success && !applied)
                {
                    request.Work = Task.Run(() => UndoAsync(slot, copied.Previous));
                    continue;
                }
                imports.Remove(slot);
                messages[slot] = new(applied ? TextKey.ManagedSoundSaved : copied.RollbackFailed
                    ? TextKey.BackupRollbackFailed : TextKey.ManagedSoundFailed);
            }
            if (applied) Reload?.Invoke(slot, request.Session);
        }
        // Adopt only selected custom sounds once per path/session. Dormant paths are left alone.
        foreach (var slot in ManagedSoundFiles.Slots)
        {
            var current = preferences.View;
            if (!ManagedSoundFiles.UsesCustomSound(current, slot)) continue;
            var path = ManagedSoundFiles.Selected(current, slot);
            lock (gate)
                if (path.Length == 0 || files.IsManaged(slot, path) || imports.ContainsKey(slot) ||
                    attempted.GetValueOrDefault(slot) == path) continue;
            ImportFile(slot, path, personal.Session);
        }
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        Task pending;
        lock (gate)
        {
            disposed = true;
            cancellation.Cancel(); cancellation.Dispose();
            var requests = imports.ToArray();
            var previous = draining;
            pending = Task.Run(async () => { await previous; await DrainAsync(requests); });
            imports.Clear(); messages.Clear(); attempted.Clear(); Reload = null;
        }
        pending.GetAwaiter().GetResult();
    }

    private sealed class Import(string previous, int session, int restoreRevision)
    {
        public string Previous { get; } = previous;
        public int Session { get; } = session;
        public int RestoreRevision { get; } = restoreRevision;
        public Task<CopyResult> Work { get; set; } = Task.FromResult(new CopyResult(false, null));
    }
    private sealed record CopyResult(bool Success, byte[]? Previous, bool RollbackFailed = false);
}
