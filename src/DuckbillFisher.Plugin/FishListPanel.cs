using System.Globalization;
using System.Collections.Frozen;
using System.Text;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Displays cached static rows, session-local collection observations, and manual overrides.</summary>
internal sealed class FishListPanel(FishCatalog catalog, ITextureProvider textures, FishingMapService mapService,
    FishPreferences preferences, FishCollectionObserver collection, FishingConditionPanel conditions, FishingRegionObserver region)
{
    private static readonly StringComparer NameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("ko-KR"), true);
    private static readonly CollectionVisibility[] CollectionChoices =
        [CollectionVisibility.All, CollectionVisibility.HideCaught, CollectionVisibility.CaughtOnly, CollectionVisibility.UnknownOnly];
    private static readonly FishGradeVisibility[] GradeChoices =
        [FishGradeVisibility.All, FishGradeVisibility.BigIncludingLegendary, FishGradeVisibility.LegendaryOnly];
    private IReadOnlyList<FishGuideRow>? lastRows;
    private FishGuideRow[] matches = [];
    private string search = "";
    private bool inLogOnly = true;
    private bool includeOceanFish;
    private bool showUnresolved;
    private int unresolvedCount;
    private bool requested;
    private bool resetScroll;
    private int sort = 2;
    private uint? selectedId;
    private FishGuideRow? selectedFish;
    private int detailCoverage;
    private bool detailsOnly;
    private int lastForecastRevision;
    private int lastPreferenceRevision;
    private int lastPreferenceSession;
    private int lastCollectionRevision;
    private bool favoritesOnly;
    private int collectionVisibility = Array.IndexOf(CollectionChoices, CollectionVisibility.HideCaught);
    private readonly FishRowRenderer rows = new(textures);
    private bool currentRegionOnly;
    private FishingRegionView? lastRegion;
    private bool timeRestrictedOnly = true;
    private int gradeVisibility;
    private readonly FishGeographyPanel geography = new();

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
        var observed = collection.View;
        if (observed.Session != personal.Session)
            observed = new(observed.Revision, personal.Session, FrozenSet<uint>.Empty, new(T.CollectionWaiting));
        var changed = !ReferenceEquals(lastRows, view.Rows) || lastPreferenceSession != personal.Session;
        var refresh = changed || lastPreferenceRevision != personal.Revision || lastCollectionRevision != observed.Revision;
        changed |= currentRegionOnly && lastRegion != location;
        lastRegion = location;
        lastCollectionRevision = observed.Revision;
        if (lastPreferenceSession != personal.Session)
        {
            selectedId = null;
            selectedFish = null;
        }
        lastPreferenceSession = personal.Session;
        lastPreferenceRevision = personal.Revision;
        if (refresh)
        {
            detailCoverage = view.Rows.Count(row => row.Details is not null);
            selectedFish = selectedId is { } selected ? view.Rows.FirstOrDefault(row => row.FishParameterId == selected) : null;
        }
        lastRows = view.Rows;
        if (view.Status.Key != T.CatalogLoaded)
            ImGui.TextWrapped(view.Status.Display);
        ImGui.SetNextItemWidth(280);
        changed |= ImGui.InputText(UiText.Label(T.FishSearch, "CatalogSearch"), ref search, 100);
        if (ImGui.BeginTable("CatalogPrimaryControls", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(UiText.Get(T.CatalogSort));
            ImGui.SetNextItemWidth(-1);
            string[] sortNames = [UiText.Get(T.CatalogSortName), UiText.Get(T.CatalogSortId), UiText.Get(T.CatalogSortWindow), UiText.Get(T.CatalogSortFavorite)];
            changed |= ImGui.Combo("###CatalogSort", ref sort, sortNames, sortNames.Length);
            if (sort == 2 && ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.Get(T.CatalogSortWindowHint));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(UiText.Get(T.CollectionVisibility));
            ImGui.SetNextItemWidth(-1);
            string[] collectionNames = [UiText.Get(T.CollectionAll), UiText.Get(T.CollectionHideCaught),
                UiText.Get(T.CollectionCaughtOnly), UiText.Get(T.CollectionUnknownOnly)];
            changed |= ImGui.Combo("###CatalogCollectionVisibility", ref collectionVisibility, collectionNames, collectionNames.Length);
            ImGui.EndTable();
        }
        changed |= DrawFilters();
        if (currentRegionOnly)
            ImGui.TextWrapped(location.State == FishingRegionState.Ready
                ? UiText.Get(T.CurrentRegionName, location.Name) : UiText.Get(T.CurrentRegionUnavailable));
        changed |= geography.Draw(view.Geography);
        if (ImGui.CollapsingHeader(UiText.Label(T.CatalogDiagnostics, "CatalogDiagnostics")))
        {
            ImGui.TextWrapped(view.Status.Display);
            ImGui.TextWrapped(UiText.Get(T.CatalogDiagnosticsHint));
            changed |= ImGui.Checkbox(UiText.Label(T.CatalogShowUnresolved, "CatalogShowUnresolved"), ref showUnresolved);
            changed |= ImGui.Checkbox(UiText.Label(T.CatalogDetailsOnly, "CatalogDetailsOnly"), ref detailsOnly);
            if (ImGui.Button(UiText.Label(T.ReloadFish, "ReloadCatalog")))
                catalog.RequestLoad(reload: true);
            if (ImGui.Button(UiText.Label(T.CopyCatalogReport, "CopyCatalogReport")))
                ImGui.SetClipboardText(CreateReport(personal, observed));
            if (ImGui.Button(UiText.Label(T.CopyFullCatalogReport, "CopyFullCatalogReport")))
                ImGui.SetClipboardText(CreateReport(personal, observed, full: true));
        }
        if (ImGui.CollapsingHeader(UiText.Label(T.CollectionHelp, "CollectionHelp")))
        {
            ImGui.TextWrapped(UiText.Get(T.CollectionObservedCount, observed.Caught.Count, observed.Uncaught.Count, observed.Status));
            ImGui.TextWrapped(UiText.Get(T.CollectionFilterHint));
            ImGui.TextWrapped(UiText.Get(T.CatalogDetailsPending));
        }
        if (!personal.HasCharacter)
            ImGui.TextWrapped(UiText.Get(T.ManualPlayerRequired));
        if (personal.Error is { } error)
            ImGui.TextWrapped(error.Display);

        // Rebuild only for data or user input changes, never on every frame or scroll step.
        if (changed || refresh || (sort == 2 && lastForecastRevision != view.ForecastRevision))
            UpdateMatches(view, personal, observed, location, changed);
        lastForecastRevision = view.ForecastRevision;
        ImGui.TextUnformatted(UiText.Get(T.CatalogCount, matches.Length, view.Rows.Count));
        if (unresolvedCount > 0)
            ImGui.TextWrapped(UiText.Get(T.CatalogUnresolvedCount, unresolvedCount));
        ImGui.Separator();

        if (ImGui.BeginTable("CatalogLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableNextColumn();
            DrawRows(view, personal, observed, location);
            ImGui.TableNextColumn();
            if (ImGui.BeginChild("CatalogDetail", new Vector2(0, 0), false))
                DrawDetails(view, personal, observed);
            ImGui.EndChild();
            ImGui.EndTable();
        }
    }

    private bool DrawFilters()
    {
        string[] gradeNames = [UiText.Get(T.GradeAll), UiText.Get(T.GradeBigIncludingLegendary), UiText.Get(T.GradeLegendaryOnly)];
        var active = (inLogOnly ? 1 : 0) + (!includeOceanFish ? 1 : 0) + (timeRestrictedOnly ? 1 : 0) +
            (favoritesOnly ? 1 : 0) + (currentRegionOnly ? 1 : 0) + (gradeVisibility != 0 ? 1 : 0);
        var open = ImGui.CollapsingHeader($"{UiText.Get(T.CatalogFiltersHeading, active)}###CatalogFilters");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(UiText.Get(T.CatalogFiltersSummary,
                new UiMessage(timeRestrictedOnly ? T.OptionOn : T.OptionOff), gradeNames[gradeVisibility],
                new UiMessage(favoritesOnly ? T.OptionOn : T.OptionOff), new UiMessage(currentRegionOnly ? T.OptionOn : T.OptionOff),
                new UiMessage(includeOceanFish ? T.OptionOn : T.OptionOff), new UiMessage(inLogOnly ? T.OptionOn : T.OptionOff)));
        if (!open) return false;
        var changed = ImGui.Checkbox(UiText.Label(T.TimeRestrictedOnly, "CatalogTimeRestrictedOnly"), ref timeRestrictedOnly);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.Get(T.TimeRestrictedHint));
        ImGui.SetNextItemWidth(190);
        changed |= ImGui.Combo(UiText.Label(T.GradeFilter, "CatalogGradeFilter"), ref gradeVisibility, gradeNames, gradeNames.Length);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.Get(T.GradeFilterHint));
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogFavoritesOnly, "CatalogFavoritesOnly"), ref favoritesOnly);
        changed |= ImGui.Checkbox(UiText.Label(T.CurrentRegionOnly, "CatalogCurrentRegionOnly"), ref currentRegionOnly);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.Get(T.CurrentRegionHint));
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogIncludeOceanFish, "CatalogIncludeOceanFish"), ref includeOceanFish);
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogInLogOnly, "CatalogInLogOnly"), ref inLogOnly);
        return changed;
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
            var rowHeight = FishRowRenderer.Height;
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
        FishingRegionView location, bool reset)
    {
        var term = search.Trim();
        var localFish = RegionFish(view, location);
        var scope = view.Rows.Where(row => (!inLogOnly || row.IsInLog) &&
            (!currentRegionOnly || localFish.Contains(row.FishParameterId)) &&
            (!geography.HasSelection || geography.Matches(row, location, currentRegionOnly))).ToArray();
        unresolvedCount = scope.Count(row => !row.IsDisplayable);
        var filtered = scope.Where(row => (showUnresolved || row.IsDisplayable) && (!detailsOnly || row.Details is not null) &&
            (includeOceanFish || !row.IsOceanFishing) &&
            FishClassificationFilter.Matches(row.Grade, row.TimeRestriction, timeRestrictedOnly, GradeChoices[gradeVisibility]) &&
            (!favoritesOnly || personal.Favorites.Contains(row.FishParameterId)) &&
            CollectionFilter.Matches(personal.Collection(row.FishParameterId, observed), CollectionChoices[collectionVisibility]) &&
            (term.Length == 0 || row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
             row.Ocean?.Area.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
             row.Spots.Any(spot => spot.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || spot.Zone.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
             row.FishParameterId.ToString(CultureInfo.InvariantCulture) == term));
        matches = (sort switch
        {
            0 => filtered.OrderBy(row => row.Name, NameComparer).ThenBy(row => row.FishParameterId),
            2 => filtered.OrderBy(row => WindowSortKey(view, row.FishParameterId))
                .ThenBy(row => row.Name, NameComparer).ThenBy(row => row.FishParameterId),
            3 => filtered.OrderByDescending(row => personal.Favorites.Contains(row.FishParameterId))
                .ThenBy(row => row.Name, NameComparer).ThenBy(row => row.FishParameterId),
            _ => filtered.OrderBy(row => row.FishParameterId),
        }).ToArray();
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
        var state = personal.Favorites.Contains(fish.FishParameterId)
            ? UiText.Get(T.FavoriteRowState, collection) : collection.Display;
        var usesOtherRegion = locationScoped && rowSpot is not null && fish.Details?.Spot is { } recommended &&
            recommended.TerritoryId != rowSpot.TerritoryId;
        if (rows.Draw(fish, height, selectedId == fish.FishParameterId, state, place, forecast, now, usesOtherRegion))
        {
            selectedId = fish.FishParameterId;
            selectedFish = fish;
        }
    }

    private static (FishingWindowSortGroup Group, DateTimeOffset Time) WindowSortKey(FishCatalogView view, uint id) =>
        FishingWindowOrdering.Key(view.Windows.GetValueOrDefault(id));

    private static IReadOnlySet<uint> RegionFish(FishCatalogView view, FishingRegionView location) =>
        location.State == FishingRegionState.Ready && view.FishByTerritory.TryGetValue(location.TerritoryId, out var fish)
            ? fish : FrozenSet<uint>.Empty;

    private void DrawDetails(FishCatalogView view, FishPreferencesView personal, FishCollectionView observed)
    {
        var selected = selectedFish;
        ImGui.TextWrapped(UiText.Get(T.CatalogDetailCoverage, detailCoverage));
        if (selected is null)
            ImGui.TextWrapped(UiText.Get(T.CatalogDetailPrompt));
        else
        {
            ImGui.TextUnformatted(selected.DisplayName);
            if (selected.Grade is FishGrade.Big or FishGrade.Legendary)
                ImGui.TextUnformatted(UiText.Get(selected.Grade == FishGrade.Legendary ? T.GradeLegendaryLabel : T.GradeBigLabel));
            if (selected.IsOceanFishing) ImGui.TextUnformatted(UiText.Get(T.OceanLabel));
            DrawPreferences(selected, personal, observed);
            ImGui.Separator();
            foreach (var spot in selected.Spots)
            {
                ImGui.TextWrapped(UiText.Get(T.SpotLine, spot.Zone, spot.Name, spot.X, spot.Y));
                if (ImGui.Button(UiText.Label(T.OpenFishingMap, $"OpenFishingMap{spot.Id}")))
                    mapService.Request(spot);
            }
            if (mapService.Result is { } mapResult)
                ImGui.TextWrapped(mapResult.Display);
            if (selected.IsOceanFishing)
                DrawOcean(selected.Ocean);
            else if (selected.Details is not { } details)
                ImGui.TextWrapped(UiText.Get(T.CatalogDetailsMissing));
            else
            {
                ImGui.Separator();
                conditions.Draw(details, personal, view.RelatedDetails);
                ImGui.Separator();
                if (view.Windows.TryGetValue(selected.FishParameterId, out var window))
                    DrawWindow(window);
                else
                    ImGui.TextWrapped(UiText.Get(T.ForecastUnknown));
                ImGui.TextWrapped(UiText.Get(T.ForecastScope));
            }
        }
        ImGui.Separator();
        if (ImGui.CollapsingHeader(UiText.Label(T.DataSourceLabel, "DataSource")))
        {
            ImGui.TextWrapped(UiText.Get(T.DataSourceInfo, FishingData.Dataset.DataVersion, FishingData.Dataset.SourceRevision[..8]));
            ImGui.TextWrapped(UiText.Get(T.SourceCoverageInfo, FishingData.Dataset.Fish.Length, FishingData.Dataset.Spearfishing.Length,
                FishingData.SpearUnknown));
            if (ImGui.Button(UiText.Label(T.CopyDataLicense, "CopyDataLicense")))
                ImGui.SetClipboardText(FishingData.Notices);
        }
    }

    private void DrawPreferences(FishGuideRow fish, FishPreferencesView personal, FishCollectionView observed)
    {
        ImGui.BeginDisabled(!personal.CanEdit || !fish.IsDisplayable);
        var favorite = personal.Favorites.Contains(fish.FishParameterId);
        if (ImGui.Checkbox(UiText.Label(T.FavoriteToggle, $"Favorite{fish.FishParameterId}"), ref favorite))
            preferences.SetFavorite(fish.FishParameterId, favorite);
        ImGui.EndDisabled();
        ImGui.TextWrapped(UiText.Get(T.ManualCollectionLabel, new UiMessage(FishRowRenderer.CollectionKey(personal, observed, fish.FishParameterId))));
        ImGui.TextWrapped(UiText.Get(T.AutomaticObservationLabel, new UiMessage(AutomaticCollectionKey(observed, fish.FishParameterId))));
        ImGui.BeginDisabled(!personal.CanEdit || !personal.HasCharacter || !fish.IsDisplayable);
        if (ImGui.Button(UiText.Label(T.MarkCaught, $"MarkCaught{fish.FishParameterId}")))
            preferences.SetManualCaught(fish.FishParameterId, true, personal.Session);
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label(T.MarkUncaught, $"MarkUncaught{fish.FishParameterId}")))
            preferences.SetManualCaught(fish.FishParameterId, false, personal.Session);
        if (ImGui.Button(UiText.Label(T.ClearManualCollection, $"ClearManual{fish.FishParameterId}")))
            preferences.SetManualCaught(fish.FishParameterId, null, personal.Session);
        ImGui.EndDisabled();
        ImGui.TextWrapped(UiText.Get(T.PreferenceScope));
    }

    private static T AutomaticCollectionKey(FishCollectionView observed, uint fishId) => observed.Collection(fishId) switch
    {
        CollectionStatus.Caught => T.CollectionAutomaticCaught,
        CollectionStatus.Uncaught => T.CollectionAutomaticUncaught,
        _ => T.CollectionUnknown,
    };

    private string CreateReport(FishPreferencesView personal, FishCollectionView observed, bool full = false)
    {
        var report = new StringBuilder();
        void Append(FormattableString line) => report.AppendLine(line.ToString(CultureInfo.InvariantCulture));
        if (full)
            report.AppendLine(catalog.CreateReport(selectedFish?.FishParameterId));
        else
        {
            var view = catalog.View;
            report.AppendLine("DuckbillFisher summary/v5");
            Append($"Plugin={typeof(Plugin).Assembly.GetName().Version}; Rows={view.Rows.Count}; Status={view.Status.Key}");
            Append($"Dataset={FishingData.Dataset.DataVersion}; Details={view.Rows.Count(row => row.Details is not null)}/{FishingData.Dataset.Fish.Length}; BuildMatch={catalog.ConditionBuildMatches}");
            Append($"Forecasts={view.Windows.Count}; OceanAreas={view.Rows.Count(row => row.Ocean is not null)}; SpearStored={FishingData.Dataset.Spearfishing.Length}; SpearUnknown={FishingData.SpearUnknown}");
            Append($"Classification={FishClassificationData.Dataset.DataVersion}; Big={view.Rows.Count(row => row.Grade is FishGrade.Big or FishGrade.Legendary)}; Legendary={view.Rows.Count(row => row.Grade == FishGrade.Legendary)}; Restricted={view.Rows.Count(row => row.TimeRestriction == FishTimeRestriction.Restricted)}");
            if (selectedFish is { } selected)
            {
                Append($"SelectedFish={selected.FishParameterId}; Item={selected.ItemId}; Ocean={selected.IsOceanFishing}; Collection={personal.Collection(selected.FishParameterId, observed)}; Grade={selected.Grade}; Timing={selected.TimeRestriction}");
                var forecast = view.Windows.GetValueOrDefault(selected.FishParameterId);
                Append($"SelectedForecast={forecast?.Kind.ToString() ?? "Unknown"}; ForecastAt={view.ForecastAt:O}; Start={forecast?.Start:O}; End={forecast?.End:O}; CopiedAt={DateTimeOffset.UtcNow:O}");
            }
        }
        report.AppendLine("DuckbillFisher MVP-01/preferences/v5");
        Append($"Ready={personal.HasCharacter}; Editable={personal.CanEdit}; Session={personal.Session}; Favorites={personal.Favorites.Count}");
        Append($"ManualCaught={personal.ManualCaught.Count(pair => pair.Value)}; ManualUncaught={personal.ManualCaught.Count(pair => !pair.Value)}");
        Append($"CollectionFilter={CollectionChoices[collectionVisibility]}; FavoritesOnly={favoritesOnly}; Sort={sort}; Displayed={matches.Length}");
        Append($"InLogOnly={inLogOnly}; IncludeOceanFish={includeOceanFish}; DetailsOnly={detailsOnly}; ShowUnresolved={showUnresolved}");
        var location = region.View;
        Append($"CurrentRegionOnly={currentRegionOnly}; Territory={location.TerritoryId}; RegionState={location.State}; RegionFish={RegionFish(catalog.View, location).Count}");
        Append($"TimeRestrictedOnly={timeRestrictedOnly}; GradeFilter={GradeChoices[gradeVisibility]}");
        Append($"Regions={string.Join(',', geography.Regions.Order().Select(id => id.ToString(CultureInfo.InvariantCulture)))}; Expansions={string.Join(',', geography.Expansions.Order().Select(id => id.ToString(CultureInfo.InvariantCulture)))}; RegionOptions={catalog.View.Geography.Regions.Length}; ExpansionOptions={catalog.View.Geography.Expansions.Length}");
        Append($"CollectionSource=ManualOverrideThenGameObservation; StorageError={personal.Error?.Key.ToString() ?? "none"}");
        report.AppendLine().AppendLine("DuckbillFisher MVP-04/collection/v2");
        Append($"Session={observed.Session}; Caught={observed.Caught.Count}; Eligible={observed.EligibleRows}; GuideVisible={observed.GuideVisible}");
        Append($"Scans={observed.Scans}; RawFalse={observed.RawFalse}; UnknownReads={observed.Unknown}; RegistrationContract={observed.RegistrationContract}");
        Append($"RegistrationMessages={observed.RegistrationMessages}; AppliedRegistrations={observed.AppliedRegistrations}; LastLogItem={observed.LastLogItem}; LastLogFish={observed.LastLogFish}");
        Append($"LastTrigger={observed.LastTrigger}; SampledAtSeconds={observed.SampledAtSeconds?.ToString("F3", CultureInfo.InvariantCulture) ?? "none"}; Status={observed.Status.Key}");
        Append($"Uncaught={observed.Uncaught.Count}; GuideState={observed.GuideState}; GuideEntries={observed.GuideEntries}; Matched={observed.GuideMatched}; Mismatches={observed.GuideMismatches}");
        report.AppendLine("AutomaticUncaught=MatchedGameGuideSnapshot; ObservationsPersisted=False");
        return report.ToString();
    }

    private static void DrawOcean(OceanFishingView? ocean)
    {
        if (ocean is null)
            ImGui.TextWrapped(UiText.Get(T.OceanArea, new UiMessage(T.ConditionUnknown)));
        else
        {
            ImGui.TextWrapped(UiText.Get(T.OceanArea, ocean.Area));
            ImGui.TextWrapped(UiText.Get(T.OceanSpot, ocean.FishingSpot));
            if (ocean.RouteNames.Length > 0)
                ImGui.TextWrapped(UiText.Get(T.OceanRoutes, string.Join(" / ", ocean.RouteNames)));
            // Reporting enum ordinals are not assumed to match the game. Only label
            // a complete three-period route cycle; a partial/unknown cycle stays unknown.
            var times = ocean.VisitTimes.SequenceEqual(new byte[] { 1, 2, 3 })
                ? string.Join(" / ", new[] { T.OceanTimeDay, T.OceanTimeSunset, T.OceanTimeNight }.Select(key => UiText.Get(key)))
                : UiText.Get(T.ConditionUnknown);
            ImGui.TextWrapped(UiText.Get(T.OceanVisitTimes, times));
        }
        ImGui.TextWrapped(UiText.Get(T.OceanScope));
    }

    private static void DrawWindow(FishingWindow window)
    {
        ImGui.TextWrapped(FishRowRenderer.FormatWindow(window, DateTimeOffset.UtcNow));
        if (window.Kind == FishingWindowKind.Upcoming && window.End is { } finish)
            ImGui.TextWrapped(UiText.Get(T.ForecastEnd, finish.ToLocalTime().ToString("MM-dd HH:mm", UiText.Culture)));
    }

}
