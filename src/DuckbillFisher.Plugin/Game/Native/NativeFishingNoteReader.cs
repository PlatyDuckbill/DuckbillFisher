using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using NativePlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace DuckbillFisher.Plugin;

internal enum FishingNoteMapState
{
    NotSampled, DataWaiting, DataUnavailable, PlayerUnavailable, WrongThread, NotOpen,
    UnsupportedShape, UnsupportedMode, BindingMismatch, DiscoveryUnknown, DiscoveryMismatch,
    Discovered, Undiscovered, MapUnavailable, Requested,
}

internal sealed record FishingNoteMapReading(FishingNoteMapState State, uint SpotId = 0,
    uint AreaId = 0, int Ordinal = -1, uint Flags = 0, int ItemCount = 0, string Check = "");

/// <summary>Reads only within the borrowed FishingNote receive-event lifetime. Never writes UI or invokes callbacks.</summary>
internal sealed class NativeFishingNoteReader(IGameGui gui, IFramework framework)
{
    public unsafe FishingNoteMapReading Read(AddonReceiveEventArgs args, ulong character,
        IReadOnlyDictionary<(uint Region, uint Area), FishingNoteArea> areas)
    {
        if (!framework.IsInFrameworkUpdateThread) return new(FishingNoteMapState.WrongThread);
        var handle = gui.GetAddonByName("FishingNote");
        if (handle.IsNull || handle.Address != args.Addon.Address) return new(FishingNoteMapState.NotOpen);
        var addon = (AtkUnitBase*)handle.Address;
        if (!addon->IsVisible || !addon->IsReady) return new(FishingNoteMapState.NotOpen);
        if (addon->RootNode == null || addon->UldManager.NodeList == null ||
            addon->UldManager.NodeListCount is < 40 or > 64)
            return new(FishingNoteMapState.UnsupportedShape, Check: "AddonNodes");

        // FishingNoteBook.uld: node 13 is the fishing-hole tree list (node 10 is the region list).
        // Find it in the current owner's node list, without calling a native node getter.
        AtkComponentTreeList* tree = null;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->NodeId != 13) continue;
            if ((uint)node->Type != 1025) return new(FishingNoteMapState.UnsupportedShape, Check: "TreeNodeType");
            var component = ((AtkComponentNode*)node)->Component;
            if (component == null || component->UldManager.NodeList == null ||
                component->UldManager.NodeListCount is < 7 or > 16 || component->UldManager.Objects == null ||
                ((AtkUldComponentInfo*)component->UldManager.Objects)->ComponentType != ComponentType.TreeList)
                return new(FishingNoteMapState.UnsupportedShape, Check: "TreeComponent");
            tree = (AtkComponentTreeList*)component;
            break;
        }
        if (tree == null || tree->Items.First == null || (nuint)tree->Items.Last < (nuint)tree->Items.First ||
            (nuint)tree->Items.End < (nuint)tree->Items.Last ||
            ((nuint)tree->Items.Last - (nuint)tree->Items.First) % (nuint)sizeof(nint) != 0 ||
            tree->Items.LongCount is < 1 or > 256 || tree->ItemCount != tree->Items.LongCount)
            return new(FishingNoteMapState.UnsupportedShape, Check: "TreeItems");
        var count = (int)tree->Items.LongCount;
        if (args.AtkEventData == 0) return new(FishingNoteMapState.BindingMismatch, Check: "EventData");
        var click = ((AtkEventData*)args.AtkEventData)->ListItemData;
        if (click.MouseButtonId != 0 || click.SelectedIndex < 0 || click.SelectedIndex >= count)
            return new(FishingNoteMapState.BindingMismatch, Check: "ClickIndex");
        var item = tree->Items[click.SelectedIndex].Value;
        if (item == null || item != click.ListItem || item->Renderer == null || item->Renderer != click.ListItemRenderer ||
            !HasRowValues(item) || item->IsHidden ||
            (item->Type & (TreeListItemType.Group | TreeListItemType.SectionHeader)) != 0)
            return new(FishingNoteMapState.BindingMismatch, Check: "ClickItem");

        var binding = item->UIntValues[1];
        var areaId = binding >> 16;
        var ordinal = (int)(binding & 0xFFFF);
        var flags = item->UIntValues[2];
        FishingNoteMapReading Result(FishingNoteMapState state, uint id = 0, string check = "") => new(state, id, areaId, ordinal, flags, count, check);
        var agentHandle = gui.GetAgentById((int)AgentId.FishingNote);
        if (agentHandle.IsNull) return Result(FishingNoteMapState.NotOpen);
        var agent = (AgentFishingNote*)agentHandle.Address;
        if (agent->AddonId != addon->Id) return Result(FishingNoteMapState.BindingMismatch, check: "AgentAddon");
        if (agent->Mode != 0) return Result(FishingNoteMapState.UnsupportedMode);
        if (!areas.TryGetValue((agent->SelectedRegionPlaceNameId, areaId), out var area) ||
            ordinal >= area.Spots.Length || area.MapId == 0)
            return Result(FishingNoteMapState.BindingMismatch, check: "AreaIndex");

        // Cross-check the entire clicked group: header map/area, packed row index and every leaf ordinal.
        // Neither a translated label nor the selected/highlighted row alone identifies a fishing hole.
        var inArea = false;
        var leaves = 0;
        var headers = 0;
        for (var i = 0; i < count; i++)
        {
            var row = tree->Items[i].Value;
            if (!HasRowValues(row)) return Result(FishingNoteMapState.UnsupportedShape, check: "RowValues");
            var type = row->UIntValues[0];
            var value = row->UIntValues[1];
            if ((type >> 8) != i) return Result(FishingNoteMapState.BindingMismatch, check: "PackedRowIndex");
            if ((type & 0xFF) == 0x12)
            {
                inArea = (value >> 16) == areaId;
                if (inArea)
                {
                    headers++;
                    if ((value & 0xFFFF) != area.MapId) return Result(FishingNoteMapState.BindingMismatch, check: "HeaderMap");
                }
                continue;
            }
            if (!inArea) continue;
            if ((type & 0xFE) != 0x20 || (value >> 16) != areaId || (value & 0xFFFF) != leaves)
                return Result(FishingNoteMapState.BindingMismatch, check: "LeafOrder");
            leaves++;
        }
        if (headers != 1 || leaves != area.Spots.Length) return Result(FishingNoteMapState.BindingMismatch, check: "GroupCount");
        var spot = area.Spots[ordinal];
        var player = NativePlayerState.StaticAddressPointers.pInstance;
        if (player == null || !player->IsLoaded || player->ContentId != character)
            return Result(FishingNoteMapState.DiscoveryUnknown, spot.Id);
        // The notebook cache indexes discovery by FishingSpot RowId, not FishingSpot.Order.
        // Stay within both the declared bit limit and the loaded fixed buffer.
        if (spot.Id > player->UnlockedFishingSpotsBitArray.BitCount || spot.Id >= player->UnlockedFishingSpots.Length * 8)
            return Result(FishingNoteMapState.DiscoveryUnknown, spot.Id);
        var discovered = (player->UnlockedFishingSpots[(int)(spot.Id >> 3)] & (1 << (int)(spot.Id & 7))) != 0;
        if (discovered != ((flags & 4) != 0)) return Result(FishingNoteMapState.DiscoveryMismatch, spot.Id);
        return Result(discovered ? FishingNoteMapState.Discovered : FishingNoteMapState.Undiscovered, spot.Id);
    }

    private static unsafe bool HasRowValues(AtkComponentTreeListItem* row) => row != null &&
        row->UIntValues.First != null &&
        (nuint)row->UIntValues.Last == (nuint)row->UIntValues.First + 3 * sizeof(uint) &&
        (nuint)row->UIntValues.End >= (nuint)row->UIntValues.Last;
}
