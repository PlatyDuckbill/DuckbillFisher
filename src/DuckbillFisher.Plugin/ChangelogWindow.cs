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
        new(new(0, 3, 11, 0),
        [
            (FontAwesomeIcon.SearchPlus, T.ChangelogUiScale),
            (FontAwesomeIcon.Star, T.ChangelogCatalogOptions),
        ]),
        new(new(0, 3, 10, 0),
        [
            (FontAwesomeIcon.Terminal, T.ChangelogShortCommands),
            (FontAwesomeIcon.Fish, T.ChangelogSpotFish),
        ]),
        new(new(0, 3, 9, 0), [(FontAwesomeIcon.Newspaper, T.ChangelogUpdateNotes)]),
        new(new(0, 3, 8, 0),
        [
            (FontAwesomeIcon.Music, T.ChangelogAudio),
            (FontAwesomeIcon.Clock, T.ChangelogForecast),
            (FontAwesomeIcon.Fish, T.ChangelogIntuition),
            (FontAwesomeIcon.Search, T.ChangelogDetails),
            (FontAwesomeIcon.Wrench, T.ChangelogStability),
        ]),
        new(new(0, 3, 0, 0),
        [
            (FontAwesomeIcon.Fish, T.ChangelogBaitPreparation),
            (FontAwesomeIcon.History, T.ChangelogRecordRecovery),
            (FontAwesomeIcon.Search, T.ChangelogDataExpansion),
        ]),
        new(new(0, 2, 6, 0), [(FontAwesomeIcon.Wrench, T.ChangelogDal4kr)]),
        new(new(0, 2, 5, 0),
        [
            (FontAwesomeIcon.Wrench, T.ChangelogKrRuntime),
            (FontAwesomeIcon.Search, T.ChangelogCompatibilityHint),
        ]),
        new(new(0, 2, 4, 0), [(FontAwesomeIcon.Fish, T.ChangelogFirstRelease)]),
    ];
    private readonly ReleaseNotes[] releases = PublishedReleases.Where(release => release.Version <= CurrentVersion).Take(5).ToArray();
    private readonly FishPreferences preferences;
    private bool contentShown;

    public ChangelogWindow(FishPreferences preferences) : base("DuckbillFisher###DuckbillFisher.Changelog")
    {
        this.preferences = preferences;
        var seen = preferences.View.LastSeenChangelogVersion;
        IsOpen = !Version.TryParse(seen, out var version) || version < CurrentVersion;
        Size = new Vector2(560, 540);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 300), MaximumSize = new Vector2(float.MaxValue),
        };
        BgAlpha = UiWidgets.BackgroundAlpha;
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
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
        if (contentShown) preferences.MarkChangelogSeen(CurrentVersion);
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
                ImGui.Spacing();
                foreach (var (icon, text) in release.Entries) Entry(icon, text);
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

    private sealed record ReleaseNotes(Version Version, (FontAwesomeIcon Icon, T Text)[] Entries);
}
