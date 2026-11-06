using Dalamud.Plugin.Services;
using NativeEvents = FFXIVClientStructs.FFXIV.Client.Game.Event.EventFramework;
using NativePlayer = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;
using NativeWeather = FFXIVClientStructs.FFXIV.Client.Game.WeatherManager;
using NativeStatus = FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.InstanceContentOceanFishing.OceanFishingStatus;

namespace DuckbillFisher.Plugin;

internal enum OceanReadState { OffVoyage, Ready, PlayerUnavailable, AddressUnavailable, DirectorUnavailable, InvalidState, RouteMismatch, WrongThread, Stale }
internal enum OceanPhase { Waiting, Moving, Fishing, Arriving, Finished }
internal enum OceanWeatherReadState { NotObserved, Ready, AddressUnavailable, InvalidValue }
internal sealed record OceanNativeSample(OceanReadState State, uint RouteId = 0, int Zone = -1,
    OceanPhase Phase = OceanPhase.Waiting, bool Spectral = false, double? RemainingSeconds = null,
    uint? ContentId = null, uint? Duration = null, float? ContentSeconds = null, uint? TimeOffset = null)
{
    public byte? WeatherId { get; init; }
    public OceanWeatherReadState WeatherState { get; init; }
}

/// <summary>Framework-only, read-only adapter. Never retains a game pointer or invokes a game action.</summary>
internal sealed class NativeOceanFishingReader(IFramework framework)
{
    public unsafe OceanNativeSample Read(ulong character, uint territory, IReadOnlyDictionary<uint, OceanRoute> routes)
    {
        if (!framework.IsInFrameworkUpdateThread) return new(OceanReadState.WrongThread);
        var player = NativePlayer.StaticAddressPointers.pInstance;
        if (character == 0 || player == null || !player->IsLoaded || player->ContentId != character)
            return new(OceanReadState.PlayerUnavailable);
        if (!routes.Values.Any(route => route.TerritoryId == territory)) return new(OceanReadState.OffVoyage);
        var address = NativeEvents.StaticAddressPointers.ppInstance;
        if (address == null || *address == null || NativeEvents.MemberFunctionPointers.GetInstanceContentDirector == null)
            return new(OceanReadState.AddressUnavailable);
        // This ClientStructs accessor checks the active director's InstanceContentType before casting.
        var ocean = (*address)->GetInstanceContentOceanFishing();
        if (ocean == null) return new(OceanReadState.DirectorUnavailable);
        var routeId = ocean->CurrentRoute;
        if (!routes.TryGetValue(routeId, out var route) || route.TerritoryId != territory || ocean->ContentId != route.ContentId)
            return new(OceanReadState.RouteMismatch, routeId, ContentId: ocean->ContentId);
        if (ocean->CurrentZone > 2 || (uint)ocean->Status > 4)
            return new(OceanReadState.InvalidState, routeId, ContentId: ocean->ContentId);
        var phase = ocean->Status switch
        {
            NativeStatus.WaitingForPlayers => OceanPhase.Waiting,
            NativeStatus.SwitchingZone => OceanPhase.Moving,
            NativeStatus.Fishing => OceanPhase.Fishing,
            NativeStatus.NewZone => OceanPhase.Arriving,
            _ => OceanPhase.Finished,
        };
        double? seconds = null;
        if (phase == OceanPhase.Fishing)
        {
            var contentTime = ocean->ContentTimeLeft;
            var remaining = contentTime - ocean->TimeOffset;
            if (ocean->Duration == 420 && float.IsFinite(contentTime) && contentTime is >= 0 and <= 3600 && remaining is >= 0 and <= 420)
                seconds = remaining; // An invalid clock does not discard a valid route/current-zone observation.
        }
        byte? weather = null;
        var weatherState = phase == OceanPhase.Fishing ? OceanWeatherReadState.AddressUnavailable : OceanWeatherReadState.NotObserved;
        // Optional weather observation has its own address contract and cannot disable the voyage.
        if (phase == OceanPhase.Fishing && NativeWeather.StaticAddressPointers.pInstance is not null &&
            NativeWeather.MemberFunctionPointers.GetCurrentWeather != null)
        {
            var id = NativeWeather.StaticAddressPointers.pInstance->GetCurrentWeather();
            weatherState = id > 0 ? OceanWeatherReadState.Ready : OceanWeatherReadState.InvalidValue;
            if (id > 0) weather = id;
        }
        return new(OceanReadState.Ready, routeId, (int)ocean->CurrentZone, phase,
            phase == OceanPhase.Fishing && ocean->SpectralCurrentActive, seconds, ocean->ContentId,
            ocean->Duration, ocean->ContentTimeLeft, ocean->TimeOffset) { WeatherId = weather, WeatherState = weatherState };
    }
}
