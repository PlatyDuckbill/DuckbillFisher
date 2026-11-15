using System.Numerics;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record UiThemeProfile(UiTheme Id, Vector4 Background, Vector4 Surface, Vector4 Field,
    Vector4 Text, Vector4 Muted, Vector4 Border, Vector4 Accent, float Radius, bool IsLight = false)
{
    public Vector4 Hover => UiThemes.Hex(IsLight ? 0xe6eee9U : 0x2d3537U);
    public Vector4 Selection => UiThemes.Hex(IsLight ? 0xdbe8e1U : 0x344541U);
    public Vector4 Pressed => UiThemes.Hex(IsLight ? 0xc4d8cfU : 0x40564fU);
    public Vector4 Input => UiThemes.Hex(IsLight ? 0xffffffU : 0x1c2022U);
}

internal static class UiThemes
{
    public static readonly UiTheme[] Choices = Enum.GetValues<UiTheme>();
    // Original designs inspired by the references documented in UI-UX-REDESIGN; no imported styles or assets.
    private static readonly UiThemeProfile[] Profiles =
    [
        new(UiTheme.Graphite, Hex(0x191c1e), Hex(0x24282a), Hex(0x303639), Hex(0xeef0ec),
            Hex(0xa8b1b2), Hex(0x454e51), Hex(0x8acdbd), .28f),
        new(UiTheme.Paper, Hex(0xeeece5), Hex(0xfaf9f4), Hex(0xe0e4dc), Hex(0x2a342f),
            Hex(0x59665f), Hex(0x87958d), Hex(0x356c64), .12f, IsLight: true),
    ];

    public static UiThemeProfile? Find(UiTheme id) => id is UiTheme.Graphite or UiTheme.Paper
        ? Profiles[(int)id - 1] : null;

    public static T Name(UiTheme id) => id switch
    {
        UiTheme.Graphite => T.ThemeGraphite, UiTheme.Paper => T.ThemePaper,
        _ => T.ThemeOriginal,
    };

    public static T Description(UiTheme id) => id switch
    {
        UiTheme.Graphite => T.ThemeGraphiteHint, UiTheme.Paper => T.ThemePaperHint,
        _ => T.ThemeOriginalHint,
    };

    internal static Vector4 Hex(uint color) => new((color >> 16 & 255) / 255f,
        (color >> 8 & 255) / 255f, (color & 255) / 255f, 1);
}
