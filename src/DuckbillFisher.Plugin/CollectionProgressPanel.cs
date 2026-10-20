using System.Numerics;
using Dalamud.Bindings.ImGui;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class CollectionProgressPanel(CollectionProgress progress, FishCatalog catalog, FishPreferences preferences,
    FishCollectionObserver collection, FishGuideOpenService guideOpener)
{
    private int scope;

    public void Draw()
    {
        var personal = preferences.View;
        var data = catalog.View;
        var view = progress.View;
        if (data.Status.Key != T.CatalogLoaded) ImGui.TextWrapped(data.Status.Display);
        UiWidgets.GuideBanner(collection.View, personal, guideOpener);
        if (view.Session != personal.Session)
        {
            ImGui.TextWrapped(UiText.Get(T.CollectionProgressWaiting));
            return;
        }
        if (ImGui.BeginChild("CollectionProgressContent"))
        {
            ImGui.TextWrapped(UiText.Get(T.CollectionProgressHint));
            ImGui.TextUnformatted(UiText.Get(T.CollectionProgressOverall, view.Overall.All.Caught, view.Overall.All.Total,
                view.Overall.All.Unknown));
            UiWidgets.Heading(T.CollectionProgressRegular);
            string[] choices = [UiText.Get(T.CollectionProgressAll), UiText.Get(T.CollectionProgressBig), UiText.Get(T.CollectionProgressLegendary)];
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 16);
            ImGui.Combo(UiText.Label(T.CollectionProgressScope, "CollectionProgressScope"), ref scope, choices, choices.Length);
            if (view.UnknownGrades > 0) ImGui.TextWrapped(UiText.Get(T.CollectionProgressUnknownGrade, view.UnknownGrades));
            var columns = scope == 0 ? 4 : 2;
            // Keep counts readable at narrow widths and large UI scales, with horizontal scrolling when needed.
            var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable |
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY;
            var tableHeight = Math.Min(ImGui.GetContentRegionAvail().Y * 0.7f,
                ImGui.GetTextLineHeightWithSpacing() * 20);
            tableHeight = Math.Max(ImGui.GetFrameHeightWithSpacing() * 4, tableHeight);
            if (ImGui.BeginTable($"CollectionProgressTable.{scope}", columns, flags, new Vector2(0, tableHeight),
                ImGui.GetFontSize() * (scope == 0 ? 57 : 32)))
            {
                ImGui.TableSetupColumn(UiText.Get(T.CollectionProgressArea), ImGuiTableColumnFlags.WidthStretch, 1.3f);
                if (scope == 0)
                {
                    foreach (var label in choices) ImGui.TableSetupColumn(label, ImGuiTableColumnFlags.WidthStretch, 1);
                }
                else ImGui.TableSetupColumn(choices[scope], ImGuiTableColumnFlags.WidthStretch, 1.2f);
                ImGui.TableSetupScrollFreeze(1, 1);
                ImGui.TableHeadersRow();
                Row("RegularTotal", UiText.Get(T.CollectionProgressRegularTotal), view.Regular);
                foreach (var expansion in view.Expansions)
                {
                    ImGui.PushID(expansion.Id is { } id ? $"Expansion.{id}" : "Expansion.Unknown");
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    var name = string.IsNullOrWhiteSpace(expansion.Name) ? UiText.Get(T.CollectionProgressUnknownArea) : expansion.Name;
                    var open = ImGui.TreeNodeEx($"{name}###Expansion", ImGuiTreeNodeFlags.SpanAvailWidth);
                    Counts(expansion.Counts);
                    if (open)
                    {
                        foreach (var region in expansion.Regions)
                            Row(region.Id is { } territory ? $"Region.{territory}" : "Region.Unknown",
                                string.IsNullOrWhiteSpace(region.Name) ? UiText.Get(T.CollectionProgressUnknownArea) : region.Name, region.Counts);
                        ImGui.TableSetColumnIndex(0);
                        ImGui.TreePop();
                    }
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
            UiWidgets.Heading(T.CollectionProgressOcean);
            ImGui.TextWrapped(UiText.Get(T.CollectionProgressOceanHint));
            DrawCount(view.Ocean);
        }
        ImGui.EndChild();
    }

    private void Row(string id, string name, CollectionProgressCategories counts)
    {
        ImGui.PushID(id);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextWrapped(name);
        Counts(counts);
        ImGui.PopID();
    }

    private void Counts(CollectionProgressCategories counts)
    {
        if (scope == 0)
        {
            ImGui.TableNextColumn(); DrawCount(counts.All);
            ImGui.TableNextColumn(); DrawCount(counts.Big);
            ImGui.TableNextColumn(); DrawCount(counts.Legendary);
        }
        else
        {
            ImGui.TableNextColumn(); DrawCount(scope == 1 ? counts.Big : counts.Legendary);
        }
    }

    private static void DrawCount(CollectionProgressCount count)
    {
        if (count.Total == 0)
        {
            ImGui.TextDisabled(UiText.Get(T.CollectionProgressNoTargets));
            return;
        }
        var fraction = (float)count.Caught / count.Total;
        var label = count.Complete ? UiText.Get(T.CollectionProgressComplete, count.Caught, count.Total)
            : UiText.Get(T.CollectionProgressRatio, count.Caught, count.Total, fraction * 100);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, count.Complete
            ? new Vector4(0.24f, 0.65f, 0.42f, 1) : new Vector4(0.24f, 0.50f, 0.72f, 1));
        ImGui.ProgressBar(fraction, new Vector2(-1, ImGui.GetFrameHeight()), label);
        ImGui.PopStyleColor();
        UiWidgets.Tooltip(UiText.Get(T.CollectionProgressBreakdown, count.Caught, count.Uncaught, count.Unknown, count.Manual));
        if (count.Unknown > 0) ImGui.TextDisabled(UiText.Get(T.CollectionProgressUnknownCount, count.Unknown));
    }
}
