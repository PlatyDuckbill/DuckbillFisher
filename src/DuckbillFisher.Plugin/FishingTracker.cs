using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using Lumina.Excel.Sheets;

namespace DuckbillFisher.Plugin;

internal enum FishingTrackerState { Waiting, Loading, Ready, Unavailable, OceanUnsupported }
internal sealed record FishingTrackerView(int Revision, int Session, FishingTrackerState State,
    IReadOnlyList<FishingAttempt> Records, FishingAttempt? Active, TimeSpan? CastStartedAt,
    TimeSpan? SessionStartedAt, DateTimeOffset? SessionStartedUtc, bool HasSession,
    FishingNativeSample? Native, FishingSpotView? Spot, IReadOnlyDictionary<uint, FishingItemView> Items,
    FishingStorageState Storage, int Casts, int Catches, int Retrieves, int Ignored, int Contracts)
{
    public FishGuideRow[] AllSpotFish { get; init; } = [];
    public FishGuideRow[] RemainingSpotFish { get; init; } = [];
}

/// <summary>Validated normal-cast observations only. Collection and notification policies are not modified.</summary>
internal sealed class FishingTracker : IDisposable
{
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly ICondition conditions;
    private readonly IFramework framework;
    private readonly IChatGui chat;
    private readonly IDataManager data;
    private readonly FishPreferences preferences;
    private readonly FishCatalog catalog;
    private readonly FishCollectionObserver collection;
    private readonly NativeFishingActivityReader reader;
    private readonly FishingHistoryStore storage;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly object gate = new();
    private readonly HashSet<uint> validLogs = [];
    private readonly Queue<LogFact> pending = new();
    private readonly Dictionary<uint, FishingItemView> items = [];
    private readonly Queue<uint> neededItems = new();
    private readonly HashSet<uint> requestedItems = [];
    private readonly HashSet<uint> oceanTerritories = [];
    private FishingAttempts history = new();
    private FishingTrackerView view;
    private IReadOnlyDictionary<uint, FishingSpotView>? lastSpots;
    private Dictionary<(uint Territory, uint Place), FishingSpotView[]> byPlace = [];
    private ulong character;
    private int session = -1;
    private bool initialized, loaded, writable, disposed;
    private int savedRevision = -1;
    private TimeSpan nextSaveRetry;
    private int revision, casts, catches, retrieves, ignored;
    private int? previousPhase;
    private bool seenIdle;
    private TimeSpan lastTick, spotAt;
    private TimeSpan? readyAt;
    private uint territory;
    private FishingSpotView? spot;
    private FishingNativeSample? native;
    private FishingTrackerState state = FishingTrackerState.Waiting;
    private IReadOnlyList<FishGuideRow>? indexedRows;
    private Dictionary<uint, FishGuideRow> fishByItem = [];
    private Dictionary<uint, FishGuideRow[]> fishBySpot = [];
    private FishGuideRow[] allSpotFish = [], remainingSpotFish = [];
    private int filterPreferences = -1, filterCollection = -1;
    private uint? filterSpot;

    public FishingTracker(IClientState client, IPlayerState player, ICondition conditions, IFramework framework,
        IChatGui chat, IDataManager data, FishPreferences preferences, FishCatalog catalog, FishCollectionObserver collection,
        NativeFishingActivityReader reader, FishingHistoryStore storage)
    {
        this.client = client; this.player = player; this.conditions = conditions; this.framework = framework;
        this.chat = chat; this.data = data; this.preferences = preferences; this.catalog = catalog;
        this.reader = reader; this.storage = storage; this.collection = collection;
        view = Snapshot();
        framework.Update += OnUpdate;
        chat.LogMessage += OnLog;
    }

    public FishingTrackerView View { get { lock (gate) return view; } }
    public TimeSpan Now => clock.Elapsed;
    public FishGuideRow? FishForItem(uint id) { lock (gate) return fishByItem.GetValueOrDefault(id); }

    private void OnLog(ILogMessage message)
    {
        lock (gate)
        {
            if (disposed || !loaded || character == 0 || !validLogs.Contains(message.LogMessageId)) return;
            var personal = preferences.View;
            var now = clock.Elapsed;
            if (personal.Session != session || personal.CharacterId != character || !personal.HasCharacter ||
                !client.IsLoggedIn || !player.IsLoaded || player.ContentId != character || player.ClassJob.RowId != 18 ||
                conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51] ||
                now - lastTick > TimeSpan.FromSeconds(5) || state != FishingTrackerState.Ready) return;
            if (FishingLogContracts.Actor(message, player.CharacterName, (ushort)player.HomeWorld.RowId) != FishingLogActor.Local)
            { ignored++; return; }
            // Only these contracts have representative KR runtime evidence. Other results stay unknown.
            if (message.LogMessageId is not (1110 or 1111 or 1116 or 3512 or 5584)) { ignored++; return; }
            uint value = 0;
            if (message.LogMessageId is 1110 or 3512)
            {
                if (!message.TryGetIntParameter(0, out var number) || number <= 0) { ignored++; return; }
                value = (uint)number;
            }
            if (pending.Count == 64) { ignored++; return; }
            pending.Enqueue(new(session, client.TerritoryType, now, DateTimeOffset.UtcNow, message.LogMessageId, value));
        }
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            FishingLogContracts.Validate(data, validLogs);
            foreach (var row in data.GetExcelSheet<IKDSpot>()!)
                foreach (var target in new[] { row.SpotMain, row.SpotSub })
                    if (target.RowId != 0) oceanTerritories.Add(target.Value.TerritoryType.RowId);
        }
        catch (Exception) { validLogs.Clear(); }
        catalog.RequestLoad();
    }

    private void OnUpdate(IFramework _)
    {
        var personal = preferences.View;
        lock (gate)
        {
            if (disposed) return;
            Initialize();
            var now = clock.Elapsed;
            var utc = DateTimeOffset.UtcNow;
            if (session != personal.Session || character != (personal.HasCharacter ? personal.CharacterId : 0))
            {
                Interrupt(now, utc, FishingEndReason.Logout);
                session = personal.Session;
                character = personal.HasCharacter ? personal.CharacterId : 0;
                history = new();
                savedRevision = -1;
                loaded = writable = false;
                native = null;
                casts = catches = retrieves = ignored = 0;
                ResetLocation();
                state = character == 0 ? FishingTrackerState.Waiting : FishingTrackerState.Loading;
                if (character != 0) storage.Load(character);
                Publish();
            }
            if (character == 0) return;
            if (!loaded)
            {
                lastTick = now;
                if (!storage.TryTakeLoad(character, out var result)) return;
                history.Restore(result.Records);
                loaded = true;
                writable = result.State == FishingStorageState.Ready;
            }
            var before = history.Revision;
            var indexed = false;
            if (!ReferenceEquals(indexedRows, catalog.View.Rows))
            {
                indexedRows = catalog.View.Rows;
                fishByItem = indexedRows.Where(f => f.IsDisplayable && f.ItemType == "Item")
                    .GroupBy(f => f.ItemId).ToDictionary(g => g.Key, g => g.First());
                fishBySpot = indexedRows.Where(f => f.IsDisplayable && !f.IsOceanFishing)
                    .SelectMany(f => f.Spots.Select(s => (s.Id, Fish: f))).GroupBy(p => p.Id)
                    .ToDictionary(g => g.Key, g => g.Select(p => p.Fish).DistinctBy(f => f.FishParameterId)
                        .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToArray());
                indexed = true;
            }
            var previousNative = native;
            var previousState = state;
            var previousSpot = spot;
            if (!ReferenceEquals(lastSpots, catalog.View.Spots))
            {
                lastSpots = catalog.View.Spots;
                byPlace = lastSpots.Values.Where(s => s.PlaceNameId != 0)
                    .GroupBy(s => (s.TerritoryId, s.PlaceNameId)).ToDictionary(g => g.Key, g => g.ToArray());
            }
            if (!client.IsLoggedIn || !player.IsLoaded || player.ContentId != character ||
                conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51])
            {
                Interrupt(now, utc, FishingEndReason.Travel);
                ResetLocation();
                state = FishingTrackerState.Unavailable;
                native = null;
            }
            else
            {
                if (lastTick != TimeSpan.Zero && now - lastTick > TimeSpan.FromSeconds(5))
                { Interrupt(now, utc, FishingEndReason.ObservationGap); ResetLocation(); }
                if (territory != 0 && territory != client.TerritoryType)
                { Interrupt(now, utc, FishingEndReason.Travel); ResetLocation(); }
                territory = client.TerritoryType;
                native = reader.Read(character, player.ClassJob.RowId == 18);
                state = oceanTerritories.Contains(territory) ? FishingTrackerState.OceanUnsupported :
                    native.Availability == FishingReadState.Ready && validLogs.Contains(1110) && validLogs.Contains(1111) &&
                    validLogs.Contains(1116) && validLogs.Contains(3512) && validLogs.Contains(5584) ? FishingTrackerState.Ready : FishingTrackerState.Unavailable;
                if (state != FishingTrackerState.Ready)
                {
                    Interrupt(now, utc, state == FishingTrackerState.OceanUnsupported
                        ? FishingEndReason.UnsupportedMode : FishingEndReason.ReadUnavailable);
                    ResetLocation();
                }
                else
                {
                    DrainLogs(now);
                    ObservePhase(now, utc, native);
                }
            }
            lastTick = now;
            var observed = collection.View;
            var filtersChanged = indexed || filterSpot != spot?.Id || filterPreferences != personal.Revision || filterCollection != observed.Revision;
            if (filtersChanged)
            {
                filterSpot = spot?.Id;
                filterPreferences = personal.Revision;
                filterCollection = observed.Revision;
                allSpotFish = spot is not null ? fishBySpot.GetValueOrDefault(spot.Id, []) : [];
                remainingSpotFish = allSpotFish.Where(f => personal.Collection(f.FishParameterId, observed) != CollectionStatus.Caught).ToArray();
            }
            var itemsChanged = ResolveItems();
            Persist();
            if (before != history.Revision || previousNative != native || previousState != state ||
                previousSpot != spot || itemsChanged || view.Records != history.Records ||
                view.Storage != storage.State(character) || view.Ignored != ignored || filtersChanged) Publish();
        }
    }

    private void DrainLogs(TimeSpan now)
    {
        while (pending.TryDequeue(out var fact))
        {
            if (fact.Session != session || fact.Territory != territory || now - fact.At > TimeSpan.FromSeconds(2))
            { ignored++; continue; }
            if (fact.Id == 1110)
            {
                spotAt = fact.At;
                spot = byPlace.TryGetValue((territory, fact.Value), out var candidates) && candidates.Length == 1 ? candidates[0] : null;
                if (spot is not null && history.CastStartedAt is { } start && fact.At >= start && fact.At - start < TimeSpan.FromSeconds(1))
                    history.SetSpot(spot.Id);
            }
            else if (fact.Id == 1116 && history.Active is not null && native?.Phase is 5 or 6 &&
                history.CastStartedAt is { } hookStart && fact.At >= hookStart)
                history.Hook(fact.At);
            else if (fact.Id == 3512 && history.Active is { HookSeconds: not null } && native?.Phase is 2 or 4 &&
                history.CastStartedAt is { } catchStart && fact.At >= catchStart)
            { history.End(fact.At, fact.Utc, FishingOutcome.Caught, FishingEndReason.CatchLog, fact.Value, 1); catches++; }
            else if (fact.Id == 5584 && history.Active is not null)
            { history.End(fact.At, fact.Utc, FishingOutcome.Cancelled, FishingEndReason.ManualRetrieve); retrieves++; }
            else if (fact.Id is 1116 or 3512 or 5584) ignored++;
            // 1111 announces quitting; only the observed return to phase 0 ends the continuous session.
        }
    }

    private void ObservePhase(TimeSpan now, DateTimeOffset utc, FishingNativeSample current)
    {
        var phase = current.Phase;
        if (phase == 1 && previousPhase is not null && previousPhase != 1)
        {
            if (now - spotAt > TimeSpan.FromSeconds(1)) spot = null;
            var kind = (current.BaitFlags.GetValueOrDefault() & 0x20) != 0 ? FishingBaitKind.Swimbait :
                (current.BaitFlags.GetValueOrDefault() & 0x10) != 0 ? FishingBaitKind.Mooch : FishingBaitKind.Equipped;
            // The actual mooch/swimbait item is not yet runtime-validated. Never snapshot equipped bait as that fish.
            var bait = kind == FishingBaitKind.Equipped && current.EquippedBait is > 0 ? current.EquippedBait : null;
            history.Start(now, utc, territory, spot?.Id, bait, kind, seenIdle);
            casts++;
            readyAt = null;
        }
        if (phase == 5) history.Bite(now);
        if (phase == 6) history.Hook(now);
        if (phase == 0)
        {
            history.EndSession(now, utc, FishingEndReason.RodFolded);
            seenIdle = true;
            spot = null;
            readyAt = null;
        }
        else if (phase == 4 && history.Active is not null)
        {
            readyAt ??= now;
            if (now - readyAt >= TimeSpan.FromSeconds(2))
                history.End(now, utc, FishingOutcome.Unknown, FishingEndReason.NativeFinished);
        }
        else readyAt = null;
        previousPhase = phase;
    }

    private void Interrupt(TimeSpan now, DateTimeOffset utc, FishingEndReason reason)
    {
        history.EndSession(now, utc, reason);
        Persist();
        pending.Clear();
    }
    private void ResetLocation()
    { previousPhase = null; seenIdle = false; readyAt = null; spot = null; spotAt = TimeSpan.Zero; territory = 0; pending.Clear(); allSpotFish = remainingSpotFish = []; filterSpot = null; }
    private void Persist()
    {
        if (!loaded || !writable || character == 0) return;
        if (savedRevision == history.Revision &&
            (storage.State(character) != FishingStorageState.WriteFailed || clock.Elapsed < nextSaveRetry)) return;
        savedRevision = history.Revision;
        nextSaveRetry = clock.Elapsed + TimeSpan.FromSeconds(5);
        storage.Save(character, history.Records);
    }
    private bool ResolveItems()
    {
        void Need(uint? id) { if (id is > 0 && requestedItems.Add(id.Value)) neededItems.Enqueue(id.Value); }
        Need(native?.EquippedBait);
        if (view.Records != history.Records)
            foreach (var record in history.Records) { Need(record.BaitItemId); Need(record.FishItemId); }
        var changed = false;
        for (var i = 0; i < 16 && neededItems.TryDequeue(out var id); i++)
        {
            try
            {
                var row = data.GetExcelSheet<Item>()?.GetRowOrDefault(id);
                items[id] = new(id, row?.Name.ExtractText() ?? "", row?.Icon ?? 0);
            }
            catch (Exception) { items[id] = new(id, "", 0); }
            changed = true;
        }
        return changed;
    }
    private FishingTrackerView Snapshot() => new(revision, session, state, history.Records, history.Active,
        history.CastStartedAt, history.SessionStartedAt, history.SessionStartedUtc, history.HasSession,
        native, spot, items.ToFrozenDictionary(), character == 0 ? FishingStorageState.Loading : storage.State(character),
        casts, catches, retrieves, ignored, validLogs.Count)
        { AllSpotFish = allSpotFish, RemainingSpotFish = remainingSpotFish };
    private void Publish() { revision++; view = Snapshot(); }

    public string CreateReport()
    {
        lock (gate) return string.Create(CultureInfo.InvariantCulture,
            $"DuckbillFisher MVP-02/timer/v1\nPlugin={typeof(Plugin).Assembly.GetName().Version}; Session={session}; State={state}; Read={native?.Availability}; Phase={native?.Phase}\nRecords={history.Records.Count}; Active={history.Active is not null}; Casts={casts}; Catches={catches}; Retrieves={retrieves}; Ignored={ignored}; Contracts={validLogs.Count}/{FishingLogContracts.Count}\nSpot={spot?.Id}; Equipped={native?.EquippedBait}; Storage={view.Storage}; PersistEnabled={writable}\nActualMoochBait=Unknown; UnvalidatedResults=Unknown; OutcomeDoesNotSetCollection=True");
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            framework.Update -= OnUpdate;
            chat.LogMessage -= OnLog;
            Interrupt(clock.Elapsed, DateTimeOffset.UtcNow, FishingEndReason.Reload);
            pending.Clear();
            clock.Stop();
        }
        storage.Dispose();
    }
    private sealed record LogFact(int Session, uint Territory, TimeSpan At, DateTimeOffset Utc, uint Id, uint Value);
}
