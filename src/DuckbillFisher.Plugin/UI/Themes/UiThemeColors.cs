using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DuckbillFisher.Plugin;

/// <summary>Semantic paint shared by controls, charts and text; these roles do not change game state.</summary>
internal static class UiThemeColors
{
    public static Vector4 Information => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0x275f9cU : 0x9dc5ffU);
    public static Vector4 Positive => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0x236b50U : 0x76d6afU);
    public static Vector4 Attention => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0x805000U : 0xf2c477U);
    public static Vector4 Negative => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0xa43d3dU : 0xf1a09dU);
    public static Vector4 Favorite => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0x775400U : 0xf0d17aU);
    public static Vector4 Preparation => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0x76541eU : 0xdcc393U);
    // Quiet orientation cues; state colors (open, success, warning, unknown) remain separate.
    public static Vector4 Location => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0x356b63U : 0x9abcb4U);
    public static Vector4 Special => UiThemes.Hex(UiThemeScope.Current?.IsLight == true ? 0x67547dU : 0xbcb0d2U);
    public static Vector4 Secondary => ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
    public static Vector4 Grid => UiThemeScope.Current is { } p ? Vector4.Lerp(p.Surface, p.Border, .48f)
        : ImGui.GetStyle().Colors[(int)ImGuiCol.Separator];
    public static Vector4 Surface(Vector4 role, float amount = .12f) => UiThemeScope.Current is { } p
        ? Vector4.Lerp(p.Surface, role, amount) : role;
}
