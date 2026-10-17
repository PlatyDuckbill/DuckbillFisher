using System.Text.Json;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

internal enum FishingStorageState { Loading, Ready, ReadFailed, InvalidFile, WriteFailed }
internal sealed record FishingArchiveView(int Revision, IReadOnlyList<FishingTripSummary> Trips, FishingStorageState State);
internal sealed record FishingHistoryLoad(ulong Character, Guid TripId, FishingTripSummary? Summary,
    FishingAttempt[] Records, FishingStorageState State);

/// <summary>Single file worker. One atomic file per trip, cached summaries, details loaded on demand.</summary>
internal sealed class FishingHistoryStore(string configDirectory) : IDisposable
{
    private const int Schema = 2;
    private const long MaxFileBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string directory = Path.Combine(configDirectory, "fishing-history");
    private readonly object gate = new();
    private readonly Dictionary<(ulong Character, Guid Trip), Document> pendingWrites = [];
    private readonly HashSet<(ulong Character, Guid Trip)> pendingDeletes = [];
    private readonly HashSet<(ulong Character, Guid Trip)> tombstones = [];
    private readonly Dictionary<ulong, List<FishingTripSummary>> catalogs = [];
    private readonly Dictionary<ulong, FishingArchiveView> views = [];
    private ulong? pendingLoad;
    private (ulong Character, Guid Trip)? pendingDetail;
    private FishingHistoryLoad? loaded;
    private Task? worker;
    private bool running, disposed;
    private int revision;

    public void Load(ulong character)
    {
        lock (gate)
        {
            if (disposed || character == 0) return;
            pendingLoad = character;
            views[character] = new(++revision, [], FishingStorageState.Loading);
            EnsureWorker();
        }
    }

    public FishingArchiveView View(ulong character)
    {
        lock (gate) return views.GetValueOrDefault(character) ?? new(0, [], FishingStorageState.Loading);
    }
    public FishingStorageState State(ulong character) => View(character).State;

    public void RequestDetail(ulong character, Guid trip)
    {
        lock (gate)
        {
            if (disposed || trip == Guid.Empty || character == 0) return;
            pendingDetail = (character, trip);
            loaded = null;
            EnsureWorker();
        }
    }
    public bool TryTakeDetail(ulong character, Guid trip, out FishingHistoryLoad result)
    {
        lock (gate)
        {
            result = loaded!;
            if (loaded?.Character != character || loaded.TripId != trip) return false;
            loaded = null;
            return true;
        }
    }

    public void Save(ulong character, FishingTripSummary summary, IReadOnlyList<FishingAttempt> records)
    {
        lock (gate)
        {
            var key = (character, summary.Id);
            if (disposed || character == 0 || tombstones.Contains(key)) return;
            pendingWrites[key] = new(Schema, summary, records.Take(FishingAttempts.Capacity).ToArray());
            EnsureWorker();
        }
    }
    public void Delete(ulong character, IEnumerable<Guid> trips)
    {
        lock (gate)
        {
            if (disposed || character == 0) return;
            foreach (var trip in trips)
            {
                if (trip == Guid.Empty) continue;
                var key = (character, trip);
                tombstones.Add(key);
                pendingWrites.Remove(key);
                pendingDeletes.Add(key);
            }
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
            Guid trip = Guid.Empty;
            Document? save = null;
            var kind = 0;
            lock (gate)
            {
                if (pendingDeletes.Count > 0)
                {
                    var key = pendingDeletes.First();
                    pendingDeletes.Remove(key);
                    character = key.Character; trip = key.Trip; kind = 1;
                }
                else if (pendingWrites.Count > 0)
                {
                    var entry = pendingWrites.First();
                    pendingWrites.Remove(entry.Key);
                    character = entry.Key.Character; trip = entry.Key.Trip; save = entry.Value; kind = 2;
                }
                else if (pendingLoad is { } target) { character = target; pendingLoad = null; kind = 3; }
                else if (pendingDetail is { } detail)
                { character = detail.Character; trip = detail.Trip; pendingDetail = null; kind = 4; }
                else { running = false; return; }
            }
            if (kind == 1)
            {
                var state = DeleteFile(character, trip);
                lock (gate)
                {
                    if (state == FishingStorageState.Ready) GetCatalog(character).RemoveAll(s => s.Id == trip);
                    Publish(character, state);
                }
            }
            else if (kind == 2)
            {
                var state = Write(character, save!);
                lock (gate)
                {
                    if (state == FishingStorageState.Ready)
                    {
                        var list = GetCatalog(character);
                        list.RemoveAll(s => s.Id == trip);
                        list.Add(save!.Summary);
                    }
                }
                if (state == FishingStorageState.Ready) state = Prune(character);
                lock (gate) Publish(character, state);
            }
            else if (kind == 3)
            {
                var (summaries, state) = ReadCatalog(character);
                lock (gate) catalogs[character] = summaries;
                if (state == FishingStorageState.Ready) state = Prune(character);
                lock (gate) Publish(character, state);
            }
            else
            {
                var result = ReadDetail(character, trip);
                lock (gate) loaded = result;
            }
        }
    }
    private List<FishingTripSummary> GetCatalog(ulong character)
    {
        if (!catalogs.TryGetValue(character, out var list)) catalogs[character] = list = [];
        return list;
    }
    private void Publish(ulong character, FishingStorageState state) =>
        views[character] = new(++revision, Array.AsReadOnly(GetCatalog(character).OrderByDescending(s => s.Sequence).ToArray()), state);
    private string TripDirectory(ulong character) => Path.Combine(directory, character.ToString("X16"));
    private string FilePath(ulong character, Guid trip) => Path.Combine(TripDirectory(character), trip.ToString("N") + ".json");

    private (List<FishingTripSummary>, FishingStorageState) ReadCatalog(ulong character)
    {
        var result = new List<FishingTripSummary>();
        try
        {
            var folder = TripDirectory(character);
            if (Directory.Exists(folder))
            {
                var paths = Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly).Take(1001).ToArray();
                if (paths.Length > 1000) return (result, FishingStorageState.InvalidFile);
                foreach (var path in paths)
                {
                    if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id)) continue;
                    JsonDocument parsed;
                    using (var stream = OpenBounded(path)) parsed = JsonDocument.Parse(stream);
                    using var document = parsed;
                    if (document.RootElement.GetProperty("version").GetInt32() != Schema) return (result, FishingStorageState.InvalidFile);
                    var summary = document.RootElement.GetProperty("summary").Deserialize<FishingTripSummary>(Json);
                    if (!Valid(summary) || summary!.Id != id) return (result, FishingStorageState.InvalidFile);
                    if (summary.IsActive)
                    {
                        var recovery = ReadDetail(character, id);
                        if (recovery.State != FishingStorageState.Ready) return (result, recovery.State);
                        var pending = recovery.Records.Count(r => r.Outcome == FishingOutcome.Pending);
                        var rows = recovery.Records.Select(r => r.Outcome == FishingOutcome.Pending
                            ? r with { Outcome = FishingOutcome.Interrupted, EndReason = FishingEndReason.Reload } : r).ToArray();
                        summary = summary with { IsActive = false, EndedUtc = null, EndReason = FishingEndReason.Reload,
                            Interrupted = summary.Interrupted + pending };
                        var state = Write(character, new(Schema, summary, rows));
                        if (state != FishingStorageState.Ready) return (result, state);
                    }
                    result.Add(summary);
                }
            }
            var migrated = MigrateLegacy(character, result);
            return (result, migrated);
        }
        catch (Exception error) when (FileError(error)) { return (result, Classify(error)); }
    }

    private FishingStorageState MigrateLegacy(ulong character, List<FishingTripSummary> result)
    {
        var folder = TripDirectory(character);
        var marker = Path.Combine(folder, ".legacy-imported");
        if (File.Exists(marker)) return FishingStorageState.Ready;
        var legacyPath = Path.Combine(directory, character.ToString("X16") + ".json");
        LegacyDocument? legacy;
        try
        {
            using var stream = OpenBounded(legacyPath);
            legacy = JsonSerializer.Deserialize<LegacyDocument>(stream, Json);
        }
        catch (FileNotFoundException) { return FishingStorageState.Ready; }
        catch (DirectoryNotFoundException) { return FishingStorageState.Ready; }
        if (legacy?.Version != 1 || legacy.Records is null || legacy.Records.Length > 1000 ||
            legacy.Records.Any(r => !Valid(r)) || legacy.Records.Select(r => r.Id).Distinct().Count() != legacy.Records.Length)
            return FishingStorageState.InvalidFile;
        var sequence = result.Select(s => s.Sequence).DefaultIfEmpty(0).Max();
        foreach (var group in legacy.Records.GroupBy(r => r.SessionId).Reverse())
        {
            if (result.Any(s => s.Id == group.Key)) continue;
            var rows = group.Select(r => r.Outcome == FishingOutcome.Pending
                ? r with { Outcome = FishingOutcome.Interrupted, EndReason = FishingEndReason.Reload } : r).ToArray();
            var first = rows[0];
            var summary = new FishingTripSummary(group.Key, ++sequence, first.TerritoryId, rows.Select(r => r.SpotId).FirstOrDefault(s => s is > 0),
                first.SessionStartedUtc, null, false, rows.Length, rows.Length,
                rows.Count(r => r.Outcome == FishingOutcome.Caught), rows.Count(r => r.Outcome == FishingOutcome.Failed),
                rows.Count(r => r.Outcome == FishingOutcome.Cancelled), rows.Count(r => r.Outcome == FishingOutcome.Interrupted),
                rows.Count(r => r.Outcome == FishingOutcome.Unknown), FishingEndReason.Reload)
                { FirstCastUtc = rows.Last().StartedUtc };
            var state = Write(character, new(Schema, summary, rows));
            if (state != FishingStorageState.Ready) return state;
            result.Add(summary);
        }
        Directory.CreateDirectory(folder);
        AtomicWrite(marker, "schema1 imported"u8.ToArray());
        return FishingStorageState.Ready;
    }

    private FishingHistoryLoad ReadDetail(ulong character, Guid trip)
    {
        try
        {
            using var stream = OpenBounded(FilePath(character, trip));
            var document = JsonSerializer.Deserialize<Document>(stream, Json);
            if (document?.Version != Schema || !Valid(document.Summary) || document.Summary.Id != trip ||
                document.Records is null || document.Records.Length != document.Summary.Retained ||
                document.Records.Any(r => !Valid(r) || r.SessionId != trip) ||
                document.Records.Select(r => r.Id).Distinct().Count() != document.Records.Length)
                return new(character, trip, null, [], FishingStorageState.InvalidFile);
            return new(character, trip, document.Summary, document.Records, FishingStorageState.Ready);
        }
        catch (Exception error) when (FileError(error)) { return new(character, trip, null, [], Classify(error)); }
    }
    private static FileStream OpenBounded(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= MaxFileBytes) return stream;
        stream.Dispose();
        throw new InvalidDataException("Fishing history file exceeds its size limit.");
    }
    private FishingStorageState Write(ulong character, Document document)
    {
        try
        {
            Directory.CreateDirectory(TripDirectory(character));
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, Json);
            if (bytes.LongLength > MaxFileBytes) return FishingStorageState.WriteFailed;
            AtomicWrite(FilePath(character, document.Summary.Id), bytes);
            return FishingStorageState.Ready;
        }
        catch (Exception error) when (FileError(error)) { return FishingStorageState.WriteFailed; }
    }
    private static void AtomicWrite(string path, byte[] bytes)
    {
        var temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes); stream.Flush(flushToDisk: true); }
        File.Move(temporary, path, overwrite: true);
    }
    private FishingStorageState DeleteFile(ulong character, Guid trip)
    {
        try { File.Delete(FilePath(character, trip)); return FishingStorageState.Ready; }
        catch (Exception error) when (FileError(error)) { return FishingStorageState.WriteFailed; }
    }
    private FishingStorageState Prune(ulong character)
    {
        while (true)
        {
            FishingTripSummary? remove;
            lock (gate)
            {
                var list = GetCatalog(character);
                if (list.Count <= FishingAttempts.TripCapacity) return FishingStorageState.Ready;
                remove = list.Where(s => !s.IsActive).OrderBy(s => s.Sequence).FirstOrDefault();
                if (remove is null) return FishingStorageState.Ready;
                tombstones.Add((character, remove.Id));
                pendingWrites.Remove((character, remove.Id));
            }
            var state = DeleteFile(character, remove.Id);
            if (state != FishingStorageState.Ready) return state;
            lock (gate) GetCatalog(character).RemoveAll(s => s.Id == remove.Id);
        }
    }
    private static bool Valid(FishingTripSummary? s) => s is not null && s.Id != Guid.Empty && s.Sequence > 0 &&
        s.TerritoryId > 0 && s.Retained is >= 0 and <= FishingAttempts.Capacity && s.Casts >= s.Retained &&
        s.Caught >= 0 && s.Failed >= 0 && s.Cancelled >= 0 && s.Interrupted >= 0 && s.Unknown >= 0 &&
        (long)s.Caught + s.Failed + s.Cancelled + s.Interrupted + s.Unknown <= s.Casts && Enum.IsDefined(s.EndReason);
    private static bool Valid(FishingAttempt? row) => row is not null && row.Id != Guid.Empty &&
        row.SessionId != Guid.Empty && row.TerritoryId != 0 && Enum.IsDefined(row.Outcome) &&
        Enum.IsDefined(row.EndReason) && Enum.IsDefined(row.BaitKind) &&
        Duration(row.DurationSeconds) && Duration(row.BiteSeconds) && Duration(row.HookSeconds) &&
        (row.Quantity is null or > 0) && (row.BaitItemId is null or > 0) && (row.SpotId is null or > 0) &&
        (row.FishItemId is null or > 0) && (row.Outcome != FishingOutcome.Caught || row.FishItemId is > 0 && row.Quantity is > 0);
    private static bool Duration(double? value) => value is null || double.IsFinite(value.Value) && value >= 0;
    private static bool FileError(Exception e) => e is IOException or UnauthorizedAccessException or JsonException or
        NotSupportedException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or System.Security.SecurityException;
    private static FishingStorageState Classify(Exception e) =>
        e is JsonException or NotSupportedException or KeyNotFoundException or InvalidOperationException or InvalidDataException or FormatException or OverflowException
            ? FishingStorageState.InvalidFile : FishingStorageState.ReadFailed;

    public void Dispose()
    {
        Task? drain;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            pendingLoad = null; pendingDetail = null;
            drain = worker;
        }
        drain?.GetAwaiter().GetResult();
    }
    private sealed record Document(int Version, FishingTripSummary Summary, FishingAttempt[] Records);
    private sealed record LegacyDocument(int Version, FishingAttempt[] Records);
}
