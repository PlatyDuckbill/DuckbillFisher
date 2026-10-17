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
    private readonly FishingTracker fishingTracker;
    private readonly FishingHistoryPanel fishingHistory;
    public Action? ShowCompact { get; set; }
    public Action? ShowTimer { get; set; }
    private string[] markerLabels = Markers.Select(key => UiText.Get(key)).ToArray();
    private IReadOnlyList<FishGuideRow>? lastRows;
    private FishGuideRow[] matches = [];
    private FishGuideRow? selectedFish;
    private string search = "";
    private int lastSession = -1;
    private int selectedMarker;
    private bool selectSettings;

    public MainWindow(FishingActivityProbe observer, FishGuideDiagnostics fishGuide, FishListPanel fishList,
        SpawnAlertsPanel spawnAlerts, FishPreferences preferences, Action languageChanged,
        FishingTracker fishingTracker, FishingHistoryPanel fishingHistory) : base("DuckbillFisher###DuckbillFisher.Main")
    {
        this.observer = observer;
        this.fishGuide = fishGuide;
        this.fishList = fishList;
        this.spawnAlerts = spawnAlerts;
        this.languageChanged = languageChanged;
        this.preferences = preferences;
        this.fishingTracker = fishingTracker;
        this.fishingHistory = fishingHistory;
        spawnAlerts.ShowSettings = OpenSettings;
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
        if (ImGui.BeginTabBar("DiagnosticsTabs"))
        {
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.TabItemButton($"{FontAwesomeIcon.CompressAlt.ToIconString()}###OpenCompactWindow", ImGuiTabItemFlags.Trailing)) ShowCompact?.Invoke();
            ImGui.PopFont();
            UiWidgets.Tooltip(UiText.Get(T.OpenCompactWindow));
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.TabItemButton($"{FontAwesomeIcon.Stopwatch.ToIconString()}###OpenTimer", ImGuiTabItemFlags.Trailing)) ShowTimer?.Invoke();
            ImGui.PopFont();
            UiWidgets.Tooltip(UiText.Get(T.CastingTimerTitle));
            if (ImGui.BeginTabItem(UiText.Label(T.TabCatalog, "CatalogTab")))
            {
                fishList.Draw();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(UiText.Label(T.TabAlerts, "SpawnAlertsTab")))
            {
                spawnAlerts.Draw();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(UiText.Label(T.TabHistory, "FishingHistoryTab")))
            {
                var activity = fishingTracker.View;
                if (ImGui.Button(UiText.Label(T.CastingTimerTitle, "OpenCastingTimer"))) ShowTimer?.Invoke();
                ImGui.SameLine();
                ImGui.TextDisabled(UiText.Get(T.HistoryCapacity, activity.Records.Count, DuckbillFisher.Core.FishingAttempts.Capacity));
                if (settings.DeveloperMode)
                {
                    ImGui.SameLine();
                    if (ImGui.Button(UiText.Label(T.CopyTimerReport, "CopyTimerReport"))) ImGui.SetClipboardText(fishingTracker.CreateReport());
                }
                fishingHistory.Draw(activity, DuckbillFisher.Core.FishingAttempts.Capacity, "MainFishingHistory");
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(UiText.Label(T.TabSettings, "SettingsTab"),
                selectSettings ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                if (ImGui.BeginChild("SettingsContent")) DrawSettings(settings);
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            selectSettings = false;
            if (settings.DeveloperMode && ImGui.BeginTabItem(UiText.Label(T.TabGuide, "GuideTab")))
            {
                if (ImGui.BeginChild("GuideDiagnosticsContent")) DrawFishGuide();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            if (settings.DeveloperMode && ImGui.BeginTabItem(UiText.Label(T.TabFishing, "FishingTab")))
            {
                if (ImGui.BeginChild("FishingDiagnosticsContent")) DrawFishingObservation();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawSettings(FishPreferencesView settings)
    {
        UiWidgets.Heading(T.InterfaceSettings);
        var language = (int)settings.Language;
        var developer = settings.DeveloperMode;
        ImGui.BeginDisabled(!settings.CanEdit);
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
        var changed = ImGui.Combo(UiText.Label(T.Language, "Language"), ref language, LanguageNames, LanguageNames.Length);
        changed |= ImGui.Checkbox(UiText.Label(T.DeveloperMode, "DeveloperMode"), ref developer);
        ImGui.EndDisabled();
        UiWidgets.Tooltip(UiText.Get(T.DeveloperModeHint));
        if (changed) preferences.SetInterface((UiLanguage)language, developer);
        if (settings.Error is { } error) ImGui.TextWrapped(error.Display);
        UiWidgets.Heading(T.AlertSettingsHeading);
        spawnAlerts.DrawSettings();
        UiWidgets.Heading(T.DataSourceLabel);
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
