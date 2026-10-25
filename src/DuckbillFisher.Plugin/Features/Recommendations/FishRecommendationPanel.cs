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
    ITextureProvider textures)
{
    private static readonly int[] Hours = [1, 3, 6, 12, 24];
    private static readonly T[] TabNames = [T.RecommendationNowTab, T.RecommendationPreparationTab, T.RecommendationRareTab];
    private static readonly T[] TabHints = [T.RecommendationNowHint, T.RecommendationPreparationHint, T.RecommendationRareHint];
    private static readonly Vector4 Green = new(0.34f, 0.78f, 0.62f, 1);
    private static readonly Vector4 Gold = new(0.98f, 0.73f, 0.35f, 1);
    private static readonly Vector4 Blue = new(0.55f, 0.75f, 1, 1);
    private readonly RecommendationText text = new(catalog);
    private readonly FishRowRenderer rows = new(textures, maps);
    private RecommendationOptions options = new();
    private RecommendationEntry? evidence;
    private FishRecommendationView? evidenceView;
    private int evidenceFocus;
    private uint evidenceRequest;
    private int generation = -1, session = -1, focus, otherShown = 20;
    private bool resetScroll;
    public void SetActive(bool value)
    {
        service.SetActive(value);
        if (!value) { evidence = null; evidenceView = null; evidenceRequest = 0; }
    }

    public void Draw()
    {
        service.SetActive(true);
        var personal = preferences.View;
        if (session != personal.Session)
        { session = personal.Session; options = new(); service.Configure(options); evidence = null; evidenceView = null; evidenceRequest = 0; resetScroll = true; }
        if (!ImGui.BeginChild("RecommendationContent", Vector2.Zero, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        { ImGui.EndChild(); return; }
        DrawHeader(service.View);
        var view = service.View;
        if (view.Generation != generation)
        { generation = view.Generation; otherShown = 20; evidence = null; evidenceView = null; evidenceRequest = 0; resetScroll = true; }
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

    private void DrawHeader(FishRecommendationView view)
    {
        var available = ImGui.GetContentRegionAvail().X;
        var frame = ImGui.GetFrameHeight();
        var gap = Math.Min(ImGui.GetStyle().ItemSpacing.X, ImGui.GetTextLineHeight() * 0.4f);
        var padding = ImGui.GetStyle().FramePadding.X * 2;
        var refreshNatural = frame + ImGui.CalcTextSize(UiText.Get(T.RecommendationRefresh)).X + padding;
        var rangeNatural = Hours.Max(value => ImGui.CalcTextSize(UiText.Get(T.RecommendationHours, value)).X) + frame + padding;
        var fixedWidth = frame * 2 + gap * 3;
        var controlsWidth = Math.Min(refreshNatural + rangeNatural + fixedWidth, Math.Max(frame * 4 + gap * 3, available * 0.54f));
        var refreshWidth = Math.Min(refreshNatural, (controlsWidth - fixedWidth) * 0.48f);
        var rangeWidth = Math.Max(1, controlsWidth - fixedWidth - refreshWidth);
        var tabWidth = Math.Max(1, available - controlsWidth - gap);
        var height = ImGui.GetFrameHeightWithSpacing();
        if (ImGui.BeginChild("RecommendationTabs", new Vector2(tabWidth, height), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (ImGui.BeginTabBar("RecommendationFocus", ImGuiTabBarFlags.FittingPolicyScroll))
            {
                var counts = new[] { view.Now.Length, view.Preparation.Length, view.Rare.Length };
                for (var index = 0; index < TabNames.Length; index++)
                {
                    var selected = ImGui.BeginTabItem($"{UiText.Get(T.RecommendationTabCount, UiText.Get(TabNames[index]), counts[index])}###RecommendationFocus{index}");
                    Tooltip(UiText.Get(TabHints[index]));
                    if (!selected) continue;
                    if (focus != index) { focus = index; resetScroll = true; }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
        ImGui.EndChild();
        ImGui.SameLine(0, gap);
        if (ImGui.BeginChild("RecommendationControls", new Vector2(controlsWidth, height), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            Toolbar(refreshWidth, rangeWidth, gap);
        ImGui.EndChild();
    }

    private void Toolbar(float refreshWidth, float rangeWidth, float gap)
    {
        if (UiWidgets.ActionButton(FontAwesomeIcon.SyncAlt, T.RecommendationRefresh, "RefreshRecommendations", refreshWidth)) service.Refresh();
        ImGui.SameLine(0, gap);
        var hours = Math.Max(0, Array.IndexOf(Hours, options.Hours));
        ImGui.SetNextItemWidth(rangeWidth);
        var labels = Hours.Select(value => UiText.Get(T.RecommendationHours, value)).ToArray();
        if (ImGui.Combo("###RecommendationHours", ref hours, labels, labels.Length))
        { options = options with { Hours = Hours[hours] }; service.Configure(options); }
        Tooltip(UiText.Get(T.RecommendationHorizonHelp));
        ImGui.SameLine(0, gap);
        if (UiWidgets.IconButton(FontAwesomeIcon.Filter, "RecommendationFilters", UiText.Get(T.RecommendationOptions),
                options.IncludeUnknown || options.FavoritesOnly || options.LegendaryOnly || !options.AllowStorage))
            ImGui.OpenPopup("RecommendationFiltersPopup");
        ImGui.SameLine(0, gap);
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
            Tooltip(UiText.Get(T.RecommendationStorageAssumption));
            ImGui.TextDisabled(UiText.Get(T.RecommendationUnknownCount, service.View.UnknownCount));
            if (changed) { options = new(options.Hours, unknown, favorites, legendary, storage); service.Configure(options); }
            ImGui.EndPopup();
        }
        var helpWidth = Math.Min(ImGui.GetFontSize() * 30, ImGui.GetMainViewport().WorkSize.X * 0.8f);
        ImGui.SetNextWindowSizeConstraints(new Vector2(helpWidth, 0), new Vector2(helpWidth, ImGui.GetMainViewport().WorkSize.Y * 0.75f));
        if (ImGui.BeginPopup("RecommendationHelpPopup"))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
            ImGui.TextUnformatted(UiText.Get(T.RecommendationRangeTitle));
            ImGui.TextUnformatted(UiText.Get(T.RecommendationHorizonHelp));
            ImGui.Spacing();
            ImGui.TextUnformatted(UiText.Get(T.RecommendationAssumptionsTitle));
            ImGui.TextUnformatted(UiText.Get(T.RecommendationIntro));
            ImGui.Spacing();
            ImGui.TextUnformatted(UiText.Get(T.RecommendationEstimate));
            if (service.View.State == RecommendationState.Ready)
                ImGui.TextUnformatted(UiText.Get(T.RecommendationAnalysis, service.View.AnalysisStart, service.View.AnalysisStart.AddDays(30)));
            ImGui.PopTextWrapPos();
            ImGui.EndPopup();
        }
    }

    private void DrawResults(FishRecommendationView view)
    {
        var entries = focus switch { 1 => view.Preparation, 2 => view.Rare, _ => view.Now };
        if (entries.Length == 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(UiText.Get(focus switch
            { 1 => T.RecommendationPreparationEmpty, 2 => T.RecommendationRareEmpty, _ => T.RecommendationNowEmpty }));
            DrawFooter(view);
            return;
        }
        var footerHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y * 2;
        var height = Math.Max(1, ImGui.GetContentRegionAvail().Y - footerHeight);
        var now = service.Now;
        DrawList(entries, height, now);
        if (evidenceRequest != 0)
        {
            evidence = Array.Find(entries, value => value.Fish.ItemId == evidenceRequest);
            evidenceView = view; evidenceFocus = focus; evidenceRequest = 0;
            if (evidence is not null) ImGui.OpenPopup("RecommendationEvidencePopup");
        }
        var popupWidth = Math.Min(ImGui.GetFontSize() * 40, ImGui.GetMainViewport().WorkSize.X * 0.85f);
        ImGui.SetNextWindowSizeConstraints(new Vector2(popupWidth, 0), new Vector2(popupWidth, ImGui.GetMainViewport().WorkSize.Y * 0.8f));
        if (ImGui.BeginPopup("RecommendationEvidencePopup"))
        {
            if (evidence is null || evidenceView is null || evidenceView.Generation != view.Generation || evidenceView.Session != session)
            { ImGui.CloseCurrentPopup(); evidence = null; evidenceView = null; }
            else DrawEvidence(evidence, evidenceView, now);
            ImGui.EndPopup();
        }
        DrawFooter(view);
    }

    private void DrawList(RecommendationEntry[] entries, float height, DateTimeOffset now)
    {
        if (ImGui.BeginChild("RecommendationList", new Vector2(0, height)))
        {
            if (resetScroll) { ImGui.SetScrollY(0); resetScroll = false; }
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle().ItemSpacing.X,
                Math.Min(ImGui.GetStyle().ItemSpacing.Y, ImGui.GetFontSize() * 0.18f)));
            var rowHeight = FishRowRenderer.GetRecommendationHeight();
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                clipper.Begin(entries.Length, rowHeight + ImGui.GetStyle().ItemSpacing.Y);
                while (clipper.Step())
                    for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; index++)
                        DrawRow(entries[index], rowHeight, now);
            }
            finally { clipper.Destroy(); ImGui.PopStyleVar(); }
        }
        ImGui.EndChild();
    }

    private void DrawRow(RecommendationEntry entry, float height, DateTimeOffset now)
    {
        var fish = entry.Fish; var value = entry.Recommendation;
        var caught = preferences.View.Collection(fish.FishParameterId, collection.View) == CollectionStatus.Caught;
        var appearance = RecommendationText.Timing(value, now);
        var hint = UiText.Get(T.RecommendationCandidateTooltip, fish.Name, RecommendationText.Appearance(value, now),
            string.Join("\n", text.Reasons(entry, focus, now)), focus == 2 ? text.Next(value, now) : text.ListContext(entry, focus, now));
        var info = new RecommendationRowInfo(caught ? UiText.Get(T.RecommendationNowCaught) : appearance,
            text.Source(entry), hint,
            caught || value.Issue == RecommendationIssue.EstimatedLate ? Gold : TimingColor(value, now),
            focus == 1 && RecommendationText.AdvancePreparationExpired(value, now) ? Gold : null)
        {
            Range = text.CurrentRange(value, now), Next = text.Next(value, now, compact: true), Frequency = text.FrequencyCompact(value),
            Reason = text.RowReason(entry, focus, now), OpenDetail = () => showDetail(fish), OpenEvidence = () => evidenceRequest = fish.ItemId,
        };
        var place = fish.Details?.Spot is { } spot ? UiText.Get(T.RecommendationPlace, spot.Zone, spot.Name) : UiText.Get(T.RecommendationUnknown);
        if (rows.Draw(fish, height, false, "", place, null, now,
                spot: fish.Details?.Spot, canOpenMap: preferences.View.HasCharacter, recommendation: info))
            showDetail(fish);
    }

    private void DrawEvidence(RecommendationEntry entry, FishRecommendationView view, DateTimeOffset now)
    {
        var value = entry.Recommendation; var fish = entry.Fish; var plan = value.Plan!;
        UiWidgets.GameIcon(textures, fish.IconId, ImGui.GetFontSize() * 2.4f, fish.Name);
        ImGui.SameLine(); ImGui.BeginGroup();
        ImGui.TextWrapped(fish.Name);
        ImGui.TextWrapped(fish.Details?.Spot is { } location
            ? UiText.Get(T.RecommendationPlace, location.Zone, location.Name) : UiText.Get(T.RecommendationUnknown));
        ImGui.EndGroup();
        ImGui.Spacing();
        Colored(TimingColor(value, now), RecommendationText.Appearance(value, now));
        if (value.OwnConditionsAlways) Tooltip(UiText.Get(T.RecommendationIntuitionTimeHelp));
        else if (value.Window is { Start: { } opening, End: { } closing })
            Tooltip(UiText.Get(T.ScheduleWindowRange, opening.ToLocalTime(), closing.ToLocalTime()));
        if (value.IntuitionCatchCount > 0)
        {
            foreach (var opportunity in value.IntuitionPreparation)
                ImGui.TextWrapped(text.IntuitionPreparation(opportunity, now));
            if (value.IntuitionPreparation.Count == 0)
                ImGui.TextWrapped(UiText.Get(T.RecommendationIntuitionSummary, text.Requirements(entry)));
        }
        if (RecommendationText.NextAt(value) is not null || RecommendationText.NextUsesPrey(value) || !value.OwnConditionsAlways)
        {
            ImGui.TextWrapped(UiText.Get(RecommendationText.NextUsesPrey(value)
                ? T.RecommendationNextPreySummary : T.RecommendationFollowingSummary, text.Next(value, now)));
            Tooltip(UiText.Get(RecommendationText.NextUsesPrey(value) ? T.RecommendationNextPreyHelp : T.RecommendationNextTargetHelp));
        }
        if (preferences.View.Collection(fish.FishParameterId, collection.View) == CollectionStatus.Caught)
            Colored(Gold, UiText.Get(T.RecommendationNowCaught));
        else if (entry.Collection == CollectionStatus.Unknown) Colored(Gold, UiText.Get(T.RecommendationUnknownCollection));
        if (plan.Readiness != RecommendationReadiness.Planned)
            Colored(Gold, UiText.Get(plan.Readiness == RecommendationReadiness.Late ? T.RecommendationEstimatedLate : T.RecommendationEstimateOnly));

        ImGui.Separator();
        ImGui.TextUnformatted(UiText.Get(T.RecommendationWhy));
        foreach (var reason in text.Reasons(entry, evidenceFocus, now)) Bullet(reason);
        ImGui.Spacing();
        if (ImGui.SmallButton(UiText.Label(T.SpotAlertDetail, "RecommendationOpenDetail"))) showDetail(fish);
        UiWidgets.FlowNext(ImGui.CalcTextSize(UiText.Get(T.RecommendationMap)).X + ImGui.GetStyle().FramePadding.X * 2,
            ImGui.GetWindowContentRegionMax().X);
        ImGui.BeginDisabled(fish.Details?.Spot is null);
        if (ImGui.SmallButton(UiText.Label(T.RecommendationMap, "RecommendationMap")) && fish.Details?.Spot is { } spot) maps.Request(spot);
        ImGui.EndDisabled();
        ImGui.Spacing();
        DrawCalculation(entry, view);
    }

    private void DrawCalculation(RecommendationEntry entry, FishRecommendationView view)
    {
        ImGui.PushID(evidenceFocus);
        if (ImGui.TreeNodeEx(UiText.Label(T.RecommendationCalculation, "RecommendationCalculation"), ImGuiTreeNodeFlags.NoTreePushOnOpen))
        {
            if (ImGui.BeginTable("RecommendationBasis", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("###BasisLabel", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 5.5f);
                ImGui.TableSetupColumn("###BasisValue", ImGuiTableColumnFlags.WidthStretch);
                foreach (var fact in text.Evidence(entry, evidenceFocus, view)) FactRow(fact.Label, fact.Value);
                ImGui.EndTable();
            }
            ImGui.TextDisabled(UiText.Get(T.RecommendationCalculationNote, view.At.ToLocalTime()));
            Tooltip(UiText.Get(evidenceFocus == 2 ? T.RecommendationFrequencyHelp : T.RecommendationEstimate));
        }
        ImGui.PopID();
    }

    private void DrawFooter(FishRecommendationView view)
    {
        if (!ImGui.BeginChild("RecommendationFooter", new Vector2(0, ImGui.GetFrameHeight()), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        { ImGui.EndChild(); return; }
        var left = ImGui.GetCursorPosX();
        var available = ImGui.GetContentRegionAvail().X;
        var buttonWidth = Math.Min(ImGui.CalcTextSize(UiText.Get(T.RecommendationOther, view.Other.Length)).X
            + ImGui.GetStyle().FramePadding.X * 2, available * 0.6f);
        DrawOther(view, buttonWidth);
        var stamp = UiText.Get(view.NeedsRefresh ? T.RecommendationSnapshotStale : T.RecommendationAsOf, view.At.ToLocalTime());
        var remaining = Math.Max(1, available - buttonWidth - ImGui.GetStyle().ItemSpacing.X);
        if (ImGui.CalcTextSize(stamp).X > remaining) stamp = UiText.Get(T.RecommendationAsOf, view.At.ToLocalTime());
        if (ImGui.CalcTextSize(stamp).X > remaining) stamp = UiText.Get(T.RecommendationClockTime, view.At.ToLocalTime());
        var width = ImGui.CalcTextSize(stamp).X;
        ImGui.SameLine(Math.Max(left + buttonWidth + ImGui.GetStyle().ItemSpacing.X, left + available - width));
        ImGui.AlignTextToFramePadding();
        if (view.NeedsRefresh) ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.TextUnformatted(stamp);
        if (view.NeedsRefresh) ImGui.PopStyleColor();
        Tooltip(UiText.Get(T.RecommendationManualHelp));
        ImGui.EndChild();
    }

    private void DrawOther(FishRecommendationView view, float width)
    {
        var label = UiText.Get(T.RecommendationOther, view.Other.Length);
        if (ImGui.Button($"{label}###RecommendationOther", new Vector2(width, ImGui.GetFrameHeight()))) ImGui.OpenPopup("RecommendationOtherPopup");
        if (ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2 > width) Tooltip(label);
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

    private static Vector4 TimingColor(FishRecommendation value, DateTimeOffset now)
    {
        var window = value.OwnConditionsAlways && value.IntuitionCatchCount > 0
            ? RecommendationText.PrimaryIntuition(value)?.Window : value.Window;
        return window?.End <= now ? Gold : window?.Start > now ? Blue : Green;
    }

    private static void FactRow(T label, string value, Vector4? color = null, string? tooltip = null)
    {
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.PushTextWrapPos(); ImGui.TextDisabled(UiText.Get(label)); ImGui.PopTextWrapPos();
        ImGui.TableNextColumn();
        if (color is { } tint) Colored(tint, value); else ImGui.TextWrapped(value);
        if (tooltip is not null) Tooltip(tooltip);
    }
    private static void Bullet(string text)
    { ImGui.PushTextWrapPos(); ImGui.BulletText(text); ImGui.PopTextWrapPos(); }
    private static void Colored(Vector4 color, string text)
    { ImGui.PushStyleColor(ImGuiCol.Text, color); ImGui.TextWrapped(text); ImGui.PopStyleColor(); }
    private static void Tooltip(string message)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) return;
        var width = Math.Min(ImGui.GetFontSize() * 28, ImGui.GetMainViewport().WorkSize.X * 0.8f);
        ImGui.SetNextWindowSize(new Vector2(width + ImGui.GetStyle().WindowPadding.X * 2, 0));
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        ImGui.TextUnformatted(message);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
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
