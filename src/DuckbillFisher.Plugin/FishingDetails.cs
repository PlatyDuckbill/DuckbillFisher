using System.Text.Json;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

internal sealed record FishingSpotView(uint Id, string Name, string Zone, uint TerritoryId, uint MapId,
    float X, float Y, WeatherChance[] WeatherRates)
{
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

internal sealed class FishingDataset
{
    public int SchemaVersion { get; init; }
    public string GameBuild { get; init; } = "";
    public string DataVersion { get; init; } = "";
    public string SourceRepository { get; init; } = "";
    public string SourceRevision { get; init; } = "";
    public FishingRule[] Fish { get; init; } = [];
    public SpearfishingRule[] Spearfishing { get; init; } = [];
}

internal static class FishingData
{
    public static readonly FishingDataset Dataset = Load();
    public static readonly int SpearUnknown = Dataset.Spearfishing.Count(fish => !fish.ConditionsKnown);
    public static readonly string Notices = ReadResource("DuckbillFisher.ThirdPartyNotices");

    private static FishingDataset Load()
    {
        var dataset = JsonSerializer.Deserialize<FishingDataset>(ReadResource("DuckbillFisher.FishingData"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Fishing dataset is empty.");
        if (dataset.SchemaVersion != 2 || dataset.Fish.Length == 0 ||
            dataset.Fish.Select(fish => fish.ItemId).Distinct().Count() != dataset.Fish.Length ||
            dataset.Fish.Select(fish => fish.FishParameterId).Distinct().Count() != dataset.Fish.Length)
            throw new InvalidDataException("Unsupported or duplicate fishing dataset.");
        foreach (var fish in dataset.Fish)
            if (fish.ItemId == 0 || fish.FishParameterId == 0 || (fish.FishingSpotId == 0 && fish.ConditionsKnown) ||
                fish.BaitSteps.Length == 0 || fish.BaitSteps.Any(step => step.Length == 0 || step.Any(id => id == 0)) ||
                fish.Hookset is not ("" or "Precision" or "Powerful") || fish.Tug is not ("" or "light" or "medium" or "heavy") ||
                fish.Lure is not ("" or "Modest" or "Ambitious") ||
                !double.IsFinite(fish.StartHour) || !double.IsFinite(fish.EndHour) || fish.StartHour < 0 || fish.StartHour >= 24 ||
                fish.EndHour < 0 || fish.EndHour > 24 || fish.StartHour == fish.EndHour ||
                fish.Predators.Any(requirement => requirement.ItemId == 0 || requirement.Count <= 0))
                throw new InvalidDataException("Invalid fishing rule.");
        if (dataset.Spearfishing.Select(fish => fish.ItemId).Distinct().Count() != dataset.Spearfishing.Length ||
            dataset.Spearfishing.Any(fish => fish.ItemId == 0 || fish.SpearfishingItemId == 0 ||
                fish.Gig is not ("Small" or "Normal" or "Large" or "UNKNOWN") || (fish.ConditionsKnown && fish.Gig == "UNKNOWN")))
            throw new InvalidDataException("Invalid spearfishing source record.");
        return dataset;
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(FishingData).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"Missing embedded resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
