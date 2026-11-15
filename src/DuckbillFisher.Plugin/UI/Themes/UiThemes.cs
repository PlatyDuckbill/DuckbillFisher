using System.Numerics;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record UiThemeProfile(UiTheme Id, Vector4 Background, Vector4 Surface, Vector4 Field,
    Vector4 Text, Vector4 Muted, Vector4 Border, Vector4 Accent, float Radius, bool IsLight = false)
{
    private GameThemePalette Palette => GameThemeScope.Current is { } p && p.Light == IsLight ? p : GameThemePalette.For(IsLight);
    public Vector4 Hover => Palette.Hover;
    public Vector4 Selection => Palette.Selection;
    public Vector4 Pressed => Palette.Pressed;
    public Vector4 Input => Palette.Input;
}
internal static class UiThemes
{
    public static readonly UiTheme[] Choices = [UiTheme.Smoke, UiTheme.Pearl];
    private static GameThemePalette? lastPalette;
    private static UiThemeProfile? lastProfile;
    public static UiThemeProfile Find(UiTheme id, int brightness = GameThemePalette.DefaultBrightness, int depth = GameThemePalette.DefaultDepth, int transparency = GameThemePalette.DefaultTransparency)
    {
        var p = GameThemePalette.For(UiThemeSettings.Normalize(id) == UiTheme.Pearl, brightness, depth, transparency);
        if (ReferenceEquals(lastPalette, p)) return lastProfile!;
        lastPalette = p;
        return lastProfile = new(p.Light ? UiTheme.Pearl : UiTheme.Smoke, p.Inset, p.Surface, p.Field, p.Text, p.Muted, p.Border, p.Accent, .2f, p.Light);
    }
    public static T Name(UiTheme id) => UiThemeSettings.Normalize(id) == UiTheme.Pearl ? T.ThemePaper : T.ThemeGraphite;
    public static T Description(UiTheme id) => UiThemeSettings.Normalize(id) == UiTheme.Pearl ? T.ThemePaperHint : T.ThemeGraphiteHint;
    internal static Vector4 Hex(uint color) => GameThemePalette.Hex(color);
}
