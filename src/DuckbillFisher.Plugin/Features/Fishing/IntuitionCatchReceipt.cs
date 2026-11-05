using System.Diagnostics;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;

namespace DuckbillFisher.Plugin;

/// <summary>Correlates collectable-capable catches with actual bag receipts, without retaining inventory data.</summary>
internal sealed class IntuitionCatchReceipt : IDisposable
{
    private sealed record Receipt(int Quantity, bool Collectable, bool? Active, long At);
    private readonly IGameInventory inventory;
    private readonly IFramework framework;
    private readonly FishingTracker tracker;
    private readonly FishCatalog catalog;
    private readonly Func<bool?> readIntuition;
    private readonly Action<FishingCatchObservation> received;
    private readonly System.Action lost;
    private readonly Dictionary<(Guid Cast, uint Item), Receipt> receipts = [];
    private IReadOnlyList<FishGuideRow>? indexedRows;
    private Dictionary<uint, HashSet<uint>> preyBySpot = [];
    private HashSet<uint> allPrey = [];
    private FishingCatchObservation? waiting;
    private Guid? completed;
    private int session = -1;
    private uint territory;
    private long waitingAt;

    public IntuitionCatchReceipt(IGameInventory inventory, IFramework framework, FishingTracker tracker,
        FishCatalog catalog, Func<bool?> readIntuition, Action<FishingCatchObservation> received, System.Action lost)
    {
        this.inventory = inventory; this.framework = framework; this.tracker = tracker; this.catalog = catalog;
        this.readIntuition = readIntuition; this.received = received; this.lost = lost;
        inventory.InventoryChangedRaw += Changed;
    }

    public void Reset(int nextSession, uint nextTerritory)
    {
        session = nextSession; territory = nextTerritory;
        waiting = null; completed = null; receipts.Clear(); waitingAt = 0;
    }

    public void Catch(FishingCatchObservation caught)
    {
        if (!catalog.View.CollectableItems.Contains(caught.ItemId) || !IsPrey(caught.ItemId, caught.SpotId))
        { received(caught); return; }
        if (caught.Session != session || caught.TerritoryId != territory || caught.CastId == completed) return;
        if (waiting is { } previous && previous.CastId != caught.CastId) lost();
        waiting = caught; waitingAt = Stopwatch.GetTimestamp();
        Match();
    }

    public void Update(FishingTrackerView current)
    {
        if (current.Session != session) return;
        if (current.State != FishingTrackerState.Ready) { Reset(session, territory); return; }
        if (waiting is { } pending && current.Active is { } active && active.Id != pending.CastId)
        {
            // A new cast cannot still be waiting for the previous collectable answer.
            waiting = null; lost();
        }
        foreach (var key in receipts.Where(pair => Stopwatch.GetElapsedTime(pair.Value.At).TotalSeconds > 2)
                     .Select(pair => pair.Key).ToArray())
        { receipts.Remove(key); lost(); }
        Match();
    }

    private static bool IsBag(GameInventoryType bag) => bag is GameInventoryType.Inventory1 or
        GameInventoryType.Inventory2 or GameInventoryType.Inventory3 or GameInventoryType.Inventory4;

    private bool IsPrey(uint itemId, uint? spotId)
    {
        var rows = catalog.View.Rows;
        if (!ReferenceEquals(indexedRows, rows))
        {
            indexedRows = rows;
            preyBySpot = rows.Where(f => !f.IsOceanFishing && f.Details is { Spot: not null, Rule.Predators.Length: > 0 })
                .GroupBy(f => f.Details!.Spot!.Id).ToDictionary(group => group.Key,
                    group => group.SelectMany(f => f.Details!.Rule.Predators).Select(p => p.ItemId).ToHashSet());
            allPrey = preyBySpot.Values.SelectMany(ids => ids).ToHashSet();
        }
        return spotId is { } spot ? preyBySpot.TryGetValue(spot, out var prey) && prey.Contains(itemId) : allPrey.Contains(itemId);
    }

    private void Changed(IReadOnlyCollection<InventoryEventArgs> changes)
    {
        if (!framework.IsInFrameworkUpdateThread) { lost(); return; }
        var current = tracker.View;
        if (current.Session != session || current.State != FishingTrackerState.Ready) return;
        // Inventory receipts can precede the validated log by one Framework update.
        var candidate = current.Active is { HookSeconds: not null } active ? active.Id : waiting?.CastId;
        if (candidate is null || candidate == completed) return;
        var candidateSpot = current.Active?.Id == candidate ? current.Active.SpotId : waiting?.SpotId;
        var presence = readIntuition();
        if (presence is null) { lost(); return; }
        var deltas = new Dictionary<(uint Item, bool Collectable), long>();
        var otherContainers = new HashSet<uint>();
        void Add(in GameInventoryItem item, int direction)
        {
            if (item.IsEmpty || !catalog.View.CollectableItems.Contains(item.BaseItemId) || !IsPrey(item.BaseItemId, candidateSpot) ||
                tracker.FishForItem(item.BaseItemId) is null) return;
            if (!IsBag(item.ContainerType)) { otherContainers.Add(item.BaseItemId); return; }
            if (item.Quantity is < 1 or > 999) { lost(); return; }
            var key = (item.BaseItemId, item.IsCollectable);
            deltas[key] = deltas.GetValueOrDefault(key) + direction * (long)item.Quantity;
        }
        foreach (var change in changes)
        {
            switch (change)
            {
                case InventoryItemAddedArgs added: Add(added.Item, 1); break;
                case InventoryItemRemovedArgs removed: Add(removed.Item, -1); break;
                case InventoryItemChangedArgs changed:
                    Add(changed.OldItemState, -1); Add(changed.Item, 1); break;
            }
        }
        // Net changes across all four bags cancel moves/splits. A downgrade is never a new catch.
        var rejected = deltas.Where(pair => pair.Value < 0).Select(pair => pair.Key.Item).ToHashSet();
        foreach (var (item, quantity) in deltas)
        {
            if (quantity <= 0 || rejected.Contains(item.Item) || otherContainers.Contains(item.Item)) continue;
            if (quantity > 999 || receipts.Count >= 64) { lost(); continue; }
            var key = (candidate.Value, item.Item);
            if (receipts.ContainsKey(key)) { receipts.Remove(key); lost(); continue; }
            receipts[key] = new((int)quantity, item.Collectable, presence, Stopwatch.GetTimestamp());
        }
        Match();
    }

    private void Match()
    {
        if (waiting is not { } caught || !receipts.Remove((caught.CastId, caught.ItemId), out var receipt)) return;
        // A late receipt must not be attached to a log from before a different cast or session.
        if (receipt.At < waitingAt && Stopwatch.GetElapsedTime(receipt.At, waitingAt).TotalSeconds > 2)
        { waiting = null; lost(); return; }
        if (receipt.Collectable ? receipt.Quantity != 1 : receipt.Quantity != caught.Quantity)
        { waiting = null; lost(); return; }
        completed = caught.CastId; waiting = null;
        received(caught with { Quantity = receipt.Quantity, IntuitionActive = receipt.Active });
    }

    public void Dispose()
    {
        inventory.InventoryChangedRaw -= Changed;
        Reset(-1, 0);
    }
}
