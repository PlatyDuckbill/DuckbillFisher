using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DuckbillFisher.Plugin;

internal enum BaitPageState { Unsupported, Inconsistent, TargetAbsent, Ready }
internal enum BaitAccentSource { Neutral, Label, SelectedPage }
internal enum BaitPageCheck { Buttons, Vector, Rows, VisibleMatch, Complete }
internal sealed record BaitPageReading(BaitPageState State, int Current = 0, int Target = 0, int Total = 0,
    int Entries = 0, int Compared = 0, int Theme = -1, Vector4 Accent = default,
    BaitAccentSource AccentSource = BaitAccentSource.Neutral, bool MarkerVisible = false,
    Vector2 Position = default, Vector2 Size = default, BaitPageCheck Check = BaitPageCheck.Buttons);

/// <summary>KR OnRefresh consumes AtkValues[6] as five values per bait and 25 baits per page.</summary>
internal static class NativeBaitPageReader
{
    private const int Columns = 5;
    private const int PerPage = 25;
    private const int PageButtons = 9;

    // Called only by the guarded Framework reader; no pointer leaves this call.
    public static unsafe BaitPageReading Read(AtkUnitBase* addon, uint target,
        IReadOnlyDictionary<int, uint> visibleItems, Func<uint, bool> isBait, int theme)
    {
        var reading = new BaitPageReading(BaitPageState.Unsupported, Theme: theme, Accent: Vector4.One);
        Span<nint> buttons = stackalloc nint[PageButtons];
        buttons.Clear();
        var checkedCount = 0;
        var current = 0;
        var manager = addon->UldManager;
        for (var i = 0; i < manager.NodeListCount; i++)
        {
            var node = manager.NodeList[i];
            if (node == null) continue;
            if (node->NodeId is 15 or 45 && node->Type == NodeType.Text && reading.AccentSource == BaitAccentSource.Neutral &&
                TextColor((AtkTextNode*)node) is { } label)
                reading = reading with { Accent = label, AccentSource = BaitAccentSource.Label };
            if (node->NodeId is < 6 or > 14) continue;
            if ((uint)node->Type != 1010 || ((AtkComponentNode*)node)->Component == null) return reading;
            var index = (int)node->NodeId - 6;
            if (buttons[index] != 0) return reading;
            buttons[index] = (nint)node;
            var button = (AtkComponentRadioButton*)((AtkComponentNode*)node)->Component;
            if (!button->IsChecked) continue;
            checkedCount++;
            current = index + 1;
            if (TextColor(button->ButtonTextNode) is { } selected)
                reading = reading with { Accent = selected, AccentSource = BaitAccentSource.SelectedPage };
        }
        for (var i = 0; i < buttons.Length; i++) if (buttons[i] == 0) return reading;
        reading = reading with { Check = BaitPageCheck.Vector };
        if (addon->AtkValues == null || addon->AtkValuesCount != 8 ||
            (addon->AtkValues[6].Type & AtkValueType.TypeMask) != AtkValueType.Vector) return reading;
        var vector = addon->AtkValues[6].Vector;
        if (vector == null) return reading;
        var begin = (nuint)vector->First;
        var last = (nuint)vector->Last;
        var end = (nuint)vector->End;
        var rowBytes = (nuint)(Columns * sizeof(AtkValue));
        if (begin == 0 || begin % 8 != 0 || last < begin || end < last ||
            last - begin > (nuint)(PerPage * PageButtons) * rowBytes ||
            (last - begin) % rowBytes != 0) return reading;
        var count = (int)((last - begin) / rowBytes);
        if (count == 0) return reading;
        var pages = (count + PerPage - 1) / PerPage;
        if (pages == 1 && checkedCount == 0) current = 1;
        else if (checkedCount != 1) return reading;
        reading = reading with { Current = current, Total = pages, Entries = count, Check = BaitPageCheck.Rows };
        if (current < 1 || current > pages) return reading with { State = BaitPageState.Inconsistent };

        var values = vector->First;
        var ids = new uint[count];
        var unique = new HashSet<uint>();
        var targetIndex = -1;
        for (var i = 0; i < count; i++)
        {
            var value = values[i * Columns];
            // The other columns are quantity, availability, name and icon. No string pointers are read.
            if ((value.Type & AtkValueType.TypeMask) != AtkValueType.Int || value.Int <= 0) return reading;
            var id = (uint)value.Int;
            if (!isBait(id) || !unique.Add(id)) return reading with { State = BaitPageState.Inconsistent };
            ids[i] = id;
            if (id == target) targetIndex = i;
        }
        var start = (current - 1) * PerPage;
        reading = reading with { Check = BaitPageCheck.VisibleMatch };
        if (visibleItems.Count != Math.Min(PerPage, count - start))
            return reading with { State = BaitPageState.Inconsistent };
        var compared = 0;
        foreach (var slot in visibleItems)
        {
            if (slot.Key is < 0 or >= PerPage || start + slot.Key >= count || ids[start + slot.Key] != slot.Value)
                return reading with { State = BaitPageState.Inconsistent, Compared = compared };
            compared++;
        }
        reading = reading with { Compared = compared, Check = BaitPageCheck.Complete };
        if (targetIndex < 0) return reading with { State = target == 0 ? BaitPageState.Ready : BaitPageState.TargetAbsent };
        var targetPage = targetIndex / PerPage + 1;
        reading = reading with { State = BaitPageState.Ready, Target = targetPage };
        var destination = (AtkResNode*)buttons[targetPage - 1];
        if (pages < 2 || targetPage == current || !NativeBaitUiReader.Visible(destination) ||
            !NativeBaitUiReader.TryBounds(destination, addon->RootNode, out var position, out var size, 8)) return reading;
        return reading with { MarkerVisible = true, Position = position, Size = size };
    }

    private static unsafe Vector4? TextColor(AtkTextNode* text)
    {
        if (text == null || text->Type != NodeType.Text || text->TextColor.A == 0) return null;
        var color = text->TextColor;
        return new(color.R / 255f, color.G / 255f, color.B / 255f, 1);
    }
}
