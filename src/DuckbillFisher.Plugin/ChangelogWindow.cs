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
            Entry(FontAwesomeIcon.Newspaper, T.ChangelogNotes);
            UiWidgets.Heading(T.ChangelogRecent);
            Entry(FontAwesomeIcon.Music, T.ChangelogAudio);
            Entry(FontAwesomeIcon.Clock, T.ChangelogForecast);
            Entry(FontAwesomeIcon.Fish, T.ChangelogIntuition);
            Entry(FontAwesomeIcon.Search, T.ChangelogDetails);
            Entry(FontAwesomeIcon.Wrench, T.ChangelogStability);
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
}
