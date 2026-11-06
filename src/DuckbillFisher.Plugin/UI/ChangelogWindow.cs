using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Bundled release notes; read status is global and saved through the existing settings queue.</summary>
internal sealed class ChangelogWindow : Window
{
    private static readonly Version CurrentVersion = typeof(Plugin).Assembly.GetName().Version!;
    // Public release history plus the release candidate being prepared, newest first.
    // Keep the confirmed candidate unchanged when it is published.
    private static readonly ReleaseNotes[] PublishedReleases =
    [
        new(new(0, 9, 0, 0),
        [
            (FontAwesomeIcon.Ship, T.ChangelogOceanBeta),
            (FontAwesomeIcon.Filter, T.ChangelogOceanCandidates),
            (FontAwesomeIcon.Fish, T.ChangelogMinimumGathering),
            (FontAwesomeIcon.List, T.ChangelogDeliveryTiers),
            (FontAwesomeIcon.Fish, T.ChangelogExpandedHookYield),
            (FontAwesomeIcon.Cog, T.ChangelogThemes),
            (FontAwesomeIcon.BookOpen, T.ChangelogFishingLogOrder),
        ], new DateTimeOffset(2026, 11, 6, 12, 28, 36, TimeSpan.Zero)),
        new(new(0, 8, 0, 0),
        [
            (FontAwesomeIcon.Fish, T.ChangelogIntuitionProgress),
            (FontAwesomeIcon.PlusCircle, T.ChangelogSpawnAttemptsAuto),
            (FontAwesomeIcon.Filter, T.ChangelogUnusedBaits),
            (FontAwesomeIcon.BookOpen, T.ChangelogFishLookup),
            (FontAwesomeIcon.List, T.ChangelogHookYieldGuide),
            (FontAwesomeIcon.History, T.ChangelogMultipleCatchQuantity),
        ], new DateTimeOffset(2026, 11, 5, 12, 37, 34, TimeSpan.Zero)),
        new(new(0, 7, 0, 0),
        [
            (FontAwesomeIcon.SyncAlt, T.ChangelogDalamudUpdates),
            (FontAwesomeIcon.Language, T.ChangelogFirstRunLanguage),
            (FontAwesomeIcon.Clock, T.ChangelogLocalReleaseTime),
        ], new DateTimeOffset(2026, 11, 4, 14, 45, 0, TimeSpan.Zero),
            T.ChangelogGlobalFeatureTitle, T.ChangelogGlobalFeatureSummary, FeatureIcon: FontAwesomeIcon.Globe),
        new(new(0, 6, 0, 1), [(FontAwesomeIcon.Wrench, T.ChangelogDal4kr15039)], new DateTimeOffset(2026, 10, 31, 13, 40, 0, TimeSpan.Zero)),
        new(new(0, 6, 0, 0),
        [
            (FontAwesomeIcon.Compass, T.ChangelogRecommendationNow),
            (FontAwesomeIcon.Fish, T.ChangelogRecommendationPreparation),
            (FontAwesomeIcon.Star, T.ChangelogRecommendationRare),
            (FontAwesomeIcon.SyncAlt, T.ChangelogRecommendationRefresh),
        ], new DateTimeOffset(2026, 10, 26, 5, 34, 15, TimeSpan.Zero),
            FeatureTitle: T.ChangelogRecommendationFeatureTitle, FeatureSummary: T.ChangelogRecommendations, OtherEntries:
        [
            (FontAwesomeIcon.Bell, T.ChangelogCurrentSpotAlerts),
            (FontAwesomeIcon.Clock, T.ChangelogZeroAlerts),
            (FontAwesomeIcon.Link, T.ChangelogSpawnDetailLinks),
            (FontAwesomeIcon.BookOpen, T.ChangelogCollectionRefreshPrompt),
        ], FeatureIcon: FontAwesomeIcon.Compass),
        new(new(0, 5, 4, 0),
        [
            (FontAwesomeIcon.Edit, T.ChangelogFishNotes),
            (FontAwesomeIcon.PlusCircle, T.ChangelogFishAttempts),
            (FontAwesomeIcon.Link, T.ChangelogFishPresetLinks),
            (FontAwesomeIcon.FileArchive, T.ChangelogPortableBackups),
            (FontAwesomeIcon.Music, T.ChangelogManagedWav),
        ], new DateTimeOffset(2026, 10, 24, 9, 14, 50, TimeSpan.Zero),
            T.ChangelogPersonalFeatureTitle, T.ChangelogPersonalFeatureSummary,
        [
            (FontAwesomeIcon.Bell, T.ChangelogSpawnSettingsTab),
            (FontAwesomeIcon.Comments, T.ChangelogCastChannel),
            (FontAwesomeIcon.Save, T.ChangelogPresetConfirmation),
            (FontAwesomeIcon.Cog, T.ChangelogSettingsPolish),
        ], FontAwesomeIcon.Fish),
        new(new(0, 5, 3, 0),
        [
            (FontAwesomeIcon.VolumeUp, T.ChangelogFishingSounds),
            (FontAwesomeIcon.Trophy, T.ChangelogFirstCatchSounds),
            (FontAwesomeIcon.Filter, T.ChangelogSpotFilterRetention),
            (FontAwesomeIcon.Cog, T.ChangelogTimerSettingsTabs),
        ], new DateTimeOffset(2026, 10, 23, 9, 41, 18, TimeSpan.Zero)),
        new(new(0, 5, 2, 0),
        [
            (FontAwesomeIcon.Bell, T.ChangelogCastAlerts),
            (FontAwesomeIcon.List, T.ChangelogTimerPresets),
            (FontAwesomeIcon.Stopwatch, T.ChangelogLureElapsed),
            (FontAwesomeIcon.Save, T.ChangelogCollectionBackup),
            (FontAwesomeIcon.Fish, T.ChangelogTimerShortcuts),
            (FontAwesomeIcon.Wrench, T.ChangelogKr15039),
        ], new DateTimeOffset(2026, 10, 23, 9, 0, 25, TimeSpan.Zero)),
        new(new(0, 5, 1, 0),
        [
            (FontAwesomeIcon.Clock, T.ChangelogFishingBuffTimers),
            (FontAwesomeIcon.Filter, T.ChangelogRemainingTimeFilter),
            (FontAwesomeIcon.Filter, T.ChangelogUpcomingTimeRange),
            (FontAwesomeIcon.Eraser, T.ChangelogEmptyTimeFilters),
        ], new DateTimeOffset(2026, 10, 21, 16, 22, 12, TimeSpan.Zero)),
        new(new(0, 5, 0, 0),
        [
            (FontAwesomeIcon.Filter, T.ChangelogScheduleTargets),
            (FontAwesomeIcon.Clock, T.ChangelogScheduleTimeline),
            (FontAwesomeIcon.CalendarAlt, T.ChangelogScheduleCalendar),
            (FontAwesomeIcon.MapMarkerAlt, T.ChangelogScheduleDetails),
        ], new DateTimeOffset(2026, 10, 20, 19, 1, 36, TimeSpan.Zero),
            T.ChangelogScheduleFeatureTitle, T.ChangelogScheduleFeatureSummary, FeatureIcon: FontAwesomeIcon.CalendarAlt),
    ];
    private readonly ReleaseNotes[] releases = PublishedReleases.Where(release => release.Version <= CurrentVersion).Take(10).ToArray();
    private readonly FishPreferences preferences;
    private bool contentShown;
    private bool acknowledged;

    public ChangelogWindow(FishPreferences preferences) : base("DuckbillFisher###DuckbillFisher.Changelog")
    {
        this.preferences = preferences;
        IsOpen = false;
        Size = new Vector2(560, 540);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 300), MaximumSize = new Vector2(float.MaxValue),
        };
        BgAlpha = UiWidgets.BackgroundAlpha;
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public void ShowIfUnread()
    {
        var seen = preferences.View.LastSeenChangelogVersion;
        if (!acknowledged && (!Version.TryParse(seen, out var version) || version < CurrentVersion)) IsOpen = true;
    }

    public override void PreDraw()
    {
        UiText.SetLanguage(preferences.View.Language);
        WindowName = $"{UiText.Get(T.ChangelogTitle)}###DuckbillFisher.Changelog";
    }

    public override void OnOpen() => contentShown = false;

    public override void OnClose()
    {
        // Hidden/collapsed windows and unloading alone do not acknowledge unread notes.
        if (contentShown)
        {
            acknowledged = true;
            preferences.MarkChangelogSeen(CurrentVersion);
        }
    }

    public override void Draw()
    {
        if (ImGui.BeginChild("ChangelogContent", new Vector2(0, -ImGui.GetFrameHeightWithSpacing())))
        {
            contentShown = true;
            UiWidgets.TextColored(UiThemeScope.Current?.Text ?? new Vector4(1, 0.8f, 0.32f, 1), UiText.Get(T.ChangelogVersion, CurrentVersion));
            ImGui.Separator();
            ImGui.TextWrapped(UiText.Get(T.ChangelogPublishedHint, releases.Length));
            ImGui.Spacing();
            for (var i = 0; i < releases.Length; i++)
            {
                var release = releases[i];
                var label = $"{UiText.Get(T.ChangelogRelease, release.Version)}###ReleaseNotes.{release.Version}";
                if (!UiWidgets.Disclosure(label, i == 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None)) continue;
                if (release.PublishedAtUtc is { } publishedAt)
                    ImGui.TextDisabled(UiText.Get(T.ChangelogPublishedAt, publishedAt.ToLocalTime()));
                ImGui.Spacing();
                if (release.FeatureTitle is { } title)
                {
                    UiWidgets.Icon(release.FeatureIcon);
                    ImGui.SameLine();
                    UiWidgets.TextColored(UiThemeScope.Current is not null ? UiThemeColors.Information : new Vector4(0.45f, 0.82f, 1, 1), UiText.Get(title));
                    if (release.FeatureSummary is { } summary) ImGui.TextWrapped(UiText.Get(summary));
                    ImGui.Separator();
                    ImGui.Spacing();
                }
                foreach (var (icon, text) in release.Entries) Entry(icon, text);
                if (release.OtherEntries is { Length: > 0 } other)
                {
                    UiWidgets.Heading(T.ChangelogOtherImprovements);
                    foreach (var (icon, text) in other) Entry(icon, text);
                }
            }
            ImGui.Spacing();
            ImGui.TextWrapped(UiText.Get(T.ChangelogReopenHint));
            if (!preferences.View.CanEdit || preferences.View.Error?.Key == T.PreferencesSaveFailed)
                ImGui.TextWrapped(UiText.Get(T.ChangelogUnsaved));
        }
        ImGui.EndChild();
        if (ImGui.Button(UiText.Label(T.ChangelogDismiss, "DismissChangelog"))) IsOpen = false;
    }

    private static void Entry(FontAwesomeIcon icon, T key)
    {
        UiWidgets.Icon(icon);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX());
        ImGui.TextUnformatted(UiText.Get(key));
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        ImGui.Spacing();
    }

    private sealed record ReleaseNotes(Version Version, (FontAwesomeIcon Icon, T Text)[] Entries, DateTimeOffset? PublishedAtUtc = null,
        T? FeatureTitle = null, T? FeatureSummary = null, (FontAwesomeIcon Icon, T Text)[]? OtherEntries = null,
        FontAwesomeIcon FeatureIcon = FontAwesomeIcon.ChartBar);
}
