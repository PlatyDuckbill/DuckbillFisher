namespace DuckbillFisher.Core;

public enum OceanBaitKind { Unknown, Direct, Mooch }
public enum OceanBaitReason { SpectralTrigger, Score, IntuitionCandidate }

/// <summary>Published facts, not catch probabilities or a skill-use instruction.</summary>
public sealed record OceanBaitTarget(uint FishItemId, uint FishParameterId, uint FishingSpotId,
    uint BaitItemId, OceanBaitKind BaitKind, int Points, bool SpectralTrigger, string Tug);
public sealed record OceanBaitChoice(OceanBaitTarget Target, OceanBaitReason Reason, OceanCandidateState Availability);
public sealed record OceanBaitRecommendation(OceanBaitChoice? Primary, OceanBaitChoice[] IntuitionCandidates);

public static class OceanBaitRecommendations
{
    public static OceanBaitRecommendation Select(IReadOnlyList<OceanBaitTarget> targets,
        IReadOnlyDictionary<(uint Spot, uint Item), OceanFishCondition> conditions,
        IReadOnlyDictionary<uint, int> minimumTripleYields, uint fishingSpot, byte time, uint? weather,
        bool? intuition, bool spectral, bool preview = false)
    {
        if (fishingSpot == 0 || time is < 1 or > 3) return new(null, []);
        var ordinary = new List<OceanBaitChoice>();
        var special = new List<OceanBaitChoice>();
        foreach (var target in targets)
        {
            if (target.FishingSpotId != fishingSpot || target.BaitItemId == 0 || target.BaitKind == OceanBaitKind.Unknown ||
                !conditions.TryGetValue((fishingSpot, target.FishItemId), out var rule) || rule.FishParameterId != target.FishParameterId) continue;
            if (rule.RequiresIntuition)
            {
                if (!preview && intuition != true) continue;
                // A positive generic status cannot identify the target, especially across pool changes.
                // Check only time/weather eligibility and keep this a conditional candidate.
                if (rule.Evaluate(time, weather, true) != OceanCandidateState.Excluded)
                    special.Add(new(target, OceanBaitReason.IntuitionCandidate, OceanCandidateState.Unconfirmed));
                continue;
            }
            if (target.BaitKind != OceanBaitKind.Direct) continue;
            var state = rule.Evaluate(time, weather, null);
            if (state == OceanCandidateState.Excluded) continue;
            var reason = !spectral && target.SpectralTrigger ? OceanBaitReason.SpectralTrigger : OceanBaitReason.Score;
            if (reason == OceanBaitReason.Score && (!minimumTripleYields.TryGetValue(target.FishItemId, out var minimum) || minimum is < 1 or > 7)) continue;
            ordinary.Add(new(target, reason, state));
        }
        // The score is a transparent comparison of published points and the minimum Triple Hook yield.
        // It is not an expected score, a rate per minute, or a claim the player can use Triple Hook.
        var primary = ordinary.OrderBy(c => c.Availability == OceanCandidateState.Unconfirmed)
            .ThenBy(c => c.Reason == OceanBaitReason.SpectralTrigger ? 0 : 1)
            .ThenByDescending(c => (long)c.Target.Points * minimumTripleYields.GetValueOrDefault(c.Target.FishItemId))
            .ThenByDescending(c => c.Target.Points).ThenBy(c => c.Target.FishItemId).FirstOrDefault();
        return new(primary, special.OrderBy(c => c.Target.FishItemId).ToArray());
    }
}
