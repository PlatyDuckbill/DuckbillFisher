namespace DuckbillFisher.Plugin;

/// <summary>Pure persisted-setting transformations. Never reads or writes the configuration file.</summary>
internal static class PreferencesMigration
{
    public const int CurrentVersion = 22;
    public static bool Supports(int version) => version is >= 1 and <= CurrentVersion;

    public static CatalogSettings Catalog(CatalogSettings source, int version)
    {
        var result = source.Copy();
        return version < 19 && result.MaximumUpcomingMinutes == 0
            ? result with { MaximumUpcomingMinutes = null } : result;
    }

    public static List<CatalogPreset> Presets(IEnumerable<CatalogPreset>? source, int version) =>
        CatalogPreset.ReadValid(source).Select(preset =>
            preset with { Settings = Catalog(preset.Settings, version) }).ToList();

    public static CompactViewSettings Compact(CompactViewSettings source, CatalogSettings catalog, int version) =>
        version < 15 ? source with { FavoritesOnly = catalog.FavoritesOnly } : source;

    public static SpawnScheduleSettings Schedule(SpawnScheduleSettings? source, int version)
    {
        var result = source is not null && source.IsValid() ? source : new();
        return version < 17 && result.Hours == 6 ? result with { Hours = 3 } : result;
    }

    public static string SeenVersion(string? source) => Version.TryParse(source, out _) ? source! : "";
}
