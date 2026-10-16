using System.Globalization;
using System.Collections.Frozen;
using System.Text;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Displays cached static rows, session-local collection observations, and manual overrides.</summary>
internal sealed class FishListPanel(FishCatalog catalog, ITextureProvider textures, FishingMapService mapService,
    FishPreferences preferences, FishCollectionObserver collection, FishingConditionPanel conditions)
{
    private static readonly StringComparer NameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("ko-KR"), true);
    private IReadOnlyList<FishGuideRow>? lastRows;
    private FishGuideRow[] matches = [];
    private string search = "";
    private bool inLogOnly = true;
    private bool includeOceanFish = true;
    private bool showUnresolved;
    private int unresolvedCount;
    private bool requested;
    private bool resetScroll;
    private int sort;
    private uint? selectedId;
    private FishGuideRow? selectedFish;
    private int detailCoverage;
    private bool detailsOnly;
    private int lastForecastRevision;
    private int lastPreferenceRevision;
    private int lastPreferenceSession;
    private int lastCollectionRevision;
    private bool favoritesOnly;
    private int collectionVisibility;

    public void Draw()
    {
        if (!requested)
        {
            catalog.RequestLoad();
            requested = true;
        }
        var view = catalog.View;
        var personal = preferences.View;
        var observed = collection.View;
        if (observed.Session != personal.Session)
            observed = new(observed.Revision, personal.Session, FrozenSet<uint>.Empty, new(T.CollectionWaiting));
        var changed = !ReferenceEquals(lastRows, view.Rows) || lastPreferenceRevision != personal.Revision ||
            lastCollectionRevision != observed.Revision;
        lastCollectionRevision = observed.Revision;
        if (lastPreferenceSession != personal.Session)
        {
            selectedId = null;
            selectedFish = null;
        }
        lastPreferenceSession = personal.Session;
        lastPreferenceRevision = personal.Revision;
        if (changed)
        {
            detailCoverage = view.Rows.Count(row => row.Details is not null);
            selectedFish = selectedId is { } selected ? view.Rows.FirstOrDefault(row => row.FishParameterId == selected) : null;
        }
        lastRows = view.Rows;
        ImGui.TextWrapped(view.Status.Display);
        ImGui.SetNextItemWidth(280);
        changed |= ImGui.InputText(UiText.Label(T.FishSearch, "CatalogSearch"), ref search, 100);
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogInLogOnly, "CatalogInLogOnly"), ref inLogOnly);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        string[] sortNames = [UiText.Get(T.CatalogSortName), UiText.Get(T.CatalogSortId), UiText.Get(T.CatalogSortWindow), UiText.Get(T.CatalogSortFavorite)];
        changed |= ImGui.Combo(UiText.Label(T.CatalogSort, "CatalogSort"), ref sort, sortNames, sortNames.Length);
        if (ImGui.Button(UiText.Label(T.ReloadFish, "ReloadCatalog")))
            catalog.RequestLoad(reload: true);
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label(T.CopyCatalogReport, "CopyCatalogReport")))
            ImGui.SetClipboardText(CreateReport(personal, observed));
        if (ImGui.CollapsingHeader(UiText.Label(T.CatalogDiagnostics, "CatalogDiagnostics")))
        {
            ImGui.TextWrapped(UiText.Get(T.CatalogDiagnosticsHint));
            if (ImGui.Button(UiText.Label(T.CopyFullCatalogReport, "CopyFullCatalogReport")))
                ImGui.SetClipboardText(CreateReport(personal, observed, full: true));
        }
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogIncludeOceanFish, "CatalogIncludeOceanFish"), ref includeOceanFish);
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogShowUnresolved, "CatalogShowUnresolved"), ref showUnresolved);
        ImGui.SameLine();
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogDetailsOnly, "CatalogDetailsOnly"), ref detailsOnly);
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogFavoritesOnly, "CatalogFavoritesOnly"), ref favoritesOnly);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180);
        string[] collectionNames = [UiText.Get(T.CollectionAll), UiText.Get(T.CollectionHideCaught),
            UiText.Get(T.CollectionUncaughtOnly), UiText.Get(T.CollectionCaughtOnly), UiText.Get(T.CollectionUnknownOnly)];
        changed |= ImGui.Combo(UiText.Label(T.CollectionVisibility, "CatalogCollectionVisibility"), ref collectionVisibility,
            collectionNames, collectionNames.Length);
        ImGui.TextWrapped(UiText.Get(T.CollectionObservedCount, observed.Caught.Count, observed.Uncaught.Count, observed.Status));
        if (ImGui.CollapsingHeader(UiText.Label(T.CollectionHelp, "CollectionHelp")))
            ImGui.TextWrapped(UiText.Get(T.CollectionFilterHint));
        if (!personal.HasCharacter)
            ImGui.TextWrapped(UiText.Get(T.ManualPlayerRequired));
        if (personal.Error is { } error)
            ImGui.TextWrapped(error.Display);

        // Rebuild only for data or user input changes, never on every frame or scroll step.
        if (changed || (sort == 2 && lastForecastRevision != view.ForecastRevision))
            UpdateMatches(view, personal, observed, changed);
        lastForecastRevision = view.ForecastRevision;
        ImGui.TextUnformatted(UiText.Get(T.CatalogCount, matches.Length, view.Rows.Count));
        if (unresolvedCount > 0)
            ImGui.TextWrapped(UiText.Get(T.CatalogUnresolvedCount, unresolvedCount));
        ImGui.TextWrapped(UiText.Get(T.CatalogDetailsPending));
        ImGui.Separator();

        if (ImGui.BeginTable("CatalogLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableNextColumn();
            DrawRows(personal, observed);
            ImGui.TableNextColumn();
            if (ImGui.BeginChild("CatalogDetail", new Vector2(0, 0), false))
                DrawDetails(view, personal, observed);
            ImGui.EndChild();
            ImGui.EndTable();
        }
    }

    private void DrawRows(FishPreferencesView personal, FishCollectionView observed)
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
            var rowHeight = ImGui.GetTextLineHeightWithSpacing() * 3 + 8;
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                clipper.Begin(matches.Length, rowHeight + ImGui.GetStyle().ItemSpacing.Y);
                while (clipper.Step())
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        DrawRow(matches[i], rowHeight, personal, observed);
            }
            finally
            {
                clipper.Destroy();
            }
        }
        ImGui.EndChild();
    }

    private void UpdateMatches(FishCatalogView view, FishPreferencesView personal, FishCollectionView observed, bool reset)
    {
        var term = search.Trim();
        var scope = view.Rows.Where(row => !inLogOnly || row.IsInLog).ToArray();
        unresolvedCount = scope.Count(row => !row.IsDisplayable);
        var filtered = scope.Where(row => (showUnresolved || row.IsDisplayable) && (!detailsOnly || row.Details is not null) &&
            (includeOceanFish || !row.IsOceanFishing) &&
            (!favoritesOnly || personal.Favorites.Contains(row.FishParameterId)) &&
            CollectionFilter.Matches(personal.Collection(row.FishParameterId, observed), (CollectionVisibility)collectionVisibility) &&
            (term.Length == 0 || row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
             row.Spots.Any(spot => spot.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || spot.Zone.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
             row.FishParameterId.ToString(CultureInfo.InvariantCulture) == term));
        matches = (sort switch
        {
            0 => filtered.OrderBy(row => row.Name, NameComparer).ThenBy(row => row.FishParameterId),
            2 => filtered.OrderBy(row => WindowSortKey(view, row.FishParameterId)).ThenBy(row => row.FishParameterId),
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

    private void DrawRow(FishGuideRow fish, float height, FishPreferencesView personal, FishCollectionView observed)
    {
        ImGui.PushID((int)fish.FishParameterId);
        var start = ImGui.GetCursorPos();
        if (ImGui.Selectable("###CatalogRow", selectedId == fish.FishParameterId,
                ImGuiSelectableFlags.None, new Vector2(ImGui.GetContentRegionAvail().X, height)))
        {
            selectedId = fish.FishParameterId;
            selectedFish = fish;
        }
        var next = ImGui.GetCursorPos();
        var iconSize = height - 8;
        ImGui.SetCursorPos(start + new Vector2(4, 4));
        // Shared immediate textures are owned by Dalamud; do not dispose the borrowed wrap.
        if (fish.IconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(fish.IconId), out var icon)
            && icon.TryGetWrap(out var wrap, out _))
            ImGui.Image(wrap.Handle, new Vector2(iconSize));
        else
            ImGui.Dummy(new Vector2(iconSize));
        ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4));
        ImGui.TextUnformatted(fish.DisplayName);
        ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4 + ImGui.GetTextLineHeightWithSpacing()));
        if (fish.Spots.FirstOrDefault() is { } spot)
            ImGui.TextUnformatted(spot.Name);
        else
            ImGui.TextDisabled(UiText.Get(fish.IsDisplayable ? T.CatalogRowInfo : T.CatalogUnresolvedRow, fish.FishParameterId));
        ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4 + ImGui.GetTextLineHeightWithSpacing() * 2));
        var collection = new UiMessage(CollectionKey(personal, observed, fish.FishParameterId));
        ImGui.TextDisabled(personal.Favorites.Contains(fish.FishParameterId)
            ? UiText.Get(T.FavoriteRowState, collection) : collection.Display);
        ImGui.SetCursorPos(next);
        ImGui.PopID();
    }

    private static DateTimeOffset WindowSortKey(FishCatalogView view, uint id) =>
        view.Windows.TryGetValue(id, out var window) ? window.Kind switch
        {
            FishingWindowKind.Open or FishingWindowKind.Always => DateTimeOffset.MinValue,
            FishingWindowKind.Upcoming => window.Start ?? DateTimeOffset.MaxValue,
            _ => DateTimeOffset.MaxValue,
        } : DateTimeOffset.MaxValue;

    private void DrawDetails(FishCatalogView view, FishPreferencesView personal, FishCollectionView observed)
    {
        var selected = selectedFish;
        ImGui.TextWrapped(UiText.Get(T.CatalogDetailCoverage, detailCoverage));
        if (selected is null)
            ImGui.TextWrapped(UiText.Get(T.CatalogDetailPrompt));
        else
        {
            ImGui.TextUnformatted(selected.DisplayName);
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
            if (selected.Details is not { } details)
                ImGui.TextWrapped(UiText.Get(T.CatalogDetailsMissing));
            else
            {
                ImGui.Separator();
                conditions.Draw(details, personal);
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
        ImGui.TextWrapped(UiText.Get(T.ManualCollectionLabel, new UiMessage(CollectionKey(personal, observed, fish.FishParameterId))));
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

    private static T CollectionKey(FishPreferencesView personal, FishCollectionView observed, uint fishId) =>
        personal.ManualCaught.TryGetValue(fishId, out var caught)
            ? caught ? T.CollectionManualCaught : T.CollectionManualUncaught
            : AutomaticCollectionKey(observed, fishId);

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
            report.AppendLine("DuckbillFisher summary/v2");
            Append($"Plugin={typeof(Plugin).Assembly.GetName().Version}; Rows={view.Rows.Count}; Status={view.Status.Key}");
            Append($"Dataset={FishingData.Dataset.DataVersion}; Details={view.Rows.Count(row => row.Details is not null)}/{FishingData.Dataset.Fish.Length}; BuildMatch={catalog.ConditionBuildMatches}");
            if (selectedFish is { } selected)
                Append($"SelectedFish={selected.FishParameterId}; Item={selected.ItemId}; Ocean={selected.IsOceanFishing}; Collection={personal.Collection(selected.FishParameterId, observed)}");
        }
        report.AppendLine("DuckbillFisher MVP-01/preferences/v2");
        Append($"Ready={personal.HasCharacter}; Editable={personal.CanEdit}; Session={personal.Session}; Favorites={personal.Favorites.Count}");
        Append($"ManualCaught={personal.ManualCaught.Count(pair => pair.Value)}; ManualUncaught={personal.ManualCaught.Count(pair => !pair.Value)}");
        Append($"CollectionFilter={(CollectionVisibility)collectionVisibility}; FavoritesOnly={favoritesOnly}; Sort={sort}; Displayed={matches.Length}");
        Append($"InLogOnly={inLogOnly}; IncludeOceanFish={includeOceanFish}; DetailsOnly={detailsOnly}; ShowUnresolved={showUnresolved}");
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

    private static void DrawWindow(FishingWindow window)
    {
        var now = DateTimeOffset.UtcNow;
        string Remaining(DateTimeOffset time)
        {
            var duration = time > now ? time - now : TimeSpan.Zero;
            return UiText.Get(T.DurationShort, Math.Floor(duration.TotalHours), duration.Minutes, duration.Seconds);
        }
        var message = window.Kind switch
        {
            FishingWindowKind.Always => UiText.Get(T.ForecastAlways),
            FishingWindowKind.Open when window.End is { } end => UiText.Get(T.ForecastOpen, Remaining(end)),
            FishingWindowKind.Open => UiText.Get(T.ForecastOpenNoEnd),
            FishingWindowKind.Upcoming when window.Start is { } start => UiText.Get(T.ForecastUpcoming,
                Remaining(start), start.ToLocalTime().ToString("MM-dd HH:mm", UiText.Culture)),
            FishingWindowKind.NotFound => UiText.Get(T.ForecastNotFound),
            _ => UiText.Get(T.ForecastUnknown),
        };
        ImGui.TextWrapped(message);
        if (window.Kind == FishingWindowKind.Upcoming && window.End is { } finish)
            ImGui.TextWrapped(UiText.Get(T.ForecastEnd, finish.ToLocalTime().ToString("MM-dd HH:mm", UiText.Culture)));
    }
}
