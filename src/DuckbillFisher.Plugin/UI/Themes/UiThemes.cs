using System.Numerics;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record UiThemeProfile(UiTheme Id, Vector4 Background, Vector4 Surface, Vector4 Field,
    Vector4 Text, Vector4 Muted, Vector4 Border, Vector4 Accent, float Radius, bool IsLight = false)
{
    public Vector4 Hover => UiThemes.Hex(IsLight ? 0xe6eee9U : 0x303536U);
    public Vector4 Selection => UiThemes.Hex(IsLight ? 0xdbe8e1U : 0x3b3f40U);
    public Vector4 Pressed => UiThemes.Hex(IsLight ? 0xc4d8cfU : 0x4a5051U);
    public Vector4 Input => UiThemes.Hex(IsLight ? 0xffffffU : 0x171a1eU);
}

internal static class UiThemes
{
    public static readonly UiTheme[] Choices = Enum.GetValues<UiTheme>();
    // Original designs inspired by the references documented in UI-UX-REDESIGN; no imported styles or assets.
    private static readonly UiThemeProfile[] Profiles =
    [
        new(UiTheme.Graphite, Hex(0x161819), Hex(0x242627), Hex(0x343738), Hex(0xf0efe9),
            Hex(0xaab0b3), Hex(0x626769), Hex(0xa3c6e2), .16f),
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
