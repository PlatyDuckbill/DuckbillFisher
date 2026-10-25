using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class FishRecommendationPanel(FishRecommendationService service, FishCatalog catalog,
    FishPreferences preferences, FishCollectionObserver collection, Action<FishGuideRow> showDetail, FishingMapService maps,
    FishGuideOpenService guideOpener, ITextureProvider textures)
{
    private static readonly int[] Hours = [1, 3, 6, 12, 24];
    private static readonly T[] TabNames = [T.RecommendationNowTab, T.RecommendationPreparationTab, T.RecommendationRareTab];
    private static readonly Vector4 Green = new(0.34f, 0.78f, 0.62f, 1);
    private static readonly Vector4 Gold = new(0.98f, 0.73f, 0.35f, 1);
    private static readonly Vector4 Blue = new(0.40f, 0.67f, 0.94f, 1);
    private static readonly Vector4 Purple = new(0.76f, 0.55f, 0.91f, 1);
    private RecommendationOptions options = new();
    private readonly uint[] selection = new uint[3];
    private int generation = -1, session = -1, focus, shown = 8, otherShown = 20, strategyIndex;
    private RecommendationPlan? groupedPlan;
    private StepGroup[] groups = [];
    private sealed record StepGroup(uint ItemId, PreparationRole Role, int Count, DateTimeOffset Start, DateTimeOffset End);
    public Action? ShowSchedule { get; set; }
    public void SetActive(bool value) => service.SetActive(value);

    public void Draw()
    {
        service.SetActive(true);
        var personal = preferences.View;
        if (session != personal.Session)
        { session = personal.Session; options = new(); service.Configure(options); Array.Clear(selection); focus = 0; }
        if (!ImGui.BeginChild("RecommendationContent")) { ImGui.EndChild(); return; }
        Toolbar();
        var view = service.View;
        if (view.Generation != generation) { generation = view.Generation; shown = 8; otherShown = 20; strategyIndex = 0; }
        if (!personal.HasCharacter) ImGui.TextWrapped(UiText.Get(T.AlertsWaitingCharacter));
        else if (view.State != RecommendationState.Ready || view.Session != personal.Session)
            ImGui.TextWrapped(UiText.Get(view.State switch
            {
                RecommendationState.Unsupported => T.RecommendationUnsupported,
                RecommendationState.Failed => T.RecommendationFailed,
                _ => T.RecommendationComputing,
            }));
        else DrawResults(view);
        ImGui.EndChild();
    }

    private void Toolbar()
    {
        if (UiWidgets.ActionButton(FontAwesomeIcon.SyncAlt, T.RecommendationRefresh, "RefreshRecommendations")) service.Refresh();
        UiWidgets.FlowNext(ImGui.GetFontSize() * 9, ImGui.GetWindowContentRegionMax().X);
        var hours = Math.Max(0, Array.IndexOf(Hours, options.Hours));
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 9);
        var labels = Hours.Select(value => UiText.Get(T.RecommendationHours, value)).ToArray();
        if (ImGui.Combo("###RecommendationHours", ref hours, labels, labels.Length))
        { options = options with { Hours = Hours[hours] }; service.Configure(options); }
        UiWidgets.Tooltip(UiText.Get(T.RecommendationHorizonHelp));
        UiWidgets.FlowNext(ImGui.GetFrameHeight(), ImGui.GetWindowContentRegionMax().X);
        if (UiWidgets.IconButton(FontAwesomeIcon.Filter, "RecommendationFilters", UiText.Get(T.RecommendationOptions),
                options.IncludeUnknown || options.FavoritesOnly || options.LegendaryOnly || !options.AllowStorage))
            ImGui.OpenPopup("RecommendationFiltersPopup");
        UiWidgets.FlowNext(ImGui.GetFrameHeight(), ImGui.GetWindowContentRegionMax().X);
        if (UiWidgets.IconButton(FontAwesomeIcon.QuestionCircle, "RecommendationHelp", UiText.Get(T.RecommendationMethod)))
            ImGui.OpenPopup("RecommendationHelpPopup");
        if (ImGui.BeginPopup("RecommendationFiltersPopup"))
        {
            var unknown = options.IncludeUnknown; var favorites = options.FavoritesOnly;
            var legendary = options.LegendaryOnly; var storage = options.AllowStorage;
            var changed = ImGui.Checkbox(UiText.Label(T.RecommendationUnknownOption, "RecommendationUnknown"), ref unknown);
            changed |= ImGui.Checkbox(UiText.Label(T.RecommendationFavorites, "RecommendationFavorites"), ref favorites);
            changed |= ImGui.Checkbox(UiText.Label(T.RecommendationLegendary, "RecommendationLegendary"), ref legendary);
            changed |= ImGui.Checkbox(UiText.Label(T.RecommendationStorageOption, "RecommendationStorage"), ref storage);
            UiWidgets.Tooltip(UiText.Get(T.RecommendationStorageAssumption));
            ImGui.TextDisabled(UiText.Get(T.RecommendationUnknownCount, service.View.UnknownCount));
            UiWidgets.GuideBanner(collection.View, preferences.View, guideOpener);
            if (changed) { options = new(options.Hours, unknown, favorites, legendary, storage); service.Configure(options); }
            ImGui.EndPopup();
        }
        if (ImGui.BeginPopup("RecommendationHelpPopup"))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetFontSize() * 32);
            ImGui.TextUnformatted(UiText.Get(T.RecommendationIntro));
            ImGui.Spacing();
            ImGui.TextUnformatted(UiText.Get(T.RecommendationMethodHelp));
            ImGui.Spacing();
            ImGui.TextUnformatted(UiText.Get(T.RecommendationHorizonHelp));
            ImGui.Spacing();
            ImGui.TextUnformatted(UiText.Get(T.RecommendationEstimate));
            if (service.View.State == RecommendationState.Ready)
                ImGui.TextUnformatted(UiText.Get(T.RecommendationAnalysis, service.View.AnalysisStart, service.View.AnalysisStart.AddDays(30)));
            ImGui.PopTextWrapPos();
            ImGui.EndPopup();
        }
        ImGui.TextWrapped(UiText.Get(T.RecommendationScope));
        UiWidgets.Tooltip(UiText.Get(T.RecommendationIntro));
    }

    private void DrawResults(FishRecommendationView view)
    {
        if (ImGui.BeginTabBar("RecommendationFocus"))
        {
            var counts = new[] { view.Now.Length, view.Preparation.Length, view.Rare.Length };
            for (var index = 0; index < TabNames.Length; index++)
            {
                if (!ImGui.BeginTabItem($"{UiText.Get(T.RecommendationTabCount, UiText.Get(TabNames[index]), counts[index])}###RecommendationFocus{index}")) continue;
                if (focus != index) { focus = index; shown = 8; strategyIndex = 0; }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.TextDisabled(UiText.Get(T.RecommendationAsOf, view.At.ToLocalTime()));
        UiWidgets.Tooltip(UiText.Get(T.RecommendationManualHelp));
        if (view.NeedsRefresh)
        { ImGui.SameLine(); Colored(Gold, UiText.Get(T.RecommendationStale)); }
        ImGui.TextWrapped(UiText.Get(focus switch
        { 1 => T.RecommendationPreparationHint, 2 => T.RecommendationRareHint, _ => T.RecommendationNowHint }));
        var entries = focus switch { 1 => view.Preparation, 2 => view.Rare, _ => view.Now };
        if (entries.Length == 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(UiText.Get(focus switch
            { 1 => T.RecommendationPreparationEmpty, 2 => T.RecommendationRareEmpty, _ => T.RecommendationNowEmpty }));
            DrawOther(view);
            return;
        }
        var selected = Array.Find(entries, value => value.Fish.ItemId == selection[focus]);
        if (selected is null) { selected = entries[0]; selection[focus] = selected.Fish.ItemId; strategyIndex = 0; }
        var space = ImGui.GetContentRegionAvail();
        var height = Math.Max(ImGui.GetFontSize() * 8, space.Y - ImGui.GetFrameHeightWithSpacing());
        var now = service.Now;
        if (space.X >= ImGui.GetFontSize() * 50 && ImGui.BeginTable("RecommendationSplit", 2,
                ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("###RecommendationListColumn", ImGuiTableColumnFlags.WidthStretch, 0.48f);
            ImGui.TableSetupColumn("###RecommendationDetailColumn", ImGuiTableColumnFlags.WidthStretch, 0.52f);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            DrawList(entries, height, now);
            ImGui.TableNextColumn();
            DrawDetail(Array.Find(entries, value => value.Fish.ItemId == selection[focus]) ?? selected, height, view.At, now);
            ImGui.EndTable();
        }
        else
        {
            DrawList(entries, Math.Max(ImGui.GetFontSize() * 5, height * 0.42f), now);
            ImGui.Separator();
            DrawDetail(Array.Find(entries, value => value.Fish.ItemId == selection[focus]) ?? selected,
                Math.Max(ImGui.GetFontSize() * 6, height * 0.58f - ImGui.GetStyle().ItemSpacing.Y * 2), view.At, now);
        }
        DrawOther(view);
    }

    private void DrawList(RecommendationEntry[] entries, float height, DateTimeOffset now)
    {
        if (ImGui.BeginChild("RecommendationList", new Vector2(0, height)))
        {
            foreach (var entry in entries.Take(shown))
            {
                var fish = entry.Fish; var value = entry.Recommendation; var plan = value.Plan!;
                ImGui.PushID((int)fish.ItemId);
                var start = ImGui.GetCursorPos(); var screen = ImGui.GetCursorScreenPos();
                var width = ImGui.GetContentRegionAvail().X;
                var line = ImGui.GetTextLineHeightWithSpacing();
                var rowHeight = line * 3 + ImGui.GetStyle().FramePadding.Y * 2;
                if (ImGui.Selectable("###RecommendationFish", selection[focus] == fish.ItemId,
                        ImGuiSelectableFlags.AllowItemOverlap, new Vector2(width, rowHeight)))
                { selection[focus] = fish.ItemId; strategyIndex = 0; }
                UiWidgets.Tooltip(UiText.Get(T.RecommendationRowTooltip, fish.Name, Timing(value, now), Budget(plan), Reason(value, now)));
                var after = ImGui.GetCursorPos();
                var iconSize = line * 1.8f;
                ImGui.SetCursorPos(start + new Vector2(4, 5));
                UiWidgets.GameIcon(textures, fish.IconId, iconSize, fish.Name);
                var x = iconSize + 12;
                Clipped(screen + new Vector2(x, 3), width - x, fish.Name, ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
                var caught = preferences.View.Collection(fish.FishParameterId, collection.View) == CollectionStatus.Caught;
                Clipped(screen + new Vector2(x, 3 + line), width - x, caught ? UiText.Get(T.RecommendationNowCaught) : Timing(value, now),
                    value.Issue == RecommendationIssue.EstimatedLate || value.Window?.End <= now ? Gold : Green);
                Clipped(screen + new Vector2(4, 3 + line * 2), width - 8,
                    UiText.Get(T.RecommendationRowFacts, Budget(plan), Reason(value, now)), ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
                ImGui.SetCursorPos(after);
                ImGui.Separator();
                ImGui.PopID();
            }
            if (shown < entries.Length && ImGui.Button(UiText.Label(T.RecommendationMore, "RecommendationMore"))) shown += 8;
        }
        ImGui.EndChild();
    }

    private void DrawDetail(RecommendationEntry entry, float height, DateTimeOffset at, DateTimeOffset now)
    {
        if (!ImGui.BeginChild("RecommendationDetail", new Vector2(0, height))) { ImGui.EndChild(); return; }
        var value = entry.Recommendation; var fish = entry.Fish;
        UiWidgets.GameIcon(textures, fish.IconId, ImGui.GetFontSize() * 2.5f, fish.Name);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped(fish.Name);
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled(fish.Details?.Spot?.Name ?? UiText.Get(T.RecommendationUnknown));
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        if (ImGui.Button(UiText.Label(T.SpotAlertDetail, "RecommendationOpenDetail"))) showDetail(fish);
        UiWidgets.FlowNext(ImGui.CalcTextSize(UiText.Get(T.RecommendationMap)).X + ImGui.GetStyle().FramePadding.X * 2,
            ImGui.GetWindowContentRegionMax().X);
        ImGui.BeginDisabled(fish.Details?.Spot is null);
        if (ImGui.Button(UiText.Label(T.RecommendationMap, "RecommendationMap")) && fish.Details?.Spot is { } spot) maps.Request(spot);
        ImGui.EndDisabled();
        UiWidgets.FlowNext(ImGui.CalcTextSize(UiText.Get(T.RecommendationSchedule)).X + ImGui.GetStyle().FramePadding.X * 2,
            ImGui.GetWindowContentRegionMax().X);
        if (ImGui.Button(UiText.Label(T.RecommendationSchedule, "RecommendationSchedule"))) ShowSchedule?.Invoke();
        if (preferences.View.Collection(fish.FishParameterId, collection.View) == CollectionStatus.Caught)
            Colored(Gold, UiText.Get(T.RecommendationNowCaught));
        else if (entry.Collection == CollectionStatus.Unknown) Colored(Gold, UiText.Get(T.RecommendationUnknownCollection));
        var plans = new[] { value.Plan! }.Concat(value.Alternatives).ToArray();
        strategyIndex = Math.Clamp(strategyIndex, 0, plans.Length - 1);
        if (plans.Length > 1)
        {
            var names = plans.Select(plan => UiText.Get(plan.Strategy == RecommendationStrategy.RonkaStored
                ? T.RecommendationStoredPlan : T.RecommendationBasicPlan)).ToArray();
            ImGui.SetNextItemWidth(-1);
            ImGui.Combo("###RecommendationStrategy", ref strategyIndex, names, names.Length);
        }
        var chosen = plans[strategyIndex];
        if (chosen.Readiness != RecommendationReadiness.Planned)
            Colored(Gold, UiText.Get(chosen.Readiness == RecommendationReadiness.Late ? T.RecommendationEstimatedLate : T.RecommendationEstimateOnly));
        var metricColumns = ImGui.GetContentRegionAvail().X >= ImGui.GetFontSize() * 22 ? 2 : 1;
        if (ImGui.BeginTable("RecommendationMetrics", metricColumns, ImGuiTableFlags.SizingStretchSame))
        {
            Metric(T.RecommendationAvailabilityLabel, Timing(value, now));
            Metric(T.RecommendationBudgetLabel, Budget(chosen), UiText.Get(T.RecommendationEstimate));
            Metric(T.RecommendationStartLabel, chosen.RecommendedStart <= now ? UiText.Get(T.RecommendationStartNow)
                : UiText.Get(T.RecommendationClock, chosen.RecommendedStart.ToLocalTime()));
            Metric(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationNextPreyLabel : T.RecommendationNextLabel,
                value.NextOpportunity is { } next ? UiText.Get(T.RecommendationDate, next.ToLocalTime())
                : UiText.Get(value.Basis == OpportunityBasis.Always ? T.RecommendationAlways : T.RecommendationNextUnknown),
                value.Basis == OpportunityBasis.PrerequisiteProxy ? UiText.Get(T.RecommendationPreyBasis, Name(value.BasisItemId)) : null);
            ImGui.EndTable();
        }
        ImGui.Spacing();
        ImGui.TextDisabled(UiText.Get(T.RecommendationFlow));
        DrawTimeline(chosen, value, at, now);
        DrawSteps(chosen, value.Window);
        if (chosen.SharedCatch) Colored(Green, UiText.Get(T.RecommendationSharedCatch));
        if (chosen.OptionalStock is { } stock)
        {
            ImGui.Separator();
            Colored(Purple, UiText.Get(T.RecommendationSpare, Name(stock.ItemId), stock.ExtraCount));
            ImGui.TextWrapped(UiText.Get(T.RecommendationSpareWork, Math.Ceiling(stock.Budget.ShortMinutes), Math.Ceiling(stock.Budget.LongMinutes)));
            if (stock.SuggestedStart is { } start)
                ImGui.TextWrapped(UiText.Get(T.RecommendationOptionalStart, start.ToLocalTime()));
            UiWidgets.Tooltip(UiText.Get(T.RecommendationExtraStorage, Name(27491), Name(28071)));
        }
        ImGui.Separator();
        ImGui.TextDisabled(UiText.Get(T.RecommendationFrequencyLabel));
        ImGui.TextWrapped(UiText.Get(value.OwnConditionsAlways ? T.RecommendationOwnAlways : T.RecommendationFrequency, value.OwnOpportunityCount));
        if (value.Basis == OpportunityBasis.PrerequisiteProxy)
            ImGui.TextWrapped(UiText.Get(T.RecommendationFrequencyProxy, value.OpportunityCount, Name(value.BasisItemId)));
        UiWidgets.Tooltip(UiText.Get(T.RecommendationFrequencyHelp));
        if (ImGui.TreeNode(UiText.Label(T.RecommendationCalculation, "RecommendationCalculation")))
        {
            ImGui.TextWrapped(UiText.Get(T.RecommendationScore, chosen.Score, 40 * chosen.Timeliness, 30 * value.Rarity, 20 * chosen.Room, 10 * value.NextGapScore));
            ImGui.TextWrapped(UiText.Get(T.RecommendationPlanSummary, UiText.Get(chosen.Strategy == RecommendationStrategy.RonkaStored
                ? T.RecommendationStoredPlan : T.RecommendationBasicPlan), chosen.WorkMinutes, chosen.WaitMinutes));
            ImGui.TextWrapped(chosen.FirstCast is { } first ? UiText.Get(T.RecommendationFirstCast, first.ToLocalTime())
                : UiText.Get(T.RecommendationFirstCastLate));
            if (fish.ItemId == 24992) ImGui.TextWrapped(UiText.Get(T.RecommendationStethacanthus, Name(20040)));
            if (fish.ItemId == 24993) ImGui.TextWrapped(UiText.Get(T.RecommendationRubyDragon, Name(24214)));
            ImGui.TextWrapped(UiText.Get(T.RecommendationRetention));
            ImGui.TreePop();
        }
        ImGui.EndChild();
    }

    private void DrawTimeline(RecommendationPlan plan, FishRecommendation value, DateTimeOffset at, DateTimeOffset now)
    {
        var from = plan.Steps.Count > 0 && plan.Steps[0].Start < at ? plan.Steps[0].Start : at;
        var cast = plan.FirstCast ?? plan.FastFirstCast ?? plan.SlowFirstCast ?? plan.Steps.LastOrDefault()?.End ?? at;
        var until = value.Window?.End ?? cast.AddMinutes(5);
        if (until <= from) until = from.AddMinutes(1);
        var origin = ImGui.GetCursorScreenPos(); var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var line = ImGui.GetTextLineHeightWithSpacing(); var draw = ImGui.GetWindowDrawList();
        ImGui.Dummy(new Vector2(width, line * 4));
        float X(DateTimeOffset date) => origin.X + (float)Math.Clamp((date - from).TotalSeconds / (until - from).TotalSeconds, 0, 1) * width;
        var muted = ImGui.GetColorU32(ImGuiCol.FrameBg);
        draw.AddRectFilled(origin + new Vector2(0, line), origin + new Vector2(width, line * 1.65f), muted, 3);
        draw.AddRectFilled(origin + new Vector2(0, line * 2), origin + new Vector2(width, line * 2.65f), muted, 3);
        var hoveredStep = false;
        foreach (var step in plan.Steps)
        {
            var min = new Vector2(X(step.Start), origin.Y + line);
            var max = new Vector2(Math.Min(origin.X + width, Math.Max(min.X + 2, X(step.End))), origin.Y + line * 1.65f);
            draw.AddRectFilled(min, max, ImGui.GetColorU32(RoleColor(step.Role)), 2);
            if (ImGui.IsMouseHoveringRect(min, max))
            {
                hoveredStep = true;
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(UiText.Get(T.RecommendationFlowStep, Name(step.ItemId), UiText.Get(RoleKey(step.Role)), step.Start.ToLocalTime(), step.End.ToLocalTime()));
                ImGui.EndTooltip();
            }
        }
        if (!value.OwnConditionsAlways)
            draw.AddRectFilled(new Vector2(X(value.Window?.Start ?? cast), origin.Y + line * 2), origin + new Vector2(width, line * 2.65f),
                ImGui.GetColorU32(Green), 2);
        if (plan.FirstCast is not null || plan.FastFirstCast is not null || plan.SlowFirstCast is not null)
            draw.AddCircleFilled(new Vector2(X(cast), origin.Y + line * 2.32f), line * 0.24f,
                ImGui.GetColorU32(plan.Readiness == RecommendationReadiness.Planned ? Purple : Gold));
        draw.AddText(origin, ImGui.GetColorU32(ImGuiCol.TextDisabled), UiText.Get(T.RecommendationTimelineLegend));
        if (now >= from && now <= until)
            draw.AddLine(new Vector2(X(now), origin.Y + line * 0.9f), new Vector2(X(now), origin.Y + line * 2.8f), ImGui.GetColorU32(ImGuiCol.Text), 1.5f);
        var beginLabel = UiText.Get(T.RecommendationDate, from.ToLocalTime());
        var endLabel = UiText.Get(T.RecommendationDate, until.ToLocalTime());
        draw.AddText(origin + new Vector2(0, line * 3), ImGui.GetColorU32(ImGuiCol.TextDisabled), beginLabel);
        draw.AddText(origin + new Vector2(width - ImGui.CalcTextSize(endLabel).X, line * 3), ImGui.GetColorU32(ImGuiCol.TextDisabled), endLabel);
        if (!hoveredStep) UiWidgets.Tooltip(UiText.Get(T.RecommendationTimelineHelp));
    }

    private void DrawSteps(RecommendationPlan plan, FishingWindow? window)
    {
        if (!ReferenceEquals(groupedPlan, plan))
        {
            groupedPlan = plan;
            var combined = new List<StepGroup>();
            foreach (var step in plan.Steps)
            {
                var last = combined.LastOrDefault();
                if (last is not null && last.ItemId == step.ItemId && last.Role == step.Role && last.End == step.Start)
                    combined[^1] = last with { Count = last.Count + step.Count, End = step.End };
                else combined.Add(new(step.ItemId, step.Role, step.Count, step.Start, step.End));
            }
            groups = combined.ToArray();
        }
        if (groups.Length == 0) ImGui.TextDisabled(UiText.Get(T.RecommendationNoPreparation));
        T? lastPhase = null;
        foreach (var group in groups)
        {
            if (window is { Basis: FishingForecastBasis.TimeWeather, Start: { } opening })
            {
                var phase = group.End <= opening ? T.RecommendationBefore : group.Start >= opening ? T.RecommendationDuring : T.RecommendationAcross;
                if (phase != lastPhase) { ImGui.TextDisabled(UiText.Get(phase)); lastPhase = phase; }
            }
            var size = ImGui.GetTextLineHeightWithSpacing() * 1.6f;
            var row = catalog.View.RelatedRows.GetValueOrDefault(group.ItemId);
            UiWidgets.GameIcon(textures, row?.IconId ?? 0, size, Name(group.ItemId));
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.TextWrapped(UiText.Get(T.RecommendationFlowCatch, Name(group.ItemId), group.Count));
            Colored(RoleColor(group.Role), UiText.Get(T.RecommendationFlowRole, UiText.Get(RoleKey(group.Role)), group.Start.ToLocalTime(), group.End.ToLocalTime()));
            ImGui.EndGroup();
        }
    }

    private void DrawOther(FishRecommendationView view)
    {
        if (ImGui.SmallButton($"{UiText.Get(T.RecommendationOther, view.Other.Length)}###RecommendationOther")) ImGui.OpenPopup("RecommendationOtherPopup");
        ImGui.SetNextWindowSize(new Vector2(ImGui.GetFontSize() * 34, ImGui.GetFontSize() * 23), ImGuiCond.Appearing);
        if (!ImGui.BeginPopup("RecommendationOtherPopup")) return;
        foreach (var entry in view.Other.Take(otherShown))
        {
            ImGui.PushID((int)entry.Fish.ItemId);
            if (ImGui.Selectable(entry.Fish.Name + "###RecommendationOtherDetail", false, ImGuiSelectableFlags.DontClosePopups)) showDetail(entry.Fish);
            ImGui.TextWrapped(UiText.Get(Issue(entry.Recommendation.Issue)));
            ImGui.Separator(); ImGui.PopID();
        }
        if (otherShown < view.Other.Length && ImGui.Button(UiText.Label(T.RecommendationMore, "RecommendationOtherMore"))) otherShown += 20;
        ImGui.EndPopup();
    }

    private string Reason(FishRecommendation value, DateTimeOffset now)
    {
        if (value.Issue == RecommendationIssue.EstimatedLate) return UiText.Get(T.RecommendationEstimatedLate);
        if (focus == 2) return UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationPreyFrequencyShort
            : T.RecommendationFrequencyShort, value.OpportunityCount, Name(value.BasisItemId));
        if (value.PreparationGateEnd is { } end && end > now)
            return UiText.Get(T.RecommendationGateReason, Name(value.PreparationGateItem), SpawnAlertOutput.Countdown(end - now));
        if (value.IntuitionCatchCount > 0) return UiText.Get(T.RecommendationIntuitionReason, value.IntuitionCatchCount);
        if (value.MoochDepth > 0) return UiText.Get(T.RecommendationMoochReason, value.MoochDepth);
        return UiText.Get(T.RecommendationDirectReason);
    }

    private static string Timing(FishRecommendation value, DateTimeOffset now)
    {
        if (value.OwnConditionsAlways)
            return value.Window?.Start > now ? UiText.Get(T.RecommendationPreparationOpens, SpawnAlertOutput.Countdown(value.Window.Start.Value - now))
                : value.Window?.End is { } gateEnd ? UiText.Get(gateEnd <= now ? T.RecommendationPreparationEnded : T.RecommendationPreyAvailable,
                SpawnAlertOutput.Countdown(gateEnd - now)) : UiText.Get(value.IntuitionCatchCount > 0 || value.MoochDepth > 0
                ? T.RecommendationAfterPreparation : T.RecommendationAlways);
        return value.Window?.End <= now ? UiText.Get(T.RecommendationEnded) : value.Window?.Start > now
            ? UiText.Get(T.RecommendationOpens, SpawnAlertOutput.Countdown(value.Window.Start.Value - now))
            : value.Window?.End is { } end ? UiText.Get(T.RecommendationEnds, SpawnAlertOutput.Countdown(end - now))
            : UiText.Get(T.RecommendationAlways);
    }
    private static string Budget(RecommendationPlan plan) => plan.LongWorkMinutes > 0
        ? UiText.Get(T.RecommendationBudget, Math.Ceiling(plan.ShortWorkMinutes), Math.Ceiling(plan.LongWorkMinutes))
        : UiText.Get(T.RecommendationNoPreparation);
    private string Name(uint itemId) => catalog.View.RelatedRows.TryGetValue(itemId, out var row) ? row.Name : UiText.Get(T.RecommendationItem, itemId);
    private static void Metric(T label, string value, string? tooltip = null)
    {
        ImGui.TableNextColumn(); ImGui.TextDisabled(UiText.Get(label)); ImGui.TextWrapped(value);
        if (tooltip is not null) UiWidgets.Tooltip(tooltip);
    }
    private static void Clipped(Vector2 origin, float width, string text, Vector4 color)
    {
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + new Vector2(Math.Max(1, width), ImGui.GetTextLineHeight()), true);
        draw.AddText(origin, ImGui.GetColorU32(color), text); draw.PopClipRect();
    }
    private static void Colored(Vector4 color, string text)
    { ImGui.PushStyleColor(ImGuiCol.Text, color); ImGui.TextWrapped(text); ImGui.PopStyleColor(); }
    private static T RoleKey(PreparationRole role) => role switch
    { PreparationRole.Trigger => T.RecommendationTrigger, PreparationRole.Intuition => T.RecommendationPrey,
        PreparationRole.StoredMooch => T.RecommendationStore, _ => T.RecommendationMooch };
    private static Vector4 RoleColor(PreparationRole role) => role switch
    { PreparationRole.Trigger => Gold, PreparationRole.StoredMooch => Purple, _ => Blue };
    private static T Issue(RecommendationIssue issue) => issue switch
    {
        RecommendationIssue.OutsideHorizon => T.RecommendationOutside,
        RecommendationIssue.NoFreshPath => T.RecommendationNoFreshPath,
        RecommendationIssue.EstimatedLate => T.RecommendationEstimatedLate,
        RecommendationIssue.IntuitionDurationUnknown => T.RecommendationDurationUnknown,
        RecommendationIssue.NestedIntuition => T.RecommendationNested,
        RecommendationIssue.AmbiguousPath => T.RecommendationAmbiguous,
        _ => T.RecommendationMissing,
    };
}
