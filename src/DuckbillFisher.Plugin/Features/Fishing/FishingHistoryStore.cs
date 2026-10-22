using System.Text.Json;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

internal enum FishingStorageState { Loading, Ready, ReadFailed, InvalidFile, WriteFailed }
internal sealed record FishingArchiveView(int Revision, IReadOnlyList<FishingTripSummary> Trips, FishingStorageState State,
    bool Writable = false);
internal sealed record FishingHistoryLoad(ulong Character, Guid TripId, FishingTripSummary? Summary,
    FishingAttempt[] Records, FishingStorageState State);

/// <summary>Single file worker. One atomic file per trip, cached summaries, details loaded on demand.</summary>
internal sealed class FishingHistoryStore(string configDirectory, Action<int, int, int> warnIncompleteDrain) : IDisposable
{
    internal static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);
    private const int Schema = 2;
    private const long MaxFileBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string directory = Path.Combine(configDirectory, "fishing-history");
    private readonly object gate = new();
    private readonly Dictionary<(ulong Character, Guid Trip), Document> pendingWrites = [];
    private readonly HashSet<(ulong Character, Guid Trip)> pendingDeletes = [];
    private readonly HashSet<(ulong Character, Guid Trip)> tombstones = [];
    private readonly Dictionary<(ulong Character, Guid Trip), Document> retryWrites = [];
    private readonly HashSet<(ulong Character, Guid Trip)> retryDeletes = [];
    private readonly HashSet<(ulong Character, Guid Trip)> writeErrors = [], deleteErrors = [];
    private readonly HashSet<ulong> loading = [], initialized = [];
    private readonly Dictionary<ulong, FishingStorageState> readErrors = [];
    private readonly Dictionary<ulong, List<FishingTripSummary>> catalogs = [];
    private readonly Dictionary<ulong, FishingArchiveView> views = [];
    private ulong? pendingLoad;
    private (ulong Character, Guid Trip, long Request)? pendingDetail;
    private long detailRequest;
    private FishingHistoryLoad? loaded;
    private Task? worker;
    private Timer? retryTimer;
    private bool running, disposed;
    private int revision;

    public void Load(ulong character)
    {
        lock (gate)
        {
            if (disposed || character == 0) return;
            pendingLoad = character;
            pendingDetail = null; loaded = null; detailRequest++;
            loading.Add(character);
            QueueRetries(character);
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
            pendingDetail = (character, trip, ++detailRequest);
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

    public bool Save(ulong character, FishingTripSummary summary, IReadOnlyList<FishingAttempt> records)
    {
        lock (gate)
        {
            var key = (character, summary.Id);
            if (disposed || !CanWrite(character) || tombstones.Contains(key)) return false;
            retryWrites.Remove(key);
            pendingWrites[key] = new(Schema, summary, records.Take(FishingAttempts.Capacity).ToArray());
            EnsureWorker();
            return true;
        }
    }
    public void Delete(ulong character, IEnumerable<Guid> trips)
    {
        lock (gate)
        {
            if (disposed || !CanWrite(character)) return;
            foreach (var trip in trips)
            {
                if (trip == Guid.Empty) continue;
                var key = (character, trip);
                tombstones.Add(key);
                pendingWrites.Remove(key);
                retryWrites.Remove(key);
                writeErrors.Remove(key);
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
    private bool CanWrite(ulong character) => character != 0 && initialized.Contains(character) &&
        !loading.Contains(character) && !readErrors.ContainsKey(character);

    // Failed snapshots belong to the store, including trips no longer held by the tracker.
    // Consume retries when queued so an old snapshot cannot follow a newer in-flight write.
    private void QueueRetries(ulong? character = null)
    {
        foreach (var pair in retryWrites.Where(p => character is null || p.Key.Character == character).ToArray())
        {
            if (readErrors.ContainsKey(pair.Key.Character)) continue;
            if (!tombstones.Contains(pair.Key)) pendingWrites.TryAdd(pair.Key, pair.Value);
            retryWrites.Remove(pair.Key);
        }
        foreach (var key in retryDeletes.Where(k => character is null || k.Character == character).ToArray())
        {
            if (readErrors.ContainsKey(key.Character)) continue;
            pendingDeletes.Add(key); retryDeletes.Remove(key);
        }
    }
    private void RetryLater()
    {
        if (disposed) return;
        retryTimer ??= new Timer(_ =>
        {
            lock (gate)
            {
                if (disposed) return;
                QueueRetries();
                if (pendingWrites.Count > 0 || pendingDeletes.Count > 0) EnsureWorker();
            }
        }, null, RetryInterval, RetryInterval);
    }
    private void Process()
    {
        while (true)
        {
            ulong character;
            Guid trip = Guid.Empty;
            long request = 0;
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
                { character = detail.Character; trip = detail.Trip; request = detail.Request; pendingDetail = null; kind = 4; }
                else { running = false; return; }
            }
            if (kind is 1 or 2 && HasReadError(character))
            {
                lock (gate)
                {
                    if (kind == 1) retryDeletes.Add((character, trip));
                    else if (!tombstones.Contains((character, trip)) && !pendingWrites.ContainsKey((character, trip)))
                        retryWrites[(character, trip)] = save!;
                }
                continue;
            }
            if (kind == 1)
            {
                var state = DeleteFile(character, trip);
                lock (gate)
                {
                    var key = (character, trip);
                    if (state == FishingStorageState.Ready)
                    { GetCatalog(character).RemoveAll(s => s.Id == trip); deleteErrors.Remove(key); }
                    else
                    { deleteErrors.Add(key); retryDeletes.Add(key); RetryLater(); }
                    Publish(character);
                }
                if (state == FishingStorageState.Ready && !HasReadError(character))
                { Prune(character); lock (gate) Publish(character); }
            }
            else if (kind == 2)
            {
                var state = Write(character, save!);
                lock (gate)
                {
                    if (state == FishingStorageState.Ready)
                    {
                        writeErrors.Remove((character, trip));
                        var list = GetCatalog(character);
                        list.RemoveAll(s => s.Id == trip);
                        list.Add(save!.Summary);
                    }
                    else if (!tombstones.Contains((character, trip)))
                    {
                        writeErrors.Add((character, trip));
                        if (!pendingWrites.ContainsKey((character, trip))) retryWrites[(character, trip)] = save!;
                        RetryLater();
                    }
                }
                if (state == FishingStorageState.Ready && !HasReadError(character)) Prune(character);
                lock (gate) Publish(character);
            }
            else if (kind == 3)
            {
                var (summaries, state) = ReadCatalog(character);
                lock (gate)
                {
                    catalogs[character] = summaries;
                    // Another login may have requested this character while this read was in flight.
                    if (pendingLoad != character) loading.Remove(character);
                    initialized.Add(character);
                    if (state != FishingStorageState.Ready) readErrors[character] = state;
                    else { readErrors.Remove(character); QueueRetries(character); }
                }
                if (state == FishingStorageState.Ready) Prune(character);
                lock (gate) Publish(character);
            }
            else
            {
                lock (gate)
                {
                    if (request != detailRequest) continue;
                    // A detail request queued before a confirmed deletion is no longer a read failure.
                    if (tombstones.Contains((character, trip)))
                    { loaded = new(character, trip, null, [], FishingStorageState.Ready); continue; }
                }
                var result = ReadDetail(character, trip);
                lock (gate)
                {
                    if (request != detailRequest) continue;
                    loaded = result;
                    if (result.State != FishingStorageState.Ready)
                    { readErrors[character] = result.State; Publish(character); }
                }
            }
        }
    }
    private List<FishingTripSummary> GetCatalog(ulong character)
    {
        if (!catalogs.TryGetValue(character, out var list)) catalogs[character] = list = [];
        return list;
    }
    private bool HasReadError(ulong character) { lock (gate) return readErrors.ContainsKey(character); }
    private void Publish(ulong character)
    {
        var state = loading.Contains(character) ? FishingStorageState.Loading :
            readErrors.TryGetValue(character, out var failure) ? failure :
            writeErrors.Any(k => k.Character == character) || deleteErrors.Any(k => k.Character == character)
                ? FishingStorageState.WriteFailed : FishingStorageState.Ready;
        views[character] = new(++revision,
            Array.AsReadOnly(GetCatalog(character).OrderByDescending(s => s.Sequence).ToArray()), state, CanWrite(character));
    }
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
                    var detail = ReadDetail(character, id);
                    if (detail.State != FishingStorageState.Ready) return (result, detail.State);
                    var summary = detail.Summary!;
                    result.Add(summary);
                }
                // Validate every trip before recovering any unfinished file. A valid summary alone
                // must not allow writes or retention cleanup over a corrupt record body.
                for (var i = 0; i < result.Count; i++)
                {
                    var summary = result[i];
                    if (!summary.IsActive) continue;
                    var detail = ReadDetail(character, summary.Id);
                    if (detail.State != FishingStorageState.Ready) return (result, detail.State);
                    var pending = detail.Records.Count(r => r.Outcome == FishingOutcome.Pending);
                    var rows = detail.Records.Select(r => r.Outcome == FishingOutcome.Pending
                        ? r with { Outcome = FishingOutcome.Interrupted, EndReason = FishingEndReason.Reload } : r).ToArray();
                    summary = summary with { IsActive = false, EndedUtc = null, EndReason = FishingEndReason.Reload,
                        Interrupted = summary.Interrupted + pending };
                    var state = Write(character, new(Schema, summary, rows));
                    if (state != FishingStorageState.Ready) return (result, state);
                    result[i] = summary;
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
                !ValidCounts(document.Summary, document.Records) ||
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
    private void Prune(ulong character)
    {
        while (true)
        {
            FishingTripSummary? remove;
            lock (gate)
            {
                if (!CanWrite(character)) return;
                var list = GetCatalog(character);
                if (list.Count <= FishingAttempts.TripCapacity) return;
                remove = list.Where(s => !s.IsActive).OrderBy(s => s.Sequence).FirstOrDefault();
                if (remove is null) return;
                tombstones.Add((character, remove.Id));
                pendingWrites.Remove((character, remove.Id));
                retryWrites.Remove((character, remove.Id));
                writeErrors.Remove((character, remove.Id));
            }
            var state = DeleteFile(character, remove.Id);
            if (state != FishingStorageState.Ready)
            {
                lock (gate)
                {
                    deleteErrors.Add((character, remove.Id));
                    retryDeletes.Add((character, remove.Id));
                    RetryLater();
                }
                return;
            }
            lock (gate)
            {
                GetCatalog(character).RemoveAll(s => s.Id == remove.Id);
                deleteErrors.Remove((character, remove.Id));
                retryDeletes.Remove((character, remove.Id));
                pendingDeletes.Remove((character, remove.Id));
            }
        }
    }
    private static bool Valid(FishingTripSummary? s) => s is not null && s.Id != Guid.Empty && s.Sequence > 0 &&
        s.TerritoryId > 0 && s.Retained is >= 0 and <= FishingAttempts.Capacity && s.Casts >= s.Retained &&
        s.Caught >= 0 && s.Failed >= 0 && s.Cancelled >= 0 && s.Interrupted >= 0 && s.Unknown >= 0 &&
        (long)s.Caught + s.Failed + s.Cancelled + s.Interrupted + s.Unknown <= s.Casts && Enum.IsDefined(s.EndReason) && Duration(s.DurationSeconds);
    private static bool ValidCounts(FishingTripSummary summary, FishingAttempt[] records)
    {
        var pending = records.Count(r => r.Outcome == FishingOutcome.Pending);
        return pending <= (summary.IsActive ? 1 : 0) &&
            (long)summary.Caught + summary.Failed + summary.Cancelled + summary.Interrupted + summary.Unknown + pending == summary.Casts &&
            records.Count(r => r.Outcome == FishingOutcome.Caught) <= summary.Caught &&
            records.Count(r => r.Outcome == FishingOutcome.Failed) <= summary.Failed &&
            records.Count(r => r.Outcome == FishingOutcome.Cancelled) <= summary.Cancelled &&
            records.Count(r => r.Outcome == FishingOutcome.Interrupted) <= summary.Interrupted &&
            records.Count(r => r.Outcome == FishingOutcome.Unknown) <= summary.Unknown;
    }
    private static bool Valid(FishingAttempt? row) => row is not null && row.Id != Guid.Empty &&
        row.SessionId != Guid.Empty && row.TerritoryId != 0 && Enum.IsDefined(row.Outcome) &&
        Enum.IsDefined(row.EndReason) && Enum.IsDefined(row.BaitKind) &&
        Duration(row.DurationSeconds) && Duration(row.BiteSeconds) && Duration(row.HookSeconds) &&
        (row.Quantity is null or > 0) && (row.BaitItemId is null or > 0) && (row.SpotId is null or > 0) &&
        (row.FishItemId is null or > 0) && (row.MoochSourceId is null || row.MoochSourceId != Guid.Empty && row.MoochSourceId != row.Id && row.BaitKind == FishingBaitKind.Mooch) &&
        (row.Outcome != FishingOutcome.Caught || row.FishItemId is > 0 && row.Quantity is > 0);
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
            retryTimer?.Dispose();
            QueueRetries();
            if (pendingWrites.Count > 0 || pendingDeletes.Count > 0) EnsureWorker();
            pendingLoad = null; pendingDetail = null;
            drain = worker;
        }
        drain?.GetAwaiter().GetResult();
        lock (gate)
        {
            if (retryWrites.Count > 0 || retryDeletes.Count > 0)
                warnIncompleteDrain(retryWrites.Count, retryDeletes.Count, readErrors.Count);
        }
    }
    private sealed record Document(int Version, FishingTripSummary Summary, FishingAttempt[] Records);
    private sealed record LegacyDocument(int Version, FishingAttempt[] Records);
}
