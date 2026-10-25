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
    private readonly RecommendationText text = new(catalog);
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
        UiWidgets.FlowNext(ImGui.GetFontSize() * 8, ImGui.GetWindowContentRegionMax().X);
        var hours = Math.Max(0, Array.IndexOf(Hours, options.Hours));
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 8);
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
        var controlsRight = ImGui.GetItemRectMax().X;
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
            ImGui.TextUnformatted(UiText.Get(T.RecommendationScope));
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
        var scope = UiText.Get(T.RecommendationScopeShort);
        if (ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - controlsRight >
            ImGui.CalcTextSize(scope).X + ImGui.GetStyle().ItemSpacing.X)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(scope);
            UiWidgets.Tooltip(UiText.Get(T.RecommendationHorizonHelp));
        }
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
        var entries = focus switch { 1 => view.Preparation, 2 => view.Rare, _ => view.Now };
        if (entries.Length == 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(UiText.Get(focus switch
            { 1 => T.RecommendationPreparationEmpty, 2 => T.RecommendationRareEmpty, _ => T.RecommendationNowEmpty }));
            DrawFooter(view);
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
        DrawFooter(view);
    }

    private void DrawList(RecommendationEntry[] entries, float height, DateTimeOffset now)
    {
        if (ImGui.BeginChild("RecommendationList", new Vector2(0, height)))
        {
            if (resetScroll) { ImGui.SetScrollY(0); resetScroll = false; }
            var rowHeight = ImGui.GetTextLineHeightWithSpacing() * (focus == 2 ? 3 : 2) + ImGui.GetStyle().FramePadding.Y * 2;
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
        if (ImGui.IsItemHovered())
            UiWidgets.Tooltip(UiText.Get(T.RecommendationCandidateTooltip, fish.Name, RecommendationText.Timing(value, now),
                string.Join("\n", text.Reasons(entry, focus, now)), text.Next(value, now)));
        var after = ImGui.GetCursorPos();
        var iconSize = line * 1.7f;
        ImGui.SetCursorPos(start + new Vector2(4, (height - iconSize) / 2));
        UiWidgets.GameIcon(textures, fish.IconId, iconSize, fish.Name);
        var x = iconSize + 12;
        var badge = focus == 2 ? UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy
            ? T.RecommendationPreyCountBadge : T.RecommendationCountBadge, value.OpportunityCount) : "";
        var badgeWidth = badge.Length == 0 ? 0 : Math.Min(ImGui.CalcTextSize(badge).X, width * 0.4f);
        var textColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        Clipped(screen + new Vector2(x, 3), width - x - (badgeWidth > 0 ? badgeWidth + 12 : 0), fish.Name, textColor);
        if (badgeWidth > 0) Clipped(screen + new Vector2(width - badgeWidth - 4, 3), badgeWidth, badge, textColor);
        var caught = preferences.View.Collection(fish.FishParameterId, collection.View) == CollectionStatus.Caught;
        Clipped(screen + new Vector2(x, 3 + line), width - x,
            caught ? UiText.Get(T.RecommendationNowCaught) : RecommendationText.Timing(value, now),
            caught || value.Issue == RecommendationIssue.EstimatedLate ? Gold : TimingColor(value, now));
        if (focus == 2)
            Clipped(screen + new Vector2(x, 3 + line * 2), width - x, text.Next(value, now, compact: true), textColor);
        ImGui.SetCursorPos(after);
        ImGui.PopID();
    }

    private void DrawDetail(RecommendationEntry entry, float height, FishRecommendationView view, DateTimeOffset now)
    {
        if (!ImGui.BeginChild("RecommendationDetail", new Vector2(0, height))) { ImGui.EndChild(); return; }
        var value = entry.Recommendation; var fish = entry.Fish; var plan = value.Plan!;
        if (ImGui.BeginTable("RecommendationIdentity", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("###Identity", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("###Actions", ImGuiTableColumnFlags.WidthFixed,
                ImGui.CalcTextSize(UiText.Get(T.SpotAlertDetail)).X + ImGui.GetStyle().FramePadding.X * 2);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            UiWidgets.GameIcon(textures, fish.IconId, ImGui.GetFontSize() * 2.2f, fish.Name);
            ImGui.SameLine(); ImGui.BeginGroup();
            ImGui.TextWrapped(fish.Name);
            ImGui.TextWrapped(fish.Details?.Spot is { } location
                ? UiText.Get(T.RecommendationPlace, location.Zone, location.Name) : UiText.Get(T.RecommendationUnknown));
            ImGui.EndGroup();
            ImGui.TableNextColumn();
            if (ImGui.Button(UiText.Label(T.SpotAlertDetail, "RecommendationOpenDetail"), new Vector2(-1, 0))) showDetail(fish);
            ImGui.BeginDisabled(fish.Details?.Spot is null);
            if (ImGui.Button(UiText.Label(T.RecommendationMap, "RecommendationMap"), new Vector2(-1, 0))
                && fish.Details?.Spot is { } spot) maps.Request(spot);
            ImGui.EndDisabled(); ImGui.EndTable();
        }
        if (ImGui.BeginTable("RecommendationTimes", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("###TimeLabel", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 5.5f);
            ImGui.TableSetupColumn("###TimeValue", ImGuiTableColumnFlags.WidthStretch);
            FactRow(T.RecommendationAvailabilityLabel, RecommendationText.Timing(value, now), TimingColor(value, now),
                value.OwnConditionsAlways ? UiText.Get(T.RecommendationIntuitionTimeHelp) : null);
            if (!value.OwnConditionsAlways && value.Window is { Start: { } opening, End: { } closing })
                FactRow(T.RecommendationTimeLabel, UiText.Get(T.RecommendationWindowTimes, opening.ToLocalTime(), closing.ToLocalTime()),
                    tooltip: UiText.Get(T.ScheduleWindowRange, opening.ToLocalTime(), closing.ToLocalTime()));
            if (value.IntuitionCatchCount > 0)
                FactRow(T.RecommendationIntuitionLabel, text.Requirements(entry));
            FactRow(RecommendationText.NextUsesPrey(value) ? T.RecommendationNextPreyShortLabel : T.RecommendationNextLabel,
                text.Next(value, now), tooltip: UiText.Get(RecommendationText.NextUsesPrey(value)
                    ? T.RecommendationNextPreyHelp : T.RecommendationNextTargetHelp));
            ImGui.EndTable();
        }
        if (preferences.View.Collection(fish.FishParameterId, collection.View) == CollectionStatus.Caught)
            Colored(Gold, UiText.Get(T.RecommendationNowCaught));
        else if (entry.Collection == CollectionStatus.Unknown) Colored(Gold, UiText.Get(T.RecommendationUnknownCollection));
        if (plan.Readiness != RecommendationReadiness.Planned)
            Colored(Gold, UiText.Get(plan.Readiness == RecommendationReadiness.Late ? T.RecommendationEstimatedLate : T.RecommendationEstimateOnly));

        ImGui.Separator();
        ImGui.TextUnformatted(UiText.Get(T.RecommendationWhy));
        foreach (var reason in text.Reasons(entry, focus, now)) Bullet(reason);
        DrawCalculation(entry, view);
        ImGui.EndChild();
    }

    private void DrawCalculation(RecommendationEntry entry, FishRecommendationView view)
    {
        ImGui.PushID(focus);
        if (ImGui.CollapsingHeader(UiText.Label(T.RecommendationCalculation, "RecommendationCalculation")))
        {
            if (ImGui.BeginTable("RecommendationBasis", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("###BasisLabel", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 6.5f);
                ImGui.TableSetupColumn("###BasisValue", ImGuiTableColumnFlags.WidthStretch);
                foreach (var fact in text.Evidence(entry, focus, view)) FactRow(fact.Label, fact.Value);
                ImGui.EndTable();
            }
            ImGui.TextDisabled(UiText.Get(T.RecommendationCalculationNote, view.At.ToLocalTime()));
            UiWidgets.Tooltip(UiText.Get(focus == 2 ? T.RecommendationFrequencyHelp : T.RecommendationEstimate));
        }
        ImGui.PopID();
    }

    private void DrawFooter(FishRecommendationView view)
    {
        var left = ImGui.GetCursorPosX();
        var buttonWidth = ImGui.CalcTextSize(UiText.Get(T.RecommendationOther, view.Other.Length)).X + ImGui.GetStyle().FramePadding.X * 2;
        DrawOther(view);
        var stamp = UiText.Get(view.NeedsRefresh ? T.RecommendationSnapshotStale : T.RecommendationAsOf, view.At.ToLocalTime());
        var width = ImGui.CalcTextSize(stamp).X;
        var right = ImGui.GetWindowContentRegionMax().X;
        if (left + buttonWidth + ImGui.GetStyle().ItemSpacing.X + width <= right)
            ImGui.SameLine(Math.Max(left + buttonWidth + ImGui.GetStyle().ItemSpacing.X, right - width));
        ImGui.TextDisabled(stamp);
        UiWidgets.Tooltip(UiText.Get(T.RecommendationManualHelp));
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

    private static Vector4 TimingColor(FishRecommendation value, DateTimeOffset now) => value.OwnConditionsAlways
        ? value.IntuitionCatchCount > 0 ? Blue : Green
        : value.Window?.End <= now ? Gold : value.Window?.Start > now ? Blue : Green;

    private static void FactRow(T label, string value, Vector4? color = null, string? tooltip = null)
    {
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.PushTextWrapPos(); ImGui.TextDisabled(UiText.Get(label)); ImGui.PopTextWrapPos();
        ImGui.TableNextColumn();
        if (color is { } tint) Colored(tint, value); else ImGui.TextWrapped(value);
        if (tooltip is not null) UiWidgets.Tooltip(tooltip);
    }
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
