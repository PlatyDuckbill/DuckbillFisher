using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace DuckbillFisher.Plugin;

internal enum FishingMovementGuardState { Disabled, Waiting, Ready, AddressUnavailable, ContractMismatch, InvalidState, HookUnavailable, WrongThread }

/// <summary>Suppresses only the fishing handler's movement-cancel callback. Never sends input or invokes an action.</summary>
internal sealed unsafe class NativeFishingMovementGuard : IDisposable
{
    private readonly IGameInteropProvider interop;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly ICondition conditions;
    private readonly FishPreferences preferences;
    private readonly NativeFishingActivityReader reader;
    private readonly IPluginLog log;
    private Hook<FishingEventHandler.Delegates.CancelByPlayerMovement>? hook;
    private volatile bool disposed;
    private bool hookFailed;
    private long hookFailedAt;
    private nint hookSourceAddress;
    private int state;

    public NativeFishingMovementGuard(IGameInteropProvider interop, IFramework framework, IClientState client,
        IPlayerState player, ICondition conditions, FishPreferences preferences, NativeFishingActivityReader reader, IPluginLog log)
    {
        this.interop = interop; this.framework = framework; this.client = client; this.player = player;
        this.conditions = conditions; this.preferences = preferences; this.reader = reader; this.log = log;
        framework.Update += Update;
    }

    public FishingMovementGuardState State => (FishingMovementGuardState)Volatile.Read(ref state);
    private void SetState(FishingMovementGuardState value) => Volatile.Write(ref state, (int)value);

    private bool HasCurrentFisher()
    {
        var personal = preferences.View;
        return personal.HasCharacter && client.IsLoggedIn && player.IsLoaded && player.ContentId == personal.CharacterId &&
            player.ClassJob.RowId == FishingGameIds.FisherJob &&
            !conditions[ConditionFlag.BetweenAreas] && !conditions[ConditionFlag.BetweenAreas51];
    }

    private void Update(IFramework _)
    {
        if (disposed) return;
        if (!preferences.View.Timer.PreventMovementCancel)
        {
            hook?.Dispose(); hook = null; hookSourceAddress = 0; hookFailed = false;
            SetState(FishingMovementGuardState.Disabled);
            return;
        }
        if (!HasCurrentFisher()) { SetState(FishingMovementGuardState.Waiting); return; }
        if (hookFailed && Stopwatch.GetElapsedTime(hookFailedAt).TotalSeconds < 5)
        { SetState(FishingMovementGuardState.HookUnavailable); return; }
        // Resolved by the running ClientStructs, never a signature or vtable slot owned by this plugin.
        var table = FishingEventHandler.StaticVirtualTablePointer;
        if (table == null || table->CancelByPlayerMovement == null)
        { SetState(FishingMovementGuardState.AddressUnavailable); return; }
        var address = (nint)table->CancelByPlayerMovement;
        if (hook is not null)
        {
            if (hookSourceAddress != address)
            { SetState(FishingMovementGuardState.ContractMismatch); return; }
            // Preserve a failed live contract until a later callback verifies it, or the option is switched off.
            return;
        }
        // Only hook creation is caught. A catch cannot make an invalid game pointer safe.
        try
        {
            hook = interop.HookFromAddress<FishingEventHandler.Delegates.CancelByPlayerMovement>(
                address, OnMovementCancel);
            // Dalamud follows existing jumps. Hook.Address may be the resolved backend target,
            // while the ClientStructs vtable still contains the original function entry.
            hookSourceAddress = address;
            hook.Enable();
            hookFailed = false;
            SetState(FishingMovementGuardState.Ready);
        }
        catch (Exception ex)
        {
            hook?.Dispose(); hook = null; hookSourceAddress = 0;
            if (!hookFailed) log.Warning("FishingMovementGuard unavailable; retrying in 5 seconds: {Reason}", ex.GetType().Name);
            hookFailed = true; hookFailedAt = Stopwatch.GetTimestamp();
            SetState(FishingMovementGuardState.HookUnavailable);
        }
    }

    private void OnMovementCancel(FishingEventHandler* self, bool a2, bool a3)
    {
        var original = hook!.Original;
        if (CanSuppress(self)) return;
        original(self, a2, a3);
    }

    private bool CanSuppress(FishingEventHandler* self)
    {
        if (disposed || !preferences.View.Timer.PreventMovementCancel || hookFailed) return false;
        if (!framework.IsInFrameworkUpdateThread)
        { SetState(FishingMovementGuardState.WrongThread); return false; }
        if (!HasCurrentFisher() || !conditions[ConditionFlag.Fishing])
        { SetState(FishingMovementGuardState.Waiting); return false; }
        var table = FishingEventHandler.StaticVirtualTablePointer;
        var root = EventFramework.StaticAddressPointers.ppInstance;
        if (table == null || root == null || *root == null)
        { SetState(FishingMovementGuardState.AddressUnavailable); return false; }
        // Check identity against the current root before dereferencing the callback's borrowed handler.
        if (self == null || (*root)->EventHandlerModule.FishingEventHandler != self ||
            self->VirtualTable != table || (nint)table->CancelByPlayerMovement != hookSourceAddress)
        { SetState(FishingMovementGuardState.ContractMismatch); return false; }
        var sample = reader.Read(preferences.View.CharacterId, true);
        if (sample.Availability != FishingReadState.Ready)
        { SetState(FishingMovementGuardState.InvalidState); return false; }
        SetState(FishingMovementGuardState.Ready);
        // Keep explicit Quit and unknown/idle transitions untouched; PoleReady covers the wait between casts.
        return (FishingState?)sample.Phase is FishingState.CastingOut or FishingState.PullingPoleIn or FishingState.PoleReady or
            FishingState.Bite or FishingState.Hooking or FishingState.ReleasingCatch or FishingState.ConfirmingCollectable or
            FishingState.AmbitiousLure or FishingState.ModestLure or FishingState.LineInWater;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        framework.Update -= Update;
        hook?.Dispose(); hook = null; hookSourceAddress = 0;
    }
}
