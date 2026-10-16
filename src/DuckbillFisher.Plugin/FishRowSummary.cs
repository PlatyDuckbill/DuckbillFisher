using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Formats cached sheet references only; no game reads or forecasting.</summary>
internal sealed record FishRowSummary(string Bait, string Conditions)
{
    public static FishRowSummary Create(FishGuideRow fish)
    {
        if (fish.IsOceanFishing)
            return new(UiText.Get(T.RowBaitUnknown), UiText.Get(T.RowOceanConditions));
        if (fish.Details is not { } details)
            return new(UiText.Get(T.RowBaitUnknown), UiText.Get(T.RowConditionsUnknown));

        // Parentheses preserve alternatives within one step instead of implying another mooch.
        var path = string.Join(" → ", details.BaitSteps.Select(step => step.Length > 1
            ? $"({string.Join(" / ", step.Select(item => item.Name))})"
            : step[0].Name));
        var parts = new List<string>();
        var rule = details.Rule;
        if (!rule.ConditionsKnown)
            parts.Add(UiText.Get(T.RowConditionsUnknown));
        else
        {
            if (rule.StartHour != 0 || rule.EndHour != 24)
                parts.Add(UiText.Get(T.TimeConditionLabel, EtClock(rule.StartHour), EtClock(rule.EndHour)));
            if (details.PreviousWeather.Length > 0)
                parts.Add(UiText.Get(T.PreviousWeatherConditionLabel,
                    string.Join(" / ", details.PreviousWeather.Select(weather => weather.Name))));
            if (details.Weather.Length > 0)
                parts.Add(UiText.Get(T.WeatherConditionLabel,
                    string.Join(" / ", details.Weather.Select(weather => weather.Name))));
        }
        if (details.Predators.Length > 0) parts.Add(UiText.Get(T.RowIntuition));
        if (rule.FolkloreRequired) parts.Add(UiText.Get(T.RowFolklore));
        if (rule.SnaggingRequired)
            parts.Add(UiText.Get(T.RequiredSkill, details.Snagging?.Name ?? UiText.Get(T.SnaggingRequired)));
        if (rule.Lure.Length > 0)
            parts.Add(UiText.Get(T.RequiredSkill, details.Lure?.Name ??
                UiText.Get(rule.Lure == "Modest" ? T.LureModest : T.LureAmbitious)));
        parts.Add(UiText.Get(T.HooksetLabel, details.Hookset?.Name ?? UiText.Get(rule.Hookset switch
            { "Precision" => T.HooksetPrecision, "Powerful" => T.HooksetPowerful, _ => T.ConditionUnknown })));

        return new(UiText.Get(T.RowBaitPath, path), string.Join(" · ", parts));
    }

    private static string EtClock(double hour) => $"{(int)hour:00}:{(int)Math.Round(hour % 1 * 60):00}";
}
