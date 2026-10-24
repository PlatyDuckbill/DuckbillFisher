namespace DuckbillFisher.Plugin;

internal static class PreferencesNormalization
{
    // Shared by normal configuration loading and portable settings imports.
    public static void Normalize(FishPreferencesConfiguration loaded)
    {
        var version = loaded.Version;
        if (version <= 3)
            loaded.SpawnAlertChannelId = (ushort)(loaded.SpawnAlertChannel switch
            {
                SpawnAlertChannel.Say => Dalamud.Game.Text.XivChatType.Say,
                SpawnAlertChannel.CrossWorldLinkshell1 => Dalamud.Game.Text.XivChatType.CrossLinkShell1,
                _ => Dalamud.Game.Text.XivChatType.SystemMessage,
            });
        if (!ChatChannelChoices.Supports((Dalamud.Game.Text.XivChatType)loaded.SpawnAlertChannelId))
            loaded.SpawnAlertChannelId = (ushort)Dalamud.Game.Text.XivChatType.SystemMessage;
        if (version < 8) loaded.RemoveFavoriteOnRegistration = true;
        loaded.Compact = PreferencesMigration.Compact(loaded.Compact, loaded.Catalog, version);
        loaded.Catalog = PreferencesMigration.Catalog(loaded.Catalog, version);
        loaded.CatalogPresets = PreferencesMigration.Presets(loaded.CatalogPresets, version);
        loaded.CustomAlertSoundPath ??= "";
        loaded.UiScalePercent = UiScale.Normalize(loaded.UiScalePercent);
        loaded.Schedule = PreferencesMigration.Schedule(loaded.Schedule, version);
        loaded.LastSeenChangelogVersion = PreferencesMigration.SeenVersion(loaded.LastSeenChangelogVersion);
        loaded.Timer = loaded.Timer is not null && loaded.Timer.IsValid() ? loaded.Timer.Copy() : new();
        // Older configurations and format-v1 backups shared the spawn channel with cast alerts.
        // Copy it once; later changes to either channel remain independent.
        if (version < 22) loaded.Timer = loaded.Timer with { AlertChannelId = loaded.SpawnAlertChannelId };
        else if (!ChatChannelChoices.Supports((Dalamud.Game.Text.XivChatType)loaded.Timer.AlertChannelId))
            loaded.Timer = loaded.Timer with { AlertChannelId = (ushort)Dalamud.Game.Text.XivChatType.SystemMessage };
        loaded.CollectionBackups = (loaded.CollectionBackups ?? []).Where(pair => pair.Key != 0 && pair.Value is not null && pair.Value.IsValid())
            .ToDictionary(pair => pair.Key, pair => pair.Value.Copy());
        loaded.PersonalRecords = new(loaded.PersonalRecords ?? []);
        FishPersonalRecord.RemoveMissingPresets(loaded.PersonalRecords, loaded.Timer);
        loaded.Version = PreferencesMigration.CurrentVersion;
    }
}
