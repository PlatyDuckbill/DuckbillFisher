namespace DuckbillFisher.Plugin;

public sealed record CatalogPreset(Guid Id, string Name, CatalogSettings Settings)
{
    public const int MaxCount = 20;
    public const int MaxNameLength = 32;

    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) &&
        name.Length <= MaxNameLength && name == name.Trim() && !name.Any(char.IsControl);

    public CatalogPreset Copy() => this with { Settings = Settings.Copy() };

    public static List<CatalogPreset> ReadValid(IEnumerable<CatalogPreset>? presets)
    {
        var result = new List<CatalogPreset>();
        var ids = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in presets ?? [])
        {
            if (preset is null || preset.Id == Guid.Empty || !ValidName(preset.Name) || preset.Settings?.IsValid() != true ||
                ids.Contains(preset.Id) || names.Contains(preset.Name)) continue;
            ids.Add(preset.Id);
            names.Add(preset.Name);
            result.Add(preset.Copy());
            if (result.Count == MaxCount) break;
        }
        return result;
    }
}
