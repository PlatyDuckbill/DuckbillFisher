namespace DuckbillFisher.Plugin;

// Persisted observations, separate from manual overrides and the current session's live sample.
public sealed record FishCollectionBackup
{
    public DateTimeOffset UpdatedUtc { get; init; }
    public Dictionary<uint, bool> States { get; init; } = [];

    public bool IsValid() => UpdatedUtc > DateTimeOffset.UnixEpoch && States is { Count: > 0 and <= 10000 } &&
        !States.ContainsKey(0);

    public FishCollectionBackup Copy() => this with { States = new(States) };
}
