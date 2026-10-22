namespace DuckbillFisher.Plugin;

/// <summary>The compact horizon and favorites toggle are independent; remaining filters come from CatalogSettings.</summary>
public sealed record CompactViewSettings
{
    public int UpcomingMinutes { get; init; } = 30;
    public bool FavoritesOnly { get; init; }
    public bool IsValid() => UpcomingMinutes is >= 1 and <= 180;
}
