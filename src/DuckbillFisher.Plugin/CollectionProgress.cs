using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record CollectionProgressCount(int Total, int Caught, int Unknown, int Manual)
{
    public int Uncaught => Total - Caught - Unknown;
    public bool Complete => Total > 0 && Caught == Total;
}

internal sealed record CollectionProgressCategories(CollectionProgressCount All, CollectionProgressCount Big,
    CollectionProgressCount Legendary);
internal sealed record CollectionProgressGroup(uint? Id, string Name, CollectionProgressCategories Counts,
    CollectionProgressGroup[] Regions);
internal sealed record CollectionProgressView(int Session, CollectionProgressCategories Overall,
    CollectionProgressCategories Regular, CollectionProgressCategories Ocean, CollectionProgressGroup[] Expansions,
    int UnknownGrades, CollectionProgressGroup[] OceanAreas);
internal enum CollectionProgressArea { Expansion, Region, Ocean }
internal sealed record CollectionProgressSelection(CollectionProgressArea Area, uint? ExpansionId, uint? RegionId, FishGradeVisibility Grade);
internal sealed record CollectionProgressFishView(int Session, CollectionProgressSelection? Selection, UiMessage? Name,
    FishGuideRow[] AllFish, FishGuideRow[] RemainingFish);

/// <summary>Builds unique-fish geography groups and character counts on Framework.Update, never in Draw.</summary>
internal sealed class CollectionProgress : IDisposable
{
    private static readonly CollectionProgressCount EmptyCount = new(0, 0, 0, 0);
    private static readonly CollectionProgressCategories EmptyCategories = new(EmptyCount, EmptyCount, EmptyCount);
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly IFramework framework;
    private readonly object gate = new();
    private CollectionProgressView view = new(-1, EmptyCategories, EmptyCategories, EmptyCategories, [], 0, []);
    private CollectionProgressFishView fishView = new(-1, null, null, [], []);
    private CollectionProgressSelection? selection;
    private (int Session, CollectionProgressSelection Selection)? pendingSelection;
    private IReadOnlyList<FishGuideRow>? lastRows;
    private FishGuideRow[] all = [], regular = [], ocean = [];
    private Scope[] expansions = [];
    private Scope[] oceanAreas = [];
    private IReadOnlyDictionary<uint, bool>? lastManual;
    private int lastSession = -1, lastCollection = -1;
    private bool disposed;

    public CollectionProgress(FishCatalog catalog, FishPreferences preferences, FishCollectionObserver collection, IFramework framework)
    {
        this.catalog = catalog; this.preferences = preferences; this.collection = collection; this.framework = framework;
        catalog.RequestLoad();
        framework.Update += Update;
    }

    public CollectionProgressView View { get { lock (gate) return view; } }
    public CollectionProgressFishView FishView { get { lock (gate) return fishView; } }

    public void Select(CollectionProgressSelection target, int session)
    {
        lock (gate)
            if (!disposed) pendingSelection = (session, target);
    }

    public void ClearSelection()
    {
        lock (gate)
        {
            pendingSelection = null;
            selection = null;
            fishView = new(-1, null, null, [], []);
        }
    }

    private void Update(IFramework _)
    {
        lock (gate)
        {
            if (disposed) return;
            var data = catalog.View;
            var personal = preferences.View;
            var observed = collection.View;
            var selectionChanged = false;
            if (pendingSelection is { } pending)
            {
                pendingSelection = null;
                if (pending.Session == personal.Session && selection != pending.Selection)
                {
                    selection = pending.Selection;
                    selectionChanged = true;
                }
            }
            if (ReferenceEquals(lastRows, data.Rows) && lastSession == personal.Session &&
                ReferenceEquals(lastManual, personal.ManualCaught) && lastCollection == observed.Revision)
            {
                if (selectionChanged) UpdateFishView(personal, observed);
                return;
            }
            if (!ReferenceEquals(lastRows, data.Rows))
            {
                all = data.Rows.Where(row => row.IsDisplayable && row.IsInLog).DistinctBy(row => row.FishParameterId).ToArray();
                regular = all.Where(row => !row.IsOceanFishing).ToArray();
                ocean = all.Where(row => row.IsOceanFishing).ToArray();
                oceanAreas = ocean.GroupBy(row => row.Ocean is { AreaId: > 0 } area ? (uint?)area.AreaId : null)
                    .OrderBy(group => group.Key.HasValue ? 0 : 1).ThenBy(group => group.Key)
                    .Select(group => new Scope(group.Key, group.First().Ocean?.Area ?? "", group.ToArray(), [])).ToArray();
                expansions = regular.SelectMany(Memberships).GroupBy(member => member.ExpansionId)
                    .OrderBy(group => group.Key.HasValue ? 0 : 1).ThenBy(group => group.Key)
                    .Select(group => new Scope(group.Key, group.First().ExpansionName,
                        group.Select(member => member.Fish).DistinctBy(row => row.FishParameterId).ToArray(),
                        group.GroupBy(member => member.TerritoryId)
                            .OrderBy(region => region.Key.HasValue ? 0 : 1)
                            .ThenBy(region => region.First().RegionName, StringComparer.OrdinalIgnoreCase)
                            .Select(region => new Scope(region.Key, region.First().RegionName,
                                region.Select(member => member.Fish).DistinctBy(row => row.FishParameterId).ToArray(), [])).ToArray()))
                    .ToArray();
            }
            // Resolve once per fish; all higher-level counts reuse this session's managed values.
            var states = all.ToDictionary(row => row.FishParameterId, row => personal.Collection(row.FishParameterId, observed));
            CollectionProgressCount Count(IEnumerable<FishGuideRow> rows)
            {
                int total = 0, caught = 0, unknown = 0, manual = 0;
                foreach (var row in rows)
                {
                    total++;
                    var state = states[row.FishParameterId];
                    if (state == CollectionStatus.Caught) caught++;
                    if (state == CollectionStatus.Unknown) unknown++;
                    if (personal.ManualCaught.ContainsKey(row.FishParameterId)) manual++;
                }
                return new(total, caught, unknown, manual);
            }
            CollectionProgressCategories Categories(FishGuideRow[] rows) => new(Count(rows),
                Count(rows.Where(row => row.Grade is FishGrade.Big or FishGrade.Legendary)),
                Count(rows.Where(row => row.Grade == FishGrade.Legendary)));
            CollectionProgressGroup Group(Scope scope) => new(scope.Id, scope.Name, Categories(scope.Rows), scope.Regions.Select(Group).ToArray());
            view = new(personal.Session, Categories(all), Categories(regular), Categories(ocean), expansions.Select(Group).ToArray(),
                regular.Count(row => row.Grade == FishGrade.Unknown),
                oceanAreas.Select(Group).ToArray());
            UpdateFishView(personal, observed);
            lastRows = data.Rows; lastSession = personal.Session;
            lastManual = personal.ManualCaught; lastCollection = observed.Revision;
        }
    }

    private void UpdateFishView(FishPreferencesView personal, FishCollectionView observed)
    {
        var chosen = SelectedScope();
        var selectedRows = chosen.Rows.Where(row => selection is { } target &&
            FishClassificationFilter.Matches(row.Grade, row.TimeRestriction, false, target.Grade))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.FishParameterId).ToArray();
        fishView = new(personal.Session, selection, chosen.Name, selectedRows,
            selectedRows.Where(row => personal.Collection(row.FishParameterId, observed) != CollectionStatus.Caught).ToArray());
    }

    private (FishGuideRow[] Rows, UiMessage? Name) SelectedScope()
    {
        if (selection is not { } target) return ([], null);
        if (target.Area == CollectionProgressArea.Ocean)
        {
            var area = oceanAreas.FirstOrDefault(group => group.Id == target.RegionId);
            return area is null ? ([], null) : (area.Rows, new(T.CollectionProgressScopeName,
                string.IsNullOrWhiteSpace(area.Name) ? new UiMessage(T.CollectionProgressUnknownArea) : area.Name));
        }
        var expansion = expansions.FirstOrDefault(group => group.Id == target.ExpansionId);
        if (expansion is null) return ([], null);
        var expansionName = string.IsNullOrWhiteSpace(expansion.Name) ? (object)new UiMessage(T.CollectionProgressUnknownArea) : expansion.Name;
        if (target.Area == CollectionProgressArea.Expansion)
            return (expansion.Rows, new(T.CollectionProgressScopeName, expansionName));
        var region = expansion.Regions.FirstOrDefault(group => group.Id == target.RegionId);
        return region is null ? ([], null) : (region.Rows, new(T.CollectionProgressRegionName, expansionName,
            string.IsNullOrWhiteSpace(region.Name) ? new UiMessage(T.CollectionProgressUnknownArea) : region.Name));
    }

    private static IEnumerable<Membership> Memberships(FishGuideRow row)
    {
        if (row.Spots.Count == 0)
        {
            yield return new(row, null, "", null, "");
            yield break;
        }
        foreach (var spot in row.Spots.DistinctBy(spot => (spot.ExpansionId, spot.TerritoryId)))
            yield return new(row, spot.ExpansionId, spot.ExpansionName,
                spot.TerritoryId != 0 ? spot.TerritoryId : null, spot.Zone);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            framework.Update -= Update;
            pendingSelection = null;
            view = new(-1, EmptyCategories, EmptyCategories, EmptyCategories, [], 0, []);
            fishView = new(-1, null, null, [], []);
        }
    }

    private sealed record Scope(uint? Id, string Name, FishGuideRow[] Rows, Scope[] Regions);
    private sealed record Membership(FishGuideRow Fish, uint? ExpansionId, string ExpansionName, uint? TerritoryId, string RegionName);
}
