using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class FishingHistoryStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "duckbill-history-tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<FishingHistoryStore> stores = [];
    private static readonly DateTimeOffset Utc = DateTimeOffset.FromUnixTimeSeconds(100000);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private FishingHistoryStore Store()
    {
        var store = new FishingHistoryStore(root, (_, _, _) => { });
        stores.Add(store);
        return store;
    }

    private static FishingAttempts Trip(int sequence = 1, bool active = false)
    {
        var history = new FishingAttempts();
        history.Start(TimeSpan.Zero, Utc, 10, null, 100, FishingBaitKind.Equipped, true);
        history.SetSequence(sequence);
        if (!active)
        {
            history.End(TimeSpan.FromSeconds(3), Utc.AddSeconds(3), FishingOutcome.Caught, FishingEndReason.CatchLog, 200, 1);
            history.EndSession(TimeSpan.FromSeconds(5), Utc.AddSeconds(5), FishingEndReason.RodFolded);
        }
        return history;
    }

    private string Folder(ulong character) => Path.Combine(root, "fishing-history", character.ToString("X16"));
    private string PathFor(ulong character, Guid trip) => Path.Combine(Folder(character), trip.ToString("N") + ".json");

    private static async Task Until(Func<bool> completed)
    {
        var deadline = Stopwatch.StartNew();
        while (!completed())
        {
            Assert.True(deadline.Elapsed < TimeSpan.FromSeconds(10), "History worker did not reach the expected state.");
            await Task.Delay(10);
        }
    }

    private static async Task Load(FishingHistoryStore store, ulong character)
    {
        store.Load(character);
        await Until(() => store.State(character) != FishingStorageState.Loading);
    }

    private static async Task<FishingHistoryLoad> Detail(FishingHistoryStore store, ulong character, Guid id)
    {
        store.RequestDetail(character, id);
        FishingHistoryLoad result = null!;
        await Until(() => store.TryTakeDetail(character, id, out result));
        return result;
    }

    [Fact]
    public async Task RoundTripSeparatesCharactersAndPreservesSnapshots()
    {
        var store = Store();
        var trip = Trip();
        Assert.False(store.Save(1, trip.Trip!, trip.Records));
        await Load(store, 1);
        await Load(store, 2);
        var empty = store.View(1);
        Assert.True(store.Save(1, trip.Trip!, trip.Records));
        await Until(() => store.View(1).Trips.Count == 1);
        Assert.Empty(empty.Trips);
        Assert.Empty(store.View(2).Trips);
        Assert.False(File.Exists(PathFor(2, trip.Trip!.Id)));
        store.Dispose();
        var restored = Store();
        await Load(restored, 1);
        Assert.True(restored.View(1).Writable);
        var detail = await Detail(restored, 1, trip.Trip.Id);
        Assert.Equal(FishingStorageState.Ready, detail.State);
        Assert.Equal(trip.Trip, detail.Summary);
        Assert.Equal(trip.Records, detail.Records);
    }

    [Fact]
    public async Task ReloadRecoversPendingCastWithoutInventingEndTime()
    {
        var trip = Trip(active: true);
        var store = Store();
        await Load(store, 1);
        Assert.True(store.Save(1, trip.Trip!, trip.Records));
        store.Dispose();
        var restored = Store();
        await Load(restored, 1);
        var detail = await Detail(restored, 1, trip.Trip!.Id);
        Assert.Equal(FishingStorageState.Ready, detail.State);
        Assert.False(detail.Summary!.IsActive);
        Assert.Null(detail.Summary.EndedUtc);
        Assert.Equal(1, detail.Summary.Interrupted);
        Assert.Equal(FishingEndReason.Reload, detail.Summary.EndReason);
        var row = Assert.Single(detail.Records);
        Assert.Equal(FishingOutcome.Interrupted, row.Outcome);
        Assert.Null(row.EndedUtc);
        Assert.Null(row.DurationSeconds);
    }

    [Fact]
    public async Task DeletePreventsStaleSaveAndReturnsEmptyDetail()
    {
        var store = Store();
        var trip = Trip();
        await Load(store, 1);
        Assert.True(store.Save(1, trip.Trip!, trip.Records));
        await Until(() => store.View(1).Trips.Count == 1);
        store.Delete(1, [trip.Trip!.Id]);
        Assert.False(store.Save(1, trip.Trip, trip.Records));
        await Until(() => store.View(1).Trips.Count == 0 && !File.Exists(PathFor(1, trip.Trip.Id)));
        var detail = await Detail(store, 1, trip.Trip.Id);
        Assert.Equal(FishingStorageState.Ready, detail.State);
        Assert.Null(detail.Summary);
        Assert.Empty(detail.Records);
        store.Dispose();
        var restored = Store();
        await Load(restored, 1);
        Assert.Empty(restored.View(1).Trips);
    }

    [Fact]
    public async Task CorruptFileBlocksWritesAndRecoveryOfOtherActiveFiles()
    {
        var store = Store();
        var active = Trip(active: true);
        await Load(store, 1);
        Assert.True(store.Save(1, active.Trip!, active.Records));
        store.Dispose();
        var activePath = PathFor(1, active.Trip!.Id);
        var original = File.ReadAllBytes(activePath);
        var brokenPath = PathFor(1, Guid.NewGuid());
        File.WriteAllText(brokenPath, "{");
        var restored = Store();
        await Load(restored, 1);
        Assert.Equal(FishingStorageState.InvalidFile, restored.State(1));
        Assert.False(restored.View(1).Writable);
        Assert.False(restored.Save(1, active.Trip, active.Records));
        restored.Delete(1, [active.Trip.Id]);
        restored.Dispose();
        Assert.Equal(original, File.ReadAllBytes(activePath));
        Assert.Equal("{", File.ReadAllText(brokenPath));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("session")]
    [InlineData("counts")]
    [InlineData("duration")]
    [InlineData("duplicate")]
    public async Task InvalidDocumentsArePreservedAndNeverWritable(string corruption)
    {
        var trip = Trip();
        var document = JsonSerializer.SerializeToNode(new { Version = 2, Summary = trip.Trip, Records = trip.Records }, Json)!;
        switch (corruption)
        {
            case "version": document["version"] = 99; break;
            case "session": document["records"]![0]!["sessionId"] = Guid.NewGuid(); break;
            case "counts": document["summary"]!["caught"] = 0; break;
            case "duration": document["records"]![0]!["durationSeconds"] = -1; break;
            case "duplicate":
                document["records"]!.AsArray().Add(document["records"]![0]!.DeepClone());
                document["summary"]!["retained"] = 2;
                document["summary"]!["casts"] = 2;
                document["summary"]!["caught"] = 2;
                break;
        }
        Directory.CreateDirectory(Folder(1));
        var json = document.ToJsonString();
        var path = PathFor(1, trip.Trip!.Id);
        File.WriteAllText(path, json);
        var store = Store();
        await Load(store, 1);
        Assert.Equal(FishingStorageState.InvalidFile, store.State(1));
        Assert.False(store.View(1).Writable);
        Assert.False(store.Save(1, trip.Trip, trip.Records));
        store.Dispose();
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public async Task FailedWriteRetainsSnapshotAndCanRetryAfterBlockerIsRemoved()
    {
        var store = Store();
        var trip = Trip();
        await Load(store, 1);
        var path = PathFor(1, trip.Trip!.Id);
        Directory.CreateDirectory(path);
        Assert.True(store.Save(1, trip.Trip, trip.Records));
        await Until(() => store.State(1) == FishingStorageState.WriteFailed);
        Directory.Delete(path);
        await Load(store, 1);
        await Until(() => store.State(1) == FishingStorageState.Ready && store.View(1).Trips.Count == 1);
        var detail = await Detail(store, 1, trip.Trip.Id);
        Assert.Equal(trip.Records, detail.Records);
        Assert.Equal(trip.Trip, detail.Summary);
    }

    [Fact]
    public async Task LatestDetailRequestCannotBeConsumedAsAnotherTripOrCharacter()
    {
        var store = Store();
        var first = Trip();
        var second = Trip(2);
        await Load(store, 1);
        store.Save(1, first.Trip!, first.Records);
        store.Save(1, second.Trip!, second.Records);
        await Until(() => store.View(1).Trips.Count == 2);
        store.RequestDetail(1, first.Trip!.Id);
        var detail = await Detail(store, 1, second.Trip!.Id);
        Assert.Equal(second.Trip.Id, detail.TripId);
        Assert.False(store.TryTakeDetail(1, first.Trip.Id, out _));
        Assert.False(store.TryTakeDetail(2, second.Trip.Id, out _));
    }

    [Fact]
    public async Task NewSnapshotReplacesFailedWriteWithoutRestoringOldContents()
    {
        var store = Store();
        var trip = Trip();
        await Load(store, 1);
        var path = PathFor(1, trip.Trip!.Id);
        Directory.CreateDirectory(path);
        Assert.True(store.Save(1, trip.Trip, trip.Records));
        await Until(() => store.State(1) == FishingStorageState.WriteFailed);
        Directory.Delete(path);
        var updated = new[] { trip.Records[0] with { FishItemId = 201 } };
        Assert.True(store.Save(1, trip.Trip, updated));
        await Until(() => store.State(1) == FishingStorageState.Ready && store.View(1).Trips.Count == 1);
        store.Dispose();
        var restored = Store();
        await Load(restored, 1);
        Assert.Equal(201u, Assert.Single((await Detail(restored, 1, trip.Trip.Id)).Records).FishItemId);
    }

    [Fact]
    public async Task FailedDeleteCanRetryAndStillRejectsStaleSaves()
    {
        var store = Store();
        var trip = Trip();
        await Load(store, 1);
        store.Save(1, trip.Trip!, trip.Records);
        await Until(() => store.View(1).Trips.Count == 1);
        var path = PathFor(1, trip.Trip!.Id);
        File.Delete(path);
        Directory.CreateDirectory(path);
        store.Delete(1, [trip.Trip.Id]);
        await Until(() => store.State(1) == FishingStorageState.WriteFailed);
        Assert.False(store.Save(1, trip.Trip, trip.Records));
        Directory.Delete(path);
        await Load(store, 1);
        Assert.Equal(FishingStorageState.Ready, store.State(1));
        Assert.Empty(store.View(1).Trips);
        Assert.False(store.Save(1, trip.Trip, trip.Records));
    }

    [Fact]
    public async Task LegacyImportPreservesOriginalAndDoesNotResurrectDeletedTrips()
    {
        var trip = Trip();
        var historyRoot = Path.Combine(root, "fishing-history");
        Directory.CreateDirectory(historyRoot);
        var legacyPath = Path.Combine(historyRoot, "0000000000000001.json");
        var legacy = JsonSerializer.Serialize(new { Version = 1, Records = trip.Records }, Json);
        File.WriteAllText(legacyPath, legacy);
        var store = Store();
        await Load(store, 1);
        Assert.Single(store.View(1).Trips);
        Assert.Equal(legacy, File.ReadAllText(legacyPath));
        store.Delete(1, [trip.Trip!.Id]);
        store.Dispose();
        var restored = Store();
        await Load(restored, 1);
        Assert.Empty(restored.View(1).Trips);
        Assert.Equal(legacy, File.ReadAllText(legacyPath));
    }

    [Fact]
    public async Task RetentionPreservesActiveTripAndDropsOldestCompletedTrip()
    {
        var store = Store();
        await Load(store, 1);
        var active = Trip(active: true);
        Assert.True(store.Save(1, active.Trip!, active.Records));
        FishingAttempts? oldest = null;
        for (var sequence = 2; sequence <= 101; sequence++)
        {
            var trip = Trip(sequence);
            oldest ??= trip;
            Assert.True(store.Save(1, trip.Trip!, trip.Records));
        }
        store.Dispose();
        Assert.True(File.Exists(PathFor(1, active.Trip!.Id)));
        Assert.False(File.Exists(PathFor(1, oldest!.Trip!.Id)));
        Assert.Equal(100, Directory.GetFiles(Folder(1), "*.json").Length);
    }

    public void Dispose()
    {
        foreach (var store in stores) store.Dispose();
        var fullRoot = Path.GetFullPath(root);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (Path.GetDirectoryName(fullRoot) != temp) throw new InvalidOperationException("Unexpected test directory.");
        if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
    }
}
