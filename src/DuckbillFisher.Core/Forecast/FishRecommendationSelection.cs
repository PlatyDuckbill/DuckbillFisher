namespace DuckbillFisher.Core;

/// <summary>Pure tab membership and ordering. Consumes captured facts without calendar or game access.</summary>
internal static class FishRecommendationSelection
{
    public static RecommendationResult Select(DateTimeOffset now, DateTimeOffset through, DateTimeOffset analysisStart,
        IReadOnlyList<FishRecommendation> facts)
    {
        var choices = facts.Select(value => ForRange(value, now, through)).ToArray();
        var included = Order(choices.Where(value => value.Issue == RecommendationIssue.None), now).ToArray();
        // A later rare prerequisite must not hide a current opportunity in the fixed thirty-minute tab.
        var soon = now.AddMinutes(FishRecommendations.SoonMinutes);
        if (through < soon) soon = through;
        var immediate = facts.Select(value => ForRange(value, now, soon))
            .Where(value => value.Issue == RecommendationIssue.None);
        return new(now, through, analysisStart, included,
            choices.Where(value => value.Issue != RecommendationIssue.None).OrderBy(value => value.Issue).ThenBy(value => value.ItemId).ToArray())
        {
            Now = Order(immediate, now).ToArray(),
            Preparation = Order(included.Where(value => value.MoochDepth > 0 || value.IntuitionCatchCount > 0), now).ToArray(),
            Rare = included.Where(value => value.OpportunityCount > 0).OrderBy(value => value.OpportunityCount)
                .ThenBy(value => value.DisplayWindow.Start ?? now).ThenBy(value => value.ItemId).ToArray(),
        };
    }

    private static FishRecommendation ForRange(FishRecommendation facts, DateTimeOffset now, DateTimeOffset through)
    {
        if (facts.Issue != RecommendationIssue.None) return facts;
        if (!facts.OwnConditionsAlways)
            return facts with { Issue = InRange(facts.Window, now, through) ? RecommendationIssue.None : RecommendationIssue.OutsideHorizon };
        var restricted = facts.Preparation.Where(value => value.Window.Kind is FishingWindowKind.Open or FishingWindowKind.Upcoming).ToArray();
        var primary = restricted.Where(value => InRange(value.Window, now, through))
            .OrderBy(value => value.OpportunityCount).ThenBy(value => value.Window.Start).ThenBy(value => value.ItemId)
                .ThenByDescending(value => value.Kind == PreparationKind.Intuition).FirstOrDefault()
            ?? restricted.OrderBy(value => value.Window.Start).ThenBy(value => value.ItemId)
                .ThenByDescending(value => value.Kind == PreparationKind.Intuition).FirstOrDefault()
            ?? facts.Preparation.OrderBy(value => value.Window.Kind == FishingWindowKind.Unknown)
                .ThenByDescending(value => value.Kind == PreparationKind.Intuition).FirstOrDefault();
        var display = primary?.Window ?? facts.Window;
        var issue = display.Kind == FishingWindowKind.Unknown ? RecommendationIssue.MissingPreparation
            : InRange(display, now, through) ? RecommendationIssue.None : RecommendationIssue.OutsideHorizon;
        var proxy = primary is not null && primary.Window.Kind != FishingWindowKind.Always;
        return facts with
        {
            Issue = issue, PrimaryPreparation = primary,
            OpportunityCount = proxy ? primary!.OpportunityCount : facts.OpportunityCount,
            Basis = proxy ? OpportunityBasis.PrerequisiteProxy : OpportunityBasis.Always,
            BasisItemId = proxy ? primary!.ItemId : facts.ItemId,
            NextOpportunity = primary?.NextOpportunity,
        };
    }

    private static IOrderedEnumerable<FishRecommendation> Order(IEnumerable<FishRecommendation> values, DateTimeOffset now) =>
        values.OrderBy(value => value.DisplayWindow.Kind == FishingWindowKind.Always ? 2 : value.DisplayWindow.Start > now ? 1 : 0)
            .ThenBy(value => value.DisplayWindow.Start > now ? value.DisplayWindow.Start : value.DisplayWindow.End ?? DateTimeOffset.MaxValue)
            .ThenBy(value => value.ItemId);

    private static bool InRange(FishingWindow window, DateTimeOffset now, DateTimeOffset through) =>
        window.Kind == FishingWindowKind.Always || window.Kind is FishingWindowKind.Open or FishingWindowKind.Upcoming
            && (window.End is null || window.End > now) && window.Start <= through;
}
