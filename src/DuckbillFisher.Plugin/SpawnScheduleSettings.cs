namespace DuckbillFisher.Plugin;

public enum SpawnScheduleSource { Favorites = 0, Catalog = 1 }

public sealed record SpawnScheduleSettings
{
    public int Hours { get; init; } = 6;
    public bool HideCaught { get; init; } = true;
    public SpawnScheduleSource Source { get; init; }
    public bool IsValid() => Hours is 1 or 3 or 6 or 12 or 24 && Enum.IsDefined(Source);
}
