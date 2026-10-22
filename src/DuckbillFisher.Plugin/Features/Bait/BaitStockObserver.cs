using System.Collections.Frozen;
using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;

namespace DuckbillFisher.Plugin;

internal enum BaitStockState { Waiting, Ready, Unavailable, Unsupported }
internal sealed record BaitStockView(int Session, BaitStockState State, IReadOnlyDictionary<uint, long> Counts)
{
    public long? Quantity(uint id) => State == BaitStockState.Ready ? Counts.GetValueOrDefault(id) : null;
}

/// <summary>Copies public inventory spans on Framework only. No retained game addresses or stored counts.</summary>
internal sealed class BaitStockObserver : IDisposable
{
    private static readonly GameInventoryType[] Bags =
        [GameInventoryType.Inventory1, GameInventoryType.Inventory2, GameInventoryType.Inventory3, GameInventoryType.Inventory4];
    private readonly IGameInventory inventory;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly ICondition conditions;
    private readonly IFramework framework;
    private readonly FishPreferences preferences;
    private readonly Func<bool> compatible;
    private readonly Dictionary<uint, long> totals = [];
    private BaitStockView view = new(-1, BaitStockState.Waiting, FrozenDictionary<uint, long>.Empty);
    private long sampledAt;
    private long sessionReadyAt;
    private int session = -1;
    private bool disposed;

    public BaitStockObserver(IGameInventory inventory, IClientState client, IPlayerState player, ICondition conditions,
        IFramework framework, FishPreferences preferences, Func<bool> compatible)
    {
        this.inventory = inventory; this.client = client; this.player = player; this.conditions = conditions;
        this.framework = framework; this.preferences = preferences; this.compatible = compatible;
        framework.Update += Update;
    }

    public BaitStockView View => Volatile.Read(ref view);

    private void Publish(int currentSession, BaitStockState state, IReadOnlyDictionary<uint, long>? counts = null)
    {
        var previous = View;
        if (previous.Session == currentSession && previous.State == state &&
            (counts is null ? previous.Counts.Count == 0 : counts.Count == previous.Counts.Count &&
                counts.All(pair => previous.Counts.TryGetValue(pair.Key, out var value) && value == pair.Value))) return;
        // The sampling buffer is reused; only immutable copies may escape to readers.
        Volatile.Write(ref view, new(currentSession, state, counts?.ToFrozenDictionary() ?? FrozenDictionary<uint, long>.Empty));
    }

    private void Update(IFramework _)
    {
        if (disposed) return;
        var personal = preferences.View;
        if (session != personal.Session)
        {
            session = personal.Session;
            sessionReadyAt = Stopwatch.GetTimestamp();
            sampledAt = 0;
            Publish(session, BaitStockState.Waiting);
        }
        if (!personal.HasCharacter || !client.IsLoggedIn || !player.IsLoaded || player.ContentId != personal.CharacterId ||
            conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51])
        {
            Publish(session, BaitStockState.Waiting);
            sessionReadyAt = Stopwatch.GetTimestamp();
            return;
        }
        if (!compatible()) { Publish(session, BaitStockState.Unsupported); return; }
        if (Stopwatch.GetElapsedTime(sessionReadyAt).TotalSeconds < 1 ||
            (sampledAt != 0 && Stopwatch.GetElapsedTime(sampledAt).TotalMilliseconds < 500)) return;
        sampledAt = Stopwatch.GetTimestamp();
        totals.Clear();
        foreach (var bag in Bags)
        {
            var slots = inventory.GetInventoryItems(bag);
            // An absent container is not an empty bag. Reject partial/uninitialised spans.
            if (slots.IsEmpty || slots.Length > 200) { Publish(session, BaitStockState.Unavailable); return; }
            for (var i = 0; i < slots.Length; i++)
            {
                ref readonly var item = ref slots[i];
                if (item.IsEmpty) continue;
                if (item.InventorySlot != i || item.ContainerType != bag)
                { Publish(session, BaitStockState.Unavailable); return; }
                var id = item.BaseItemId;
                var count = item.Quantity;
                if (id == 0 || count <= 0) { Publish(session, BaitStockState.Unavailable); return; }
                totals[id] = totals.GetValueOrDefault(id) + count;
            }
        }
        if (!client.IsLoggedIn || !player.IsLoaded || player.ContentId != personal.CharacterId ||
            preferences.View.Session != session)
        { Publish(session, BaitStockState.Waiting); return; }
        Publish(session, BaitStockState.Ready, totals);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        framework.Update -= Update;
        Publish(-1, BaitStockState.Waiting);
    }
}
