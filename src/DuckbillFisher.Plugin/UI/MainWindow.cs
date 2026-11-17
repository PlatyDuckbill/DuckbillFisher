using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class MainWindow : Window
{
    private readonly ThemeAdjustmentEditor appearance = new();
    private static readonly T[] Markers = [T.MarkCast, T.MarkWait, T.MarkBite, T.MarkHook, T.MarkRetrieve, T.MarkEnd, T.MarkMooch, T.MarkMove, T.MarkLogout];
    private static readonly T[] NavigationLabels = [T.TabCatalog, T.TabAlerts, T.RecommendationTitle, T.ScheduleTitle,
        T.OceanBetaTitle, T.TabCollectionProgress, T.TabHistory, T.TabSettings, T.TabGuide, T.TabFishing];
    private static readonly string[] LanguageNames = [UiText.Get(T.Korean), UiText.Get(T.English)];
    private readonly FishingActivityProbe observer;
    private readonly FishGuideDiagnostics fishGuide;
    private readonly FishListPanel fishList;
    private readonly SpawnAlertsPanel spawnAlerts;
    private readonly Action languageChanged;
    private readonly FishPreferences preferences;
    private readonly FishingHistoryBrowser fishingHistory;
    private readonly CollectionProgressPanel collectionProgress;
    private readonly SpawnSchedulePanel schedule;
    private readonly FishRecommendationPanel recommendations;
    private readonly OceanFishingPanel ocean;
    private readonly SettingsBackupPanel backup;
    private readonly FishCollectionObserver collection;
    private readonly FishGuideOpenService guideOpener;
    private readonly NativeFishingMovementGuard movementGuard;
    public Action? ShowCompact { get; set; }
    public Action? ShowTimer { get; set; }
    public Action? ShowChangelog { get; set; }
    public Func<bool>? HasUnreadChangelog { get; set; }
    public Action? ShowUnreadChangelog { get; set; }
    private string[] markerLabels = Markers.Select(key => UiText.Get(key)).ToArray();
    private IReadOnlyList<FishGuideRow>? lastRows;
    private FishGuideRow[] matches = [];
    private FishGuideRow? selectedFish;
    private string search = "";
    private int lastSession = -1;
    private int selectedMarker;
    private bool selectSettings;
    private MainPage selectedPage;
    private enum MainPage { Catalog, Recommendations, Collection, Schedule, Ocean, Alerts, History, Settings, Guide, Fishing }

    public MainWindow(FishingActivityProbe observer, FishGuideDiagnostics fishGuide, FishListPanel fishList,
        SpawnAlertsPanel spawnAlerts, FishPreferences preferences, Action languageChanged,
        FishingHistoryBrowser fishingHistory, CollectionProgressPanel collectionProgress, SpawnSchedulePanel schedule,
        SettingsBackupPanel backup, FishRecommendationPanel recommendations, FishCollectionObserver collection,
        FishGuideOpenService guideOpener, OceanFishingPanel ocean, NativeFishingMovementGuard movementGuard) : base("DuckbillFisher###DuckbillFisher.Main")
    {
        this.observer = observer;
        this.fishGuide = fishGuide;
        this.fishList = fishList;
        this.spawnAlerts = spawnAlerts;
        this.languageChanged = languageChanged;
        this.preferences = preferences;
        this.fishingHistory = fishingHistory;
        this.collectionProgress = collectionProgress;
        this.schedule = schedule;
        this.recommendations = recommendations;
        this.backup = backup;
        this.collection = collection;
        this.guideOpener = guideOpener;
        this.ocean = ocean;
        this.movementGuard = movementGuard;
        UiText.SetLanguage(preferences.View.Language);
        markerLabels = Markers.Select(key => UiText.Get(key)).ToArray();
        languageChanged();
        Size = new Vector2(980, 680);
        SizeCondition = ImGuiCond.FirstUseEver;
        BgAlpha = UiWidgets.BackgroundAlpha;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(700, 450), MaximumSize = new Vector2(float.MaxValue) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public void OpenSettings()
    {
        selectSettings = true;
        IsOpen = true;
    }

    public override void OnOpen() => ShowUnreadChangelog?.Invoke();
    public override void OnClose() { schedule.SetActive(false); recommendations.SetActive(false); ocean.SetActive(false); backup.CloseDialogs(); }

    public override void Draw()
    {
        using var contentStyle = GameThemeChrome.Content();
        GameThemeChrome.WindowSurface();
        var settings = preferences.View;
        if (UiText.Language != settings.Language)
        {
            UiText.SetLanguage(settings.Language);
            markerLabels = Markers.Select(key => UiText.Get(key)).ToArray();
            languageChanged();
        }
        fishList.DeveloperMode = settings.DeveloperMode;
        spawnAlerts.DeveloperMode = settings.DeveloperMode;
        DrawNavigation(settings.DeveloperMode);
        UiWidgets.GuideWarning(collection.View, settings, guideOpener);
        schedule.SetActive(selectedPage == MainPage.Schedule);
        recommendations.SetActive(selectedPage == MainPage.Recommendations);
        ocean.SetActive(selectedPage == MainPage.Ocean);
        ImGui.PushID(selectedPage.ToString());
        switch (selectedPage)
        {
            case MainPage.Catalog: fishList.Draw(); break;
            case MainPage.Recommendations: recommendations.Draw(); break;
            case MainPage.Collection: collectionProgress.Draw(); break;
            case MainPage.Schedule: schedule.Draw(); break;
            case MainPage.Ocean: ocean.Draw(); break;
            case MainPage.Alerts: spawnAlerts.Draw(); break;
            case MainPage.History: fishingHistory.Draw(settings.DeveloperMode, ShowTimer); break;
            case MainPage.Settings:
                if (ImGui.BeginChild("SettingsContent")) DrawSettings(settings);
                ImGui.EndChild();
                break;
            case MainPage.Guide:
                if (ImGui.BeginChild("GuideDiagnosticsContent")) DrawFishGuide();
                ImGui.EndChild();
                break;
            case MainPage.Fishing:
                if (ImGui.BeginChild("FishingDiagnosticsContent")) DrawFishingObservation();
                ImGui.EndChild();
                break;
        }
        ImGui.PopID();
        backup.DrawDialogs();
    }

    private void DrawNavigation(bool developerMode)
    {
        if (!developerMode && selectedPage is MainPage.Guide or MainPage.Fishing) selectedPage = MainPage.Catalog;
        var compact = UiText.Get(T.CompactWindowTitle);
        var timer = UiText.Get(T.CastingTimerTitle);
        var padding = ImGui.GetStyle().FramePadding;
        var iconWidth = ImGui.GetFrameHeight();
        var compactWidth = iconWidth + ImGui.CalcTextSize(compact).X + padding.X * 2;
        var timerWidth = iconWidth + ImGui.CalcTextSize(timer).X + padding.X * 2;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var available = ImGui.GetContentRegionAvail().X;
        var tabPadding = NavigationTabPadding();
        var labelsWidth = 0f;
        var labelCount = developerMode ? NavigationLabels.Length : NavigationLabels.Length - 2;
        for (var i = 0; i < labelCount; i++)
            labelsWidth += ImGui.CalcTextSize(tabPadding + UiText.Get(NavigationLabels[i])).X + padding.X * 2;
        labelsWidth += ImGui.GetStyle().ItemInnerSpacing.X * (labelCount - 1);
        var showShortcutLabels = labelsWidth + compactWidth + timerWidth + spacing * 2 <= available;
        if (!showShortcutLabels) compactWidth = timerWidth = iconWidth;
        var themed = UiThemeScope.Current is { };
        if (themed)
        {
            UiThemeDecorations.NavigationBand(ImGui.GetCursorScreenPos(), available, ImGui.GetFrameHeight());
            // Let the continuous band show through the native tab container; keep all tab geometry and IDs.
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Tab, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.TabActive, UiThemeDecorations.NavigationSelection);
            ImGui.PushStyleColor(ImGuiCol.TabHovered, UiThemeDecorations.NavigationHover);
            ImGui.PushStyleColor(ImGuiCol.TabUnfocused, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, UiThemeDecorations.NavigationSelection);
        }
        // The public tab bar uses its container width. Reserve the shortcut area in a header-only child.
        var tabWidth = Math.Max(1, available - compactWidth - timerWidth - spacing * 2);
        if (ImGui.BeginChild("MainNavigationTabs", new Vector2(tabWidth, ImGui.GetFrameHeightWithSpacing()), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            // A live ImGui context can retain the previous tab order across plugin reloads.
            // Use a stable ID for this menu order instead of reusing the old tab-bar state.
            if (ImGui.BeginTabBar("MainNavigation.OceanBeta", ImGuiTabBarFlags.FittingPolicyScroll))
            {
                NavigationTab(MainPage.Catalog, T.TabCatalog, "CatalogTab", FontAwesomeIcon.Fish);
                NavigationTab(MainPage.Alerts, T.TabAlerts, "SpawnAlertsTab", FontAwesomeIcon.Star);
                NavigationTab(MainPage.Recommendations, T.RecommendationTitle, "RecommendationsTab", FontAwesomeIcon.Compass);
                NavigationTab(MainPage.Schedule, T.ScheduleTitle, "SpawnScheduleTab", FontAwesomeIcon.CalendarAlt);
                NavigationTab(MainPage.Ocean, T.OceanBetaTitle, "OceanBetaTab", FontAwesomeIcon.Ship);
                NavigationTab(MainPage.Collection, T.TabCollectionProgress, "CollectionProgressTab", FontAwesomeIcon.ChartBar);
                NavigationTab(MainPage.History, T.TabHistory, "FishingHistoryTab", FontAwesomeIcon.History);
                NavigationTab(MainPage.Settings, T.TabSettings, "SettingsTab", FontAwesomeIcon.Cog, selectSettings);
                if (developerMode)
                {
                    NavigationTab(MainPage.Guide, T.TabGuide, "GuideTab", FontAwesomeIcon.BookOpen);
                    NavigationTab(MainPage.Fishing, T.TabFishing, "FishingTab", FontAwesomeIcon.Search);
                }
                ScrollNavigationTabs();
                ImGui.EndTabBar();
                selectSettings = false;
            }
        }
        ImGui.EndChild();
        if (themed) ImGui.PopStyleColor(6);
        ImGui.SameLine(0, spacing);
        if (Shortcut(FontAwesomeIcon.List, showShortcutLabels ? compact : "", "OpenCompactWindow", compactWidth)) ShowCompact?.Invoke();
        UiWidgets.Tooltip(UiText.Get(T.OpenCompactWindow));
        ImGui.SameLine();
        if (Shortcut(FontAwesomeIcon.Stopwatch, showShortcutLabels ? timer : "", "OpenTimer", timerWidth)) ShowTimer?.Invoke();
        UiWidgets.Tooltip(timer);
    }

    private static void ScrollNavigationTabs()
    {
        var io = ImGui.GetIO();
        var wheel = io.MouseWheelH != 0 ? io.MouseWheelH : io.MouseWheel;
        if (wheel == 0 || !float.IsFinite(wheel) || !ImGui.IsWindowHovered()) return;

        // Dalamud's ImGui 1.88 tab layout does not consume wheel input. Use the current
        // binding's tab-bar state only during this BeginTabBar/EndTabBar scope, after layout.
        var tabs = ImGui.GetCurrentContext().CurrentTabBar;
        if (tabs.IsNull || tabs.WantLayout || !ImGui.IsMouseHoveringRect(tabs.BarRect.Min, tabs.BarRect.Max)) return;
        var visibleWidth = tabs.ScrollingRectMaxX - tabs.ScrollingRectMinX;
        var totalWidth = tabs.WidthAllTabs;
        if (!float.IsFinite(visibleWidth) || !float.IsFinite(totalWidth) || visibleWidth <= 0 || totalWidth <= visibleWidth ||
            !float.IsFinite(tabs.ScrollingTarget)) return;
        var step = Math.Max(ImGui.GetFontSize() * 4, visibleWidth / 3);
        tabs.ScrollingTarget = Math.Clamp(tabs.ScrollingTarget - wheel * step, 0, totalWidth - visibleWidth);
        tabs.ScrollingTargetDistToVisibility = 0;
        // Native layout animates/clamps this target next frame. Selection IDs and shared IO stay intact.
    }

    private void NavigationTab(MainPage page, T label, string id, FontAwesomeIcon icon, bool select = false)
    {
        // Reserve icon space using the text font, then draw the glyph with the existing icon font.
        // The translated label, fixed tab ID, tab selection and native overflow scrolling remain intact.
        var fontSize = ImGui.GetFontSize();
        var padding = NavigationTabPadding();
        var theme = UiThemeScope.Current;
        var accent = theme is not null ? UiThemeDecorations.NavigationAccent : Vector4.Zero;
        if (theme is not null) ImGui.PushStyleColor(ImGuiCol.Text, selectedPage == page || select ? theme.Text : theme.Muted);
        var active = ImGui.BeginTabItem($"{padding}{UiText.Label(label, id)}", select ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None);
        if (theme is not null) ImGui.PopStyleColor();
        if (ImGui.IsItemVisible())
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var draw = ImGui.GetWindowDrawList();
            draw.PushClipRect(min, max, true);
            var iconSize = theme is not null ? fontSize * .88f : fontSize;
            draw.AddText(UiBuilder.IconFont, iconSize,
                min + new Vector2(ImGui.GetStyle().FramePadding.X + (fontSize - iconSize) / 2,
                    Math.Max(0, (max.Y - min.Y - iconSize) / 2)),
                theme is not null ? ImGui.GetColorU32(active ? accent : NavigationIconColor(page))
                    : ImGui.GetColorU32(active ? ImGuiCol.Text : ImGuiCol.TextDisabled), icon.ToIconString());
            if (active && theme is not null)
            {

                draw.AddLine(new(min.X + ImGui.GetStyle().FramePadding.X, max.Y - 1),
                    new(max.X - ImGui.GetStyle().FramePadding.X, max.Y - 1), ImGui.GetColorU32(accent), 2);
            }
            draw.PopClipRect();
        }
        if (active)
        {
            selectedPage = page;
            ImGui.EndTabItem();
        }
    }

    private static string NavigationTabPadding()
    {
        var gap = ImGui.GetFontSize() + ImGui.GetStyle().ItemInnerSpacing.X;
        return new string(' ', Math.Max(1, (int)MathF.Ceiling(gap / Math.Max(1, ImGui.CalcTextSize(" ").X))));
    }

    private static Vector4 NavigationIconColor(MainPage page)
    {
        var role = page switch
        {
            MainPage.Catalog or MainPage.Ocean or MainPage.Fishing => UiThemeColors.Location,
            MainPage.Alerts or MainPage.Recommendations => UiThemeColors.Preparation,
            MainPage.Schedule => UiThemeColors.Information,
            MainPage.Collection or MainPage.History or MainPage.Guide => UiThemeColors.Special,
            _ => UiThemeColors.Secondary,
        };
        return Vector4.Lerp(role, UiThemeColors.Secondary, .25f);
    }

    private static bool Shortcut(FontAwesomeIcon icon, string label, string id, float width) => UiWidgets.ActionButton(icon, label, id, width);

    public (int Brightness, int Depth, int Transparency) ResolveAppearance(FishPreferencesView settings) =>
        appearance.Resolve(settings.ThemeBrightness, settings.ThemeDepth, settings.ThemeTransparency, IsOpen && selectedPage == MainPage.Settings);

    private void DrawSettings(FishPreferencesView settings)
    {
        if (GameThemeChrome.SettingsHeader(UiText.Get(T.TabSettings),
            UiText.Get(HasUnreadChangelog?.Invoke() == true ? T.SettingsViewNewUpdates : T.SettingsViewUpdates))) ShowChangelog?.Invoke();
        var language = (int)settings.Language;
        var developer = settings.DeveloperMode;
        var changed = false;
        ImGui.BeginDisabled(!settings.CanEdit);
        GameThemeChrome.SectionLabel(UiText.Get(T.AppearanceStyle));
        if (GameThemeChrome.BeginSettingsFields(3))
        {
            GameThemeChrome.SettingsField(UiText.Get(T.ThemeSetting));
            if (GameThemeChrome.BeginCombo("###InterfaceTheme", UiText.Get(UiThemes.Name(settings.Theme))))
            {
                foreach (var theme in UiThemes.Choices)
                {
                    var selected = theme == UiThemeSettings.Normalize(settings.Theme);
                    if (ImGui.Selectable(UiText.Label(UiThemes.Name(theme), $"Theme.{theme}"), selected)) preferences.SetTheme(theme);
                    if (selected) ImGui.SetItemDefaultFocus();
                    UiWidgets.Tooltip(UiText.Get(UiThemes.Description(theme)));
                }
                ImGui.EndCombo();
            }
            UiWidgets.Tooltip(UiText.Get(T.ThemeSettingHint));
            GameThemeChrome.SettingsField(UiText.Get(T.Language));
            changed = GameThemeChrome.Combo("###Language", ref language, LanguageNames, LanguageNames.Length);
            GameThemeChrome.SettingsField(UiText.Get(T.UiScale));
            var scaleIndex = Array.IndexOf(UiScale.Choices, settings.UiScalePercent);
            var scaleWidth = Math.Min(ImGui.GetContentRegionAvail().X, ImGui.GetFontSize() * 20);
            ImGui.SetNextItemWidth(Math.Max(1, scaleWidth - ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X));
            if (GameThemeChrome.Combo("###UiScale", ref scaleIndex, UiScale.Labels, UiScale.Labels.Length))
                preferences.SetUiScale(UiScale.Choices[scaleIndex]);
            UiWidgets.Tooltip(UiText.Get(T.UiScaleHint));
            ImGui.SameLine();
            if (UiWidgets.IconButton(FontAwesomeIcon.Undo, "ResetUiScale", UiText.Get(T.UiScaleReset))) preferences.SetUiScale(UiScale.DefaultPercent);
            ImGui.EndTable();
        }
        if (appearance.Draw(UiThemeSettings.Normalize(settings.Theme) == UiTheme.Pearl, settings.ThemeBrightness, settings.ThemeDepth, settings.ThemeTransparency,
            UiText.Get(T.AppearanceBrightness), UiText.Get(T.AppearanceDepth), UiText.Get(T.AppearanceTransparency), UiText.Get(T.AppearanceHint),
            UiText.Get(T.AppearanceReset), UiText.Get(T.AppearanceDetails)) is { } adjustment)
            preferences.SetAppearance(adjustment.Brightness, adjustment.Depth, adjustment.Transparency);
        ImGui.Spacing();
        changed |= ImGui.Checkbox(UiText.Label(T.DeveloperMode, "DeveloperMode"), ref developer);
        UiWidgets.Tooltip(UiText.Get(T.DeveloperModeHint));
        ImGui.Spacing();
        GameThemeChrome.SectionLabel(UiText.Get(T.FishingConvenienceSettings));
        var preventMovement = settings.Timer.PreventMovementCancel;
        if (ImGui.Checkbox(UiText.Label(T.PreventMovementCancel, "PreventMovementCancel"), ref preventMovement))
            preferences.SetPreventMovementCancel(preventMovement, settings.Session, settings.SettingsRestoreRevision);
        UiWidgets.Tooltip(UiText.Get(T.PreventMovementCancelHint));
        if (preventMovement && movementGuard.State is not (FishingMovementGuardState.Disabled or FishingMovementGuardState.Waiting or FishingMovementGuardState.Ready))
            ImGui.TextWrapped(UiText.Get(T.MovementGuardUnavailable, movementGuard.State));
        ImGui.EndDisabled();
        if (changed) preferences.SetInterface((UiLanguage)language, developer);
        if (settings.Error is { } error) ImGui.TextWrapped(error.Display);
        ImGui.Spacing();
        backup.Draw(settings);
        ImGui.Spacing(); ImGui.Separator();
        if (FishingData.Failure is not null || UiWidgets.Disclosure(UiText.Label(T.DataSourceLabel, "SettingsSources"))) FishDetailPanel.DrawSources();
    }

    private void DrawFishGuide()
    {
        var view = fishGuide.View;
        if (!ReferenceEquals(lastRows, view.Rows) || lastSession != view.Session)
        {
            lastRows = view.Rows;
            lastSession = view.Session;
            selectedFish = null;
            UpdateMatches(view.Rows);
        }

        ImGui.TextWrapped(UiText.Get(T.GuideIntro));
        ImGui.TextWrapped(view.Status.Display);
        ImGui.Separator();
        object loadedValue = view.IsLoaded is bool value ? value : new UiMessage(T.NotQueried);
        ImGui.TextUnformatted(UiText.Get(T.LoginState, view.IsLoggedIn, loadedValue));
        ImGui.TextUnformatted(UiText.Get(T.GuideVisibility,
            new UiMessage(view.FishGuideVisible ? T.Opened : T.Closed), new UiMessage(view.FishingNoteVisible ? T.Opened : T.Closed)));
        ImGui.TextWrapped(UiText.Get(T.GuideCounters, view.OpenCount, view.RefreshCount, view.LastTrigger));
        var fallback = view.LoginFallback;
        if (ImGui.Checkbox(UiText.Label(T.LoginFallback, "LoginFallback"), ref fallback))
            fishGuide.SetLoginFallback(fallback);
        if (UiWidgets.Button(UiText.Label(T.ReloadFish, "ReloadFish")))
            fishGuide.RequestRefresh();
        ImGui.SameLine();
        if (UiWidgets.Button(UiText.Label(T.CopyGuide, "CopyGuide")))
            ImGui.SetClipboardText(fishGuide.CreateReport(selectedFish));
        ImGui.TextUnformatted(UiText.Get(T.GuideRows, view.Rows.Count, view.InLogCount, view.MaxInLogId));

        ImGui.SetNextItemWidth(300);
        if (ImGui.InputText(UiText.Label(T.FishSearch, "FishSearch"), ref search, 100))
            UpdateMatches(view.Rows);
        ImGui.TextUnformatted(UiText.Get(T.SearchHint));
        if (ImGui.BeginChild("FishMatches", new Vector2(0, 120), true))
        {
            foreach (var fish in matches)
            {
                if (ImGui.Selectable($"{UiText.Get(T.FishOption, fish.DisplayName, fish.FishParameterId)}###Fish{fish.FishParameterId}",
                    selectedFish == fish))
                {
                    selectedFish = fish;
                    fishGuide.RequestCollectionRead(fish.FishParameterId);
                }
            }
        }
        ImGui.EndChild();
        if (selectedFish is { } selected)
        {
            ImGui.TextWrapped(UiText.Get(T.SelectedFish, selected.DisplayName, selected.FishParameterId, selected.ItemType, selected.ItemId, selected.IsInLog));
            if (view.CollectionSample is { } sample && sample.FishParameterId == selected.FishParameterId)
                ImGui.TextWrapped(UiText.Get(T.BitValue, sample.Value?.ToString() ?? "Unknown", sample.Reason));
            else
                ImGui.TextUnformatted(UiText.Get(T.BitPending));
            if (UiWidgets.Button(UiText.Label(T.ReadBitAgain, "ReadBitAgain")))
                fishGuide.RequestCollectionRead(selected.FishParameterId);
        }
        if (UiWidgets.Disclosure(UiText.Label(T.GuideHistory, "GuideHistory")))
        {
            if (ImGui.BeginChild("FishGuideHistory", new Vector2(0, 130), true, ImGuiWindowFlags.HorizontalScrollbar))
            {
                for (var i = view.History.Count - 1; i >= 0; i--)
                    ImGui.TextUnformatted(view.History[i].Display);
            }
            ImGui.EndChild();
        }
    }

    private void UpdateMatches(IReadOnlyList<FishGuideRow> rows)
    {
        var term = search.Trim();
        matches = rows.Where(row => row.IsInLog &&
                (term.Length == 0 || row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 row.FishParameterId.ToString(System.Globalization.CultureInfo.InvariantCulture) == term))
            .Take(30).ToArray();
    }

    private void DrawFishingObservation()
    {
        ImGui.TextUnformatted(UiText.Get(T.FishingTitle));
        ImGui.Separator();
        ImGui.TextWrapped(UiText.Get(T.CastingProbeIntro));
        var probe = observer.View;
        var view = probe.Observation;
        ImGui.BeginDisabled(view.IsRecording);
        if (UiWidgets.Button(UiText.Label(T.StartObservation, "StartObservation")))
            observer.Start();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!view.IsRecording);
        if (UiWidgets.Button(UiText.Label(T.StopObservation, "StopObservation")))
            observer.Stop();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (UiWidgets.Button(UiText.Label(T.CopyObservation, "CopyObservation")))
            ImGui.SetClipboardText(observer.CreateReport());

        // Start/stop are Framework requests; manual markers can update the snapshot immediately.
        probe = observer.View;
        view = probe.Observation;
        ImGui.TextWrapped(UiText.Get(T.CastingProbeCounters, probe.ValidContracts, probe.TotalContracts,
            probe.Logs, probe.QueueDropped, probe.Native?.Availability.ToString() ?? "NotSampled"));
        ImGui.TextUnformatted(UiText.Get(T.ObservationCounts, new UiMessage(view.IsRecording ? T.Recording : T.Stopped), view.Entries.Count, view.DroppedCount));
        ImGui.TextWrapped(UiText.Get(T.ObservationHint));
        ImGui.BeginDisabled(!view.IsRecording);
        ImGui.SetNextItemWidth(180);
        GameThemeChrome.Combo(UiText.Label(T.ManualMarker, "ManualMarker"), ref selectedMarker, markerLabels, markerLabels.Length);
        ImGui.SameLine();
        if (UiWidgets.Button(UiText.Label(T.AddMarker, "AddMarker")))
            observer.Mark($"Marker={Markers[selectedMarker]}");
        ImGui.EndDisabled();
        ImGui.TextWrapped(UiText.Get(T.MarkerHint));
        if (view.Entries.Count > 0)
        {
            var last = view.Entries[^1].Conditions;
            ImGui.TextWrapped(UiText.Get(T.LastSnapshot, last.Fishing, last.ExecutingGatheringAction, last.Casting));
        }
        ImGui.TextUnformatted(UiText.Get(T.NewestFirst));
        if (ImGui.BeginChild("ObservationHistory", new Vector2(0, -40), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            for (var i = view.Entries.Count - 1; i >= 0; i--)
                ImGui.TextUnformatted(FishingConditionObserver.FormatEntry(view.Entries[i]));
        }
        ImGui.EndChild();
        ImGui.Spacing();
        if (UiWidgets.Button(UiText.Label(T.Close, "Close")))
            IsOpen = false;
    }
}
