using System.Text.Json;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

internal sealed record FishingSpotView(uint Id, string Name, string Zone, uint TerritoryId, uint MapId,
    float X, float Y, WeatherChance[] WeatherRates);

internal sealed record FishingItemView(uint Id, string Name, uint IconId);
internal sealed record FishingConditionIcon(uint Id, string Name, uint IconId);

internal sealed record FishingDetailView(FishingRule Rule, FishingSpotView Spot, FishingItemView[] BaitPath,
    FishingConditionIcon[] Weather, FishingConditionIcon[] PreviousWeather, FishingItemView[] Predators,
    FishingConditionIcon? Hookset);

internal sealed class FishingDataset
{
    public int SchemaVersion { get; init; }
    public string GameBuild { get; init; } = "";
    public string DataVersion { get; init; } = "";
    public string SourceRepository { get; init; } = "";
    public string SourceRevision { get; init; } = "";
    public FishingRule[] Fish { get; init; } = [];
}

internal static class FishingData
{
    public static readonly FishingDataset Dataset = Load();
    public static readonly string Notices = ReadResource("DuckbillFisher.ThirdPartyNotices");

    private static FishingDataset Load()
    {
        var dataset = JsonSerializer.Deserialize<FishingDataset>(ReadResource("DuckbillFisher.FishingData"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Fishing dataset is empty.");
        if (dataset.SchemaVersion != 1 || dataset.Fish.Length == 0 ||
            dataset.Fish.Select(fish => fish.ItemId).Distinct().Count() != dataset.Fish.Length)
            throw new InvalidDataException("Unsupported or duplicate fishing dataset.");
        foreach (var fish in dataset.Fish)
            if (fish.ItemId == 0 || fish.FishParameterId == 0 || fish.FishingSpotId == 0 ||
                fish.BaitPath.Length == 0 || fish.BaitPath.Any(id => id == 0) ||
                fish.Hookset is not ("Precision" or "Powerful") || fish.Tug is not ("light" or "medium" or "heavy") ||
                fish.Predators.Any(requirement => requirement.ItemId == 0 || requirement.Count <= 0))
                throw new InvalidDataException("Invalid fishing rule.");
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
