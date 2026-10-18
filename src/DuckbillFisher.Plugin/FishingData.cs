using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

/// <summary>Invalid embedded facts disable their consumers, not the game-sheet catalog or manual state.</summary>
internal static class FishingData
{
    public static readonly FishingDataset Dataset;
    public static readonly string ContentHash = "";
    public static readonly FishingDataIssue? Failure;
    public static readonly int SpearUnknown;
    public static readonly string Notices;

    static FishingData()
    {
        try
        {
            var json = ReadResource("DuckbillFisher.FishingData");
            Dataset = FishingDatasetReader.ReadConditions(json);
            ContentHash = FishingDatasetReader.ContentHash(json);
            SpearUnknown = Dataset.Spearfishing.Count(fish => !fish.ConditionsKnown);
        }
        catch (FishingDataValidationException ex)
        {
            Dataset = new();
            Failure = ex.Issue;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dataset = new();
            Failure = new(FishingDataError.ResourceUnavailable, "DuckbillFisher.FishingData");
        }
        Notices = ReadResource("DuckbillFisher.ThirdPartyNotices");
    }

    internal static string ReadResource(string name)
    {
        using var stream = typeof(FishingData).Assembly.GetManifestResourceStream(name)
            ?? throw new IOException($"Missing embedded resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
