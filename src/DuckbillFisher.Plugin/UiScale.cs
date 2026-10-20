using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DuckbillFisher.Plugin;

/// <summary>Scopes the plugin multiplier to its own windows, including popups and icon fonts.</summary>
internal static class UiScale
{
    public const int DefaultPercent = 100;
    public static readonly int[] Choices = [80, 90, 100, 110, 125, 150, 175, 200];
    public static readonly string[] Labels = Choices.Select(value => $"{value}%").ToArray();

    public static int Normalize(int percent) => Choices.Contains(percent) ? percent : DefaultPercent;

    public static void Draw(WindowSystem windows, int percent)
    {
        var scale = Normalize(percent) / 100f;
        if (scale == 1)
        {
            windows.Draw();
            return;
        }

        var io = ImGui.GetIO();
        var originalScale = io.FontGlobalScale;
        var style = ImGui.GetStyle();
        // Style sizes already include Dalamud's multiplier. Stack changes restore them exactly.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, style.WindowPadding * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, style.WindowMinSize * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, style.WindowRounding * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, style.ChildRounding * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, style.PopupRounding * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, style.FramePadding * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, style.FrameRounding * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, style.ItemInnerSpacing * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, style.CellPadding * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, style.IndentSpacing * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, style.ScrollbarSize * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, style.ScrollbarRounding * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, style.GrabMinSize * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, style.GrabRounding * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, style.TabRounding * scale);
        io.FontGlobalScale = originalScale * scale;
        // Refresh ImGui's cached font size before WindowSystem begins its first window.
        ImGui.PushFont(ImGui.GetFont());
        try
        {
            windows.Draw();
        }
        finally
        {
            io.FontGlobalScale = originalScale;
            ImGui.PopFont();
            ImGui.PopStyleVar(16);
        }
    }
}
