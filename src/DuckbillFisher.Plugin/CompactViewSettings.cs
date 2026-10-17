namespace DuckbillFisher.Plugin;

/// <summary>The compact horizon is independent; ordinary filters come from CatalogSettings.</summary>
public sealed record CompactViewSettings
{
    public int UpcomingMinutes { get; init; } = 30;
    public bool IsValid() => UpcomingMinutes is >= 1 and <= 180;
}
