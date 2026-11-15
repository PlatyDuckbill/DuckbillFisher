using System.Numerics;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record UiThemeProfile(UiTheme Id, Vector4 Background, Vector4 Surface, Vector4 Field,
    Vector4 Text, Vector4 Muted, Vector4 Border, Vector4 Accent, float Radius, bool IsLight = false)
{
    private GameThemePalette Palette => GameThemePalette.For(IsLight);
    public Vector4 Hover => Palette.Hover;
    public Vector4 Selection => Palette.Selection;
    public Vector4 Pressed => Palette.Pressed;
    public Vector4 Input => Palette.Input;
}
internal static class UiThemes
{
    public static readonly UiTheme[] Choices = [UiTheme.Smoke, UiTheme.Pearl];
    private static readonly UiThemeProfile[] Profiles = [Make(false), Make(true)];
    private static UiThemeProfile Make(bool light)
    {
        var p = GameThemePalette.For(light);
        return new(light ? UiTheme.Pearl : UiTheme.Smoke, p.Inset, p.Surface, p.Field, p.Text, p.Muted, p.Border, p.Accent, .2f, light);
    }
    public static UiThemeProfile Find(UiTheme id) => Profiles[UiThemeSettings.Normalize(id) == UiTheme.Pearl ? 1 : 0];
    public static T Name(UiTheme id) => UiThemeSettings.Normalize(id) == UiTheme.Pearl ? T.ThemePaper : T.ThemeGraphite;
    public static T Description(UiTheme id) => UiThemeSettings.Normalize(id) == UiTheme.Pearl ? T.ThemePaperHint : T.ThemeGraphiteHint;
    internal static Vector4 Hex(uint color) => GameThemePalette.Hex(color);
}
