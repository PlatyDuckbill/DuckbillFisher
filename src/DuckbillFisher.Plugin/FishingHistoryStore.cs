using System.Text.Json;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

internal enum FishingStorageState { Loading, Ready, ReadFailed, InvalidFile, WriteFailed }
internal sealed record FishingHistoryLoad(ulong Character, FishingAttempt[] Records, FishingStorageState State);

/// <summary>One coalescing local-file worker. Character paths and immutable save snapshots never cross.</summary>
internal sealed class FishingHistoryStore(string configDirectory) : IDisposable
{
    private const int Schema = 1;
    private const long MaxFileBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string directory = Path.Combine(configDirectory, "fishing-history");
    private readonly object gate = new();
    private readonly Dictionary<ulong, FishingAttempt[]> pendingWrites = [];
    private readonly Dictionary<ulong, FishingStorageState> states = [];
    private ulong? pendingLoad;
    private FishingHistoryLoad? loaded;
    private Task? worker;
    private bool running;
    private bool disposed;

    public void Load(ulong character)
    {
        lock (gate)
        {
            if (disposed || character == 0) return;
            pendingLoad = character;
            loaded = null;
            states[character] = FishingStorageState.Loading;
            EnsureWorker();
        }
    }

    public bool TryTakeLoad(ulong character, out FishingHistoryLoad result)
    {
        lock (gate)
        {
            result = loaded!;
            if (loaded?.Character != character) return false;
            loaded = null;
            return true;
        }
    }

    public FishingStorageState State(ulong character)
    {
        lock (gate) return states.GetValueOrDefault(character, FishingStorageState.Loading);
    }

    public void Save(ulong character, IReadOnlyList<FishingAttempt> records)
    {
        lock (gate)
        {
            if (disposed || character == 0) return;
            pendingWrites[character] = records.Take(FishingAttempts.Capacity).ToArray();
            EnsureWorker();
        }
    }

    private void EnsureWorker()
    {
        if (running) return;
        running = true;
        worker = Task.Run(Process);
    }

    private void Process()
    {
        while (true)
        {
            ulong character;
            FishingAttempt[]? save = null;
            lock (gate)
            {
                if (pendingWrites.Count > 0)
                {
                    var entry = pendingWrites.First();
                    character = entry.Key;
                    save = entry.Value;
                    pendingWrites.Remove(character);
                }
                else if (pendingLoad is { } requested)
                {
                    character = requested;
                    pendingLoad = null;
                }
                else { running = false; return; }
            }
            if (save is not null)
            {
                var state = Write(character, save);
                lock (gate) states[character] = state;
            }
            else
            {
                var result = Read(character);
                lock (gate)
                {
                    loaded = result;
                    states[character] = result.State;
                }
            }
        }
    }

    private string FilePath(ulong character) => Path.Combine(directory, $"{character:X16}.json");

    private FishingHistoryLoad Read(ulong character)
    {
        try
        {
            var path = FilePath(character);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxFileBytes) return new(character, [], FishingStorageState.InvalidFile);
            var document = JsonSerializer.Deserialize<Document>(stream, Json);
            if (document is null || document.Version != Schema || document.Records is null ||
                document.Records.Length > FishingAttempts.Capacity || document.Records.Any(row => !Valid(row)) ||
                document.Records.Select(row => row.Id).Distinct().Count() != document.Records.Length)
                return new(character, [], FishingStorageState.InvalidFile);
            return new(character, document.Records, FishingStorageState.Ready);
        }
        catch (FileNotFoundException) { return new(character, [], FishingStorageState.Ready); }
        catch (DirectoryNotFoundException) { return new(character, [], FishingStorageState.Ready); }
        catch (JsonException) { return new(character, [], FishingStorageState.InvalidFile); }
        catch (NotSupportedException) { return new(character, [], FishingStorageState.InvalidFile); }
        catch (IOException) { return new(character, [], FishingStorageState.ReadFailed); }
        catch (UnauthorizedAccessException) { return new(character, [], FishingStorageState.ReadFailed); }
    }

    private FishingStorageState Write(ulong character, FishingAttempt[] records)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = FilePath(character);
            var temporary = path + ".tmp";
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Document(Schema, records), Json);
            if (bytes.LongLength > MaxFileBytes) return FishingStorageState.WriteFailed;
            // Same-directory replacement; a failed write does not first delete the last good file.
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
            return FishingStorageState.Ready;
        }
        catch (IOException) { return FishingStorageState.WriteFailed; }
        catch (UnauthorizedAccessException) { return FishingStorageState.WriteFailed; }
    }

    private static bool Valid(FishingAttempt? row) => row is not null && row.Id != Guid.Empty &&
        row.SessionId != Guid.Empty && row.TerritoryId != 0 && Enum.IsDefined(row.Outcome) &&
        Enum.IsDefined(row.EndReason) && Enum.IsDefined(row.BaitKind) &&
        Duration(row.DurationSeconds) && Duration(row.BiteSeconds) && Duration(row.HookSeconds) &&
        (row.Quantity is null or > 0) && (row.BaitItemId is null or > 0) && (row.SpotId is null or > 0) &&
        (row.FishItemId is null or > 0) && (row.Outcome != FishingOutcome.Caught || row.FishItemId is > 0 && row.Quantity is > 0);
    private static bool Duration(double? value) => value is null || double.IsFinite(value.Value) && value >= 0;

    public void Dispose()
    {
        Task? drain;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            pendingLoad = null;
            drain = worker;
        }
        // The producer queues its final interruption before disposal; do not abandon that write on unload.
        drain?.GetAwaiter().GetResult();
    }

    private sealed record Document(int Version, FishingAttempt[] Records);
}
