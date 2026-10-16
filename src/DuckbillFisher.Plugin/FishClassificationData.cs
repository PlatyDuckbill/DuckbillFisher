using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

internal sealed record FishGradeEntry(uint FishParameterId, uint ItemId, FishGrade Grade);
internal sealed class FishGradeDataset
{
    public int SchemaVersion { get; init; }
    public string GameBuild { get; init; } = "";
    public string DataVersion { get; init; } = "";
    public FishGradeEntry[] Entries { get; init; } = [];
}

/// <summary>Build-scoped positive grade facts. Runtime never classifies translated text.</summary>
internal static class FishClassificationData
{
    public static readonly FishGradeDataset Dataset = Load();
    public static readonly FrozenDictionary<uint, FishGradeEntry> ByFishId = Dataset.Entries.ToFrozenDictionary(row => row.FishParameterId);

    private static FishGradeDataset Load()
    {
        using var stream = typeof(FishClassificationData).Assembly.GetManifestResourceStream("DuckbillFisher.FishClassifications")
            ?? throw new InvalidDataException("Missing fish classification resource.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter<FishGrade>(allowIntegerValues: false));
        var data = JsonSerializer.Deserialize<FishGradeDataset>(stream, options)
            ?? throw new InvalidDataException("Empty fish classification resource.");
        if (data.SchemaVersion != 1 || data.GameBuild.Length == 0 || data.Entries.Length == 0 ||
            data.Entries.Any(row => row.FishParameterId == 0 || row.ItemId == 0 || row.Grade is not (FishGrade.Big or FishGrade.Legendary)) ||
            data.Entries.Select(row => row.FishParameterId).Distinct().Count() != data.Entries.Length ||
            data.Entries.Select(row => row.ItemId).Distinct().Count() != data.Entries.Length)
            throw new InvalidDataException("Invalid fish classification resource.");
        return data;
    }
}
