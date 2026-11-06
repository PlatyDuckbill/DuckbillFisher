using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DuckbillFisher.Plugin;

internal static class UiThemeCharts
{
    // Same item size as the original bar. Label and fill have separate lanes, so neither obscures the other.
    public static void Collection(float fraction, Vector2 size, string label, bool complete)
    {
        if (UiThemeScope.Current is not { } p) return;
        var start = ImGui.GetCursorScreenPos();
        var end = start + size;
        var draw = ImGui.GetWindowDrawList();
        var unit = ImGui.GetFontSize();
        var pad = Math.Min(unit * .35f, size.X / 4);
        var trackHeight = Math.Max(2, unit * .20f);
        var a = new Vector2(start.X + pad, end.Y - trackHeight - 1);
        var b = new Vector2(end.X - pad, end.Y - 1);
        draw.AddRectFilled(start, end, ImGui.GetColorU32(p.Input), unit * .12f);
        draw.AddRectFilled(a, b, ImGui.GetColorU32(p.Border), trackHeight / 2);
        var fill = Math.Clamp(fraction, 0, 1);
        var color = complete ? UiThemeColors.Positive : UiThemeColors.Information;
        if (fill > 0)
        {
            var fillEnd = new Vector2(a.X + (b.X - a.X) * fill, b.Y);
            draw.AddRectFilled(a, fillEnd, ImGui.GetColorU32(color), trackHeight / 2);
        }
        draw.PushClipRect(start, end, true);
        var text = ImGui.CalcTextSize(label);
        var labelInset = pad + (complete ? unit * .8f : 0);
        draw.AddText(new(start.X + Math.Max(labelInset, labelInset + (size.X - labelInset - pad - text.X) / 2),
            start.Y + Math.Max(0, (size.Y - trackHeight - unit - 2) / 2)), ImGui.GetColorU32(p.Text), label);
        if (complete)
        {
            // Completion also has a tick, independent of hue; reserve its space in the caller's fit calculation.
            var x = start.X + pad;
            var y = start.Y + Math.Max(unit * .5f, (size.Y - trackHeight) / 2);
            draw.AddLine(new(x, y), new(x + unit * .18f, y + unit * .18f), ImGui.GetColorU32(color), 2);
            draw.AddLine(new(x + unit * .18f, y + unit * .18f), new(x + unit * .5f, y - unit * .2f), ImGui.GetColorU32(color), 2);
        }
        draw.PopClipRect();
        ImGui.Dummy(size);
    }

    public static void ScheduleBar(Vector2 start, Vector2 end, bool open, bool selected)
    {
        if (UiThemeScope.Current is not { } p) return;
        var draw = ImGui.GetWindowDrawList();
        var color = open ? UiThemeColors.Positive : UiThemeColors.Information;
        const float radius = 2;
        draw.AddRectFilled(start, end, ImGui.GetColorU32(open ? color : UiThemeColors.Surface(color, .18f)), radius);
        if (!open) draw.AddRect(start, end, ImGui.GetColorU32(color), radius);
        if (selected)
        {
            draw.AddRect(start - new Vector2(2), end + new Vector2(2), ImGui.GetColorU32(p.Text), radius);
            draw.AddRectFilled(start - new Vector2(2), new(start.X, end.Y + 2), ImGui.GetColorU32(p.Text));
        }
    }
}
