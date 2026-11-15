namespace DuckbillFisher.Core;

public enum OceanBaitKind { Unknown, Direct, Mooch }
public enum OceanBaitReason { SpectralTrigger, Score, IntuitionCandidate, SpeciesGoal }
public enum OceanFishingGoal { Default, Shark, Jellyfish, Fugu, Crab, Shrimp, Shellfish, Squid, Manta, Octopus, Seadragon, Mantis, Prehistoric }

/// <summary>Published facts, not catch probabilities or a skill-use instruction.</summary>
public sealed record OceanBaitTarget(uint FishItemId, uint FishParameterId, uint FishingSpotId,
    uint BaitItemId, OceanBaitKind BaitKind, int Points, bool SpectralTrigger, string Tug)
{
    // Default means the source did not classify this fish for a selectable group.
    public OceanFishingGoal Group { get; init; }
}
public sealed record OceanBaitChoice(OceanBaitTarget Target, OceanBaitReason Reason, OceanCandidateState Availability);
public sealed record OceanBaitRecommendation(OceanBaitChoice? Primary, OceanBaitChoice[] IntuitionCandidates);

public static class OceanBaitRecommendations
{
    // Source: Tyo'to Tayuun's FAQ, pinned in ocean-guidance.v1.json policySource.
    public const int SpectralTriggerCutoffSeconds = 90;

    public static OceanBaitRecommendation Select(IReadOnlyList<OceanBaitTarget> targets,
        IReadOnlyDictionary<(uint Spot, uint Item), OceanFishCondition> conditions,
        IReadOnlyDictionary<uint, int> minimumTripleYields, uint fishingSpot, byte time, uint? weather,
        bool? intuition, bool spectral, bool preview = false, double? remainingSeconds = null, OceanFishingGoal goal = OceanFishingGoal.Default)
    {
        if (fishingSpot == 0 || time is < 1 or > 3 || !Enum.IsDefined(goal)) return new(null, []);
        bool? triggerAvailable = preview ? true : remainingSeconds is { } seconds && double.IsFinite(seconds) && seconds is >= 0 and <= 420
            ? seconds > SpectralTriggerCutoffSeconds : null;
        var ordinary = new List<OceanBaitChoice>();
        var special = new List<OceanBaitChoice>();
        foreach (var target in targets)
        {
            if (target.FishingSpotId != fishingSpot || target.BaitItemId == 0 || target.BaitKind == OceanBaitKind.Unknown ||
                !conditions.TryGetValue((fishingSpot, target.FishItemId), out var rule) || rule.FishParameterId != target.FishParameterId) continue;
            if (goal != OceanFishingGoal.Default && target.Group != goal) continue;
            if (rule.RequiresIntuition)
            {
                if (!preview && intuition != true) continue;
                // A positive generic status cannot identify the target, especially across pool changes.
                // Check only time/weather eligibility and keep this a conditional candidate.
                if (rule.Evaluate(time, weather, true) != OceanCandidateState.Excluded)
                    special.Add(new(target, OceanBaitReason.IntuitionCandidate, OceanCandidateState.Unconfirmed));
                continue;
            }
            if (goal == OceanFishingGoal.Default && target.BaitKind != OceanBaitKind.Direct) continue;
            var state = rule.Evaluate(time, weather, null);
            if (state == OceanCandidateState.Excluded) continue;
            var reason = goal != OceanFishingGoal.Default ? OceanBaitReason.SpeciesGoal
                : !spectral && target.SpectralTrigger && triggerAvailable != false ? OceanBaitReason.SpectralTrigger : OceanBaitReason.Score;
            if (reason == OceanBaitReason.SpectralTrigger && triggerAvailable is null) state = OceanCandidateState.Unconfirmed;
            if (reason != OceanBaitReason.SpectralTrigger && (!minimumTripleYields.TryGetValue(target.FishItemId, out var minimum) || minimum is < 1 or > 7)) continue;
            ordinary.Add(new(target, reason, state));
        }
        // Default goals compare points × minimum Triple Hook yield; group goals compare yield alone.
        // Neither is an expected rate, an achievement-progress check, or a claim Triple Hook is unlocked.
        var primary = ordinary.OrderBy(c => c.Availability == OceanCandidateState.Unconfirmed)
            .ThenBy(c => c.Reason == OceanBaitReason.SpectralTrigger ? 0 : 1)
            .ThenByDescending(c => (goal == OceanFishingGoal.Default ? (long)c.Target.Points : 1) * minimumTripleYields.GetValueOrDefault(c.Target.FishItemId))
            .ThenBy(c => c.Target.BaitKind == OceanBaitKind.Mooch)
            .ThenByDescending(c => c.Target.Points).ThenBy(c => c.Target.FishItemId).FirstOrDefault();
        return new(primary, special.OrderBy(c => c.Target.FishItemId).ToArray());
    }
}
