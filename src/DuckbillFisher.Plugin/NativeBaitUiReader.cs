using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;

namespace DuckbillFisher.Plugin;

internal enum BaitUiState { Unavailable, NotOpen, UnsupportedShape, BindingUnavailable, NotVisible, Found }
internal sealed record BaitUiReading(BaitUiState State, Vector2 Position = default, Vector2 Size = default,
    string Bindings = "");

/// <summary>Borrowed KR UI pointers stay inside one Framework callback. No native function calls or writes.</summary>
internal sealed class NativeBaitUiReader(IGameGui gui, IFramework framework, Func<bool> compatible)
{
    // The embedded field is internal in the supplied binding. Offset 0xD2690 was checked in all
    // three exact KR DLL profiles and their pinned source; avoid invoking the virtual getter.
    private const int RaptureAtkModuleOffset = 0xD2690;
    public unsafe BaitUiReading Read(uint target, bool diagnostics = false)
    {
        if (!framework.IsInFrameworkUpdateThread || !compatible()) return new(BaitUiState.Unavailable);
        var handle = gui.GetAddonByName("Bait");
        if (handle.IsNull) return new(BaitUiState.NotOpen);
        var addon = (AtkUnitBase*)handle.Address;
        if (!addon->IsVisible) return new(BaitUiState.NotOpen);
        var nodes = addon->UldManager;
        if (nodes.NodeList == null || nodes.NodeListCount is < 42 or > 64 || addon->RootNode == null)
            return new(BaitUiState.UnsupportedShape);
        var module = gui.GetUIModule();
        if (module.IsNull) return new(BaitUiState.Unavailable);
        var stage = ((RaptureAtkModule*)((byte*)module.Address + RaptureAtkModuleOffset))->AtkModule.AtkStage;
        if (stage == null) return new(BaitUiState.Unavailable);
        ref var tooltips = ref stage->TooltipManager.TooltipMap;
        if (tooltips.LongCount is < 0 or > 8192) return new(BaitUiState.UnsupportedShape);
        var bindings = diagnostics ? new List<string>(25) : null;
        var matches = 0;
        AtkResNode* matched = null;
        var grids = 0;
        for (var i = 0; i < nodes.NodeListCount; i++)
        {
            var node = nodes.NodeList[i];
            if (node == null || node->NodeId is < 17 or > 41) continue;
            if ((uint)node->Type != 1005 || node->Width != 44 || node->Height != 48)
                return new(BaitUiState.UnsupportedShape);
            grids++;
            // The inspected ULD is a button with five internal nodes, not a list of item IDs.
            var component = ((AtkComponentNode*)node)->Component;
            if (component == null || component->UldManager.NodeList == null ||
                component->UldManager.NodeListCount is < 1 or > 16) return new(BaitUiState.UnsupportedShape);
            uint item = 0;
            var ambiguous = false;
            for (var childIndex = -1; childIndex < component->UldManager.NodeListCount; childIndex++)
            {
                var child = childIndex < 0 ? node : component->UldManager.NodeList[childIndex];
                if (child == null) continue;
                Pointer<AtkResNode> key = child;
                if (!tooltips.TryGetValue(key, out var value, false)) continue;
                var info = (AtkTooltipManager.AtkTooltipInfo*)value;
                if (info == null || info->ParentId != addon->Id) continue;
                if (info->Type != AtkTooltipType.Item)
                { bindings?.Add($"{node->NodeId}/{child->NodeId}:type={(byte)info->Type}"); continue; }
                var args = info->AtkTooltipArgs.ItemArgs;
                bindings?.Add($"{node->NodeId}/{child->NodeId}:kind={(byte)args.Kind}:id={args.ItemId}");
                // InventoryItem stores a container at the same union offset. Never interpret it as an item ID.
                if (args.Kind is not (DetailKind.Item or DetailKind.ItemId) || args.ItemId <= 0) continue;
                var id = (uint)args.ItemId;
                if (item != 0 && item != id) ambiguous = true;
                item = id;
            }
            if (!ambiguous && item == target && target != 0) { matched = node; matches++; }
        }
        var report = bindings is null ? "" : $"MapCount={tooltips.LongCount}; Entries=" + string.Join(',', bindings.Take(100));
        if (grids != 25) return new(BaitUiState.UnsupportedShape, Bindings: report);
        if (matches != 1 || matched == null) return new(BaitUiState.BindingUnavailable, Bindings: report);
        if (!Visible(matched) || !Visible(addon->RootNode)) return new(BaitUiState.NotVisible, Bindings: report);
        var position = new Vector2(matched->ScreenX, matched->ScreenY);
        var scale = matched->Transform;
        var size = new Vector2(matched->Width * scale.M11, matched->Height * scale.M22);
        var root = addon->RootNode;
        var rootEnd = new Vector2(root->ScreenX + root->Width * root->Transform.M11,
            root->ScreenY + root->Height * root->Transform.M22);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
            !float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X is < 12 or > 180 || size.Y is < 12 or > 180 ||
            Math.Abs(scale.M12) > 0.01f || Math.Abs(scale.M21) > 0.01f ||
            position.X < root->ScreenX || position.Y < root->ScreenY ||
            position.X + size.X > rootEnd.X + 1 || position.Y + size.Y > rootEnd.Y + 1)
            return new(BaitUiState.NotVisible, Bindings: report);
        return new(BaitUiState.Found, position, size, report);
    }

    private static unsafe bool Visible(AtkResNode* node)
    {
        for (var depth = 0; depth < 24; depth++)
        {
            if (node == null) return true;
            if ((node->NodeFlags & NodeFlags.Visible) == 0) return false;
            node = node->ParentNode;
        }
        return false;
    }
}
