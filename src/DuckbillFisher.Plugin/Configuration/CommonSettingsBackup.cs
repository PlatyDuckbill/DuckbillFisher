using Dalamud.Game.Text;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

// Only portable settings belong here. Character IDs, collection, history and delivery state never do.
// Baseline fields are required. Future additions must be optional with their original defaults,
// so format-v1 backups remain readable; retain the older schema migrations.
internal sealed class CommonSettingsBackup
{
    public required HashSet<uint> Favorites { get; set; } = [];
    public required bool SpawnAlertsEnabled { get; set; }
    public required int SpawnAlertLeadMinutes { get; set; } = SpawnAlertScheduler.DefaultLeadMinutes;
    public required ushort SpawnAlertChannelId { get; set; } = (ushort)XivChatType.SystemMessage;
    public required int SpawnAlertSound { get; set; }
    public required string CustomAlertSoundPath { get; set; } = "";
    public required SpawnAlertAudience SpawnAlertAudience { get; set; } = SpawnAlertAudience.AllFavorites;
    public required bool RemoveFavoriteOnRegistration { get; set; } = true;
    public required CatalogSettings Catalog { get; set; } = new();
    public required List<CatalogPreset> CatalogPresets { get; set; } = [];
    public required UiLanguage Language { get; set; } = UiLanguage.Korean;
    public required int UiScalePercent { get; set; } = UiScale.DefaultPercent;
    public required bool DeveloperMode { get; set; }
    public required CompactViewSettings Compact { get; set; } = new();
    public required SpawnScheduleSettings Schedule { get; set; } = new();
    public required CastingTimerSettings Timer { get; set; } = new();
    public required bool SpotFishHideCaught { get; set; } = true;
    public required Dictionary<uint, int> FishAlertLeadMinutes { get; set; } = [];
    // Optional since settings schema23; format-v1/schema21~22 backups have no personal records.
    public Dictionary<uint, FishPersonalRecord> PersonalRecords { get; set; } = [];

    public static CommonSettingsBackup From(FishPreferencesConfiguration value) => new()
    {
        Favorites = [.. value.Favorites], SpawnAlertsEnabled = value.SpawnAlertsEnabled,
        SpawnAlertLeadMinutes = value.SpawnAlertLeadMinutes, SpawnAlertChannelId = value.SpawnAlertChannelId,
        SpawnAlertSound = value.SpawnAlertSound, CustomAlertSoundPath = value.CustomAlertSoundPath,
        SpawnAlertAudience = value.SpawnAlertAudience, RemoveFavoriteOnRegistration = value.RemoveFavoriteOnRegistration,
        Catalog = value.Catalog.Copy(), CatalogPresets = value.CatalogPresets.Select(preset => preset.Copy()).ToList(),
        Language = value.Language, UiScalePercent = value.UiScalePercent, DeveloperMode = value.DeveloperMode,
        Compact = value.Compact, Schedule = value.Schedule, Timer = value.Timer.Copy(),
        SpotFishHideCaught = value.SpotFishHideCaught, FishAlertLeadMinutes = new(value.FishAlertLeadMinutes),
        PersonalRecords = new(value.PersonalRecords),
    };

    public void Apply(FishPreferencesConfiguration target)
    {
        target.Favorites = [.. Favorites];
        target.SpawnAlertsEnabled = SpawnAlertsEnabled;
        target.SpawnAlertLeadMinutes = SpawnAlertLeadMinutes;
        target.SpawnAlertChannelId = SpawnAlertChannelId;
        target.SpawnAlertSound = SpawnAlertSound;
        target.CustomAlertSoundPath = CustomAlertSoundPath;
        target.SpawnAlertAudience = SpawnAlertAudience;
        target.RemoveFavoriteOnRegistration = RemoveFavoriteOnRegistration;
        target.Catalog = Catalog.Copy();
        target.CatalogPresets = CatalogPresets.Select(preset => preset.Copy()).ToList();
        target.Language = Language;
        target.UiScalePercent = UiScalePercent;
        target.DeveloperMode = DeveloperMode;
        target.Compact = Compact;
        target.Schedule = Schedule;
        target.Timer = Timer.Copy();
        target.SpotFishHideCaught = SpotFishHideCaught;
        target.FishAlertLeadMinutes = new(FishAlertLeadMinutes);
        target.PersonalRecords = new(PersonalRecords);
    }

    public bool IsValid(int version)
    {
        if (!FishPersonalRecord.ValidEntries(PersonalRecords) || Favorites is null || Catalog?.IsValid() != true || Compact?.IsValid() != true ||
            Schedule?.IsValid() != true || Timer?.IsValid() != true || FishAlertLeadMinutes is null ||
            CatalogPresets is null || CatalogPresets.Count > CatalogPreset.MaxCount ||
            CatalogPresets.Any(preset => preset is null || preset.Id == Guid.Empty ||
                !CatalogPreset.ValidName(preset.Name) || preset.Settings?.IsValid() != true) ||
            CatalogPresets.Select(preset => preset.Id).Distinct().Count() != CatalogPresets.Count ||
            CatalogPresets.Select(preset => preset.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != CatalogPresets.Count ||
            CustomAlertSoundPath is null || CustomAlertSoundPath.Length > 4096 || CustomAlertSoundPath.Any(char.IsControl) ||
            !UiScale.Choices.Contains(UiScalePercent)) return false;
        var candidate = new FishPreferencesConfiguration { Version = version };
        Apply(candidate);
        return PreferencesValidation.IsValid(candidate);
    }

    public CommonSettingsBackup Migrate(int version)
    {
        var candidate = new FishPreferencesConfiguration { Version = version };
        Apply(candidate);
        PreferencesNormalization.Normalize(candidate);
        return From(candidate);
    }
}
