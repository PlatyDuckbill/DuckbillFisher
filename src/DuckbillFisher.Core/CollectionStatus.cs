namespace DuckbillFisher.Core;

public enum CollectionStatus
{
    Unknown,
    Uncaught,
    Caught,
}

public enum CollectionVisibility { All, HideCaught, UncaughtOnly, CaughtOnly, UnknownOnly }

/// <summary>Manual overrides never overwrite the last observed game state.</summary>
public static class CollectionFilter
{
    public static bool Matches(CollectionStatus status, CollectionVisibility visibility) => visibility switch
    {
        CollectionVisibility.All => true,
        CollectionVisibility.HideCaught => status != CollectionStatus.Caught,
        CollectionVisibility.UncaughtOnly => status == CollectionStatus.Uncaught,
        CollectionVisibility.CaughtOnly => status == CollectionStatus.Caught,
        CollectionVisibility.UnknownOnly => status == CollectionStatus.Unknown,
        _ => false,
    };

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
