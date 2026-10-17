namespace DuckbillFisher.Plugin;

/// <summary>Independent of catalog filters and alert eligibility.</summary>
public sealed record CompactViewSettings
{
    public bool FavoritesOnly { get; init; } = true;
    public int UpcomingMinutes { get; init; } = 30;
    public bool IsValid() => UpcomingMinutes is >= 1 and <= 180;
}
