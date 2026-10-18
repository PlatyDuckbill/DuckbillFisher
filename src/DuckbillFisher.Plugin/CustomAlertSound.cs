using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Plugin.Services;

namespace DuckbillFisher.Plugin;

internal enum CustomSoundState { Disabled, NoFile, Loading, Ready, FileUnavailable, InvalidFile, LimitExceeded, AudioUnavailable }
internal sealed record CustomSoundView(CustomSoundState State, uint ErrorCode = 0);

/// <summary>Local WAV only. A dedicated thread owns its MCI alias; no game APIs or global audio stop.</summary>
internal sealed class CustomAlertSound : IDisposable
{
    public const int Choice = 17;
    public const int MaxMegabytes = 10;
    public const int MaxSeconds = 30;
    private readonly object gate = new();
    private readonly AutoResetEvent wake = new(false);
    private readonly Thread worker;
    private readonly IPluginLog log;
    private readonly string alias = "Duckbill" + Guid.NewGuid().ToString("N");
    private readonly string cachePath = Path.Combine(Path.GetTempPath(), "DuckbillFisher-" + Guid.NewGuid().ToString("N") + ".wav");
    private CustomSoundView view = new(CustomSoundState.Disabled);
    private string path = "";
    private int session = -1, generation;
    private bool enabled, reload, stop, disposed;
    private (int Generation, long At)? play;
    // Accessed exclusively by worker.
    private bool opened;
    private bool ownsCache;

    public CustomAlertSound(IPluginLog log)
    {
        this.log = log;
        worker = new Thread(Run) { IsBackground = true, Name = "DuckbillFisher custom sound" };
        worker.Start();
    }

    public CustomSoundView View { get { lock (gate) return view; } }

    public void Configure(bool active, string file, int currentSession, bool force = false)
    {
        lock (gate)
        {
            if (disposed || !force && enabled == active && path == file && session == currentSession) return;
            enabled = active; path = file; session = currentSession; generation++;
            reload = true; play = null; stop = false;
            view = new(!active ? CustomSoundState.Disabled : file.Length == 0 ? CustomSoundState.NoFile : CustomSoundState.Loading);
            wake.Set();
        }
    }

    // A true result means queued, not confirmed playback. Never replay delayed requests.
    public bool TryPlay(int expectedSession)
    {
        lock (gate)
        {
            if (disposed || !enabled || session != expectedSession || view.State != CustomSoundState.Ready) return false;
            play = (generation, Stopwatch.GetTimestamp());
            wake.Set();
            return true;
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            if (disposed) return;
            play = null; stop = true;
            wake.Set();
        }
    }

    private void Run()
    {
        try
        {
            while (true)
            {
                wake.WaitOne();
                bool load, halt, active;
                string file;
                int version;
                (int Generation, long At)? request;
                lock (gate)
                {
                    if (disposed) return;
                    load = reload; reload = false;
                    halt = stop; stop = false;
                    request = play; play = null;
                    active = enabled; file = path; version = generation;
                }
                try
                {
                    if (load)
                    {
                        Close();
                        var result = active && file.Length > 0 ? Load(file) : new CustomSoundView(
                            active ? CustomSoundState.NoFile : CustomSoundState.Disabled);
                        lock (gate)
                        {
                            if (!disposed && generation == version) view = result;
                        }
                        // Settings/session may have changed during file IO. Close stale audio
                        // before waiting again; the newer Configure already signalled wake.
                        bool stale;
                        lock (gate) stale = disposed || generation != version;
                        if (stale) { Close(); continue; }
                    }
                    if (halt && opened) Send($"stop {alias}");
                    if (request is not { } next || Stopwatch.GetElapsedTime(next.At).TotalSeconds > 2) continue;
                    lock (gate)
                        if (disposed || !enabled || generation != next.Generation || view.State != CustomSoundState.Ready) continue;
                    if (!opened) continue;
                    var error = Send($"stop {alias}");
                    if (error == 0) error = Send($"play {alias} from 0");
                    if (error != 0)
                    {
                        Close();
                        lock (gate)
                            if (generation == next.Generation) view = new(CustomSoundState.AudioUnavailable, error);
                        log.Warning("CustomAlertSoundPlaybackFailed: Code={Code}", error);
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
                    NotSupportedException or System.Security.SecurityException or DllNotFoundException or EntryPointNotFoundException)
                {
                    Close();
                    lock (gate)
                        if (!disposed && generation == version) view = new(error is IOException or UnauthorizedAccessException
                            ? CustomSoundState.FileUnavailable : CustomSoundState.AudioUnavailable);
                    log.Warning("CustomAlertSoundFailed: Type={Type}", error.GetType().Name);
                }
            }
        }
        finally { Close(); }
    }

    private CustomSoundView Load(string file)
    {
        // Reject remote/command-like paths before passing a quoted name to MCI.
        if (file.Length > 4096 || file.Contains('"') || file.Any(char.IsControl) ||
            !Path.IsPathFullyQualified(file) || file.StartsWith(@"\", StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(file), ".wav", StringComparison.OrdinalIgnoreCase))
            return new(CustomSoundState.InvalidFile);
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length > MaxMegabytes * 1024 * 1024) return new(CustomSoundState.LimitExceeded);
            Span<byte> header = stackalloc byte[12];
            if (stream.Read(header) != header.Length || !header[..4].SequenceEqual("RIFF"u8) || !header[8..].SequenceEqual("WAVE"u8))
                return new(CustomSoundState.InvalidFile);
            // MCI holds its file open. Keep the original editable and own only this copy.
            stream.Position = 0;
            using var cache = new FileStream(cachePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            ownsCache = true;
            stream.CopyTo(cache);
        }
        var error = Send($"open \"{cachePath}\" type waveaudio alias {alias}");
        if (error != 0) { Close(); return new(CustomSoundState.AudioUnavailable, error); }
        opened = true;
        error = Send($"set {alias} time format milliseconds");
        var length = new StringBuilder(32);
        if (error == 0) error = MciSendString($"status {alias} length", length, length.Capacity, nint.Zero);
        if (error != 0) { Close(); return new(CustomSoundState.AudioUnavailable, error); }
        if (!int.TryParse(length.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds) || milliseconds <= 0)
        { Close(); return new(CustomSoundState.InvalidFile); }
        if (milliseconds > MaxSeconds * 1000) { Close(); return new(CustomSoundState.LimitExceeded); }
        return new(CustomSoundState.Ready);
    }

    private static uint Send(string command) => MciSendString(command, null, 0, nint.Zero);

    private void Close()
    {
        if (opened)
        {
            var error = Send($"close {alias}");
            if (error != 0)
            {
                log.Warning("CustomAlertSoundCloseFailed: Code={Code}", error);
                return; // Retain ownership so a later close can retry; never delete an open copy.
            }
            opened = false;
        }
        if (!ownsCache) return;
        try { File.Delete(cachePath); ownsCache = false; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { log.Warning("CustomAlertSoundCacheCleanupFailed: Type={Type}", error.GetType().Name); }
    }

    [DllImport("winmm.dll", EntryPoint = "mciSendStringW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MciSendString(string command, StringBuilder? result, int capacity, nint callback);

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; generation++; play = null;
            wake.Set();
        }
        worker.Join();
        wake.Dispose();
    }
}
