using System.Collections.Frozen;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

/// <summary>Build-scoped positive grade facts bound to the exact condition dataset.</summary>
internal static class FishClassificationData
{
    public static readonly FishGradeDataset Dataset;
    public static readonly FrozenDictionary<uint, FishGradeEntry> ByFishId;
    public static readonly FishingDataIssue? Failure;

    static FishClassificationData()
    {
        try
        {
            if (FishingData.Failure is not null)
                throw new FishingDataValidationException(FishingDataError.SourceMismatch, "conditions");
            Dataset = FishingDatasetReader.ReadGrades(FishingData.ReadResource("DuckbillFisher.FishClassifications"),
                FishingData.Dataset, FishingData.ContentHash);
        }
        catch (FishingDataValidationException ex)
        {
            Dataset = new();
            Failure = ex.Issue;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dataset = new();
            Failure = new(FishingDataError.ResourceUnavailable, "DuckbillFisher.FishClassifications");
        }
        ByFishId = Dataset.Entries.ToFrozenDictionary(row => row.FishParameterId);
    }
}
