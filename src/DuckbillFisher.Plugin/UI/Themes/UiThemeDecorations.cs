using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DuckbillFisher.Plugin;

internal static class UiThemeDecorations
{
    // Shared selection and accent, with a quiet navigation surface.
    public static Vector4 NavigationSelection => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0xdbe8e1U : 0x344541U);
    public static Vector4 NavigationHover => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0xe6eee9U : 0x2d3537U);
    public static Vector4 NavigationAccent => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0x356c64U : 0x8acdbdU);

    public static void NavigationBand(Vector2 start, float width, float height)
    {
        if (UiThemeScope.Current is not { } theme || width <= 1 || height <= 1) return;
        var draw = ImGui.GetWindowDrawList();
        var end = start + new Vector2(width, height);
        draw.AddRectFilled(start, end, ImGui.GetColorU32(theme.Background), ImGui.GetFontSize() * .28f);
        draw.AddLine(new(start.X, end.Y - 1), end - new Vector2(0, 1),
            ImGui.GetColorU32(Vector4.Lerp(theme.Surface, theme.Border, .45f)));
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
