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
        new(new(0, 6, 0, 0),
        [
            (FontAwesomeIcon.Compass, T.ChangelogRecommendations),
            (FontAwesomeIcon.Sync, T.ChangelogRecommendationRefresh),
            (FontAwesomeIcon.Bell, T.ChangelogCurrentSpotAlerts),
            (FontAwesomeIcon.Clock, T.ChangelogZeroAlerts),
        ]),
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
        new(new(0, 4, 1, 0),
        [
            (FontAwesomeIcon.MapMarkerAlt, T.ChangelogHistoryLocation),
            (FontAwesomeIcon.History, T.ChangelogHistoryPresentation),
            (FontAwesomeIcon.Link, T.ChangelogMoochLink),
            (FontAwesomeIcon.Clock, T.ChangelogTimerRetention),
        ], new DateTimeOffset(2026, 10, 20, 11, 50, 52, TimeSpan.Zero),
            OtherEntries: [(FontAwesomeIcon.ChartBar, T.ChangelogProgressPalette)]),
        new(new(0, 4, 0, 0),
        [
            (FontAwesomeIcon.ChartBar, T.ChangelogCollectionProgress),
            (FontAwesomeIcon.Filter, T.ChangelogProgressFilters),
            (FontAwesomeIcon.ChartBar, T.ChangelogProgressBars),
            (FontAwesomeIcon.Fish, T.ChangelogProgressList),
            (FontAwesomeIcon.Check, T.ChangelogProgressCollection),
        ], new DateTimeOffset(2026, 10, 20, 8, 43, 34, TimeSpan.Zero),
            T.ChangelogProgressFeatureTitle, T.ChangelogProgressFeatureSummary,
        [
            (FontAwesomeIcon.List, T.ChangelogCompactToggle),
            (FontAwesomeIcon.Star, T.ChangelogNavigationIcons),
        ]),
        new(new(0, 3, 11, 0),
        [
            (FontAwesomeIcon.SearchPlus, T.ChangelogUiScale),
            (FontAwesomeIcon.Star, T.ChangelogCatalogOptions),
            (FontAwesomeIcon.ExternalLinkAlt, T.ChangelogSeparateDetail),
            (FontAwesomeIcon.Fish, T.ChangelogCollectionBadges),
            (FontAwesomeIcon.MapMarkerAlt, T.ChangelogUnknownSpot),
            (FontAwesomeIcon.List, T.ChangelogSpotToggle),
        ], new DateTimeOffset(2026, 10, 20, 7, 59, 10, TimeSpan.Zero)),
        new(new(0, 3, 10, 0),
        [
            (FontAwesomeIcon.Terminal, T.ChangelogShortCommands),
            (FontAwesomeIcon.Fish, T.ChangelogSpotFish),
        ], new DateTimeOffset(2026, 10, 19, 8, 5, 59, TimeSpan.Zero)),
        new(new(0, 3, 9, 0), [(FontAwesomeIcon.Newspaper, T.ChangelogUpdateNotes)], new DateTimeOffset(2026, 10, 18, 20, 7, 42, TimeSpan.Zero)),
        new(new(0, 3, 8, 0),
        [
            (FontAwesomeIcon.Music, T.ChangelogAudio),
            (FontAwesomeIcon.Clock, T.ChangelogForecast),
            (FontAwesomeIcon.Fish, T.ChangelogIntuition),
            (FontAwesomeIcon.Search, T.ChangelogDetails),
            (FontAwesomeIcon.Wrench, T.ChangelogStability),
        ], new DateTimeOffset(2026, 10, 18, 19, 20, 50, TimeSpan.Zero)),
        new(new(0, 3, 0, 0),
        [
            (FontAwesomeIcon.Fish, T.ChangelogBaitPreparation),
            (FontAwesomeIcon.History, T.ChangelogRecordRecovery),
            (FontAwesomeIcon.Search, T.ChangelogDataExpansion),
        ], new DateTimeOffset(2026, 10, 17, 20, 32, 15, TimeSpan.Zero)),
        new(new(0, 2, 6, 0), [(FontAwesomeIcon.Wrench, T.ChangelogDal4kr)], new DateTimeOffset(2026, 10, 17, 11, 48, 17, TimeSpan.Zero)),
        new(new(0, 2, 5, 0),
        [
            (FontAwesomeIcon.Wrench, T.ChangelogKrRuntime),
            (FontAwesomeIcon.Search, T.ChangelogCompatibilityHint),
        ], new DateTimeOffset(2026, 10, 17, 11, 19, 46, TimeSpan.Zero)),
        new(new(0, 2, 4, 0), [(FontAwesomeIcon.Fish, T.ChangelogFirstRelease)], new DateTimeOffset(2026, 10, 17, 10, 52, 8, TimeSpan.Zero)),
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
            ImGui.TextColored(new Vector4(1, 0.8f, 0.32f, 1), UiText.Get(T.ChangelogVersion, CurrentVersion));
            ImGui.Separator();
            ImGui.TextWrapped(UiText.Get(T.ChangelogPublishedHint, releases.Length));
            ImGui.Spacing();
            for (var i = 0; i < releases.Length; i++)
            {
                var release = releases[i];
                var label = $"{UiText.Get(T.ChangelogRelease, release.Version)}###ReleaseNotes.{release.Version}";
                if (!ImGui.CollapsingHeader(label, i == 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None)) continue;
                if (release.PublishedAtUtc is { } publishedAt)
                    ImGui.TextDisabled(UiText.Get(T.ChangelogPublishedAt, publishedAt.ToOffset(TimeSpan.FromHours(9))));
                ImGui.Spacing();
                if (release.FeatureTitle is { } title)
                {
                    UiWidgets.Icon(release.FeatureIcon);
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.45f, 0.82f, 1, 1), UiText.Get(title));
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
