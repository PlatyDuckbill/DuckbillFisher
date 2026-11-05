using System.Collections.Frozen;
using System.Collections.Concurrent;
using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

internal sealed record IntuitionPreyProgress(FishingItemView Item, int Observed, int Required);
internal sealed record IntuitionProgressView(int Session, FishGuideRow[] Targets, uint Selected,
    IntuitionPreyProgress[] Prey, bool? Active, bool Exact)
{
    public static IntuitionProgressView Empty { get; } = new(-1, [], 0, [], null, false);
}

/// <summary>Framework-only public status and verified catch observations; independent of timer visibility.</summary>
internal sealed class IntuitionProgress : IDisposable
{
    private readonly FishingTracker tracker;
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly IObjectTable objects;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly ICondition conditions;
    private readonly IFramework framework;
    private readonly IntuitionCatchReceipt receipts;
    private readonly IntuitionCounter counter = new();
    private IntuitionProgressView view = IntuitionProgressView.Empty;
    private FishGuideRow[] targets = [];
    private IReadOnlyList<FishGuideRow>? rows;
    private IReadOnlySet<uint> preyItems = FrozenSet<uint>.Empty;
    private uint spotId, territory, selected;
    private int session = -1, restoreRevision = -1, publishedRevision = -1;
    private Guid? lastCatch;
    private readonly ConcurrentQueue<FishingCatchObservation> pending = new();
    private int observationLost;
    private long lastUpdate;
    private long lastStatusSample;
    private long awaitingSince;
    private bool publishedSuspended;
    private (uint Id, int Session)? requested;

    public IntuitionProgress(FishingTracker tracker, FishCatalog catalog, FishPreferences preferences,
        IObjectTable objects, IClientState client, IPlayerState player, ICondition conditions, IFramework framework,
        IGameInventory inventory)
    {
        this.tracker = tracker; this.catalog = catalog; this.preferences = preferences;
        this.objects = objects; this.client = client; this.player = player; this.conditions = conditions; this.framework = framework;
        receipts = new(inventory, framework, tracker, catalog, ReadIntuition, Catch, ObservationLost);
        tracker.IntuitionAtCatch = _ => ReadIntuition();
        tracker.CatchRecorded += receipts.Catch;
        tracker.CatchObservationLost += ObservationLost;
        framework.Update += Update;
    }

    public IntuitionProgressView View => Volatile.Read(ref view);
    public void Select(uint id, int expectedSession) => requested = (id, expectedSession);

    private bool? ReadIntuition()
    {
        var personal = preferences.View;
        if (!framework.IsInFrameworkUpdateThread || !personal.HasCharacter || !client.IsLoggedIn || !player.IsLoaded ||
            player.ContentId != personal.CharacterId || player.ClassJob.RowId != FishingGameIds.FisherJob ||
            conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51]) return null;
        var local = objects.LocalPlayer;
        if (local is null || local.ClassJob.RowId != FishingGameIds.FisherJob) return null;
        bool? result = false;
        var found = false;
        foreach (var status in local.StatusList)
        {
            if (status.StatusId != FishingGameIds.IntuitionStatus) continue;
            if (found || !float.IsFinite(status.RemainingTime) || status.RemainingTime < 0) return null;
            found = true;
            result = true;
        }
        return result;
    }

    private void ObservationLost() => Interlocked.Exchange(ref observationLost, 1);

    private void Catch(FishingCatchObservation caught)
    {
        if (pending.Count >= 64) { ObservationLost(); return; }
        pending.Enqueue(caught);
    }

    private void ApplyCatch(FishingCatchObservation caught, FishingTrackerView current)
    {
        if (caught.Session != session || caught.TerritoryId != territory || caught.CastId == lastCatch) return;
        // A first local catch can identify the fishing hole after CatchRecorded was emitted.
        if (caught.SpotId is null && current.SpotState == FishingSpotState.Known &&
            current.CurrentRecords.FirstOrDefault(c => c.Id == caught.CastId && c.Outcome == FishingOutcome.Caught) is { SpotId: { } resolved })
            caught = caught with { SpotId = resolved };
        lastCatch = caught.CastId;
        if (caught.SpotId != spotId || spotId == 0) { counter.Reset(); return; }
        if (preyItems.Contains(caught.ItemId))
            counter.Catch(caught.ItemId, caught.Quantity, caught.IntuitionActive, targets.Length == 1);
        else counter.ObserveBuff(caught.IntuitionActive, targets.Length == 1);
    }

    private void Update(IFramework _)
    {
        var personal = preferences.View;
        var current = tracker.View;
        var now = Stopwatch.GetTimestamp();
        var changed = false;
        if (session != personal.Session || restoreRevision != personal.SettingsRestoreRevision)
        {
            session = personal.Session; restoreRevision = personal.SettingsRestoreRevision;
            counter.Reset(); spotId = territory = selected = 0; targets = []; preyItems = FrozenSet<uint>.Empty;
            rows = null; lastCatch = null; requested = null; changed = true;
            pending.Clear(); lastStatusSample = 0;
            awaitingSince = 0; publishedSuspended = false;
            receipts.Reset(session, client.TerritoryType);
        }
        if (lastUpdate != 0 && Stopwatch.GetElapsedTime(lastUpdate).TotalSeconds > 1)
        { counter.Reset(); pending.Clear(); receipts.Reset(session, client.TerritoryType); }
        lastUpdate = now;
        if (!personal.HasCharacter)
        { Volatile.Write(ref view, IntuitionProgressView.Empty); return; }
        if (territory != client.TerritoryType)
        {
            counter.Reset(territory != 0); territory = client.TerritoryType;
            spotId = selected = 0; targets = []; preyItems = FrozenSet<uint>.Empty; changed = true;
            awaitingSince = 0;
            receipts.Reset(session, territory);
        }
        // A different, verified cast resets game preparation. Folding the rod or closing the timer does not.
        var latestCast = current.Active ?? current.CurrentRecords.FirstOrDefault();
        var verifiedSpot = current.Session == session && current.State == FishingTrackerState.Ready && !current.IsLastSpot &&
            latestCast is { StartedUtc: not null, SpotId: { } observedSpot } && current.SpotState == FishingSpotState.Known &&
            observedSpot == current.Spot?.Id ? current.Spot?.Id : null;
        if (verifiedSpot is { } nextSpot && nextSpot != spotId)
        {
            counter.Reset(spotId != 0 || counter.Exact); spotId = nextSpot; rows = null; changed = true;
        }
        var data = catalog.View;
        if (!ReferenceEquals(rows, data.Rows))
        {
            rows = data.Rows;
            targets = rows.Where(f => !f.IsOceanFishing && f.Details is { Rule.Predators.Length: > 0 } detail &&
                detail.Spot?.Id == spotId).OrderBy(f => f.Name, ClientNameOrder.Comparer).ToArray();
            preyItems = targets.SelectMany(f => f.Details!.Rule.Predators).Select(p => p.ItemId).ToFrozenSet();
            if (!targets.Any(f => f.FishParameterId == selected)) selected = targets.FirstOrDefault()?.FishParameterId ?? 0;
            changed = true;
        }
        var suspended = current.Session == session && current.State == FishingTrackerState.Ready && !current.IsLastSpot &&
            latestCast is { StartedUtc: not null } && current.SpotState == FishingSpotState.AwaitingIdentification;
        if (suspended && awaitingSince == 0) awaitingSince = now;
        if (!suspended) awaitingSince = 0;
        if ((current.Active is not null || suspended) && current.SpotState != FishingSpotState.Known &&
            (!suspended || Stopwatch.GetElapsedTime(awaitingSince).TotalSeconds > 2))
        {
            counter.ObserveBuff(null, false); pending.Clear(); receipts.Reset(session, territory);
            spotId = selected = 0; targets = []; preyItems = FrozenSet<uint>.Empty; rows = data.Rows;
            publishedRevision = -1;
            Volatile.Write(ref view, IntuitionProgressView.Empty); return;
        }
        if (requested is { } choose)
        {
            requested = null;
            if (choose.Session == session && targets.Any(f => f.FishParameterId == choose.Id)) { selected = choose.Id; changed = true; }
        }
        receipts.Update(current);
        if (Interlocked.Exchange(ref observationLost, 0) != 0) counter.Reset();
        while (!suspended && pending.TryDequeue(out var caught))
            if (current.MultipleCatchSupported) ApplyCatch(caught, current);
        if (!current.MultipleCatchSupported) counter.ObserveBuff(null, false);
        if (targets.Length > 0 && (lastStatusSample == 0 || Stopwatch.GetElapsedTime(lastStatusSample).TotalMilliseconds >= 200 || changed))
        {
            lastStatusSample = now;
            var presence = current.Session == session && current.State == FishingTrackerState.Ready && current.MultipleCatchSupported ? ReadIntuition() : null;
            counter.ObserveBuff(presence, targets.Length == 1 && !suspended);
        }
        if (!changed && publishedRevision == counter.Revision && publishedSuspended == suspended) return;
        publishedRevision = counter.Revision;
        publishedSuspended = suspended;
        var target = targets.FirstOrDefault(f => f.FishParameterId == selected)?.Details;
        var prey = target is null ? [] : target.Predators.Select((item, index) => new IntuitionPreyProgress(item,
            counter.Counts.GetValueOrDefault(item.Id), target.Rule.Predators[index].Count))
            .OrderBy(p => p.Observed >= p.Required).ToArray();
        Volatile.Write(ref view, new(session, targets, selected, prey, suspended ? null : counter.Active, !suspended && counter.Exact));
    }

    public void Dispose()
    {
        framework.Update -= Update; tracker.CatchRecorded -= receipts.Catch; tracker.CatchObservationLost -= ObservationLost;
        receipts.Dispose();
        tracker.IntuitionAtCatch = null; pending.Clear(); counter.Reset(); Volatile.Write(ref view, IntuitionProgressView.Empty);
    }
}
