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

    public void Draw()
    {
        var personal = preferences.View;
        var source = (int)preparation.Source;
        string[] choices = [UiText.Get(T.BaitSourceFavorites), UiText.Get(T.BaitSourceCatalog)];
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 19);
        if (ImGui.Combo(UiText.Label(T.BaitSource, "BaitSource"), ref source, choices, choices.Length))
            preparation.SetSource((BaitPreparationSource)source);
        UiWidgets.Tooltip(UiText.Get(source == 0 ? T.BaitSourceFavoritesHint : T.BaitSourceCatalogHint));
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
        ImGui.TextWrapped(UiText.Get(T.BaitPreparationOverlapHint));
        var footer = personal.DeveloperMode ? ImGui.GetTextLineHeightWithSpacing() * 5 : 0;
        var height = Math.Max(ImGui.GetTextLineHeight() * 3, ImGui.GetContentRegionAvail().Y - footer);
        if (ImGui.BeginChild("BaitPreparationItems", new Vector2(0, height)))
        {
            if (view.Entries.Length == 0 && view.Missing.Length == 0) ImGui.TextWrapped(UiText.Get(T.BaitPreparationEmpty));
            foreach (var entry in view.Entries)
            {
                var count = ready ? inventory.Quantity(entry.Item.Id) : null;
                if (missingOnly && ready && count != 0) continue;
                ImGui.PushID((int)entry.Item.Id);
                var open = ImGui.TreeNodeEx("###BaitTargets", ImGuiTreeNodeFlags.None);
                ImGui.SameLine();
                DrawBait(entry.Item, count, personal);
                UiWidgets.Tooltip(UiText.Get(T.BaitTargetsCount, entry.Targets.Length));
                if (open)
                {
                    foreach (var fish in entry.Targets) ImGui.TextWrapped(fish.DisplayName);
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
            if (view.Missing.Length > 0)
            {
                UiWidgets.Heading(T.BaitUnresolvedSection);
                ImGui.TextDisabled(UiText.Get(T.BaitMissingData, view.Missing.Length));
                UiWidgets.Tooltip(UiText.Get(T.BaitMissingDataHint));
                foreach (var fish in view.Missing) ImGui.TextWrapped(fish.DisplayName);
            }
        }
        ImGui.EndChild();
        if (personal.DeveloperMode)
        {
            ImGui.BeginDisabled(probe.IsWaiting);
            if (ImGui.SmallButton(UiText.Label(T.BaitProbeRequest, "BaitUiProbeRequest"))) probe.Request(personal.Session);
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.SmallButton(UiText.Label(T.BaitProbeCopy, "BaitUiProbeCopy"))) ImGui.SetClipboardText(probe.Report);
            if (probe.IsWaiting)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton(UiText.Label(T.BaitProbeCancel, "BaitUiProbeCancel"))) probe.Cancel();
                ImGui.TextWrapped(UiText.Get(T.BaitProbeWaiting, probe.SecondsRemaining));
            }
            else ImGui.TextWrapped(probe.StatusMessage.Display);
            UiWidgets.Tooltip(UiText.Get(T.BaitProbeReady));
        }
    }

    private void DrawBait(FishingItemView item, long? count, FishPreferencesView personal)
    {
        UiWidgets.GameIcon(textures, item.IconId, ImGui.GetFrameHeight(), item.Name);
        ImGui.SameLine();
        ImGui.BeginDisabled(!personal.HasCharacter);
        if (ImGui.SmallButton($"{item.Name}###BaitItemLink")) links.Request(item, personal.Session);
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (count == 0) ImGui.TextColored(new Vector4(1, 0.55f, 0.45f, 1), UiText.Get(T.BaitQuantity, 0));
        else ImGui.TextUnformatted(count is { } quantity ? UiText.Get(T.BaitQuantity, quantity) : UiText.Get(T.BaitQuantityUnknown));
    }

    public static T StockReason(BaitStockState state) => state == BaitStockState.Unsupported
        ? T.BaitStockUnsupported : state == BaitStockState.Waiting ? T.BaitStockWaiting : T.BaitStockUnavailable;
}
