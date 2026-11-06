namespace DuckbillFisher.Core;

public enum OceanCandidateState { Candidate, Excluded, Unconfirmed }

/// <summary>Verified time/weather facts only. An absent observation never excludes a fish.</summary>
public sealed record OceanFishCondition(uint ItemId, uint FishParameterId, uint FishingSpotId,
    int TimeMask, uint[] AllowedWeatherIds, uint[] ExcludedWeatherIds, uint[] UnknownWeatherIds, bool RequiresIntuition)
{
    public OceanCandidateState Evaluate(byte time, uint? weather, bool? intuition)
    {
        if (time is >= 1 and <= 3 && (TimeMask & (1 << time)) == 0) return OceanCandidateState.Excluded;
        var unknown = time is < 1 or > 3;
        if (AllowedWeatherIds.Length > 0 || ExcludedWeatherIds.Length > 0 || UnknownWeatherIds.Length > 0)
        {
            if (weather is null || UnknownWeatherIds.Contains(weather.Value)) unknown = true;
            else if (ExcludedWeatherIds.Contains(weather.Value)) return OceanCandidateState.Excluded;
            else if (!AllowedWeatherIds.Contains(weather.Value)) unknown = true;
        }
        if (RequiresIntuition)
        {
            if (intuition == false) return OceanCandidateState.Excluded;
            if (intuition is null) unknown = true;
        }
        return unknown ? OceanCandidateState.Unconfirmed : OceanCandidateState.Candidate;
    }
}
