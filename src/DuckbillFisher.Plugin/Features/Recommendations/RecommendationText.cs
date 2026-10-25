using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Presentation of captured recommendation facts; no forecasting or game reads.</summary>
internal sealed class RecommendationText(FishCatalog catalog)
{
    // A duration is deliberately suffix-free. SpawnAlertOutput.Remaining is a complete
    // "in ..." message and cannot be embedded in "... remaining" or another "in ...".
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
        if (value.Window?.End <= now) return UiText.Get(T.RecommendationEnded);
        if (value.OwnConditionsAlways)
            return value.IntuitionCatchCount > 0 ? IntuitionTiming(value, now) : UiText.Get(T.RecommendationAlways);
        return value.Window?.Start > now
            ? UiText.Get(T.RecommendationOpens, Duration(value.Window.Start.Value - now))
            : value.Window?.End is { } end ? UiText.Get(T.RecommendationEnds, Duration(end - now))
            : UiText.Get(T.RecommendationUnknown);
    }

    public static bool NextUsesPrey(FishRecommendation value) => value.OwnConditionsAlways && value.Basis == OpportunityBasis.PrerequisiteProxy;
    public static DateTimeOffset? NextAt(FishRecommendation value) => value.OwnConditionsAlways ? value.NextOpportunity : value.NextOwnOpportunity;
    public static bool AdvancePreparationExpired(FishRecommendation value, DateTimeOffset now) =>
        !value.OwnConditionsAlways && value.Window?.Start <= now;

    public static IntuitionPreparationWindow? PrimaryIntuition(FishRecommendation value) =>
        value.IntuitionPreparation.FirstOrDefault(prey => prey.ItemId == value.PrimaryIntuitionItemId);

    private static string IntuitionTiming(FishRecommendation value, DateTimeOffset now)
    {
        if (PrimaryIntuition(value) is not { } prey) return UiText.Get(T.RecommendationIntuitionTimeUnknown);
        if (prey.Window.Kind == FishingWindowKind.Always) return UiText.Get(T.RecommendationIntuitionAlways);
        if (prey.Window.End <= now) return UiText.Get(T.RecommendationIntuitionExpired);
        if (prey.Window.Start is { } start && start > now)
            return UiText.Get(T.RecommendationIntuitionUpcoming, When(start, now));
        return prey.Window.End is { } end ? UiText.Get(T.RecommendationIntuitionOpen, Duration(end - now))
            : UiText.Get(T.RecommendationIntuitionTimeUnknown);
    }

    public string IntuitionPreparation(IntuitionPreparationWindow prey, DateTimeOffset now)
    {
        var name = UiText.Get(T.RecommendationCatchCount, Name(prey.ItemId), prey.Count);
        var timing = prey.Window.Kind == FishingWindowKind.Always ? UiText.Get(T.RecommendationAlways)
            : prey.Window.End <= now ? UiText.Get(T.RecommendationPreparationWindowEnded)
            : prey.Window is { Start: { } start, End: { } end }
                ? UiText.Get(T.RecommendationWindowRange, When(start, now),
                    start.ToLocalTime().Date == end.ToLocalTime().Date ? UiText.Get(T.RecommendationClockTime, end.ToLocalTime()) : When(end, now))
                : UiText.Get(T.RecommendationUnknown);
        return UiText.Get(T.RecommendationIntuitionPreparationLine, name, timing);
    }

    public static string When(DateTimeOffset at, DateTimeOffset now)
    {
        var local = at.ToLocalTime();
        var days = (local.Date - now.ToLocalTime().Date).Days;
        return UiText.Get(days == 0 ? T.RecommendationTodayTime : days == 1 ? T.RecommendationTomorrowTime : T.RecommendationDate, local);
    }

    public static string Appearance(FishRecommendation value, DateTimeOffset now) => !value.OwnConditionsAlways &&
        value.Window is { Start: { } start, End: { } end }
            ? UiText.Get(T.RecommendationAppearanceSummary, When(start, now),
                start.ToLocalTime().Date == end.ToLocalTime().Date ? UiText.Get(T.RecommendationClockTime, end.ToLocalTime()) : When(end, now), Timing(value, now))
            : Timing(value, now);

    public string CurrentRange(FishRecommendation value, DateTimeOffset now)
    {
        var window = value.OwnConditionsAlways ? PrimaryIntuition(value)?.Window : value.Window;
        return window is { Start: { } start, End: { } end }
            ? UiText.Get(T.RecommendationWindowRange, When(start, now),
                start.ToLocalTime().Date == end.ToLocalTime().Date ? UiText.Get(T.RecommendationClockTime, end.ToLocalTime()) : When(end, now)) : "";
    }

    public string Source(RecommendationEntry entry) => entry.Recommendation.OwnConditionsAlways && PrimaryIntuition(entry.Recommendation) is { } primary
        ? PrimaryRequirement(entry, primary) : "";

    public string FrequencyCompact(FishRecommendation value) => value.OpportunityCount <= 0 ? ""
        : UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationProxyFrequencyCompact : T.RecommendationCountBadge,
            value.OpportunityCount, Name(value.BasisItemId));

    public string RowReason(RecommendationEntry entry, int focus, DateTimeOffset now)
    {
        var reasons = Reasons(entry, focus, now);
        var value = entry.Recommendation;
        // The frequency is already visible. Prefer the gap after this appearance in
        // the rarity view, and for fish with no prerequisite catches to explain.
        var showGap = reasons.Length > 1 && (focus == 2 || value.IntuitionCatchCount == 0 && value.MoochDepth == 0);
        if (showGap && !value.OwnConditionsAlways && value.NextOwnOpportunity is { } next && value.Window?.End is { } end && next > end)
            return UiText.Get(T.RecommendationGapShortReason, Duration(next - end));
        if (showGap && value.OwnConditionsAlways && value.Basis == OpportunityBasis.PrerequisiteProxy && value.GapHours is > 0)
            return UiText.Get(T.RecommendationPreyGapShortReason, Name(value.BasisItemId), Duration(TimeSpan.FromHours(value.GapHours.Value)));
        var reason = showGap ? reasons[1] : reasons[0];
        return UiText.Get(T.RecommendationRowReason, reason);
    }

    public string Next(FishRecommendation value, DateTimeOffset now, bool compact = false)
    {
        if (NextAt(value) is not { } next)
        {
            if (compact && value.OwnConditionsAlways && !NextUsesPrey(value)) return "";
            return NextUsesPrey(value) ? UiText.Get(T.RecommendationNextPreyUnknown, Name(value.BasisItemId))
                : UiText.Get(value.OwnConditionsAlways ? T.RecommendationNoFixedSpawn : T.RecommendationNextUnknown);
        }
        if (next <= now) return UiText.Get(T.RecommendationRefreshNeeded);
        if (NextUsesPrey(value))
            return UiText.Get(compact ? T.RecommendationNextPreyCompact : T.RecommendationNextPreyTime,
                Name(value.BasisItemId), When(next, now), Duration(next - now));
        return UiText.Get(compact ? T.RecommendationNextCompact : T.RecommendationNextTime, When(next, now), Duration(next - now));
    }

    public string ListContext(RecommendationEntry entry, int focus, DateTimeOffset now)
    {
        var value = entry.Recommendation;
        if (focus == 2)
        {
            if (value.OwnConditionsAlways && PrimaryIntuition(value) is { } prey && value.BasisItemId == prey.ItemId && NextAt(value) is { } next && next > now)
                return UiText.Get(T.RecommendationIntuitionFollowing, PrimaryRequirement(entry, prey), When(next, now));
            return Next(value, now, compact: true);
        }
        if (value.Window?.End <= now) return UiText.Get(T.RecommendationRefreshNeeded);
        if (focus == 1 && AdvancePreparationExpired(value, now)) return UiText.Get(T.RecommendationAdvanceExpiredShort);
        if (value.OwnConditionsAlways && value.IntuitionCatchCount > 0 && PrimaryIntuition(value) is { } primary)
            return UiText.Get(T.RecommendationIntuitionSummary, PrimaryRequirement(entry, primary));
        if (focus == 1 && value.Window?.Start > now)
        {
            var intuition = BeforeIntuition(value);
            if (intuition.Length > 0) return UiText.Get(T.RecommendationPrepareFish, intuition);
            if (!value.OwnConditionsAlways && value.Window?.Start is { } opening &&
                value.Plan!.Steps.LastOrDefault(step => step.End <= opening) is { } prepared)
                return UiText.Get(T.RecommendationPrepareFish, Name(prepared.ItemId));
        }
        if (value.IntuitionCatchCount > 0) return UiText.Get(T.RecommendationIntuitionSummary, Requirements(entry));
        if (value.MoochDepth > 0 && value.Plan!.Steps.LastOrDefault() is { } bait)
            return UiText.Get(T.RecommendationMoochSummary, Name(bait.ItemId));
        return ActionReason(entry, now);
    }

    private string PrimaryRequirement(RecommendationEntry entry, IntuitionPreparationWindow primary) =>
        UiText.Get(T.RecommendationCatchCount, Name(primary.ItemId),
            entry.Fish.Details?.Rule.Predators.FirstOrDefault(prey => prey.ItemId == primary.ItemId)?.Count ?? primary.Count);

    public string Frequency(FishRecommendation value) => value.OpportunityCount > 0
        ? UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationFrequencyProxy : T.RecommendationFrequency,
            value.OpportunityCount, Name(value.BasisItemId)) : UiText.Get(T.RecommendationAlways);

    public string Requirements(RecommendationEntry entry) => entry.Fish.Details?.Rule.Predators is { Length: > 0 } predators
        ? string.Join(", ", predators.Select(prey => UiText.Get(T.RecommendationCatchCount, Name(prey.ItemId), prey.Count)))
        : UiText.Get(T.RecommendationUnknown);

    public string[] Reasons(RecommendationEntry entry, int focus, DateTimeOffset now)
    {
        var value = entry.Recommendation; var plan = value.Plan!;
        if (value.Window?.End <= now) return [UiText.Get(T.RecommendationRefreshNeeded)];
        if (value.OwnConditionsAlways && PrimaryIntuition(value)?.Window.End <= now)
            return [UiText.Get(T.RecommendationRefreshNeeded)];
        if (focus == 1 && AdvancePreparationExpired(value, now)) return [UiText.Get(T.RecommendationAdvanceExpired)];
        var first = focus == 2 ? FrequencyComparison(entry) : ActionReason(entry, now);
        if (focus == 1 && plan.RecommendedStart > now.AddMinutes(FishRecommendations.ActionSoonMinutes))
            return [first, UiText.Get(T.RecommendationStartReason, plan.RecommendedStart.ToLocalTime())];
        if (!value.OwnConditionsAlways && value.NextOwnOpportunity is { } next && value.Window?.End is { } end && next > end)
            return [first, UiText.Get(T.RecommendationMissedGap, Duration(next - end))];
        if (value.OwnConditionsAlways && value.Basis == OpportunityBasis.PrerequisiteProxy && value.GapHours is > 0)
            return [first, UiText.Get(T.RecommendationPreyGapReason, Name(value.BasisItemId), Duration(TimeSpan.FromHours(value.GapHours.Value)))];
        if (focus != 2 && value.OpportunityCount > 0) return [first, Frequency(value)];
        return [first];
    }

    private string ActionReason(RecommendationEntry entry, DateTimeOffset now)
    {
        var value = entry.Recommendation; var plan = value.Plan!;
        if (plan.StoredItemId != 0)
            return UiText.Get(T.RecommendationStoredStrategyReason, Name(plan.StoredItemId));
        if (value.PreparationGateEnd is { } deadline && deadline > now)
            return UiText.Get(T.RecommendationGateDeadlineReason, Name(value.PreparationGateItem), deadline.ToLocalTime());
        if (value.IntuitionCatchCount > 0)
        {
            var before = BeforeIntuition(value);
            return before.Length > 0 && value.Window?.Start > now
                ? UiText.Get(T.RecommendationIntuitionBeforeReason, before)
                : UiText.Get(T.RecommendationIntuitionRequirementReason, Requirements(entry));
        }
        if (value.MoochDepth > 0)
        {
            if (value.Window?.Start is { } opening && opening > now &&
                plan.Steps.LastOrDefault(step => step.End <= opening) is { } prepared)
            {
                return UiText.Get(T.RecommendationBaitBeforeReason, Name(prepared.ItemId), opening.ToLocalTime());
            }
            var bait = plan.Steps.LastOrDefault();
            if (bait is not null)
                return UiText.Get(plan.Readiness == RecommendationReadiness.Planned ? T.RecommendationBaitFitsReason : T.RecommendationBaitNeededReason,
                    Name(bait.ItemId));
        }
        return value.Window?.Start is { } start && start > now
            ? UiText.Get(T.RecommendationDirectUpcomingReason, start.ToLocalTime())
            : UiText.Get(T.RecommendationDirectNowReason);
    }

    private string BeforeIntuition(FishRecommendation value) => !value.OwnConditionsAlways && value.Window?.Start is { } opening
        ? string.Join(", ", value.Plan!.Steps.Where(step => step.End <= opening && (step.Role is PreparationRole.Intuition or PreparationRole.Trigger))
            .GroupBy(step => step.ItemId).Select(group => UiText.Get(T.RecommendationCatchCount, Name(group.Key), group.Sum(step => step.Count)))) : "";

    private string FrequencyComparison(RecommendationEntry entry)
    {
        var value = entry.Recommendation;
        if (entry.FrequencyRank <= 0) return Frequency(value);
        return UiText.Get(value.Basis == OpportunityBasis.PrerequisiteProxy ? T.RecommendationRarePreyRankReason
                : entry.FrequencyRank == 1 ? T.RecommendationRarestReason : T.RecommendationRareRankReason,
            entry.FrequencyComparisonCount, entry.FrequencyRank, value.OpportunityCount, Name(value.BasisItemId));
    }

    public IEnumerable<(T Label, string Value)> Evidence(RecommendationEntry entry, int focus, FishRecommendationView view)
    {
        var value = entry.Recommendation; var plan = value.Plan!;
        if (focus == 2)
        {
            yield return (T.RecommendationFrequencyShortLabel, Frequency(value));
            yield return (T.RecommendationCountBasis, Name(value.BasisItemId));
            if (value.GapHours is { } gap)
                yield return (T.RecommendationTieBreak, UiText.Get(T.RecommendationGapComparison, Duration(TimeSpan.FromHours(gap))));
            yield return (T.RecommendationAnalysisLabel, UiText.Get(T.RecommendationAnalysisDates, view.AnalysisStart, view.AnalysisStart.AddDays(30)));
            yield break;
        }
        var start = plan.RecommendedStart < view.At ? view.At : plan.RecommendedStart;
        yield return (T.RecommendationStartEstimate, UiText.Get(T.RecommendationDate, start.ToLocalTime()));
        if (focus == 1)
        {
            var before = BeforeIntuition(value);
            if (before.Length > 0) yield return (T.RecommendationBeforeCatchLabel, before);
            else if (!value.OwnConditionsAlways && value.Window?.Start is { } opening &&
                plan.Steps.LastOrDefault(step => step.End <= opening) is { } prepared)
                yield return (T.RecommendationBeforeCatchLabel, Name(prepared.ItemId));
        }
        if (plan.Steps.Count > 0)
            yield return (T.RecommendationFirstCastLabel, plan.FirstCast is { } cast
                ? UiText.Get(T.RecommendationDate, cast.ToLocalTime()) : UiText.Get(T.RecommendationEstimateOnly));
        if (value.OpportunityCount > 0) yield return (T.RecommendationFrequencyShortLabel, Frequency(value));
    }

    private string Name(uint itemId) => catalog.View.RelatedRows.TryGetValue(itemId, out var row) ? row.Name : UiText.Get(T.RecommendationItem, itemId);
}
