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
internal enum FishingSpotState { NotObserved, AwaitingIdentification, Unknown, Known, LastKnown }
internal sealed record FishingTrackerView(int Revision, int Session, FishingTrackerState State,
    IReadOnlyList<FishingAttempt> Records, FishingAttempt? Active, TimeSpan? CastStartedAt,
    TimeSpan? SessionStartedAt, DateTimeOffset? SessionStartedUtc, bool HasSession,
    FishingNativeSample? Native, FishingSpotView? Spot, IReadOnlyDictionary<uint, FishingItemView> Items,
    FishingStorageState Storage, int Casts, int Catches, int Retrieves, int Ignored, int Contracts)
{
    public FishGuideRow[] AllSpotFish { get; init; } = [];
    public FishGuideRow[] RemainingSpotFish { get; init; } = [];
    public IReadOnlyList<FishingTripSummary> Trips { get; init; } = [];
    public FishingTripSummary? CurrentTrip { get; init; }
    public IReadOnlyList<FishingAttempt> CurrentRecords { get; init; } = [];
    public FishingTripSummary? SelectedTrip { get; init; }
    public IReadOnlyList<FishingAttempt> SelectedRecords { get; init; } = [];
    public bool DetailLoading { get; init; }
    public bool DetailError { get; init; }
    public IReadOnlyDictionary<uint, string> TerritoryNames { get; init; } = new Dictionary<uint, string>();
    public bool IsLastSpot { get; init; }
    public FishingSpotState SpotState { get; init; }
    public bool HistoryWritable { get; init; }
    public TimeSpan? TripStartedAt { get; init; }
    public TimeSpan? TimerRetainedUntil { get; init; }
}

/// <summary>KR contract-guarded public logs and inspected native phases. Collection and notifications remain separate.</summary>
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
    private int revision, casts, catches, retrieves, ignored;
    private int? previousPhase;
    private bool seenIdle;
    private TimeSpan lastTick;
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
    private FishingSpotView? pendingSpot;
    private FishingSpotView? lastConfirmedSpot;
    private FishingSpotState spotState;
    private FishingSpotState pendingSpotState;
    private TimeSpan pendingSpotAt;
    private TimeSpan? lastLocalCatchAt;
    private Guid? lastLocalCatchId;
    internal static readonly TimeSpan TimerRetention = TimeSpan.FromMinutes(5);
    private TimeSpan? timerRetainedUntil;
    private FishingSpotState retainedSpotState;
    private LogFact? pendingDiscovery;
    private int discoveryMessages, appliedDiscoveries, recordedSpotUpdates;
    private uint lastDiscoveryPlace;
    private FishingLogActor lastDiscoveryActor;
    private LogFact? pendingMooch;
    private (Guid Source, FishingSpotView Spot)? moochSpotContinuation;
    private int moochLogs, moochApplied, failures;
    private uint lastFailureLog;
    private long lastSequence;
    private int archiveRevision = -1;
    private IReadOnlyList<FishingTripSummary> archiveTrips = [], trips = [];
    private readonly HashSet<Guid> deletedTrips = [];
    private readonly Dictionary<uint, string> territoryNames = [];
    private (int Session, Guid? Trip)? requestedDetail;
    private readonly Queue<(int Session, Guid[] Trips)> requestedDeletes = new();
    private Guid? selectedTrip;
    private FishingTripSummary? selectedSummary;
    private IReadOnlyList<FishingAttempt> selectedRecords = [];
    private bool detailLoading, detailError;

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
    public void SelectTrip(int expectedSession, Guid? id)
    { lock (gate) if (!disposed) requestedDetail = (expectedSession, id); }
    public void DeleteTrips(int expectedSession, IEnumerable<Guid> ids)
    { lock (gate) if (!disposed) requestedDeletes.Enqueue((expectedSession, ids.Distinct().ToArray())); }

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
            var discovery = IsSpotDiscovery(message.LogMessageId);
            var actor = FishingLogContracts.Actor(message, player.CharacterName, (ushort)player.HomeWorld.RowId);
            // Source-less discovery messages are only candidates; a verified local catch must corroborate them.
            if (actor != FishingLogActor.Local && !(discovery && actor == FishingLogActor.NoSource))
            { ignored++; return; }
            // Each ID also passed the installed KR macro fingerprint. Other/ambiguous actors stay unknown.
            if (message.LogMessageId is not (1110 or 1111 or 1116 or 1121 or 3512 or 5584) &&
                !IsFailureLog(message.LogMessageId) && !discovery) { ignored++; return; }
            uint value = 0;
            if (message.LogMessageId is 1110 or 1118 or 1121 or 3512 || discovery)
            {
                if (!message.TryGetIntParameter(0, out var number) || number <= 0) { ignored++; return; }
                value = (uint)number;
            }
            if (pending.Count == 64) { ignored++; return; }
            pending.Enqueue(new(session, client.TerritoryType, now, DateTimeOffset.UtcNow, message.LogMessageId, value, actor));
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
                timerRetainedUntil = null;
                savedRevision = -1;
                loaded = writable = false;
                native = null;
                archiveTrips = trips = [];
                archiveRevision = -1;
                lastSequence = 0;
                selectedTrip = null; selectedSummary = null; selectedRecords = [];
                detailLoading = detailError = false;
                requestedDetail = null; requestedDeletes.Clear(); deletedTrips.Clear();
                casts = catches = retrieves = ignored = 0;
                moochLogs = moochApplied = failures = 0; lastFailureLog = 0;
                discoveryMessages = appliedDiscoveries = recordedSpotUpdates = 0; lastDiscoveryPlace = 0; lastDiscoveryActor = default;
                ResetLocation();
                state = character == 0 ? FishingTrackerState.Waiting : FishingTrackerState.Loading;
                if (character != 0) storage.Load(character);
                Publish();
            }
            if (character == 0) return;
            if (!loaded)
            {
                lastTick = now;
                var archive = storage.View(character);
                if (archive.State == FishingStorageState.Loading) return;
                loaded = true;
                writable = archive.Writable;
            }
            // Restore the sequence and write permission before observing a new cast.
            var archiveChanged = UpdateArchive();
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
            var previousSpotState = spotState;
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
                    ApplySpotDiscovery(now);
                }
            }
            lastTick = now;
            var retentionChanged = timerRetainedUntil is { } expires && now >= expires;
            if (retentionChanged) timerRetainedUntil = null;
            archiveChanged |= UpdateArchive();
            var selectionChanged = UpdateSelection();
            var observed = collection.View;
            var browsingSpot = DisplayedSpot;
            var filtersChanged = indexed || filterSpot != browsingSpot?.Id || filterPreferences != personal.CatalogRevision || filterCollection != observed.Revision;
            if (filtersChanged)
            {
                filterSpot = browsingSpot?.Id;
                filterPreferences = personal.CatalogRevision;
                filterCollection = observed.Revision;
                allSpotFish = browsingSpot is not null ? fishBySpot.GetValueOrDefault(browsingSpot.Id, []) : [];
                remainingSpotFish = allSpotFish.Where(f => personal.Collection(f.FishParameterId, observed) != CollectionStatus.Caught).ToArray();
            }
            var itemsChanged = ResolveItems();
            Persist();
            if (before != history.Revision || previousNative != native || previousState != state ||
                previousSpot != spot || previousSpotState != spotState || itemsChanged || view.Records != history.Records ||
                view.Storage != storage.State(character) || view.Ignored != ignored || filtersChanged || archiveChanged || selectionChanged || retentionChanged) Publish();
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
                moochSpotContinuation = null;
                pendingSpotAt = fact.At;
                pendingSpot = byPlace.TryGetValue((territory, fact.Value), out var candidates) && candidates.Length == 1 ? candidates[0] : null;
                pendingSpotState = fact.Value == FishingLogContracts.UnknownSpotPlaceName ? FishingSpotState.Unknown
                    : pendingSpot is not null ? FishingSpotState.Known : FishingSpotState.AwaitingIdentification;
                if (pendingSpotState != FishingSpotState.Known) pendingSpot = null;
                lastConfirmedSpot = pendingSpot;
                spotState = pendingSpotState;
                if (native?.Phase != 0) spot = pendingSpot;
                if (history.CastStartedAt is { } start && fact.At >= start && fact.At - start < TimeSpan.FromSeconds(1))
                {
                    if (spot is not null) history.SetSpot(spot.Id);
                    pendingSpot = null;
                    pendingSpotAt = TimeSpan.Zero;
                }
            }
            else if (IsSpotDiscovery(fact.Id))
            {
                discoveryMessages++;
                lastDiscoveryPlace = fact.Value;
                lastDiscoveryActor = fact.Actor;
                pendingDiscovery = fact;
            }
            else if (fact.Id == 1121)
            {
                moochLogs++;
                if (fishByItem.ContainsKey(fact.Value)) pendingMooch = fact;
                else ignored++;
            }
            else if (fact.Id == 1116 && history.Active is not null && native?.Phase is 5 or 6 &&
                history.CastStartedAt is { } hookStart && fact.At >= hookStart)
                history.Hook(fact.At);
            else if (fact.Id == 3512 && history.Active is { HookSeconds: not null } && native?.Phase is 2 or 4 &&
                history.CastStartedAt is { } catchStart && fact.At >= catchStart)
            {
                lastLocalCatchId = history.Active.Id;
                history.End(fact.At, fact.Utc, FishingOutcome.Caught, FishingEndReason.CatchLog, fact.Value, 1);
                lastLocalCatchAt = fact.At;
                catches++;
            }
            else if (fact.Id == 5584 && history.Active is not null)
            { history.End(fact.At, fact.Utc, FishingOutcome.Cancelled, FishingEndReason.ManualRetrieve); retrieves++; }
            else if (IsFailureLog(fact.Id) && history.Active is not null && native?.Phase is 2 or 4 &&
                history.CastStartedAt is { } failureStart && fact.At >= failureStart)
            {
                // 1118 names the fish that escaped. 3515 names lost bait, never a caught fish.
                uint? missedFish = fact.Id == 1118 && fishByItem.ContainsKey(fact.Value) ? fact.Value : null;
                history.End(fact.At, fact.Utc, FishingOutcome.Failed, FishingEndReason.FailureLog, missedFish);
                lastFailureLog = fact.Id; failures++;
            }
            else if (IsFailureLog(fact.Id)) ignored++;
            else if (fact.Id is 1116 or 3512 or 5584) ignored++;
            // 1111 announces quitting; only the observed return to phase 0 ends the continuous session.
        }
    }

    private static bool IsFailureLog(uint id) => id is 1117 or 1118 or 1119 or 1120 or 1127 or 1129 or 3515 or 3527;

    private static bool IsSpotDiscovery(uint id) => id is 1115 or 1130 or 3513;

    private FishingSpotView? BrowsingSpot => spotState is FishingSpotState.Known or FishingSpotState.LastKnown
        ? spot ?? lastConfirmedSpot : null;
    private bool TimerVisible => history.Trip is { } trip && !deletedTrips.Contains(trip.Id) &&
        (history.HasSession || timerRetainedUntil is { } expires && clock.Elapsed < expires);
    private FishingSpotView? DisplayedSpot => !TimerVisible ? null : history.HasSession ? BrowsingSpot :
        history.Trip?.SpotId is { } id ? catalog.View.Spots.GetValueOrDefault(id) : null;

    private void ApplySpotDiscovery(TimeSpan now)
    {
        if (pendingDiscovery is not { } fact) return;
        if (now - fact.At > TimeSpan.FromSeconds(2)) { pendingDiscovery = null; return; }
        if (spotState is not (FishingSpotState.Unknown or FishingSpotState.AwaitingIdentification) ||
            lastLocalCatchAt is not { } caughtAt || Math.Abs((caughtAt - fact.At).TotalSeconds) > 2 ||
            native?.Phase is not (2 or 4)) return;
        pendingDiscovery = null;
        if (!byPlace.TryGetValue((territory, fact.Value), out var candidates) || candidates.Length != 1 ||
            fact.Value == FishingLogContracts.UnknownSpotPlaceName) return;
        spot = lastConfirmedSpot = candidates[0];
        spotState = FishingSpotState.Known;
        if (lastLocalCatchId is { } attempt && history.SetSpot(attempt, territory, spot.Id)) recordedSpotUpdates++;
        appliedDiscoveries++;
    }

    private void ApplyMooch(TimeSpan now, FishingNativeSample current)
    {
        if (pendingMooch is not { } fact) return;
        if (now - fact.At > TimeSpan.FromSeconds(2)) { pendingMooch = null; moochSpotContinuation = null; return; }
        var kind = CastBaitKind(current);
        if (current.Phase is not (1 or 12) || kind != FishingBaitKind.Mooch ||
            history.Active is not { BiteSeconds: null, HookSeconds: null } || history.CastStartedAt is not { } start ||
            Math.Abs((fact.At - start).TotalSeconds) > 2) return;
        if (history.SetCastBait(fact.Value, kind))
        {
            moochApplied++;
            // A verified mooch may omit 1110. Keep the known location only when the
            // stored source catch belongs to this uninterrupted trip and cast link.
            if (moochSpotContinuation is { } continuation && history.Active?.MoochSourceId == continuation.Source &&
                continuation.Spot.TerritoryId == territory)
            {
                spot = lastConfirmedSpot = continuation.Spot;
                spotState = FishingSpotState.Known;
                history.SetSpot(spot.Id);
            }
        }
        pendingMooch = null;
        moochSpotContinuation = null;
    }

    private static FishingBaitKind CastBaitKind(FishingNativeSample sample) =>
        (sample.BaitFlags.GetValueOrDefault() & 0x20) != 0 ? FishingBaitKind.Swimbait :
        (sample.BaitFlags.GetValueOrDefault() & 0x10) != 0 ? FishingBaitKind.Mooch : FishingBaitKind.Equipped;

    private uint? CastBaitItem(FishingNativeSample sample, FishingBaitKind kind)
    {
        if (kind == FishingBaitKind.Equipped) return sample.EquippedBait is > 0 ? sample.EquippedBait : null;
        if (kind != FishingBaitKind.Swimbait) return null;
        if ((sample.BaitFlags.GetValueOrDefault() & 0x30) != 0x20) return null;
        // KR sample: the selected slot survives through CastingOut/Waiting, then clears at the result.
        // Copy at the observed cast start; never substitute equipped bait or the last caught fish.
        var item = sample.SelectedSwimbait switch
        { 0 => sample.Swimbait0, 1 => sample.Swimbait1, 2 => sample.Swimbait2, _ => null };
        return item is > 0 && fishByItem.ContainsKey(item.Value) ? item : null;
    }

    private void ObservePhase(TimeSpan now, DateTimeOffset utc, FishingNativeSample current)
    {
        var phase = current.Phase;
        if (phase == 1 && previousPhase is not null && previousPhase != 1)
        {
            var kind = CastBaitKind(current);
            var freshSpot = pendingSpotAt != TimeSpan.Zero && now >= pendingSpotAt && now - pendingSpotAt <= TimeSpan.FromSeconds(2);
            var previousCatch = history.Records.FirstOrDefault();
            var confirmed = BrowsingSpot;
            moochSpotContinuation = !freshSpot && history.HasSession && kind == FishingBaitKind.Mooch &&
                confirmed is not null && previousCatch is { Outcome: FishingOutcome.Caught } &&
                previousCatch.TerritoryId == territory && previousCatch.SpotId == confirmed.Id
                ? (previousCatch.Id, confirmed) : null;
            spot = freshSpot ? pendingSpot : null;
            spotState = freshSpot ? pendingSpotState : FishingSpotState.AwaitingIdentification;
            // Invalidate old locations; only the verified mooch link above may restore one.
            lastConfirmedSpot = spot;
            pendingDiscovery = null;
            lastLocalCatchAt = null;
            lastLocalCatchId = null;
            timerRetainedUntil = null;
            var bait = CastBaitItem(current, kind);
            if (kind == FishingBaitKind.Swimbait) pendingMooch = null;
            history.Start(now, utc, territory, spot?.Id, bait, kind, seenIdle);
            if (history.Trip?.Sequence == 0) history.SetSequence(++lastSequence);
            pendingSpot = null;
            pendingSpotAt = TimeSpan.Zero;
            casts++;
            readyAt = null;
        }
        ApplyMooch(now, current);
        if (phase == 5) history.Bite(now);
        if (phase == 6) history.Hook(now);
        if (phase == 0)
        {
            moochSpotContinuation = null;
            EndSession(now, utc, FishingEndReason.RodFolded);
            seenIdle = true;
            spot = null;
            if (lastConfirmedSpot is not null) spotState = FishingSpotState.LastKnown;
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
        EndSession(now, utc, reason);
        Persist();
        pending.Clear();
        pendingMooch = null;
        moochSpotContinuation = null;
    }
    private void EndSession(TimeSpan now, DateTimeOffset utc, FishingEndReason reason)
    {
        var wasActive = history.HasSession;
        history.EndSession(now, utc, reason);
        if (!wasActive) return;
        timerRetainedUntil = now + TimerRetention;
        retainedSpotState = spotState;
    }
    private void ResetLocation()
    {
        previousPhase = null; seenIdle = false; readyAt = null; spot = pendingSpot = lastConfirmedSpot = null;
        spotState = pendingSpotState = FishingSpotState.NotObserved;
        pendingSpotAt = TimeSpan.Zero; pendingDiscovery = null; lastLocalCatchAt = null; lastLocalCatchId = null;
        pendingMooch = null; moochSpotContinuation = null; territory = 0; pending.Clear();
        if (!TimerVisible) { allSpotFish = remainingSpotFish = []; filterSpot = null; }
    }

    private bool UpdateArchive()
    {
        var archive = storage.View(character);
        writable = archive.Writable;
        if (archiveRevision == archive.Revision && view.Records == history.Records) return false;
        archiveRevision = archive.Revision;
        archiveTrips = archive.Trips;
        lastSequence = Math.Max(lastSequence, archiveTrips.Select(s => s.Sequence).DefaultIfEmpty(0).Max());
        var combined = archiveTrips.ToList();
        if (history.Trip is { } current && !deletedTrips.Contains(current.Id))
        { combined.RemoveAll(s => s.Id == current.Id); combined.Add(current); }
        trips = Array.AsReadOnly(combined.OrderByDescending(s => s.Sequence).Take(FishingAttempts.TripCapacity).ToArray());
        foreach (var id in trips.Select(t => t.TerritoryId).Distinct())
        {
            if (territoryNames.ContainsKey(id)) continue;
            try { territoryNames[id] = data.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(id)?.PlaceName.Value.Name.ExtractText() ?? ""; }
            catch (Exception) { territoryNames[id] = ""; }
        }
        return true;
    }

    private bool UpdateSelection()
    {
        var changed = false;
        while (requestedDeletes.TryDequeue(out var request))
        {
            if (request.Session != session || !writable) continue;
            var ids = request.Trips.Where(id => trips.Any(t => t.Id == id && !t.IsActive) &&
                !(history.HasSession && history.Trip?.Id == id)).ToArray();
            foreach (var id in ids) deletedTrips.Add(id);
            if (history.Trip is { } retained && ids.Contains(retained.Id)) { timerRetainedUntil = null; changed = true; }
            storage.Delete(character, ids);
        }
        if (requestedDetail is { } selection)
        {
            requestedDetail = null;
            if (selection.Session == session)
            {
                selectedTrip = selection.Trip;
                selectedSummary = null; selectedRecords = []; detailError = detailLoading = false;
                if (selectedTrip is { } id && trips.Any(t => t.Id == id))
                {
                    if (history.Trip?.Id != id) { detailLoading = true; storage.RequestDetail(character, id); }
                }
                else selectedTrip = null;
                changed = true;
            }
        }
        if (selectedTrip is { } selected)
        {
            var summary = trips.FirstOrDefault(t => t.Id == selected);
            if (summary is null)
            { selectedTrip = null; selectedSummary = null; selectedRecords = []; detailLoading = detailError = false; return true; }
            if (selectedSummary != summary) { selectedSummary = summary; changed = true; }
            if (history.Trip?.Id == selected && !deletedTrips.Contains(selected))
            {
                changed |= !ReferenceEquals(selectedRecords, history.Records);
                selectedRecords = history.Records;
                detailLoading = false;
            }
            else if (detailLoading && storage.TryTakeDetail(character, selected, out var result))
            {
                detailLoading = false;
                detailError = result.State != FishingStorageState.Ready;
                selectedRecords = result.Records;
                changed = true;
            }
        }
        return changed;
    }
    private void Persist()
    {
        if (!loaded || !writable || character == 0 || history.Trip is null || deletedTrips.Contains(history.Trip.Id)) return;
        if (savedRevision == history.Revision) return;
        if (storage.Save(character, history.Trip, history.Records)) savedRevision = history.Revision;
    }
    private bool ResolveItems()
    {
        void Need(uint? id) { if (id is > 0 && requestedItems.Add(id.Value)) neededItems.Enqueue(id.Value); }
        Need(native?.EquippedBait);
        if (view.Records != history.Records)
            foreach (var record in history.Records) { Need(record.BaitItemId); Need(record.FishItemId); }
        if (!ReferenceEquals(view.SelectedRecords, selectedRecords))
            foreach (var record in selectedRecords) { Need(record.BaitItemId); Need(record.FishItemId); }
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
        native, DisplayedSpot, items.ToFrozenDictionary(), character == 0 ? FishingStorageState.Loading : storage.State(character),
        casts, catches, retrieves, ignored, validLogs.Count)
        {
            AllSpotFish = allSpotFish, RemainingSpotFish = remainingSpotFish, Trips = trips,
            CurrentTrip = TimerVisible ? history.Trip : null,
            CurrentRecords = TimerVisible ? history.Records : Array.Empty<FishingAttempt>(),
            SelectedTrip = selectedSummary, SelectedRecords = selectedRecords,
            DetailLoading = detailLoading, DetailError = detailError, TerritoryNames = territoryNames.ToFrozenDictionary(),
            IsLastSpot = TimerVisible && !history.HasSession || spotState == FishingSpotState.LastKnown,
            SpotState = !TimerVisible ? FishingSpotState.NotObserved : history.HasSession ? spotState :
                DisplayedSpot is not null ? FishingSpotState.LastKnown : retainedSpotState,
            HistoryWritable = writable,
            TripStartedAt = history.TripStartedAt, TimerRetainedUntil = timerRetainedUntil,
        };
    private void Publish() { revision++; view = Snapshot(); }

    public string CreateReport()
    {
        lock (gate) return string.Create(CultureInfo.InvariantCulture,
            $"DuckbillFisher MVP-02/timer/v5\nPlugin={typeof(Plugin).Assembly.GetName().Version}; Session={session}; State={state}; Read={native?.Availability}; Phase={native?.Phase}\nTrips={trips.Count}; CurrentRecords={view.CurrentRecords.Count}; Active={history.Active is not null}; Casts={casts}; Catches={catches}; Retrieves={retrieves}; Ignored={ignored}; Contracts={validLogs.Count}/{FishingLogContracts.Count}\nSpot={spot?.Id}; PendingSpot={pendingSpot?.Id}; Equipped={native?.EquippedBait}; Storage={view.Storage}; PersistEnabled={writable}\nTimerEnd=HookObservation; TripLimit={FishingAttempts.TripCapacity}; CastLimit={FishingAttempts.Capacity}\nMoochLogs={moochLogs}; MoochApplied={moochApplied}; Failures={failures}; LastFailureLog={lastFailureLog}\nActualMoochBait=LocalLog1121AndCastFlags; UnvalidatedResults=Unknown; OutcomeDoesNotSetCollection=True\nSpotState={spotState}; DiscoveryMessages={discoveryMessages}; AppliedDiscoveries={appliedDiscoveries}; LastDiscoveryPlace={lastDiscoveryPlace}; LastDiscoveryActor={lastDiscoveryActor}; RecordedSpotUpdates={recordedSpotUpdates}\nTimerRetentionSeconds=300; RetainedUntilSeconds={timerRetainedUntil?.TotalSeconds}; TripDuration=FirstObservedCastToEndMonotonic; MoochLink=VerifiedBaitAndPreviousCatch");
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
    private sealed record LogFact(int Session, uint Territory, TimeSpan At, DateTimeOffset Utc, uint Id, uint Value, FishingLogActor Actor);
}
