using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class BaitPreparationPanel(BaitPreparation preparation, BaitStockObserver stock,
    FishPreferences preferences, ITextureProvider textures, FishingItemLinkService links, BaitUiProbe probe)
{
    private bool missingOnly;

    public void DrawButton()
    {
        if (ImGui.Button(UiText.Label(T.BaitPreparation, "BaitPreparation"))) ImGui.OpenPopup("BaitPreparationPopup");
        ImGui.SetNextWindowSizeConstraints(new Vector2(ImGui.GetFontSize() * 28, 0),
            new Vector2(ImGui.GetFontSize() * 45, ImGui.GetMainViewport().WorkSize.Y * 0.8f));
        if (!ImGui.BeginPopup("BaitPreparationPopup", ImGuiWindowFlags.NoScrollbar)) return;
        Draw();
        ImGui.EndPopup();
    }

    private void Draw()
    {
        var personal = preferences.View;
        var source = (int)preparation.Source;
        string[] choices = [UiText.Get(T.BaitSourceFavorites), UiText.Get(T.BaitSourceCatalog)];
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 19);
        if (ImGui.Combo(UiText.Label(T.BaitSource, "BaitSource"), ref source, choices, choices.Length))
            preparation.SetSource((BaitPreparationSource)source);
        UiWidgets.Tooltip(UiText.Get(source == 0 ? T.BaitSourceFavoritesHint : T.BaitSourceCatalogHint));
        ImGui.SameLine();
        if (ImGui.SmallButton(UiText.Label(T.Close, "CloseBaitPreparation"))) ImGui.CloseCurrentPopup();
        var inventory = stock.View;
        var ready = personal.HasCharacter && inventory.Session == personal.Session && inventory.State == BaitStockState.Ready;
        ImGui.TextDisabled(UiText.Get(T.BaitBagScope));
        if (!ready) ImGui.TextWrapped(UiText.Get(StockReason(inventory.State)));
        ImGui.BeginDisabled(!ready);
        ImGui.Checkbox(UiText.Label(T.BaitMissingOnly, "BaitMissingOnly"), ref missingOnly);
        ImGui.EndDisabled();
        var view = preparation.View;
        if (view.Session != personal.Session) view = new(personal.Session, 0, [], []);
        ImGui.TextDisabled(UiText.Get(T.BaitPreparationCounts, view.TargetCount, view.Entries.Length, view.Missing.Length));
        var height = Math.Clamp(ImGui.GetMainViewport().WorkSize.Y * 0.4f, ImGui.GetFontSize() * 8, ImGui.GetFontSize() * 23);
        if (ImGui.BeginChild("BaitPreparationItems", new Vector2(0, height)))
        {
            if (view.Entries.Length == 0) ImGui.TextWrapped(UiText.Get(T.BaitPreparationEmpty));
            foreach (var entry in view.Entries)
            {
                var count = ready ? inventory.Quantity(entry.Item.Id) : null;
                if (missingOnly && ready && count != 0) continue;
                ImGui.PushID((int)entry.Item.Id);
                var open = ImGui.TreeNodeEx("###BaitTargets", ImGuiTreeNodeFlags.None);
                ImGui.SameLine();
                UiWidgets.GameIcon(textures, entry.Item.IconId, ImGui.GetFrameHeight(), entry.Item.Name);
                ImGui.SameLine();
                ImGui.BeginDisabled(!personal.HasCharacter);
                if (ImGui.SmallButton($"{entry.Item.Name}###BaitItemLink")) links.Request(entry.Item, personal.Session);
                ImGui.EndDisabled();
                ImGui.SameLine();
                if (count == 0) ImGui.TextColored(new Vector4(1, 0.55f, 0.45f, 1), UiText.Get(T.BaitQuantity, 0));
                else ImGui.TextUnformatted(count is { } quantity ? UiText.Get(T.BaitQuantity, quantity) : UiText.Get(T.BaitQuantityUnknown));
                UiWidgets.Tooltip(UiText.Get(T.BaitTargetsCount, entry.Targets.Length));
                if (entry.Alternative) { ImGui.SameLine(); ImGui.TextDisabled(UiText.Get(T.BaitAlternative)); }
                if (open)
                {
                    foreach (var fish in entry.Targets) ImGui.TextWrapped(fish.DisplayName);
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
            if (view.Missing.Length > 0 && ImGui.TreeNode(UiText.Label(T.BaitMissingData, "BaitMissingData")))
            {
                foreach (var fish in view.Missing) ImGui.TextWrapped(fish.DisplayName);
                ImGui.TreePop();
            }
        }
        ImGui.EndChild();
        if (personal.DeveloperMode)
        {
            if (ImGui.SmallButton(UiText.Label(T.BaitProbeRequest, "BaitUiProbeRequest"))) probe.Request(personal.Session);
            ImGui.SameLine();
            ImGui.BeginDisabled(probe.Report is null || probe.IsWaiting);
            if (ImGui.SmallButton(UiText.Label(T.BaitProbeCopy, "BaitUiProbeCopy"))) ImGui.SetClipboardText(probe.Report!);
            ImGui.EndDisabled();
            if (probe.IsWaiting) ImGui.TextWrapped(UiText.Get(T.BaitProbeWaiting));
            else if (probe.Report is not null) ImGui.TextWrapped(UiText.Get(T.BaitProbeReady));
        }
    }

    public static T StockReason(BaitStockState state) => state == BaitStockState.Unsupported
        ? T.BaitStockUnsupported : state == BaitStockState.Waiting ? T.BaitStockWaiting : T.BaitStockUnavailable;
}
