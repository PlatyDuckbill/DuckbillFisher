using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class IntuitionProgressPanel(IntuitionProgress progress, ITextureProvider textures,
    Action<FishGuideRow> showDetail)
{
    public void Draw()
    {
        var view = progress.View;
        if (view.Targets.Length == 0) return;
        var themed = UiThemeScope.Current is not null;
        if (themed) ImGui.PushStyleColor(ImGuiCol.Text, UiThemeColors.Special);
        if (ImGui.SmallButton(UiText.Label(T.IntuitionPreparation, "IntuitionProgressDetails"))) ImGui.OpenPopup("IntuitionProgress");
        if (themed) ImGui.PopStyleColor();
        var target = view.Targets.FirstOrDefault(f => f.FishParameterId == view.Selected);
        UiWidgets.Tooltip(target?.DisplayName ?? "");
        if (view.Active == true)
        {
            ImGui.SameLine(); ImGui.TextDisabled(UiText.Get(T.IntuitionActiveSummary));
        }
        else
        {
            var right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
            var line = 0;
            for (var i = 0; i < view.Prey.Length; i++)
            {
                var prey = view.Prey[i];
                var count = Count(view, prey);
                var icon = ImGui.GetTextLineHeightWithSpacing();
                var moreWidth = ImGui.CalcTextSize(UiText.Get(T.IntuitionMore, view.Prey.Length - i)).X + ImGui.GetStyle().FramePadding.X * 2;
                var chipWidth = icon + ImGui.GetStyle().ItemSpacing.X + ImGui.CalcTextSize(count).X;
                var nextX = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().ItemSpacing.X;
                if (nextX + chipWidth + (i + 1 < view.Prey.Length ? moreWidth : 0) > right)
                {
                    if (line == 1)
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton(UiText.Get(T.IntuitionMore, view.Prey.Length - i) + "###IntuitionMore")) ImGui.OpenPopup("IntuitionProgress");
                        break;
                    }
                    line++;
                }
                else ImGui.SameLine();
                if (view.Prey.Length == 1)
                {
                    var nameWidth = Math.Max(1, right - ImGui.GetCursorPosX() - chipWidth - ImGui.GetStyle().ItemSpacing.X);
                    FishingHistoryPanel.Clipped(prey.Item.Name, nameWidth);
                    UiWidgets.Tooltip(prey.Item.Name); ImGui.SameLine();
                }
                UiWidgets.GameIcon(textures, prey.Item.IconId, icon, prey.Item.Name);
                ImGui.SameLine(); ImGui.TextUnformatted(count);
                UiWidgets.Tooltip(UiText.Get(T.IntuitionPreyProgress, prey.Item.Name, count));
            }
        }
        if (ImGui.IsPopupOpen("IntuitionProgress"))
            ImGui.SetNextWindowSizeConstraints(new Vector2(ImGui.GetFontSize() * 20, 0), new Vector2(ImGui.GetFontSize() * 31, ImGui.GetFontSize() * 28));
        if (!ImGui.BeginPopup("IntuitionProgress")) return;
        if (view.Targets.Length > 1 && ImGui.BeginCombo("###IntuitionTarget", target?.DisplayName ?? ""))
        {
            foreach (var fish in view.Targets)
                if (ImGui.Selectable($"{fish.DisplayName}###IntuitionTarget{fish.FishParameterId}", fish.FishParameterId == view.Selected))
                    progress.Select(fish.FishParameterId, view.Session);
            ImGui.EndCombo();
        }
        else if (view.Targets.Length == 1) ImGui.TextWrapped(target!.DisplayName);
        if (view.Active == true) ImGui.TextWrapped(UiText.Get(T.IntuitionPreparationActive));
        else if (!view.Exact || view.Active is null) ImGui.TextWrapped(UiText.Get(T.IntuitionObservedHint));
        var height = ImGui.GetTextLineHeightWithSpacing() * Math.Clamp(view.Prey.Length * 3, 3, 12);
        if (ImGui.BeginChild("IntuitionPrey", new Vector2(0, height)))
            foreach (var prey in view.Prey)
            {
                ImGui.PushID((int)prey.Item.Id);
                UiWidgets.GameIcon(textures, prey.Item.IconId, ImGui.GetTextLineHeightWithSpacing(), prey.Item.Name);
                ImGui.SameLine(); ImGui.TextWrapped(UiText.Get(T.IntuitionPreyProgress, prey.Item.Name, Count(view, prey)));
                HookYieldText.Draw(prey.Item.Id);
                ImGui.PopID();
            }
        ImGui.EndChild();
        if (target is not null && ImGui.SmallButton(UiText.Label(T.IntuitionOpenDetail, "IntuitionOpenDetail")))
        { showDetail(target); ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }

    private static string Count(IntuitionProgressView view, IntuitionPreyProgress prey) => UiText.Get(
        view.Active == true || view.Active is null ? T.IntuitionProgressUnknown : view.Exact ? T.IntuitionProgressExact : T.IntuitionProgressObserved,
        prey.Observed, prey.Required);
}
