using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DuckbillFisher.Plugin;

/// <summary>Scoped colors, rounding and control edges. Host layout spacing stays unchanged.</summary>
internal sealed class UiThemeScope : IDisposable
{
    [ThreadStatic] private static UiThemeProfile? current;
    public static UiThemeProfile? Current => current;
    private readonly UiThemeProfile? previous = current;
    private int colors;
    private int variables;
    private bool disposed;

    public UiThemeScope(UiTheme theme)
    {
        current = UiThemes.Find(theme);
        if (current is not { } p) return;
        try
        {
            var hover = p.Hover;
            var active = p.Selection;
            var graphite = p.Id == UiTheme.Graphite;
            Color(ImGuiCol.Text, p.Text);
            Color(ImGuiCol.TextDisabled, p.Muted);
            Color(ImGuiCol.WindowBg, p.Surface);
            Color(ImGuiCol.ChildBg, p.Surface);
            Color(ImGuiCol.PopupBg, Vector4.Lerp(p.Surface, p.Field, .65f));
            Color(ImGuiCol.Border, p.Border);
            Color(ImGuiCol.BorderShadow, Vector4.Zero);
            Color(ImGuiCol.TitleBg, p.Background);
            Color(ImGuiCol.TitleBgActive, graphite ? p.Surface : p.Field);
            Color(ImGuiCol.TitleBgCollapsed, p.Background);
            Color(ImGuiCol.MenuBarBg, p.Surface);
            Color(ImGuiCol.FrameBg, p.Input);
            Color(ImGuiCol.FrameBgHovered, graphite ? Vector4.Lerp(p.Input, p.Field, .45f) : hover);
            Color(ImGuiCol.FrameBgActive, p.Pressed);
            Color(ImGuiCol.CheckMark, p.Accent);
            Color(ImGuiCol.SliderGrab, Vector4.Lerp(p.Field, p.Accent, .7f));
            Color(ImGuiCol.SliderGrabActive, p.Accent);
            Color(ImGuiCol.Button, p.Field);
            Color(ImGuiCol.ButtonHovered, graphite ? Vector4.Lerp(p.Field, p.Text, .12f) : hover);
            Color(ImGuiCol.ButtonActive, p.Pressed);
            Color(ImGuiCol.Header, active);
            Color(ImGuiCol.HeaderHovered, hover);
            Color(ImGuiCol.HeaderActive, active);
            Color(ImGuiCol.Separator, Vector4.Lerp(p.Surface, p.Border, .26f));
            Color(ImGuiCol.SeparatorHovered, p.Accent);
            Color(ImGuiCol.SeparatorActive, p.Accent);
            Color(ImGuiCol.Tab, p.Background);
            Color(ImGuiCol.TabHovered, hover);
            Color(ImGuiCol.TabActive, p.Surface);
            Color(ImGuiCol.TabUnfocused, p.Background);
            Color(ImGuiCol.TabUnfocusedActive, p.Surface);
            Color(ImGuiCol.TableHeaderBg, p.Field);
            Color(ImGuiCol.TableBorderStrong, Vector4.Lerp(p.Surface, p.Border, .45f));
            Color(ImGuiCol.TableBorderLight, Vector4.Lerp(p.Surface, p.Border, .20f));
            Color(ImGuiCol.TableRowBg, Vector4.Zero);
            Color(ImGuiCol.TableRowBgAlt, new Vector4(p.Text.X, p.Text.Y, p.Text.Z, .025f));
            Color(ImGuiCol.ScrollbarBg, p.Background);
            Color(ImGuiCol.ScrollbarGrab, p.Border);
            Color(ImGuiCol.ScrollbarGrabHovered, p.IsLight ? Vector4.Lerp(p.Border, p.Text, .2f) : hover);
            Color(ImGuiCol.ScrollbarGrabActive, p.Accent);
            Color(ImGuiCol.PlotLines, UiThemeColors.Information);
            Color(ImGuiCol.PlotHistogram, UiThemeColors.Positive);
            Color(ImGuiCol.TextSelectedBg, p.Pressed);
            Color(ImGuiCol.NavHighlight, p.Accent);
            var unit = ImGui.GetFontSize();
            Variable(ImGuiStyleVar.WindowRounding, unit * p.Radius);
            Variable(ImGuiStyleVar.ChildRounding, unit * p.Radius);
            Variable(ImGuiStyleVar.PopupRounding, unit * p.Radius);
            Variable(ImGuiStyleVar.FrameRounding, unit * p.Radius * .7f);
            Variable(ImGuiStyleVar.GrabRounding, unit * p.Radius);
            Variable(ImGuiStyleVar.TabRounding, unit * p.Radius * .7f);
            Variable(ImGuiStyleVar.ScrollbarRounding, unit * p.Radius);
            // Controls need a persistent edge, including icon-only buttons and input fields.
            Variable(ImGuiStyleVar.FrameBorderSize, Math.Max(1, unit / 16));
        }
        catch { Dispose(); throw; }
    }

    private void Color(ImGuiCol key, Vector4 value) { ImGui.PushStyleColor(key, value); colors++; }
    private void Variable(ImGuiStyleVar key, float value) { ImGui.PushStyleVar(key, value); variables++; }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (variables > 0) ImGui.PopStyleVar(variables);
        if (colors > 0) ImGui.PopStyleColor(colors);
        current = previous;
    }
}
