using System.Numerics;
using Dalamud.Bindings.ImGui;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class CollectionProgressPanel(CollectionProgress progress, FishCatalog catalog, FishPreferences preferences,
    FishCollectionObserver collection, FishGuideOpenService guideOpener, Action<CollectionProgressSelection> showFish)
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
            string[] choices = [UiText.Get(T.CollectionProgressAll), UiText.Get(T.CollectionProgressBig), UiText.Get(T.CollectionProgressLegendary)];
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 16);
            ImGui.Combo(UiText.Label(T.CollectionProgressScope, "CollectionProgressScope"), ref scope, choices, choices.Length);
            Summary(view.Regular, choices);
            if (view.UnknownGrades > 0) ImGui.TextWrapped(UiText.Get(T.CollectionProgressUnknownGrade, view.UnknownGrades));
            var columns = scope == 0 ? 4 : 2;
            // All columns follow the available width; compact labels retain full values in the tooltip.
            var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable |
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY;
            var tableHeight = Math.Min(ImGui.GetContentRegionAvail().Y * 0.7f,
                ImGui.GetTextLineHeightWithSpacing() * 20);
            tableHeight = Math.Max(ImGui.GetFrameHeightWithSpacing() * 4, tableHeight);
            if (ImGui.BeginTable($"CollectionProgressTable.{scope}", columns, flags, new Vector2(0, tableHeight)))
            {
                ImGui.TableSetupColumn(UiText.Get(T.CollectionProgressArea), ImGuiTableColumnFlags.WidthStretch, 1.3f);
                if (scope == 0)
                {
                    foreach (var label in choices) ImGui.TableSetupColumn(label, ImGuiTableColumnFlags.WidthStretch, 1);
                }
                else ImGui.TableSetupColumn(choices[scope], ImGuiTableColumnFlags.WidthStretch, 1.2f);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                ImGui.TableNextColumn(); ImGui.TextWrapped(UiText.Get(T.CollectionProgressArea));
                if (scope == 0)
                    foreach (var label in choices) { ImGui.TableNextColumn(); ImGui.TextWrapped(label); }
                else { ImGui.TableNextColumn(); ImGui.TextWrapped(choices[scope]); }
                foreach (var expansion in view.Expansions)
                {
                    ImGui.PushID(expansion.Id is { } id ? $"Expansion.{id}" : "Expansion.Unknown");
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    var name = string.IsNullOrWhiteSpace(expansion.Name) ? UiText.Get(T.CollectionProgressUnknownArea) : expansion.Name;
                    var open = ImGui.TreeNodeEx($"{name}###Expansion", ImGuiTreeNodeFlags.SpanAvailWidth);
                    Counts(expansion.Counts, CollectionProgressArea.Expansion, expansion.Id, null);
                    if (open)
                    {
                        foreach (var region in expansion.Regions)
                            Row(region.Id is { } territory ? $"Region.{territory}" : "Region.Unknown",
                                string.IsNullOrWhiteSpace(region.Name) ? UiText.Get(T.CollectionProgressUnknownArea) : region.Name,
                                region.Counts, expansion.Id, region.Id);
                        ImGui.TableSetColumnIndex(0);
                        ImGui.TreePop();
                    }
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
            UiWidgets.Heading(T.CollectionProgressOcean);
            if (view.OceanHasGrades)
            {
                if (scope == 0 && ImGui.BeginTable("OceanProgress", 3, ImGuiTableFlags.SizingStretchSame))
                {
                    ImGui.TableNextColumn(); ImGui.TextWrapped(choices[0]); DrawCount(view.Ocean.All, Ocean(FishGradeVisibility.All));
                    ImGui.TableNextColumn(); ImGui.TextWrapped(choices[1]); DrawCount(view.Ocean.Big, Ocean(FishGradeVisibility.BigIncludingLegendary));
                    ImGui.TableNextColumn(); ImGui.TextWrapped(choices[2]); DrawCount(view.Ocean.Legendary, Ocean(FishGradeVisibility.LegendaryOnly));
                    ImGui.EndTable();
                }
                else if (scope != 0)
                    DrawCount(scope == 1 ? view.Ocean.Big : view.Ocean.Legendary, Ocean(SelectedGrade));
            }
            else
            {
                ImGui.TextWrapped(UiText.Get(T.CollectionProgressOceanNoGrades));
                DrawCount(view.Ocean.All, Ocean(FishGradeVisibility.All));
            }
        }
        ImGui.EndChild();
    }

    private FishGradeVisibility SelectedGrade => scope == 1 ? FishGradeVisibility.BigIncludingLegendary : FishGradeVisibility.LegendaryOnly;
    private static CollectionProgressSelection Ocean(FishGradeVisibility grade) => new(CollectionProgressArea.Ocean, null, null, grade);

    private void Summary(CollectionProgressCategories counts, string[] choices)
    {
        UiWidgets.Heading(T.CollectionProgressRegularTotal);
        if (scope != 0)
        {
            DrawCount(scope == 1 ? counts.Big : counts.Legendary, large: true);
            return;
        }
        // The summary stacks vertically when there is insufficient room for three large blocks.
        var wide = ImGui.GetContentRegionAvail().X >= ImGui.GetFontSize() * 40;
        if (wide && ImGui.BeginTable("RegularProgressSummary", 3, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn(); ImGui.TextWrapped(choices[0]); DrawCount(counts.All, large: true);
            ImGui.TableNextColumn(); ImGui.TextWrapped(choices[1]); DrawCount(counts.Big, large: true);
            ImGui.TableNextColumn(); ImGui.TextWrapped(choices[2]); DrawCount(counts.Legendary, large: true);
            ImGui.EndTable();
        }
        else if (!wide)
        {
            ImGui.TextWrapped(choices[0]); DrawCount(counts.All, large: true);
            ImGui.TextWrapped(choices[1]); DrawCount(counts.Big, large: true);
            ImGui.TextWrapped(choices[2]); DrawCount(counts.Legendary, large: true);
        }
        ImGui.Spacing();
    }

    private void Row(string id, string name, CollectionProgressCategories counts, uint? expansionId, uint? regionId)
    {
        ImGui.PushID(id);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextWrapped(name);
        Counts(counts, CollectionProgressArea.Region, expansionId, regionId);
        ImGui.PopID();
    }

    private void Counts(CollectionProgressCategories counts, CollectionProgressArea area, uint? expansionId, uint? regionId)
    {
        CollectionProgressSelection Target(FishGradeVisibility grade) => new(area, expansionId, regionId, grade);
        if (scope == 0)
        {
            ImGui.TableNextColumn(); DrawCount(counts.All, Target(FishGradeVisibility.All));
            ImGui.TableNextColumn(); DrawCount(counts.Big, Target(FishGradeVisibility.BigIncludingLegendary));
            ImGui.TableNextColumn(); DrawCount(counts.Legendary, Target(FishGradeVisibility.LegendaryOnly));
        }
        else
        {
            ImGui.TableNextColumn(); DrawCount(scope == 1 ? counts.Big : counts.Legendary, Target(SelectedGrade));
        }
    }

    private void DrawCount(CollectionProgressCount count, CollectionProgressSelection? target = null, bool large = false)
    {
        if (count.Total == 0)
        {
            ImGui.TextDisabled(UiText.Get(T.CollectionProgressNoTargets));
            return;
        }
        var fraction = (float)count.Caught / count.Total;
        var label = count.Complete ? UiText.Get(T.CollectionProgressComplete, count.Caught, count.Total)
            : UiText.Get(T.CollectionProgressRatio, count.Caught, count.Total, fraction * 100);
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var compact = UiText.Get(T.CollectionProgressCompactRatio, count.Caught, count.Total);
        var overlay = ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2 <= width ? label : compact;
        if (ImGui.CalcTextSize(overlay).X > width) overlay = "";
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, ProgressColor(fraction));
        ImGui.ProgressBar(fraction, new Vector2(width, ImGui.GetFrameHeight() * (large ? 1.5f : 1)), overlay);
        ImGui.PopStyleColor();
        if (target is not null && ImGui.IsItemClicked(ImGuiMouseButton.Left)) showFish(target);
        if (target is not null && ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 32);
            ImGui.TextUnformatted(label);
            ImGui.TextUnformatted(UiText.Get(T.CollectionProgressBreakdown, count.Caught, count.Uncaught, count.Unknown, count.Manual));
            ImGui.TextWrapped(UiText.Get(T.CollectionProgressColorHint));
            if (target is not null) ImGui.TextUnformatted(UiText.Get(T.CollectionProgressClickHint));
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
        if (count.Unknown > 0) ImGui.TextDisabled(UiText.Get(T.CollectionProgressUnknownCount, count.Unknown));
    }

    private static Vector4 ProgressColor(float fraction) => fraction switch
    {
        <= 0 => new(0.40f, 0.44f, 0.48f, 1),
        < 0.25f => new(0.32f, 0.43f, 0.63f, 1),
        < 0.50f => new(0.24f, 0.54f, 0.72f, 1),
        < 0.75f => new(0.20f, 0.61f, 0.62f, 1),
        < 1 => new(0.35f, 0.65f, 0.47f, 1),
        _ => new(0.52f, 0.74f, 0.30f, 1),
    };
}
