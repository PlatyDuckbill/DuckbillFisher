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
    private static readonly T[] TabHints = [T.RecommendationNowHint, T.RecommendationPreparationHint, T.RecommendationRareHint];
    private static readonly Vector4 Green = new(0.34f, 0.78f, 0.62f, 1);
    private static readonly Vector4 Gold = new(0.98f, 0.73f, 0.35f, 1);
    private static readonly Vector4 Blue = new(0.55f, 0.75f, 1, 1);
    private RecommendationOptions options = new();
    private readonly uint[] selection = new uint[3];
    private int generation = -1, session = -1, focus, otherShown = 20;
    private bool resetScroll;
    public void SetActive(bool value) => service.SetActive(value);

    public void Draw()
    {
        service.SetActive(true);
        var personal = preferences.View;
        if (session != personal.Session)
        { session = personal.Session; options = new(); service.Configure(options); Array.Clear(selection); resetScroll = true; }
        if (!ImGui.BeginChild("RecommendationContent")) { ImGui.EndChild(); return; }
        Toolbar();
        var view = service.View;
        if (view.Generation != generation) { generation = view.Generation; otherShown = 20; resetScroll = true; }
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
                var selected = ImGui.BeginTabItem($"{UiText.Get(T.RecommendationTabCount, UiText.Get(TabNames[index]), counts[index])}###RecommendationFocus{index}");
                UiWidgets.Tooltip(UiText.Get(TabHints[index]));
                if (!selected) continue;
                if (focus != index) { focus = index; resetScroll = true; }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.TextDisabled(UiText.Get(TabHints[focus]));
        var stamp = UiText.Get(view.NeedsRefresh ? T.RecommendationSnapshotStale : T.RecommendationAsOf, view.At.ToLocalTime());
        UiWidgets.FlowNext(ImGui.CalcTextSize(stamp).X, ImGui.GetWindowContentRegionMax().X);
        ImGui.TextDisabled(stamp);
        UiWidgets.Tooltip(UiText.Get(T.RecommendationManualHelp));
        var entries = focus switch { 1 => view.Preparation, 2 => view.Rare, _ => view.Now };
        if (entries.Length == 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(UiText.Get(focus switch
            { 1 => T.RecommendationPreparationEmpty, 2 => T.RecommendationRareEmpty, _ => T.RecommendationNowEmpty }));
            DrawOther(view);
            return;
        }
        var selectedEntry = Array.Find(entries, value => value.Fish.ItemId == selection[focus]) ?? entries[0];
        selection[focus] = selectedEntry.Fish.ItemId;
        var height = Math.Max(ImGui.GetFontSize() * 8, ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing());
        var now = service.Now;
        if (ImGui.BeginTable("RecommendationSplit", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("###RecommendationListColumn", ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn("###RecommendationDetailColumn", ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            DrawList(entries, height, now);
            ImGui.TableNextColumn();
            DrawDetail(Array.Find(entries, value => value.Fish.ItemId == selection[focus]) ?? selectedEntry, height, view, now);
            ImGui.EndTable();
        }
        DrawOther(view);
    }

    private void DrawList(RecommendationEntry[] entries, float height, DateTimeOffset now)
    {
        if (ImGui.BeginChild("RecommendationList", new Vector2(0, height)))
        {
            if (resetScroll) { ImGui.SetScrollY(0); resetScroll = false; }
            var rowHeight = ImGui.GetTextLineHeightWithSpacing() * 2 + ImGui.GetStyle().FramePadding.Y * 2;
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                clipper.Begin(entries.Length, rowHeight + ImGui.GetStyle().ItemSpacing.Y);
                while (clipper.Step())
                    for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; index++)
                        DrawRow(entries[index], rowHeight, now);
            }
            finally { clipper.Destroy(); }
        }
        ImGui.EndChild();
    }

    private void DrawRow(RecommendationEntry entry, float height, DateTimeOffset now)
    {
        var fish = entry.Fish; var value = entry.Recommendation;
        ImGui.PushID((int)fish.ItemId);
        var start = ImGui.GetCursorPos(); var screen = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var line = ImGui.GetTextLineHeightWithSpacing();
        var chosen = selection[focus] == fish.ItemId;
        if (ImGui.Selectable("###RecommendationFish", chosen, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(width, height)))
            selection[focus] = fish.ItemId;
        UiWidgets.Tooltip(UiText.Get(T.RecommendationCandidateTooltip, fish.Name, Timing(value, now), Reason(value, now)));
        var after = ImGui.GetCursorPos();
        var iconSize = line * 1.7f;
        ImGui.SetCursorPos(start + new Vector2(4, (height - iconSize) / 2));
        UiWidgets.GameIcon(textures, fish.IconId, iconSize, fish.Name);
        var x = iconSize + 12;
        var badge = Badge(value);
        var badgeWidth = badge.Length == 0 ? 0 : Math.Min(ImGui.CalcTextSize(badge).X, width * 0.4f);
        var textColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        Clipped(screen + new Vector2(x, 3), width - x - (badgeWidth > 0 ? badgeWidth + 12 : 0), fish.Name, textColor);
        if (badgeWidth > 0) Clipped(screen + new Vector2(width - badgeWidth - 4, 3), badgeWidth, badge, textColor);
        var caught = preferences.View.Collection(fish.FishParameterId, collection.View) == CollectionStatus.Caught;
        Clipped(screen + new Vector2(x, 3 + line), width - x,
            caught ? UiText.Get(T.RecommendationNowCaught) : Timing(value, now),
            caught || value.Issue == RecommendationIssue.EstimatedLate ? Gold : TimingColor(value, now));
        ImGui.SetCursorPos(after);
        ImGui.PopID();
    }

    private void DrawDetail(RecommendationEntry entry, float height, FishRecommendationView view, DateTimeOffset now)
    {
        if (!ImGui.BeginChild("RecommendationDetail", new Vector2(0, height))) { ImGui.EndChild(); return; }
        var value = entry.Recommendation; var fish = entry.Fish; var plan = value.Plan!;
        UiWidgets.GameIcon(textures, fish.IconId, ImGui.GetFontSize() * 2.4f, fish.Name);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped(fish.Name);
        ImGui.TextWrapped(fish.Details?.Spot is { } location
            ? UiText.Get(T.RecommendationPlace, location.Zone, location.Name) : UiText.Get(T.RecommendationUnknown));
        ImGui.EndGroup();
        if (UiWidgets.ActionButton(FontAwesomeIcon.Fish, T.SpotAlertDetail, "RecommendationOpenDetail")) showDetail(fish);
        UiWidgets.FlowNext(ImGui.GetFrameHeight() + ImGui.CalcTextSize(UiText.Get(T.RecommendationMap)).X
            + ImGui.GetStyle().FramePadding.X * 2, ImGui.GetWindowContentRegionMax().X);
        ImGui.BeginDisabled(fish.Details?.Spot is null);
        if (UiWidgets.ActionButton(FontAwesomeIcon.MapMarkerAlt, T.RecommendationMap, "RecommendationMap")
            && fish.Details?.Spot is { } spot) maps.Request(spot);
        ImGui.EndDisabled();

        ImGui.Spacing();
        Colored(TimingColor(value, now), Timing(value, now));
        if (!value.OwnConditionsAlways && value.Window is { Start: { } opening, End: { } closing })
        {
            ImGui.TextWrapped(UiText.Get(T.RecommendationWindowTimes, opening.ToLocalTime(), closing.ToLocalTime()));
            UiWidgets.Tooltip(UiText.Get(T.ScheduleWindowRange, opening.ToLocalTime(), closing.ToLocalTime()));
        }
        if (preferences.View.Collection(fish.FishParameterId, collection.View) == CollectionStatus.Caught)
            Colored(Gold, UiText.Get(T.RecommendationNowCaught));
        else if (entry.Collection == CollectionStatus.Unknown) Colored(Gold, UiText.Get(T.RecommendationUnknownCollection));
        if (plan.Readiness != RecommendationReadiness.Planned)
            Colored(Gold, UiText.Get(plan.Readiness == RecommendationReadiness.Late ? T.RecommendationEstimatedLate : T.RecommendationEstimateOnly));

        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        ImGui.TextUnformatted(UiText.Get(T.RecommendationWhy));
        Bullet(Reason(value, now));
        if (focus != 2 && value.OpportunityCount > 0) Bullet(Frequency(value));
        if (!value.OwnConditionsAlways && value.Window?.Start > now && plan.AdvanceMoochCount >= 2 &&
            (plan.Strategy == RecommendationStrategy.RonkaStored || value.Alternatives.Any(alternative => alternative.Strategy == RecommendationStrategy.RonkaStored)))
        {
            Bullet(UiText.Get(T.RecommendationStorageNote));
            UiWidgets.Tooltip(UiText.Get(T.RecommendationStorageAssumption));
        }
        ImGui.Spacing();
        ImGui.TextDisabled(UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationNextPreyLabel : T.RecommendationNextLabel));
        if (value.Basis == OpportunityBasis.PrerequisiteProxy) ImGui.TextWrapped(Name(value.BasisItemId));
        ImGui.TextWrapped(value.NextOpportunity is { } next
            ? next > now ? UiText.Get(T.RecommendationNextTime, next.ToLocalTime(), Remaining(next - now))
                : UiText.Get(T.RecommendationRefreshNeeded)
            : UiText.Get(value.Basis == OpportunityBasis.Always ? T.RecommendationAlways : T.RecommendationNextUnknown));
        if (value.Basis == OpportunityBasis.PrerequisiteProxy)
            UiWidgets.Tooltip(UiText.Get(T.RecommendationPreyBasis, Name(value.BasisItemId)));

        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawCalculation(entry, view);
        ImGui.EndChild();
    }

    private void DrawCalculation(RecommendationEntry entry, FishRecommendationView view)
    {
        ImGui.PushID(focus);
        if (ImGui.CollapsingHeader(UiText.Label(T.RecommendationCalculation, "RecommendationCalculation")))
        {
            var value = entry.Recommendation; var plan = value.Plan!;
            if (ImGui.BeginTable("RecommendationBasis", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("###BasisLabel", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 7.5f);
                ImGui.TableSetupColumn("###BasisValue", ImGuiTableColumnFlags.WidthStretch);
                CalculationRow(T.RecommendationOrderLabel, UiText.Get(focus switch
                { 1 => T.RecommendationPreparationOrder, 2 => T.RecommendationRareOrder, _ => T.RecommendationNowOrder }));
                if (focus == 2)
                {
                    CalculationRow(T.RecommendationCountBasis, Name(value.BasisItemId));
                    CalculationRow(T.RecommendationTieBreak, UiText.Get(T.RecommendationRareTieBreak));
                    CalculationRow(T.RecommendationAnalysisLabel, UiText.Get(T.RecommendationAnalysisDates,
                        view.AnalysisStart, view.AnalysisStart.AddDays(30)));
                }
                else
                {
                    CalculationRow(T.RecommendationStartEstimate, plan.RecommendedStart <= view.At ? UiText.Get(T.RecommendationAtSnapshot)
                        : UiText.Get(T.RecommendationDate, plan.RecommendedStart.ToLocalTime()));
                    if (focus == 1)
                        CalculationRow(T.RecommendationAdvanceWorkLabel, plan.AdvanceWorkMinutes < 1
                            ? UiText.Get(T.RecommendationUnderMinute) : UiText.Get(T.RecommendationWorkEstimate, plan.AdvanceWorkMinutes));
                    CalculationRow(T.RecommendationConnectionLabel, UiText.Get(plan.Readiness switch
                    {
                        RecommendationReadiness.Planned => plan.Steps.Count > 0 ? T.RecommendationConnectionReady : T.RecommendationConnectionDirect,
                        RecommendationReadiness.EstimateOnly => T.RecommendationEstimateOnly,
                        _ => T.RecommendationEstimatedLate,
                    }));
                }
                ImGui.EndTable();
            }
            ImGui.TextWrapped(UiText.Get(T.RecommendationCalculationNote, view.At.ToLocalTime()));
            UiWidgets.Tooltip(UiText.Get(focus == 2 ? T.RecommendationFrequencyHelp : T.RecommendationEstimate));
        }
        ImGui.PopID();
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

    private string Badge(FishRecommendation value) => focus == 2
        ? UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationPreyCountBadge : T.RecommendationCountBadge, value.OpportunityCount)
        : value.IntuitionCatchCount > 0 ? UiText.Get(T.RecommendationIntuitionReason, value.IntuitionCatchCount)
        : value.MoochDepth > 0 ? UiText.Get(T.RecommendationMoochReason, value.MoochDepth) : "";

    private string Frequency(FishRecommendation value) => UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy
        ? T.RecommendationFrequencyProxy : T.RecommendationFrequency, value.OpportunityCount, Name(value.BasisItemId));

    private string Reason(FishRecommendation value, DateTimeOffset now)
    {
        if (focus == 2) return Frequency(value);
        if (value.PreparationGateEnd is { } end && end > now)
            return UiText.Get(T.RecommendationGateReason, Name(value.PreparationGateItem), Remaining(end - now));
        var plan = value.Plan!;
        if (!value.OwnConditionsAlways && value.Window?.Start > now && plan.AdvanceIntuitionCount > 0)
            return UiText.Get(T.RecommendationAdvanceIntuition, plan.AdvanceIntuitionCount);
        if (value.IntuitionCatchCount > 0) return UiText.Get(T.RecommendationIntuitionReason, value.IntuitionCatchCount);
        if (!value.OwnConditionsAlways && value.Window?.Start > now && plan.AdvanceMoochCount > 0)
            return UiText.Get(T.RecommendationAdvanceMooch, plan.AdvanceMoochCount);
        if (value.MoochDepth > 0) return UiText.Get(T.RecommendationMoochReason, value.MoochDepth);
        return UiText.Get(T.RecommendationDirectReason);
    }

    private static Vector4 TimingColor(FishRecommendation value, DateTimeOffset now) => value.Window?.End <= now
        ? Gold : value.Window?.Start > now ? Blue : Green;

    private static string Timing(FishRecommendation value, DateTimeOffset now)
    {
        if (value.OwnConditionsAlways)
            return value.Window?.Start > now ? UiText.Get(T.RecommendationPreparationOpens, Remaining(value.Window.Start.Value - now))
                : value.Window?.End is { } gateEnd ? UiText.Get(gateEnd <= now ? T.RecommendationPreparationEnded : T.RecommendationPreyAvailable,
                Remaining(gateEnd - now)) : UiText.Get(value.IntuitionCatchCount > 0 || value.MoochDepth > 0
                ? T.RecommendationAfterPreparation : T.RecommendationAlways);
        return value.Window?.End <= now ? UiText.Get(T.RecommendationEnded) : value.Window?.Start > now
            ? UiText.Get(T.RecommendationOpens, Remaining(value.Window.Start.Value - now))
            : value.Window?.End is { } end ? UiText.Get(T.RecommendationEnds, Remaining(end - now))
            : UiText.Get(T.RecommendationAlways);
    }

    private static string Remaining(TimeSpan time)
    {
        if (time <= TimeSpan.Zero) return UiText.Get(T.RecommendationStartNow);
        if (time.TotalDays >= 1)
        {
            var hours = (int)Math.Ceiling(time.TotalHours);
            return UiText.Get(T.RecommendationDaysHours, hours / 24, hours % 24);
        }
        return time.TotalMinutes < 1 ? UiText.Get(T.RecommendationSeconds, Math.Ceiling(time.TotalSeconds)) : SpawnAlertOutput.Remaining(time);
    }
    private string Name(uint itemId) => catalog.View.RelatedRows.TryGetValue(itemId, out var row) ? row.Name : UiText.Get(T.RecommendationItem, itemId);
    private static void CalculationRow(T label, string value)
    { ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.TextWrapped(UiText.Get(label)); ImGui.TableNextColumn(); ImGui.TextWrapped(value); }
    private static void Bullet(string text)
    { ImGui.PushTextWrapPos(); ImGui.BulletText(text); ImGui.PopTextWrapPos(); }
    private static void Clipped(Vector2 origin, float width, string text, Vector4 color)
    {
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + new Vector2(Math.Max(1, width), ImGui.GetTextLineHeight()), true);
        draw.AddText(origin, ImGui.GetColorU32(color), text); draw.PopClipRect();
    }
    private static void Colored(Vector4 color, string text)
    { ImGui.PushStyleColor(ImGuiCol.Text, color); ImGui.TextWrapped(text); ImGui.PopStyleColor(); }
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
