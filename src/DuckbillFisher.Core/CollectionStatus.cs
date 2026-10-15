namespace DuckbillFisher.Core;

public enum CollectionStatus
{
    Unknown,
    Uncaught,
    Caught,
}

/// <summary>Manual overrides never overwrite the last observed game state.</summary>
public static class CollectionFilter
{
    public static CollectionStatus Resolve(CollectionStatus observed, bool? manualCaught) =>
        manualCaught switch
        {
            true => CollectionStatus.Caught,
            false => CollectionStatus.Uncaught,
            null => observed,
        };

    public static bool ShouldNotify(bool isFavorite, CollectionStatus status) =>
        isFavorite && status == CollectionStatus.Uncaught;
}
