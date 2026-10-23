using System.Globalization;
using System.Collections.Frozen;
using System.Text;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Displays cached static rows, session-local collection observations, and manual overrides.</summary>
internal sealed class FishListPanel(FishCatalog catalog, ITextureProvider textures, FishDetailPanel detailPanel,
    FishPreferences preferences, FishCollectionObserver collection, FishingRegionObserver region, FishingMapService maps,
    FishGuideOpenService guideOpener, CatalogQueryState preparationQuery, Action showBaitPreparation, Action<FishGuideRow> showDetail)
{
    private static readonly StringComparer NameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("ko-KR"), true);
    private static readonly CollectionVisibility[] CollectionChoices =
        [CollectionVisibility.All, CollectionVisibility.HideCaught, CollectionVisibility.CaughtOnly, CollectionVisibility.UnknownOnly];
    private static readonly FishGradeVisibility[] GradeChoices =
        [FishGradeVisibility.All, FishGradeVisibility.BigIncludingLegendary, FishGradeVisibility.LegendaryOnly];
    private static readonly CatalogSort[] SortChoices =
        [CatalogSort.Name, CatalogSort.FishId, CatalogSort.RemainingTime, CatalogSort.FavoritesFirst];
    private IReadOnlyList<FishGuideRow>? lastRows;
    private FishGuideRow[] matches = [];
    private string search = "";
    private bool inLogOnly;
    private bool includeOceanFish;
    private bool showUnresolved;
    private int unresolvedCount;
    private bool requested;
    private bool resetScroll;
    private int sort;
    private uint? selectedId;
    private FishGuideRow? selectedFish;

    private bool detailsOnly;
    private int lastForecastRevision;
    private int lastPreferenceRevision;
    private int lastPreferenceSession;
    private int lastCollectionRevision;
    private bool favoritesOnly;
    private int collectionVisibility;
    private readonly FishRowRenderer rows = new(textures, maps);
    private bool currentRegionOnly;
    private int minimumRemainingMinutes;
    private int? minimumUpcomingMinutes, maximumUpcomingMinutes;
    private string minimumRemainingInput = "", minimumUpcomingInput = "", maximumUpcomingInput = "";
    private DateTimeOffset nextWindowFilterUpdate, lastWindowFilterClock;
    private FishingRegionView? lastRegion;
    private bool timeRestrictedOnly;
    private int gradeVisibility;
    private readonly FishGeographyPanel geography = new();
    private readonly CatalogPresetsPanel presets = new(preferences);
    private bool restoredSettings;
    private bool resetDetailScroll;
    private bool jumpUpcoming;
    private int upcomingIndex = -1;

    public bool DeveloperMode { get; set; }
    public void Draw()
    {
        if (!requested)
        {
            catalog.RequestLoad();
            requested = true;
        }
        var view = catalog.View;
        var location = region.View;
        var personal = preferences.View;
        var restored = !restoredSettings;
        if (restored)
        {
            ApplyCatalogSettings(personal.Catalog);
            restoredSettings = true;
        }
        var observed = collection.View;
        if (observed.Session != personal.Session)
            observed = new(observed.Revision, personal.Session, FrozenSet<uint>.Empty, new(T.CollectionWaiting));
        var diagnosticsCleared = !DeveloperMode && (showUnresolved || detailsOnly);
        if (!DeveloperMode) { showUnresolved = false; detailsOnly = false; }
        var changed = diagnosticsCleared || restored || !ReferenceEquals(lastRows, view.Rows) || lastPreferenceSession != personal.Session;
        var refresh = changed || lastPreferenceRevision != personal.CatalogRevision || lastCollectionRevision != observed.Revision;
        changed |= currentRegionOnly && lastRegion != location;
        lastRegion = location;
        lastCollectionRevision = observed.Revision;
        if (lastPreferenceSession != personal.Session)
        {
            selectedId = null;
            selectedFish = null;
        }
        lastPreferenceSession = personal.Session;
        lastPreferenceRevision = personal.CatalogRevision;
        if (refresh)
        {

            selectedFish = selectedId is { } selected ? view.Rows.FirstOrDefault(row => row.FishParameterId == selected) : null;
        }
        lastRows = view.Rows;
        if (view.Status.Key != T.CatalogLoaded)
            ImGui.TextWrapped(view.Status.Display);
        UiWidgets.GuideBanner(observed, personal, guideOpener);
        ImGui.SetNextItemWidth(-1);
        changed |= ImGui.InputTextWithHint("###CatalogSearch", UiText.Get(T.SearchPlaceholder), ref search, 100);
        var toolbarRight = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        var comboWidth = ImGui.GetFontSize() * 12;
        string[] sortNames = [UiText.Get(T.CatalogSortName), UiText.Get(T.CatalogSortId), UiText.Get(T.CatalogSortWindow), UiText.Get(T.CatalogSortFavorite)];
        ImGui.SetNextItemWidth(comboWidth);
        var filtersChanged = ImGui.Combo("###CatalogSort", ref sort, sortNames, sortNames.Length);
        UiWidgets.Tooltip(UiText.Get(sort == 2 ? T.CatalogSortWindowHint : T.CatalogSort));
        UiWidgets.FlowNext(comboWidth, toolbarRight);
        string[] collectionNames = [UiText.Get(T.CollectionAll), UiText.Get(T.CollectionHideCaught),
            UiText.Get(T.CollectionCaughtOnly), UiText.Get(T.CollectionUnknownOnly)];
        ImGui.SetNextItemWidth(comboWidth);
        filtersChanged |= ImGui.Combo("###CatalogCollectionVisibility", ref collectionVisibility, collectionNames, collectionNames.Length);
        UiWidgets.Tooltip(UiText.Get(T.CollectionVisibility));
        UiWidgets.FlowNext(ImGui.GetFontSize() * 7, toolbarRight);
        filtersChanged |= DrawFilters(view.Geography);
        UiWidgets.FlowNext(ImGui.GetFrameHeight(), toolbarRight);
        if (UiWidgets.GameIconButton(textures, view.FishEyesSkill?.IconId ?? 0, "CatalogFishEyes",
            UiText.Get(view.FishEyesEnabled ? T.FishEyesToggleOn : T.FishEyesToggleOff), view.FishEyesEnabled))
        {
            catalog.SetFishEyes(!view.FishEyesEnabled);
            view = catalog.View;
            sort = 2;
            filtersChanged = true;
        }
        UiWidgets.FlowNext(ImGui.GetFontSize() * 7, toolbarRight);
        if (ImGui.Button(UiText.Label(T.BaitPreparation, "BaitPreparation"))) showBaitPreparation();
        UiWidgets.FlowNext(ImGui.GetFontSize() * 7, toolbarRight);
        if (presets.Draw(personal, CurrentCatalogSettings()) is { } preset)
        {
            ApplyCatalogSettings(preset);
            search = "";
            showUnresolved = false;
            detailsOnly = false;
            filtersChanged = true;
        }
        UiWidgets.FlowNext(ImGui.GetFrameHeight(), toolbarRight);
        if (UiWidgets.IconButton(FontAwesomeIcon.Undo, "RestoreCatalogDefaults", UiText.Get(T.CatalogRestoreDefaults)))
        {
            catalog.SetFishEyes(false);
            view = catalog.View;
            ApplyCatalogSettings(new());
            search = "";
            showUnresolved = false;
            detailsOnly = false;
            filtersChanged = true;
        }
        if (minimumUpcomingMinutes is { } min && maximumUpcomingMinutes is { } max && min > max)
            ImGui.TextWrapped(UiText.Get(T.UpcomingRangeInvalid));
        if (currentRegionOnly)
            ImGui.TextWrapped(location.State == FishingRegionState.Ready
                ? UiText.Get(T.CurrentRegionName, location.Name) : UiText.Get(T.CurrentRegionUnavailable));
        if (filtersChanged)
        {
            preferences.SetCatalogSettings(CurrentCatalogSettings());
            changed = true;
        }
        if (DeveloperMode)
        {
            if (ImGui.SmallButton(UiText.Label(T.CatalogDiagnostics, "CatalogDiagnostics"))) ImGui.OpenPopup("CatalogDiagnosticsPopup");
            ImGui.SetNextWindowSizeConstraints(new Vector2(ImGui.GetFontSize() * 24, 0),
                new Vector2(ImGui.GetFontSize() * 40, ImGui.GetMainViewport().WorkSize.Y * 0.8f));
            if (ImGui.BeginPopup("CatalogDiagnosticsPopup"))
            {
                ImGui.TextWrapped(view.Status.Display);
                changed |= ImGui.Checkbox(UiText.Label(T.CatalogShowUnresolved, "CatalogShowUnresolved"), ref showUnresolved);
                changed |= ImGui.Checkbox(UiText.Label(T.CatalogDetailsOnly, "CatalogDetailsOnly"), ref detailsOnly);
                if (ImGui.Button(UiText.Label(T.ReloadFish, "ReloadCatalog"))) catalog.RequestLoad(reload: true);
                if (ImGui.Button(UiText.Label(T.CopyCatalogReport, "CopyCatalogReport"))) ImGui.SetClipboardText(CreateReport(personal, observed));
                if (ImGui.Button(UiText.Label(T.CopyFullCatalogReport, "CopyFullCatalogReport"))) ImGui.SetClipboardText(CreateReport(personal, observed, full: true));
                if (ImGui.CollapsingHeader(UiText.Label(T.CollectionHelp, "CollectionHelp")))
                {
                    ImGui.TextWrapped(UiText.Get(T.CollectionObservedCount, observed.Caught.Count, observed.Uncaught.Count, observed.Status));
                    ImGui.TextWrapped(UiText.Get(T.CollectionFilterHint));
                    ImGui.TextWrapped(UiText.Get(T.CatalogDetailsPending));
                }
                ImGui.EndPopup();
            }
        }
        if (!personal.HasCharacter)
            ImGui.TextWrapped(UiText.Get(T.ManualPlayerRequired));
        if (personal.Error is { } error)
            ImGui.TextWrapped(error.Display);

        // Rebuild only for data or user input changes, never on every frame or scroll step.
        var filterNow = DateTimeOffset.UtcNow;
        var timedFilter = minimumRemainingMinutes > 0 || minimumUpcomingMinutes is not null || maximumUpcomingMinutes is not null;
        if (changed || refresh || ((sort == 2 || timedFilter) && lastForecastRevision != view.ForecastRevision) ||
            timedFilter && (filterNow >= nextWindowFilterUpdate || filterNow < lastWindowFilterClock))
        {
            preparationQuery.Publish(new(CurrentCatalogSettings(), search.Trim(), showUnresolved, detailsOnly));
            UpdateMatches(view, personal, observed, location, changed, filterNow);
        }
        lastWindowFilterClock = filterNow;
        if (lastForecastRevision != view.ForecastRevision)
            upcomingIndex = Array.FindIndex(matches, row => view.Windows.GetValueOrDefault(row.FishParameterId)?.Kind == FishingWindowKind.Upcoming);
        lastForecastRevision = view.ForecastRevision;
        ImGui.TextDisabled(UiText.Get(T.ResultCount, matches.Length));
        ImGui.SameLine();
        ImGui.BeginDisabled(upcomingIndex < 0);
        if (ImGui.SmallButton(UiText.Label(T.JumpUpcoming, "CatalogJumpUpcoming"))) jumpUpcoming = true;
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.SmallButton(UiText.Label(T.JumpTop, "CatalogJumpTop"))) resetScroll = true;
        if (DeveloperMode && unresolvedCount > 0)
            ImGui.TextWrapped(UiText.Get(T.CatalogUnresolvedCount, unresolvedCount));
        ImGui.Separator();

        if (ImGui.BeginTable("CatalogLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("List", ImGuiTableColumnFlags.WidthStretch, 1.15f);
            ImGui.TableSetupColumn("Detail", ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.TableNextColumn();
            DrawRows(view, personal, observed, location);
            ImGui.TableNextColumn();
            if (ImGui.BeginChild("CatalogDetail", new Vector2(0, 0), false))
                DrawDetails(view, personal, observed);
            ImGui.EndChild();
            ImGui.EndTable();
        }
    }

    private bool DrawFilters(FishingGeography options)
    {
        string[] gradeNames = [UiText.Get(T.GradeAll), UiText.Get(T.GradeBigIncludingLegendary), UiText.Get(T.GradeLegendaryOnly)];
        var active = (inLogOnly ? 1 : 0) + (!includeOceanFish ? 1 : 0) + (timeRestrictedOnly ? 1 : 0) +
            (favoritesOnly ? 1 : 0) + (currentRegionOnly ? 1 : 0) + (gradeVisibility != 0 ? 1 : 0) +
            (geography.Regions.Count > 0 ? 1 : 0) + (geography.Expansions.Count > 0 ? 1 : 0) +
            (minimumRemainingMinutes > 0 ? 1 : 0) + (minimumUpcomingMinutes is not null ? 1 : 0) +
            (maximumUpcomingMinutes is not null ? 1 : 0);
        if (ImGui.Button($"{UiText.Get(T.CatalogFiltersHeading, active)}###CatalogFilters")) ImGui.OpenPopup("CatalogFiltersPopup");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(UiText.Get(T.CatalogFiltersSummary,
                new UiMessage(timeRestrictedOnly ? T.OptionOn : T.OptionOff), gradeNames[gradeVisibility],
                new UiMessage(favoritesOnly ? T.OptionOn : T.OptionOff), new UiMessage(currentRegionOnly ? T.OptionOn : T.OptionOff),
                new UiMessage(includeOceanFish ? T.OptionOn : T.OptionOff), new UiMessage(inLogOnly ? T.OptionOn : T.OptionOff)));
        ImGui.SetNextWindowSizeConstraints(new Vector2(ImGui.GetFontSize() * 24, 0),
            new Vector2(ImGui.GetFontSize() * 38, ImGui.GetMainViewport().WorkSize.Y * 0.85f));
        if (!ImGui.BeginPopup("CatalogFiltersPopup")) return false;
        var changed = false;
        if (ImGui.Button(UiText.Label(T.CatalogShowAll, "CatalogShowAll")))
        {
            ApplyCatalogSettings(CatalogSettings.AllFish(SortChoices[sort]));
            search = "";
            showUnresolved = false;
            detailsOnly = false;
            changed = true;
        }
        UiWidgets.Tooltip(UiText.Get(T.CatalogShowAllHint));
        ImGui.Separator();
        changed |= ImGui.Checkbox(UiText.Label(T.TimeRestrictedOnly, "CatalogTimeRestrictedOnly"), ref timeRestrictedOnly);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.Get(T.TimeRestrictedHint));
        int? remainingLimit = minimumRemainingMinutes > 0 ? minimumRemainingMinutes : null;
        if (DrawTimeInput(T.MinimumRemainingMinutes, T.MinimumRemainingHint, "CatalogMinimumRemaining", ref minimumRemainingInput, ref remainingLimit))
        { minimumRemainingMinutes = remainingLimit ?? 0; changed = true; }
        changed |= DrawTimeInput(T.MinimumUpcomingMinutes, T.MinimumUpcomingHint, "CatalogMinimumUpcoming", ref minimumUpcomingInput, ref minimumUpcomingMinutes);
        changed |= DrawTimeInput(T.MaximumUpcomingMinutes, T.MaximumUpcomingHint, "CatalogMaximumUpcoming", ref maximumUpcomingInput, ref maximumUpcomingMinutes);
        if (!TryTimeMinutes(minimumRemainingInput, out _) || !TryTimeMinutes(minimumUpcomingInput, out _) || !TryTimeMinutes(maximumUpcomingInput, out _))
            ImGui.TextWrapped(UiText.Get(T.UpcomingInputInvalid, CatalogSettings.MaxTimeFilterMinutes));
        ImGui.SetNextItemWidth(190);
        changed |= ImGui.Combo(UiText.Label(T.GradeFilter, "CatalogGradeFilter"), ref gradeVisibility, gradeNames, gradeNames.Length);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.Get(T.GradeFilterHint));
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogFavoritesOnly, "CatalogFavoritesOnly"), ref favoritesOnly);
        changed |= ImGui.Checkbox(UiText.Label(T.CurrentRegionOnly, "CatalogCurrentRegionOnly"), ref currentRegionOnly);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.Get(T.CurrentRegionHint));
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogIncludeOceanFish, "CatalogIncludeOceanFish"), ref includeOceanFish);
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogInLogOnly, "CatalogInLogOnly"), ref inLogOnly);
        ImGui.Separator();
        changed |= geography.Draw(options);
        if (ImGui.Button(UiText.Label(T.Close, "CloseFilters"))) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
        return changed;
    }

    private CatalogSettings CurrentCatalogSettings() => new()
    {
        Sort = SortChoices[sort], Collection = CollectionChoices[collectionVisibility], Grade = GradeChoices[gradeVisibility],
        InLogOnly = inLogOnly, IncludeOceanFish = includeOceanFish, TimeRestrictedOnly = timeRestrictedOnly,
        FavoritesOnly = favoritesOnly, CurrentRegionOnly = currentRegionOnly,
        MinimumRemainingMinutes = minimumRemainingMinutes, MinimumUpcomingMinutes = minimumUpcomingMinutes, MaximumUpcomingMinutes = maximumUpcomingMinutes,
        Regions = geography.Regions.Order().ToArray(), Expansions = geography.Expansions.Order().ToArray(),
    };

    private void ApplyCatalogSettings(CatalogSettings settings)
    {
        sort = Array.IndexOf(SortChoices, settings.Sort);
        collectionVisibility = Array.IndexOf(CollectionChoices, settings.Collection);
        gradeVisibility = Array.IndexOf(GradeChoices, settings.Grade);
        inLogOnly = settings.InLogOnly;
        includeOceanFish = settings.IncludeOceanFish;
        timeRestrictedOnly = settings.TimeRestrictedOnly;
        favoritesOnly = settings.FavoritesOnly;
        currentRegionOnly = settings.CurrentRegionOnly;
        minimumRemainingMinutes = settings.MinimumRemainingMinutes;
        minimumRemainingInput = minimumRemainingMinutes > 0 ? minimumRemainingMinutes.ToString(CultureInfo.InvariantCulture) : "";
        minimumUpcomingMinutes = settings.MinimumUpcomingMinutes;
        maximumUpcomingMinutes = settings.MaximumUpcomingMinutes;
        minimumUpcomingInput = minimumUpcomingMinutes?.ToString(CultureInfo.InvariantCulture) ?? "";
        maximumUpcomingInput = maximumUpcomingMinutes?.ToString(CultureInfo.InvariantCulture) ?? "";
        geography.RestoreSelections(settings.Regions, settings.Expansions);
    }

    private static bool DrawTimeInput(T label, T hint, string id, ref string input, ref int? value)
    {
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 8);
        var edited = ImGui.InputTextWithHint(UiText.Label(label, id), UiText.Get(T.UpcomingNoLimit), ref input, 6, ImGuiInputTextFlags.CharsDecimal);
        UiWidgets.Tooltip(UiText.Get(hint));
        if (!edited || !TryTimeMinutes(input, out var parsed) || value == parsed) return false;
        value = parsed;
        return true;
    }

    private static bool TryTimeMinutes(string input, out int? value)
    {
        value = null;
        if (input.Length == 0) return true;
        if (!int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 0 || number > CatalogSettings.MaxTimeFilterMinutes) return false;
        value = number;
        return true;
    }

    private void DrawRows(FishCatalogView view, FishPreferencesView personal, FishCollectionView observed, FishingRegionView location)
    {
        if (ImGui.BeginChild("CatalogRows", new Vector2(0, 0), true))
        {
            if (resetScroll)
            {
                ImGui.SetScrollY(0);
                resetScroll = false;
            }
            if (matches.Length == 0)
                ImGui.TextWrapped(UiText.Get(T.CatalogNoMatches));
            var rowHeight = FishRowRenderer.GetHeight(ImGui.GetContentRegionAvail().X);
            if (jumpUpcoming)
            {
                if (upcomingIndex >= 0) ImGui.SetScrollY(upcomingIndex * (rowHeight + ImGui.GetStyle().ItemSpacing.Y));
                jumpUpcoming = false;
            }
            var now = DateTimeOffset.UtcNow;
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                clipper.Begin(matches.Length, rowHeight + ImGui.GetStyle().ItemSpacing.Y);
                while (clipper.Step())
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        DrawRow(matches[i], rowHeight, view, personal, observed, location, now);
            }
            finally
            {
                clipper.Destroy();
            }
        }
        ImGui.EndChild();
    }

    private void UpdateMatches(FishCatalogView view, FishPreferencesView personal, FishCollectionView observed,
        FishingRegionView location, bool reset, DateTimeOffset now)
    {
        var query = preparationQuery.Current;
        var filter = new CatalogFilter(query.Settings);
        var scope = view.Rows.Where(row => filter.MatchesScope(row, location)).ToArray();
        unresolvedCount = scope.Count(row => !row.IsDisplayable);
        var filtered = scope.Where(row => query.Matches(row, filter, personal, observed, location, view, now));
        nextWindowFilterUpdate = CatalogWindowFilter.NextChange(query.Settings, view.Windows.Values, now);
        matches = (sort switch
        {
            0 => filtered.OrderBy(row => row.Name, NameComparer).ThenBy(row => row.FishParameterId),
            2 => filtered.OrderBy(row => WindowSortKey(view, row.FishParameterId))
                .ThenBy(row => row.Name, NameComparer).ThenBy(row => row.FishParameterId),
            3 => filtered.OrderByDescending(row => personal.Favorites.Contains(row.FishParameterId))
                .ThenBy(row => row.Name, NameComparer).ThenBy(row => row.FishParameterId),
            _ => filtered.OrderBy(row => row.FishParameterId),
        }).ToArray();
        upcomingIndex = Array.FindIndex(matches, row => view.Windows.GetValueOrDefault(row.FishParameterId)?.Kind == FishingWindowKind.Upcoming);
        if (selectedId is { } selected && !matches.Any(row => row.FishParameterId == selected))
        {
            selectedId = null;
            selectedFish = null;
        }
        resetScroll |= reset;
    }

    private void DrawRow(FishGuideRow fish, float height, FishCatalogView view,
        FishPreferencesView personal, FishCollectionView observed, FishingRegionView location, DateTimeOffset now)
    {
        view.Windows.TryGetValue(fish.FishParameterId, out var forecast);
        var locationScoped = currentRegionOnly || geography.HasSelection;
        var rowSpot = locationScoped ? fish.Spots.FirstOrDefault(spot => geography.MatchesSpot(spot, location, currentRegionOnly))
            : fish.Spots.FirstOrDefault();
        var place = fish.Ocean is { } ocean ? UiText.Get(T.OceanArea, ocean.Area)
            : rowSpot is { } spot ? spot.Name
            : UiText.Get(fish.IsDisplayable ? T.CatalogRowInfo : T.CatalogUnresolvedRow, fish.FishParameterId);
        var collection = new UiMessage(FishRowRenderer.CollectionKey(personal, observed, fish.FishParameterId));
        var state = collection.Display;
        var usesOtherRegion = locationScoped && rowSpot is not null && fish.Details?.Spot is { } recommended &&
            recommended.TerritoryId != rowSpot.TerritoryId;
        if (rows.Draw(fish, height, selectedId == fish.FishParameterId, state, place, forecast, now, usesOtherRegion,
            personal.Favorites.Contains(fish.FishParameterId), spot: rowSpot, canOpenMap: personal.HasCharacter))
        {
            resetDetailScroll |= selectedId != fish.FishParameterId;
            selectedId = fish.FishParameterId;
            selectedFish = fish;
        }
    }

    private static (FishingWindowSortGroup Group, DateTimeOffset Time) WindowSortKey(FishCatalogView view, uint id) =>
        view.FishEyesEnabled && view.Windows.GetValueOrDefault(id)?.Kind == FishingWindowKind.Always &&
            view.NormalWindows.GetValueOrDefault(id)?.Kind != FishingWindowKind.Always
            ? (FishingWindowSortGroup.Open, DateTimeOffset.MaxValue)
            : FishingWindowOrdering.Key(view.Windows.GetValueOrDefault(id));

    private static IReadOnlySet<uint> RegionFish(FishCatalogView view, FishingRegionView location) =>
        location.State == FishingRegionState.Ready && view.FishByTerritory.TryGetValue(location.TerritoryId, out var fish)
            ? fish : FrozenSet<uint>.Empty;

    private void DrawDetails(FishCatalogView view, FishPreferencesView personal, FishCollectionView observed)
    {
        if (resetDetailScroll) { ImGui.SetScrollY(0); resetDetailScroll = false; }
        detailPanel.Draw(selectedFish, view, personal, observed, showDetail);
    }

    private string CreateReport(FishPreferencesView personal, FishCollectionView observed, bool full = false)
    {
        var report = new StringBuilder();
        void Append(FormattableString line) => report.AppendLine(line.ToString(CultureInfo.InvariantCulture));
        if (full)
            report.AppendLine(catalog.CreateReport(selectedFish?.FishParameterId));
        else
        {
            var view = catalog.View;
            report.AppendLine("DuckbillFisher summary/v10");
            Append($"Plugin={typeof(Plugin).Assembly.GetName().Version}; Rows={view.Rows.Count}; Status={view.Status.Key}");
            Append($"Dataset={FishingData.Dataset.DataVersion}; Details={view.Rows.Count(row => row.Details is not null)}/{FishingData.Dataset.Fish.Length}; BuildMatch={catalog.ConditionBuildMatches}");
            Append($"Forecasts={view.Windows.Count}; OceanAreas={view.Rows.Count(row => row.Ocean is not null)}; SpearStored={FishingData.Dataset.Spearfishing.Length}; SpearUnknown={FishingData.SpearUnknown}");
            Append($"Classification={FishClassificationData.Dataset.DataVersion}; Big={view.Rows.Count(row => row.Grade is FishGrade.Big or FishGrade.Legendary)}; Legendary={view.Rows.Count(row => row.Grade == FishGrade.Legendary)}; Restricted={view.Rows.Count(row => row.TimeRestriction == FishTimeRestriction.Restricted)}");
            if (selectedFish is { } selected)
            {
                Append($"SelectedFish={selected.FishParameterId}; Item={selected.ItemId}; Ocean={selected.IsOceanFishing}; Collection={personal.Collection(selected.FishParameterId, observed)}; Grade={selected.Grade}; Timing={selected.TimeRestriction}");
                var forecast = view.Windows.GetValueOrDefault(selected.FishParameterId);
                Append($"SelectedForecast={forecast?.Kind.ToString() ?? "Unknown"}; Basis={forecast?.Basis}; Issue={forecast?.Issue}; FishEyes={view.FishEyesEnabled}; ForecastAt={view.ForecastAt:O}; Start={forecast?.Start:O}; End={forecast?.End:O}; CopiedAt={DateTimeOffset.UtcNow:O}");
                var normal = view.NormalWindows.GetValueOrDefault(selected.FishParameterId);
                var eyes = view.FishEyesWindows.GetValueOrDefault(selected.FishParameterId);
                Append($"ForecastComparison: Normal={normal?.Kind}; NormalStart={normal?.Start:O}; NormalEnd={normal?.End:O}; FishEyes={eyes?.Kind}; FishEyesStart={eyes?.Start:O}; FishEyesEnd={eyes?.End:O}; Different={FishingWindowPanel.Different(normal, eyes)}");
                if (selected.Details is { Spot: { } spot } detail)
                {
                    var rule = detail.Rule;
                    Append($"SelectedConditions: Spot={spot.Id}; Territory={spot.TerritoryId}; Map={spot.MapId}; Known={rule.ConditionsKnown}; StartET={rule.StartHour}; EndET={rule.EndHour}; Weather={string.Join(',', rule.WeatherIds)}; PreviousWeather={string.Join(',', rule.PreviousWeatherIds)}; Rates={string.Join(',', spot.WeatherRates.Select(rate => FormattableString.Invariant($"{rate.WeatherId}:{rate.Chance}")))}; HorizonDays={FishingAvailability.SearchDays}; FishEyesSupported={rule.FishEyesSupported}");
                    Append($"SelectedIntuition: DurationSeconds={rule.IntuitionSeconds}; EffectiveDurationSeconds={forecast?.Breakdown?.IntuitionSeconds}; DurationAssumed={forecast?.Breakdown?.DurationAssumed}; Predators={string.Join(',', rule.Predators.Select(predator => FormattableString.Invariant($"{predator.ItemId}:{predator.Count}")))}");
                }
            }
        }
        report.AppendLine("DuckbillFisher MVP-01/preferences/v10");
        Append($"Ready={personal.HasCharacter}; Editable={personal.CanEdit}; Session={personal.Session}; Favorites={personal.Favorites.Count}");
        Append($"ManualCaught={personal.ManualCaught.Count(pair => pair.Value)}; ManualUncaught={personal.ManualCaught.Count(pair => !pair.Value)}");
        Append($"CollectionFilter={CollectionChoices[collectionVisibility]}; FavoritesOnly={favoritesOnly}; Sort={sort}; Displayed={matches.Length}");
        Append($"CatalogSaved={personal.Catalog.Matches(CurrentCatalogSettings())}; SettingsScope=Shared; SearchPersisted=False; DiagnosticsPersisted=False");
        Append($"InterfaceLanguage={personal.Language}; DeveloperMode={personal.DeveloperMode}; CompactFilters=SharedCatalog; CompactFavoritesOnly={personal.Catalog.FavoritesOnly}; CompactUpcomingMinutes={personal.Compact.UpcomingMinutes}");
        Append($"InLogOnly={inLogOnly}; IncludeOceanFish={includeOceanFish}; DetailsOnly={detailsOnly}; ShowUnresolved={showUnresolved}");
        var location = region.View;
        Append($"CurrentRegionOnly={currentRegionOnly}; Territory={location.TerritoryId}; RegionState={location.State}; RegionFish={RegionFish(catalog.View, location).Count}");
        Append($"TimeRestrictedOnly={timeRestrictedOnly}; GradeFilter={GradeChoices[gradeVisibility]}");
        Append($"Regions={string.Join(',', geography.Regions.Order().Select(id => id.ToString(CultureInfo.InvariantCulture)))}; Expansions={string.Join(',', geography.Expansions.Order().Select(id => id.ToString(CultureInfo.InvariantCulture)))}; RegionOptions={catalog.View.Geography.Regions.Length}; ExpansionOptions={catalog.View.Geography.Expansions.Length}");
        Append($"CollectionSource=ManualOverrideThenGameObservationThenSavedBackup; StorageError={personal.Error?.Key.ToString() ?? "none"}");
        Append($"CatalogPresets={personal.CatalogPresets.Count}; PresetError={personal.CatalogPresetError?.Key.ToString() ?? "none"}");
        report.AppendLine().AppendLine("DuckbillFisher MVP-04/collection/v4");
        report.AppendLine(collection.RuntimeDiagnostic);
        Append($"Session={observed.Session}; Caught={observed.Caught.Count}; Eligible={observed.EligibleRows}; GuideVisible={observed.GuideVisible}");
        Append($"Scans={observed.Scans}; RawFalse={observed.RawFalse}; UnknownReads={observed.Unknown}; RegistrationContract={observed.RegistrationContract}");
        Append($"RegistrationMessages={observed.RegistrationMessages}; AppliedRegistrations={observed.AppliedRegistrations}; LastLogItem={observed.LastLogItem}; LastLogFish={observed.LastLogFish}");
        Append($"LastTrigger={observed.LastTrigger}; SampledAtSeconds={observed.SampledAtSeconds?.ToString("F3", CultureInfo.InvariantCulture) ?? "none"}; Status={observed.Status.Key}");
        Append($"Uncaught={observed.Uncaught.Count}; GuideState={observed.GuideState}; GuideEntries={observed.GuideEntries}; Matched={observed.GuideMatched}; Mismatches={observed.GuideMismatches}");
        report.AppendLine("AutomaticUncaught=MatchedGameGuideSnapshot; ObservationsPersisted=True");
        Append($"SavedCaught={personal.SavedCollection.Count(pair => pair.Value)}; SavedUncaught={personal.SavedCollection.Count(pair => !pair.Value)}; SavedAtUtc={personal.CollectionSavedUtc:O}; Fallback=OnlyWhenCurrentObservationUnknown");
        return report.ToString();
    }

}
