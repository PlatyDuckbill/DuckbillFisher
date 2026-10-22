namespace DuckbillFisher.Core;

public sealed class FishingDataset
{
    public int SchemaVersion { get; init; }
    public string Region { get; init; } = "";
    public string GameBuild { get; init; } = "";
    public string DataVersion { get; init; } = "";
    public string SourceRepository { get; init; } = "";
    public string SourceRevision { get; init; } = "";
    public FishingRule[] Fish { get; init; } = [];
    public SpearfishingRule[] Spearfishing { get; init; } = [];
    public FishingSupplementSource[] SupplementalSources { get; init; } = [];
}

public sealed record FishingSupplementSource(string Name, string Repository, string Revision);
public sealed record FishGradeEntry(uint FishParameterId, uint ItemId, FishGrade Grade);

public sealed class FishGradeDataset
{
    public int SchemaVersion { get; init; }
    public string GameBuild { get; init; } = "";
    public string DataVersion { get; init; } = "";
    public string ConditionDataSha256 { get; init; } = "";
    public FishGradeEntry[] Entries { get; init; } = [];
}

public enum FishingDataError
{
    InvalidJson, MissingField, DuplicateField, UnsupportedSchema, InvalidValue,
    DuplicateIdentity, SourceMismatch, InconsistentReference, ResourceUnavailable,
}

public sealed record FishingDataIssue(FishingDataError Code, string Path);

public sealed class FishingDataValidationException(FishingDataError code, string path) : Exception($"{code}: {path}")
{
    public FishingDataIssue Issue { get; } = new(code, path);
}
