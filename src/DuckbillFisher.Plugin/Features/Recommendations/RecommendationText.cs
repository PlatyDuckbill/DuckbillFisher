using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Captured schedule and requirement facts. No preparation simulation or game reads.</summary>
internal sealed class RecommendationText(FishCatalog catalog)
{
    public static string Duration(TimeSpan time)
    {
        if (time.TotalMinutes < 1) return UiText.Get(T.RecommendationSeconds, Math.Max(0, Math.Ceiling(time.TotalSeconds)));
        var minutes = (int)Math.Ceiling(time.TotalMinutes);
        return minutes >= 1440 ? UiText.Get(T.RecommendationDurationDays, minutes / 1440, minutes % 1440 / 60, minutes % 60)
            : minutes >= 60 ? UiText.Get(T.RecommendationDurationHours, minutes / 60, minutes % 60)
            : UiText.Get(T.RecommendationDurationMinutes, minutes);
    }

    public static string Timing(FishRecommendation value, DateTimeOffset now)
    {
        var window = value.DisplayWindow;
        if (value.OwnConditionsAlways && value.PrimaryPreparation is { } primary)
        {
            var intuition = primary.Kind == PreparationKind.Intuition;
            return window.Kind == FishingWindowKind.Always
                ? UiText.Get(intuition ? T.RecommendationIntuitionAlways : T.RecommendationBaitAlways)
                : window.End <= now ? UiText.Get(intuition ? T.RecommendationIntuitionExpired : T.RecommendationBaitExpired)
                : window.Start > now ? UiText.Get(intuition ? T.RecommendationIntuitionUpcoming : T.RecommendationBaitUpcoming, When(window.Start.Value, now))
                : window.End is { } end ? UiText.Get(intuition ? T.RecommendationIntuitionOpen : T.RecommendationBaitOpen, Duration(end - now))
                : window.Start <= now ? UiText.Get(T.RecommendationPreyOpenNoEnd) : UiText.Get(T.RecommendationUnknown);
        }
        if (window.End <= now) return UiText.Get(T.RecommendationEnded);
        return window.Kind == FishingWindowKind.Always ? UiText.Get(T.RecommendationAlways)
            : window.Start > now ? UiText.Get(T.RecommendationOpens, Duration(window.Start.Value - now))
            : window.End is { } closing ? UiText.Get(T.RecommendationEnds, Duration(closing - now))
            : window.Start <= now ? UiText.Get(T.RecommendationOpenNoEnd) : UiText.Get(T.RecommendationUnknown);
    }

    public static bool NextUsesPrey(FishRecommendation value) => value.OwnConditionsAlways && value.Basis == OpportunityBasis.PrerequisiteProxy;
    public static DateTimeOffset? NextAt(FishRecommendation value) => value.OwnConditionsAlways ? value.NextOpportunity : value.NextOwnOpportunity;
    public static string When(DateTimeOffset at, DateTimeOffset now)
    {
        var local = at.ToLocalTime();
        var days = (local.Date - now.ToLocalTime().Date).Days;
        return UiText.Get(days == 0 ? T.RecommendationTodayTime : days == 1 ? T.RecommendationTomorrowTime : T.RecommendationDate, local);
    }

    private static string Range(FishingWindow window, DateTimeOffset now) => window.Kind == FishingWindowKind.Always
        ? UiText.Get(T.RecommendationAlways)
        : window is { Start: { } start, End: { } end } ? UiText.Get(T.RecommendationWindowRange, When(start, now),
            start.ToLocalTime().Date == end.ToLocalTime().Date ? UiText.Get(T.RecommendationClockTime, end.ToLocalTime()) : When(end, now))
        : window.Start is { } from && window.End is null ? UiText.Get(T.RecommendationRangeNoEnd, When(from, now))
        : window.Kind == FishingWindowKind.NotFound ? UiText.Get(T.RecommendationFrequencyUnavailable) : UiText.Get(T.RecommendationUnknown);

    public static string Appearance(FishRecommendation value, DateTimeOffset now) => Timing(value, now);
    public string CurrentRange(FishRecommendation value, DateTimeOffset now) => value.DisplayWindow.Kind == FishingWindowKind.Always
        ? "" : Range(value.DisplayWindow, now);
    public string Source(RecommendationEntry entry) => entry.Recommendation.OwnConditionsAlways && entry.Recommendation.PrimaryPreparation is { } primary
        ? primary.Kind == PreparationKind.Intuition ? UiText.Get(T.RecommendationCatchCount, Name(primary.ItemId), primary.Count) : Name(primary.ItemId) : "";

    public string Preparation(PreparationCondition condition, DateTimeOffset now) => UiText.Get(
        T.RecommendationIntuitionPreparationLine,
        condition.Kind == PreparationKind.Intuition ? UiText.Get(T.RecommendationCatchCount, Name(condition.ItemId), condition.Count) : Name(condition.ItemId),
        Range(condition.Window, now));

    public string FrequencyCompact(FishRecommendation value) => value.OpportunityCount <= 0 ? ""
        : UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationProxyFrequencyCompact : T.RecommendationCountBadge,
            value.OpportunityCount, Name(value.BasisItemId));
    public string Frequency(FishRecommendation value) => value.OpportunityCount > 0
        ? UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationFrequencyProxy : T.RecommendationFrequency,
            value.OpportunityCount, Name(value.BasisItemId))
        : UiText.Get(value.Basis == OpportunityBasis.Always ? T.RecommendationAlways : T.RecommendationFrequencyUnavailable);

    public string Next(FishRecommendation value, DateTimeOffset now, bool compact = false)
    {
        if (NextAt(value) is not { } next)
        {
            if (compact && value.OwnConditionsAlways && !NextUsesPrey(value)) return "";
            return NextUsesPrey(value) ? UiText.Get(T.RecommendationNextPreyUnknown, Name(value.BasisItemId))
                : UiText.Get(value.OwnConditionsAlways ? T.RecommendationNoFixedSpawn : T.RecommendationNextUnknown);
        }
        if (next <= now) return UiText.Get(T.RecommendationRefreshNeeded);
        return NextUsesPrey(value)
            ? UiText.Get(compact ? T.RecommendationNextPreyCompact : T.RecommendationNextPreyTime, Name(value.BasisItemId), When(next, now), Duration(next - now))
            : UiText.Get(compact ? T.RecommendationNextCompact : T.RecommendationNextTime, When(next, now), Duration(next - now));
    }

    public string Requirements(RecommendationEntry entry) => string.Join(", ", entry.Recommendation.Preparation
        .Where(value => value.Kind == PreparationKind.Intuition).Select(value => UiText.Get(T.RecommendationCatchCount, Name(value.ItemId), value.Count)));
    public string MoochPath(RecommendationEntry entry) => string.Join(" → ", entry.Recommendation.MoochPath
        .Select(step => string.Join(" / ", step.Select(Name))));

    private string RequirementSummary(RecommendationEntry entry)
    {
        var intuition = Requirements(entry); var mooch = MoochPath(entry);
        if (intuition.Length > 0 && mooch.Length > 0) return UiText.Get(T.RecommendationCombinedRequirements, mooch, intuition);
        if (intuition.Length > 0) return UiText.Get(T.RecommendationIntuitionSummary, intuition);
        return mooch.Length > 0 ? UiText.Get(T.RecommendationMoochSummary, mooch) : UiText.Get(T.RecommendationDirectNowReason);
    }

    public string RowReason(RecommendationEntry entry, int focus, DateTimeOffset now)
    {
        var value = entry.Recommendation;
        if (value.DisplayWindow.End <= now) return UiText.Get(T.RecommendationRefreshNeeded);
        if ((focus == 2 || value.MoochDepth == 0 && value.IntuitionCatchCount == 0) &&
            NextAt(value) is { } next && value.DisplayWindow.End is { } end && next > end)
            return NextUsesPrey(value) ? UiText.Get(T.RecommendationPreyGapShortReason, Name(value.BasisItemId), Duration(next - end))
                : UiText.Get(T.RecommendationGapShortReason, Duration(next - end));
        return UiText.Get(T.RecommendationRowReason, focus == 2 ? Frequency(value) : RequirementSummary(entry));
    }

    public string[] Reasons(RecommendationEntry entry, int focus, DateTimeOffset now)
    {
        var value = entry.Recommendation;
        if (value.DisplayWindow.End <= now) return [UiText.Get(T.RecommendationRefreshNeeded)];
        var first = focus == 2 ? FrequencyComparison(entry)
            : value.OwnConditionsAlways && value.PrimaryPreparation is { } primary ? UiText.Get(T.RecommendationPreparationScheduleReason, Name(primary.ItemId))
            : value.Window.Kind == FishingWindowKind.Always ? UiText.Get(T.RecommendationAlways)
            : value.Window.Start > now ? UiText.Get(T.RecommendationScheduleUpcomingReason, When(value.Window.Start.Value, now))
            : UiText.Get(value.MoochDepth == 0 && value.IntuitionCatchCount == 0
                ? T.RecommendationScheduleDirectOpenReason : T.RecommendationScheduleOpenReason);
        return [first, RequirementSummary(entry)];
    }

    private string FrequencyComparison(RecommendationEntry entry)
    {
        var value = entry.Recommendation;
        if (entry.FrequencyRank <= 0) return Frequency(value);
        return UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationRarePreyRankReason
                : entry.FrequencyRank == 1 ? T.RecommendationRarestReason : T.RecommendationRareRankReason,
            entry.FrequencyComparisonCount, entry.FrequencyRank, value.OpportunityCount, Name(value.BasisItemId));
    }

    public IEnumerable<(T Label, string Value)> Evidence(RecommendationEntry entry, FishRecommendationView view)
    {
        yield return (T.RecommendationFrequencyShortLabel, Frequency(entry.Recommendation));
        if (entry.Recommendation.OpportunityCount > 0) yield return (T.RecommendationCountBasis, Name(entry.Recommendation.BasisItemId));
        yield return (T.RecommendationAnalysisLabel, UiText.Get(T.RecommendationAnalysisDates, view.AnalysisStart, view.AnalysisStart.AddDays(30)));
    }

    private string Name(uint itemId) => catalog.View.RelatedRows.TryGetValue(itemId, out var row) ? row.Name : UiText.Get(T.RecommendationItem, itemId);
}
