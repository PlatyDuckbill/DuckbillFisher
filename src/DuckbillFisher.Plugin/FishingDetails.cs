using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

internal sealed record FishingSpotView(uint Id, string Name, string Zone, uint TerritoryId, uint MapId,
    float X, float Y, WeatherChance[] WeatherRates)
{
    public uint PlaceNameId { get; init; }
    public uint? ExpansionId { get; init; }
    public string ExpansionName { get; init; } = "";
    public FishingLocation Location => new(TerritoryId, ExpansionId);
}

internal sealed record FishingItemView(uint Id, string Name, uint IconId);
internal sealed record FishingConditionIcon(uint Id, string Name, uint IconId);

internal sealed record FishingDetailView(FishingRule Rule, FishingSpotView? Spot, FishingItemView[][] BaitSteps,
    FishingConditionIcon[] Weather, FishingConditionIcon[] PreviousWeather, FishingItemView[] Predators,
    FishingConditionIcon? Hookset, FishingConditionIcon? Snagging, FishingConditionIcon? Lure,
    FishingItemView? Folklore)
{
    public FishingConditionIcon? Intuition { get; init; }
    public UiMessage ConditionWarning => new(Localization.TextKey.ConditionsPartial);
}

internal sealed record OceanFishingView(string Area, string FishingSpot, string[] RouteNames, byte[] VisitTimes);
