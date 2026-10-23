using Dalamud.Configuration;
using Dalamud.Game.Text;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

// Schema <= 3 serialized this three-value enum. Retained only for migration.
public enum SpawnAlertChannel { System, Say, CrossWorldLinkshell1 }

// Public for Dalamud's configuration serializer. Content IDs stay in local configuration only.
public sealed class FishPreferencesConfiguration : IPluginConfiguration
{
    public const int CurrentVersion = PreferencesMigration.CurrentVersion;
    public int Version { get; set; } = CurrentVersion;
    public HashSet<uint> Favorites { get; set; } = [];
    public Dictionary<ulong, Dictionary<uint, bool>> Characters { get; set; } = [];
    public Dictionary<ulong, FishCollectionBackup> CollectionBackups { get; set; } = [];
    public bool SpawnAlertsEnabled { get; set; }
    public int SpawnAlertLeadMinutes { get; set; } = SpawnAlertScheduler.DefaultLeadMinutes;
    public SpawnAlertChannel SpawnAlertChannel { get; set; }
    public ushort SpawnAlertChannelId { get; set; } = (ushort)XivChatType.SystemMessage;
    public int SpawnAlertSound { get; set; }
    public string CustomAlertSoundPath { get; set; } = "";
    public SpawnAlertAudience SpawnAlertAudience { get; set; } = SpawnAlertAudience.AllFavorites;
    public bool RemoveFavoriteOnRegistration { get; set; } = true;
    public CatalogSettings Catalog { get; set; } = new();
    public List<CatalogPreset> CatalogPresets { get; set; } = [];
    public UiLanguage Language { get; set; } = UiLanguage.Korean;
    public int UiScalePercent { get; set; } = UiScale.DefaultPercent;
    public bool DeveloperMode { get; set; }
    public string LastSeenChangelogVersion { get; set; } = "";
    public CompactViewSettings Compact { get; set; } = new();
    public SpawnScheduleSettings Schedule { get; set; } = new();
    public CastingTimerSettings Timer { get; set; } = new();
    public Dictionary<uint, int> FishAlertLeadMinutes { get; set; } = [];
    // Last reserved window start per fish/character. Also suppresses repeats after clock rollback.
    public Dictionary<ulong, Dictionary<uint, long>> AlertWindows { get; set; } = [];
}
