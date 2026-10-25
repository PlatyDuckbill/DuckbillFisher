using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class MainWindow : Window
{
    private static readonly T[] Markers = [T.MarkCast, T.MarkWait, T.MarkBite, T.MarkHook, T.MarkRetrieve, T.MarkEnd, T.MarkMooch, T.MarkMove, T.MarkLogout];
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
    private readonly SettingsBackupPanel backup;
    public Action? ShowCompact { get; set; }
    public Action? ShowTimer { get; set; }
    public Action? ShowChangelog { get; set; }
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
    private enum MainPage { Catalog, Recommendations, Collection, Schedule, Alerts, History, Settings, Guide, Fishing }

    public MainWindow(FishingActivityProbe observer, FishGuideDiagnostics fishGuide, FishListPanel fishList,
        SpawnAlertsPanel spawnAlerts, FishPreferences preferences, Action languageChanged,
        FishingHistoryBrowser fishingHistory, CollectionProgressPanel collectionProgress, SpawnSchedulePanel schedule,
        SettingsBackupPanel backup, FishRecommendationPanel recommendations) : base("DuckbillFisher###DuckbillFisher.Main")
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
    public override void OnClose() { schedule.SetActive(false); recommendations.SetActive(false); backup.CloseDialogs(); }

    public override void Draw()
    {
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
        schedule.SetActive(selectedPage == MainPage.Schedule);
        recommendations.SetActive(selectedPage == MainPage.Recommendations);
        ImGui.PushID(selectedPage.ToString());
        switch (selectedPage)
        {
            case MainPage.Catalog: fishList.Draw(); break;
            case MainPage.Recommendations: recommendations.Draw(); break;
            case MainPage.Collection: collectionProgress.Draw(); break;
            case MainPage.Schedule: schedule.Draw(); break;
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
        // The public tab bar uses its container width. Reserve the shortcut area in a header-only child.
        var tabWidth = Math.Max(1, available - compactWidth - timerWidth - spacing * 2);
        if (ImGui.BeginChild("MainNavigationTabs", new Vector2(tabWidth, ImGui.GetFrameHeightWithSpacing()), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (ImGui.BeginTabBar("DiagnosticsTabs", ImGuiTabBarFlags.FittingPolicyScroll))
            {
                NavigationTab(MainPage.Catalog, T.TabCatalog, "CatalogTab", FontAwesomeIcon.Fish);
                NavigationTab(MainPage.Recommendations, T.RecommendationTitle, "RecommendationsTab", FontAwesomeIcon.Compass);
                NavigationTab(MainPage.Collection, T.TabCollectionProgress, "CollectionProgressTab", FontAwesomeIcon.ChartBar);
                NavigationTab(MainPage.Schedule, T.ScheduleTitle, "SpawnScheduleTab", FontAwesomeIcon.CalendarAlt);
                NavigationTab(MainPage.Alerts, T.TabAlerts, "SpawnAlertsTab", FontAwesomeIcon.Star);
                NavigationTab(MainPage.History, T.TabHistory, "FishingHistoryTab", FontAwesomeIcon.History);
                NavigationTab(MainPage.Settings, T.TabSettings, "SettingsTab", FontAwesomeIcon.Cog, selectSettings);
                if (developerMode)
                {
                    NavigationTab(MainPage.Guide, T.TabGuide, "GuideTab", FontAwesomeIcon.BookOpen);
                    NavigationTab(MainPage.Fishing, T.TabFishing, "FishingTab", FontAwesomeIcon.Search);
                }
                ImGui.EndTabBar();
                selectSettings = false;
            }
        }
        ImGui.EndChild();
        ImGui.SameLine(0, spacing);
        if (Shortcut(FontAwesomeIcon.List, compact, "OpenCompactWindow", compactWidth)) ShowCompact?.Invoke();
        UiWidgets.Tooltip(UiText.Get(T.OpenCompactWindow));
        ImGui.SameLine();
        if (Shortcut(FontAwesomeIcon.Stopwatch, timer, "OpenTimer", timerWidth)) ShowTimer?.Invoke();
        UiWidgets.Tooltip(timer);
    }

    private void NavigationTab(MainPage page, T label, string id, FontAwesomeIcon icon, bool select = false)
    {
        // Reserve icon space using the text font, then draw the glyph with the existing icon font.
        // The translated label, fixed tab ID, tab selection and native overflow scrolling remain intact.
        var fontSize = ImGui.GetFontSize();
        var gap = fontSize + ImGui.GetStyle().ItemInnerSpacing.X;
        var padding = new string(' ', Math.Max(1, (int)MathF.Ceiling(gap / Math.Max(1, ImGui.CalcTextSize(" ").X))));
        var active = ImGui.BeginTabItem($"{padding}{UiText.Label(label, id)}", select ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None);
        if (ImGui.IsItemVisible())
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var draw = ImGui.GetWindowDrawList();
            draw.PushClipRect(min, max, true);
            draw.AddText(UiBuilder.IconFont, fontSize,
                min + new Vector2(ImGui.GetStyle().FramePadding.X, Math.Max(0, (max.Y - min.Y - fontSize) / 2)),
                ImGui.GetColorU32(active ? ImGuiCol.Text : ImGuiCol.TextDisabled), icon.ToIconString());
            draw.PopClipRect();
        }
        if (active)
        {
            selectedPage = page;
            ImGui.EndTabItem();
        }
    }

    private static bool Shortcut(FontAwesomeIcon icon, string label, string id, float width)
    {
        var clicked = ImGui.Button($"###{id}", new Vector2(width, ImGui.GetFrameHeight()));
        var origin = ImGui.GetItemRectMin() + ImGui.GetStyle().FramePadding;
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(ImGuiCol.Text);
        draw.AddText(UiBuilder.IconFont, ImGui.GetFontSize(), origin, color, icon.ToIconString());
        draw.AddText(origin + new Vector2(ImGui.GetFrameHeight(), 0), color, label);
        return clicked;
    }

    private void DrawSettings(FishPreferencesView settings)
    {
        UiWidgets.Heading(T.InterfaceSettings);
        var language = (int)settings.Language;
        var developer = settings.DeveloperMode;
        var changed = false;
        if (ImGui.BeginTable("InterfaceSettingsFields", 2, ImGuiTableFlags.SizingFixedFit))
        {
            var labelWidth = new[] { T.Language, T.UiScale, T.DeveloperMode, T.ChangelogTitle }
                .Max(key => ImGui.CalcTextSize(UiText.Get(key)).X);
            ImGui.TableSetupColumn("Setting", ImGuiTableColumnFlags.WidthFixed, labelWidth);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            ImGui.BeginDisabled(!settings.CanEdit);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(UiText.Get(T.Language));
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(Math.Max(1, Math.Min(ImGui.GetContentRegionAvail().X, ImGui.GetFontSize() * 14)));
            changed = ImGui.Combo("###Language", ref language, LanguageNames, LanguageNames.Length);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(UiText.Get(T.UiScale));
            ImGui.TableNextColumn();
            var scaleIndex = Array.IndexOf(UiScale.Choices, settings.UiScalePercent);
            var scaleWidth = Math.Min(ImGui.GetContentRegionAvail().X, ImGui.GetFontSize() * 14);
            ImGui.SetNextItemWidth(Math.Max(1, scaleWidth - ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X));
            if (ImGui.Combo("###UiScale", ref scaleIndex, UiScale.Labels, UiScale.Labels.Length))
                preferences.SetUiScale(UiScale.Choices[scaleIndex]);
            UiWidgets.Tooltip(UiText.Get(T.UiScaleHint));
            ImGui.SameLine();
            if (UiWidgets.IconButton(FontAwesomeIcon.Undo, "ResetUiScale", UiText.Get(T.UiScaleReset)))
                preferences.SetUiScale(UiScale.DefaultPercent);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(UiText.Get(T.DeveloperMode));
            ImGui.TableNextColumn();
            changed |= ImGui.Checkbox("###DeveloperMode", ref developer);
            UiWidgets.Tooltip(UiText.Get(T.DeveloperModeHint));
            ImGui.EndDisabled();
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(UiText.Get(T.ChangelogTitle));
            ImGui.TableNextColumn();
            var updatesWidth = Math.Max(1, Math.Min(ImGui.GetContentRegionAvail().X, ImGui.GetFontSize() * 14));
            if (UiWidgets.ActionButton(FontAwesomeIcon.History, T.SettingsViewUpdates, "ShowChangelog", updatesWidth)) ShowChangelog?.Invoke();
            ImGui.EndTable();
        }
        if (changed) preferences.SetInterface((UiLanguage)language, developer);
        if (settings.Error is { } error) ImGui.TextWrapped(error.Display);
        backup.Draw(settings);
        ImGui.Spacing();
        ImGui.Separator();
        if (FishingData.Failure is not null || ImGui.CollapsingHeader(UiText.Label(T.DataSourceLabel, "SettingsSources")))
            FishDetailPanel.DrawSources();
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
        if (ImGui.Button(UiText.Label(T.ReloadFish, "ReloadFish")))
            fishGuide.RequestRefresh();
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label(T.CopyGuide, "CopyGuide")))
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
            if (ImGui.Button(UiText.Label(T.ReadBitAgain, "ReadBitAgain")))
                fishGuide.RequestCollectionRead(selected.FishParameterId);
        }
        if (ImGui.CollapsingHeader(UiText.Label(T.GuideHistory, "GuideHistory")))
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
        if (ImGui.Button(UiText.Label(T.StartObservation, "StartObservation")))
            observer.Start();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!view.IsRecording);
        if (ImGui.Button(UiText.Label(T.StopObservation, "StopObservation")))
            observer.Stop();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label(T.CopyObservation, "CopyObservation")))
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
        ImGui.Combo(UiText.Label(T.ManualMarker, "ManualMarker"), ref selectedMarker, markerLabels, markerLabels.Length);
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label(T.AddMarker, "AddMarker")))
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
        if (ImGui.Button(UiText.Label(T.Close, "Close")))
            IsOpen = false;
    }
}
