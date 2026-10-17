using Dalamud.Plugin.Services;
using NativeEvents = FFXIVClientStructs.FFXIV.Client.Game.Event.EventFramework;
using NativePlayer = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace DuckbillFisher.Plugin;

internal enum FishingReadState
{
    Ready, Unsupported, WrongThread, PlayerUnavailable, NotFisher, AddressUnavailable, InvalidState,
}

internal sealed record FishingNativeSample(FishingReadState Availability, int? Phase = null,
    uint? EquippedBait = null, int? BaitFlags = null, int? SelectedSwimbait = null,
    uint? Swimbait0 = null, uint? Swimbait1 = null, uint? Swimbait2 = null, bool? AtFishingHole = null);

/// <summary>Read-only, inspected KR layouts. No pointer or borrowed span escapes a Framework call.</summary>
internal sealed class NativeFishingActivityReader(IFramework framework, NativeFishCollectionReader compatibility)
{
    public unsafe FishingNativeSample Read(ulong expectedCharacter, bool isFisher)
    {
        if (!compatibility.IsCompatible) return new(FishingReadState.Unsupported);
        if (!framework.IsInFrameworkUpdateThread) return new(FishingReadState.WrongThread);
        if (expectedCharacter == 0) return new(FishingReadState.PlayerUnavailable);
        if (!isFisher) return new(FishingReadState.NotFisher);

        // These generated addresses were checked against the installed DLL and KR executable.
        // A resolved signature is not treated as proof of the runtime meaning of a phase.
        var player = NativePlayer.StaticAddressPointers.pInstance;
        if (player == null || !player->IsLoaded || player->ContentId != expectedCharacter)
            return new(FishingReadState.PlayerUnavailable);
        var rootAddress = NativeEvents.StaticAddressPointers.ppInstance;
        if (rootAddress == null || *rootAddress == null)
            return new(FishingReadState.AddressUnavailable);
        var handler = (*rootAddress)->EventHandlerModule.FishingEventHandler;
        if (handler == null) return new(FishingReadState.AddressUnavailable);
        var phase = (int)handler->State;
        var selected = (int)handler->CurrentSelectedSwimBait;
        if (phase is < 0 or > 12 || selected is < -1 or > 2)
            return new(FishingReadState.InvalidState);

        // Equipped bait and actual cast bait are intentionally separate raw facts.
        // Mooch/swimbait attribution requires event-order validation before production use.
        return new(FishingReadState.Ready, phase, player->FishingBait, (int)handler->CurrentCastBaitFlags,
            selected, handler->SwimBaitItemIds[0], handler->SwimBaitItemIds[1], handler->SwimBaitItemIds[2], handler->CanFish);
    }
}
