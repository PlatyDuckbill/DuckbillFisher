using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DuckbillFisher.Plugin;

internal static class UiThemeDecorations
{
    // The same selection hierarchy uses silver on Graphite and ivory on Paper.
    public static Vector4 NavigationSelection => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0xf7f5e9U : 0x656d69U);
    public static Vector4 NavigationHover => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0xeaf0e7U : 0x3b403fU);
    public static Vector4 NavigationAccent => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0x76541eU : 0xead4a3U);

    public static void NavigationBand(Vector2 start, float width, float height)
    {
        if (UiThemeScope.Current is not { } theme || width <= 1 || height <= 1) return;
        var draw = ImGui.GetWindowDrawList();
        var end = start + new Vector2(width, height);
        var top = ImGui.GetColorU32(UiThemes.Hex(theme.IsLight ? 0xe1e3daU : 0x303535U));
        var bottom = ImGui.GetColorU32(UiThemes.Hex(theme.IsLight ? 0xd0d7cdU : 0x181b1cU));
        draw.AddRectFilledMultiColor(start, end, top, top, bottom, bottom);
        draw.AddLine(start, new(end.X, start.Y),
            ImGui.GetColorU32(Vector4.Lerp(theme.Surface, theme.Border, .65f)));
        draw.AddLine(new(start.X, end.Y - 1), end - new Vector2(0, 1),
            ImGui.GetColorU32(UiThemes.Hex(theme.IsLight ? 0xadb9b0U : 0x101213U)));
    }

    public static void ListItemSurface(Vector2 start, float width, float height, bool selected, bool hovered)
    {
        if (UiThemeScope.Current is not { } theme || width <= 1 || height <= 1) return;
        var draw = ImGui.GetWindowDrawList();
        var end = start + new Vector2(width, height);
        if (selected || hovered)
            draw.AddRectFilled(start, end, ImGui.GetColorU32(selected ? theme.Selection : theme.Hover), 2);
        else
            draw.AddLine(new(start.X + 6, end.Y - 1), new(end.X - 6, end.Y - 1),
                ImGui.GetColorU32(Vector4.Lerp(theme.Surface, theme.Border, .25f)));
        if (selected)
            draw.AddRectFilled(start + new Vector2(1, 5), new Vector2(start.X + 3, end.Y - 5), ImGui.GetColorU32(theme.Accent), 1);
    }

}
