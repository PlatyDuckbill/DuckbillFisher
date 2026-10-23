using Dalamud.Game.Text;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

internal static class PreferencesValidation
{
    public static bool IsValid(FishPreferencesConfiguration value) =>
        PreferencesMigration.Supports(value.Version) && value.Favorites is not null &&
        (value.Version < 20 || value.Timer is not null && value.Timer.IsValid() && value.CollectionBackups is not null &&
            value.CollectionBackups.All(pair => pair.Key != 0 && pair.Value is not null && pair.Value.IsValid())) &&
        Enum.IsDefined(value.Language) &&
        value.Compact is not null && value.Compact.IsValid() &&
        value.Catalog is not null && value.Catalog.IsValid() &&
        !value.Favorites.Contains(0) && value.Characters is not null &&
        value.Characters.All(pair => pair.Key != 0 && pair.Value is not null && !pair.Value.ContainsKey(0)) &&
        value.SpawnAlertLeadMinutes is >= SpawnAlertScheduler.MinLeadMinutes and <= SpawnAlertScheduler.MaxLeadMinutes &&
        (value.Version <= 3 ? Enum.IsDefined(value.SpawnAlertChannel) : ChatChannelChoices.IsKnown((XivChatType)value.SpawnAlertChannelId)) &&
        (value.SpawnAlertSound is >= 0 and <= 16 || value.Version >= 12 && value.SpawnAlertSound == CustomAlertSound.Choice) &&
        Enum.IsDefined(value.SpawnAlertAudience) &&
        value.FishAlertLeadMinutes is not null && value.FishAlertLeadMinutes.All(pair => pair.Key != 0 &&
            pair.Value is >= SpawnAlertScheduler.MinLeadMinutes and <= SpawnAlertScheduler.MaxLeadMinutes) &&
        value.AlertWindows is not null && value.AlertWindows.All(pair => pair.Key != 0 && pair.Value is not null &&
            pair.Value.All(window => window.Key != 0 && window.Value is > 0 and <= 253402300799999));
}
