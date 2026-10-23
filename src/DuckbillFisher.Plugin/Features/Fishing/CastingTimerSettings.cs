using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

public sealed record CastingTimerSettings
{
    public const int MaxAlerts = 32;
    public const int MaxSeconds = 3600;
    public const int MaxPresets = 20;
    public const int MaxNameLength = 64;
    public List<CastTimerAlert> Alerts { get; init; } = [];
    public List<CastingTimerPreset> Presets { get; init; } = [];

    public static bool ValidName(string? name) => name is not null && name.Length <= MaxNameLength && !name.Any(char.IsControl);
    public static bool ValidAlerts(List<CastTimerAlert>? alerts) => alerts is { Count: <= MaxAlerts } && alerts.All(alert =>
        alert is not null && alert.Id != Guid.Empty && alert.Seconds is >= 1 and <= MaxSeconds &&
        ValidName(alert.Name) && alert.Sound is >= 0 and <= 16) &&
        alerts.Select(alert => alert.Id).Distinct().Count() == alerts.Count;
    public bool IsValid() => ValidAlerts(Alerts) && Presets is { Count: <= MaxPresets } &&
        Presets.All(preset => preset is not null && preset.Id != Guid.Empty && ValidName(preset.Name) &&
            !string.IsNullOrWhiteSpace(preset.Name) && ValidAlerts(preset.Alerts)) &&
        Presets.Select(preset => preset.Id).Distinct().Count() == Presets.Count &&
        Presets.Select(preset => preset.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Presets.Count;
    public CastingTimerSettings Copy() => this with { Alerts = [.. Alerts], Presets = Presets.Select(preset => preset.Copy()).ToList() };
    public bool Matches(CastingTimerSettings other) => Alerts.SequenceEqual(other.Alerts) &&
        Presets.Count == other.Presets.Count && Presets.Zip(other.Presets).All(pair =>
            pair.First.Id == pair.Second.Id && pair.First.Name == pair.Second.Name && pair.First.Alerts.SequenceEqual(pair.Second.Alerts));
}

public sealed record CastingTimerPreset(Guid Id, string Name, List<CastTimerAlert> Alerts)
{
    public CastingTimerPreset Copy() => this with { Alerts = [.. Alerts] };
}
