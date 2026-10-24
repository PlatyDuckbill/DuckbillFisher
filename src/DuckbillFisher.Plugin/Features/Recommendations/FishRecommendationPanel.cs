using System.Numerics;
using Dalamud.Bindings.ImGui;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class FishRecommendationPanel(FishRecommendationService service, FishCatalog catalog,
    FishPreferences preferences, FishCollectionObserver collection, Action<FishGuideRow> showDetail, FishingMapService maps,
    FishGuideOpenService guideOpener)
{
    private static readonly int[] Hours = [1, 3, 6, 12, 24];
    private static readonly Vector4 ReadyColor = new(0.34f, 0.78f, 0.62f, 1);
    private static readonly Vector4 WarningColor = new(0.98f, 0.73f, 0.35f, 1);
    private RecommendationOptions options = new();
    private int generation = -1, session = -1, shown = 5, otherShown = 20;
    public Action? ShowSchedule { get; set; }
    public void SetActive(bool value) => service.SetActive(value);

    public void Draw()
    {
        service.SetActive(true);
        var personal = preferences.View;
        if (session != personal.Session)
        { session = personal.Session; options = new(); service.Configure(options); }
        if (!ImGui.BeginChild("RecommendationContent")) { ImGui.EndChild(); return; }
        ImGui.TextWrapped(UiText.Get(T.RecommendationIntro));
        UiWidgets.GuideBanner(collection.View, personal, guideOpener);
        if (ImGui.Button(UiText.Label(T.RecommendationRefresh, "RefreshRecommendations"))) service.Refresh();
        ImGui.SameLine();
        var hours = Array.IndexOf(Hours, options.Hours);
        ImGui.SetNextItemWidth(Math.Max(80, Math.Min(170, ImGui.GetContentRegionAvail().X)));
        var labels = Hours.Select(value => UiText.Get(T.RecommendationHours, value)).ToArray();
        if (ImGui.Combo("###RecommendationHours", ref hours, labels, labels.Length))
        { options = options with { Hours = Hours[hours] }; service.Configure(options); }
        UiWidgets.Tooltip(UiText.Get(T.RecommendationHorizonHelp));
        if (ImGui.CollapsingHeader(UiText.Label(T.RecommendationOptions, "RecommendationOptions")))
        {
            var unknown = options.IncludeUnknown; var favorites = options.FavoritesOnly;
            var legendary = options.LegendaryOnly; var storage = options.AllowStorage;
            var changed = ImGui.Checkbox(UiText.Label(T.RecommendationUnknownOption, "RecommendationUnknown"), ref unknown);
            changed |= ImGui.Checkbox(UiText.Label(T.RecommendationFavorites, "RecommendationFavorites"), ref favorites);
            changed |= ImGui.Checkbox(UiText.Label(T.RecommendationLegendary, "RecommendationLegendary"), ref legendary);
            changed |= ImGui.Checkbox(UiText.Label(T.RecommendationStorageOption, "RecommendationStorage"), ref storage);
            ImGui.TextWrapped(UiText.Get(T.RecommendationStorageAssumption));
            if (changed) { options = new(options.Hours, unknown, favorites, legendary, storage); service.Configure(options); }
        }
        var view = service.View;
        if (view.Generation != generation) { generation = view.Generation; shown = 5; otherShown = 20; }
        if (!personal.HasCharacter) ImGui.TextWrapped(UiText.Get(T.AlertsWaitingCharacter));
        else if (view.State != RecommendationState.Ready || view.Session != personal.Session)
            ImGui.TextWrapped(UiText.Get(view.State switch
            {
                RecommendationState.Unsupported => T.RecommendationUnsupported,
                RecommendationState.Failed => T.RecommendationFailed,
                _ => T.RecommendationComputing,
            }));
        else
        {
            ImGui.TextWrapped(UiText.Get(T.RecommendationSnapshot, view.At.ToLocalTime(), view.Ranked.Length, view.Other.Length, view.UnknownCount));
            if (view.NeedsRefresh) ColoredText(WarningColor, UiText.Get(T.RecommendationStale));
            ImGui.Separator();
            if (view.Ranked.Length == 0) ImGui.TextWrapped(UiText.Get(T.RecommendationEmpty));
            var now = service.Now;
            for (var index = 0; index < Math.Min(shown, view.Ranked.Length); index++) DrawEntry(view.Ranked[index], index + 1, now);
            if (shown < view.Ranked.Length && ImGui.Button(UiText.Label(T.RecommendationMore, "RecommendationMore"))) shown += 5;
            if (ImGui.CollapsingHeader($"{UiText.Get(T.RecommendationOther, view.Other.Length)}###RecommendationOther"))
            {
                foreach (var entry in view.Other.Take(otherShown))
                {
                    ImGui.PushID((int)entry.Fish.FishParameterId);
                    ImGui.TextWrapped(UiText.Get(T.RecommendationOtherRow, entry.Fish.Name, UiText.Get(Issue(entry.Recommendation.Issue))));
                    if (ImGui.SmallButton(UiText.Label(T.SpotAlertDetail, "RecommendationOtherDetail"))) showDetail(entry.Fish);
                    ImGui.PopID();
                }
                if (otherShown < view.Other.Length && ImGui.Button(UiText.Label(T.RecommendationMore, "RecommendationOtherMore"))) otherShown += 20;
            }
            if (ImGui.CollapsingHeader(UiText.Label(T.RecommendationMethod, "RecommendationMethod")))
            {
                ImGui.TextWrapped(UiText.Get(T.RecommendationMethodHelp));
                ImGui.TextWrapped(UiText.Get(T.RecommendationAnalysis, view.AnalysisStart, view.AnalysisStart.AddDays(30)));
            }
        }
        ImGui.EndChild();
    }

    private void DrawEntry(RecommendationEntry entry, int rank, DateTimeOffset now)
    {
        var value = entry.Recommendation;
        var plan = value.Plan!;
        var fish = entry.Fish;
        ImGui.PushID((int)fish.FishParameterId);
        var expanded = ImGui.CollapsingHeader($"{rank}. {fish.Name}###RecommendationFish");
        var ended = value.Window?.End <= now;
        var caught = preferences.View.Collection(fish.FishParameterId, collection.View) == CollectionStatus.Caught;
        var timing = ended ? UiText.Get(T.RecommendationEnded) : value.Window?.Start > now
            ? UiText.Get(T.RecommendationOpens, SpawnAlertOutput.Remaining(value.Window.Start.Value - now))
            : value.Window?.End is { } end ? UiText.Get(T.RecommendationEnds, SpawnAlertOutput.Countdown(end - now))
            : UiText.Get(T.RecommendationAlways);
        ImGui.TextWrapped(UiText.Get(T.RecommendationLocationTime, fish.Details?.Spot?.Name ?? UiText.Get(T.RecommendationUnknown), timing));
        if (caught) ColoredText(WarningColor, UiText.Get(T.RecommendationNowCaught));
        else
        {
            var start = plan.RecommendedStart <= now ? UiText.Get(T.RecommendationStartNow)
                : UiText.Get(T.RecommendationStartLater, SpawnAlertOutput.Remaining(plan.RecommendedStart - now));
            var room = UiText.Get(plan.Room == 1 ? T.RecommendationRoom : plan.Room == 0.5 ? T.RecommendationTight : T.RecommendationLate);
            var frequency = value.OpportunityCount > 0 ? UiText.Get(T.RecommendationFrequencyShort, value.OpportunityCount)
                : UiText.Get(T.RecommendationAlwaysFrequency);
            ColoredText(ended || plan.Room < 1 ? WarningColor : ReadyColor, UiText.Get(T.RecommendationSummary, start, room, frequency));
        }
        if (entry.Collection == CollectionStatus.Unknown) ImGui.TextWrapped(UiText.Get(T.RecommendationUnknownCollection));
        if (expanded)
        {
            if (ImGui.Button(UiText.Label(T.SpotAlertDetail, "RecommendationDetail"))) showDetail(fish);
            ImGui.SameLine();
            if (ImGui.Button(UiText.Label(T.RecommendationMap, "RecommendationMap")) && fish.Details?.Spot is { } spot) maps.Request(spot);
            ImGui.SameLine();
            if (ImGui.Button(UiText.Label(T.RecommendationSchedule, "RecommendationSchedule"))) ShowSchedule?.Invoke();
            ImGui.TextWrapped(UiText.Get(T.RecommendationEstimate));
            DrawPlan(plan, value.Window);
            if (value.OpportunityCount > 0)
                ImGui.TextWrapped(UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationFrequencyProxy : T.RecommendationFrequency,
                    value.OpportunityCount, Name(value.BasisItemId)));
            else ImGui.TextWrapped(UiText.Get(T.RecommendationNoRarity));
            ImGui.TextWrapped(value.NextOpportunity is { } next
                ? UiText.Get(T.RecommendationNext, next.ToLocalTime()) : UiText.Get(T.RecommendationNextUnknown));
            ImGui.TextWrapped(UiText.Get(T.RecommendationScore, plan.Score, 40 * plan.Timeliness,
                30 * value.Rarity, 20 * plan.Room, 10 * value.NextGapScore));
            if (value.Alternatives.Count > 0 && ImGui.TreeNode(UiText.Label(T.RecommendationAlternatives, "RecommendationAlternatives")))
            {
                foreach (var alternative in value.Alternatives) DrawPlan(alternative, value.Window);
                ImGui.TreePop();
            }
            if (fish.ItemId == 33243 && options.AllowStorage) ImGui.TextWrapped(UiText.Get(T.RecommendationExtraStorage, Name(27491), Name(28071)));
            if (fish.ItemId == 24992) ImGui.TextWrapped(UiText.Get(T.RecommendationStethacanthus, Name(20040)));
            if (fish.ItemId == 24993) ImGui.TextWrapped(UiText.Get(T.RecommendationRubyDragon, Name(24214)));
        }
        ImGui.Spacing();
    }

    private void DrawPlan(RecommendationPlan plan, FishingWindow? window)
    {
        ImGui.TextWrapped(UiText.Get(T.RecommendationPlanSummary,
            UiText.Get(plan.Strategy == RecommendationStrategy.RonkaStored ? T.RecommendationStoredPlan : T.RecommendationBasicPlan),
            plan.WorkMinutes, plan.WaitMinutes));
        ImGui.TextWrapped(UiText.Get(T.RecommendationPreparationStart, plan.RecommendedStart.ToLocalTime()));
        ImGui.TextWrapped(plan.FirstCast is { } first ? UiText.Get(T.RecommendationFirstCast, first.ToLocalTime())
            : UiText.Get(T.RecommendationFirstCastLate));
        if (plan.SharedCatch) ImGui.TextWrapped(UiText.Get(T.RecommendationSharedCatch));
        if (plan.OptionalStock is { } stock)
        {
            ImGui.TextWrapped(UiText.Get(T.RecommendationOptionalStock, Name(stock.ItemId), stock.ExtraCount,
                stock.Budget.ShortMinutes, stock.Budget.LongMinutes, stock.Budget.PlanningMinutes));
            if (stock.SuggestedStart is { } at) ImGui.TextWrapped(UiText.Get(T.RecommendationOptionalStart, at.ToLocalTime()));
        }
        T? lastPhase = null;
        foreach (var step in plan.Steps)
        {
            if (window is { Basis: FishingForecastBasis.TimeWeather, Start: { } opening })
            {
                var phase = step.End <= opening ? T.RecommendationBefore : step.Start >= opening ? T.RecommendationDuring : T.RecommendationAcross;
                if (phase != lastPhase) { ImGui.TextDisabled(UiText.Get(phase)); lastPhase = phase; }
            }
            ImGui.TextWrapped(UiText.Get(T.RecommendationStep, step.Start.ToLocalTime(), step.End.ToLocalTime(), Name(step.ItemId),
                UiText.Get(step.Role switch { PreparationRole.Trigger => T.RecommendationTrigger,
                    PreparationRole.Intuition => T.RecommendationPrey, PreparationRole.StoredMooch => T.RecommendationStore,
                    _ => T.RecommendationMooch }), step.Budget.ShortMinutes, step.Budget.LongMinutes));
        }
        if (plan.Steps.Count > 0) ImGui.TextWrapped(UiText.Get(T.RecommendationRetention));
    }

    private string Name(uint itemId) => catalog.View.RelatedRows.TryGetValue(itemId, out var row) ? row.Name : UiText.Get(T.RecommendationItem, itemId);

    private static void ColoredText(Vector4 color, string text)
    { ImGui.PushStyleColor(ImGuiCol.Text, color); ImGui.TextWrapped(text); ImGui.PopStyleColor(); }

    private static T Issue(RecommendationIssue issue) => issue switch
    {
        RecommendationIssue.OutsideHorizon => T.RecommendationOutside,
        RecommendationIssue.NoFreshPath => T.RecommendationNoFreshPath,
        RecommendationIssue.IntuitionDurationUnknown => T.RecommendationDurationUnknown,
        RecommendationIssue.NestedIntuition => T.RecommendationNested,
        RecommendationIssue.AmbiguousPath => T.RecommendationAmbiguous,
        _ => T.RecommendationMissing,
    };
}
