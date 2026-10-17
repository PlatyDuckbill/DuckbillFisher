using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

// Serialized values are independent of translated labels and UI choice order.
public enum CatalogSort { Name = 0, FishId = 1, RemainingTime = 2, FavoritesFirst = 3 }

public sealed record CatalogSettings
{
    public CatalogSort Sort { get; init; } = CatalogSort.RemainingTime;
    public CollectionVisibility Collection { get; init; } = CollectionVisibility.HideCaught;
    public FishGradeVisibility Grade { get; init; } = FishGradeVisibility.BigIncludingLegendary;
    public bool InLogOnly { get; init; } = true;
    public bool IncludeOceanFish { get; init; }
    public bool TimeRestrictedOnly { get; init; } = true;
    public bool FavoritesOnly { get; init; }
    public bool CurrentRegionOnly { get; init; }
    public uint[] Regions { get; init; } = [];
    public uint[] Expansions { get; init; } = [];

    public bool IsValid() => Enum.IsDefined(Sort) && Enum.IsDefined(Grade) &&
        Collection is CollectionVisibility.All or CollectionVisibility.HideCaught or CollectionVisibility.CaughtOnly or CollectionVisibility.UnknownOnly &&
        Regions is not null && Regions.All(id => id != 0) && Expansions is not null;

    public CatalogSettings Copy() => this with
    {
        Regions = Regions.Distinct().Order().ToArray(),
        Expansions = Expansions.Distinct().Order().ToArray(),
    };

    public bool Matches(CatalogSettings other) => Sort == other.Sort && Collection == other.Collection && Grade == other.Grade &&
        InLogOnly == other.InLogOnly && IncludeOceanFish == other.IncludeOceanFish && TimeRestrictedOnly == other.TimeRestrictedOnly &&
        FavoritesOnly == other.FavoritesOnly && CurrentRegionOnly == other.CurrentRegionOnly &&
        Regions.SequenceEqual(other.Regions) && Expansions.SequenceEqual(other.Expansions);
}
