using System.Text;
using System.Collections.Frozen;
using DuckbillFisher.Core;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using Lumina.Excel.Sheets;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal enum FishRowResolution
{
    Unknown,
    Resolved,
    ZeroRow,
    EmptyItemReference,
    UnsupportedReference,
    MissingReferencedRow,
    EmptyName,
}

internal sealed record FishGuideRow(uint FishParameterId, uint ItemId, string ItemType, string Name, bool IsInLog)
{
    public uint IconId { get; init; }
    public bool IsOceanFishing { get; init; }
    public FishRowResolution Resolution { get; init; }
    public bool IsDisplayable => Resolution == FishRowResolution.Resolved;
    public IReadOnlyList<FishingSpotView> Spots { get; init; } = [];
    public IReadOnlyList<FishingLocation> Locations { get; init; } = [];
    public FishingDetailView? Details { get; init; }
    public OceanFishingView? Ocean { get; init; }
    public FishGrade Grade { get; init; }
    public FishTimeRestriction TimeRestriction { get; init; }
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? UiText.Get(T.MissingName) : Name;
}

internal sealed record FishCatalogView(IReadOnlyList<FishGuideRow> Rows, UiMessage Status)
{
    public FishingGeography Geography { get; init; } = new([], []);
    public IReadOnlyDictionary<uint, IReadOnlySet<uint>> FishByTerritory { get; init; } = FrozenDictionary<uint, IReadOnlySet<uint>>.Empty;
    public IReadOnlyDictionary<uint, FishingDetailView> RelatedDetails { get; init; } = new Dictionary<uint, FishingDetailView>();
    public IReadOnlyDictionary<uint, FishingWindow> Windows { get; init; } = new Dictionary<uint, FishingWindow>();
    public int ForecastRevision { get; init; }
    public DateTimeOffset? ForecastAt { get; init; }
}

/// <summary>Character-independent sheet data shared by the list and collection diagnostics.</summary>
internal sealed class FishCatalog : IDisposable
{
    private readonly IDataManager dataManager;
    private readonly IFramework framework;
    private readonly object gate = new();
    private FishCatalogView view = new([], new(T.CatalogWaiting));
    private bool requested;
    private bool forceReload;
    private bool disposed;
    private readonly bool conditionBuildMatches;
    private DateTimeOffset nextForecast;
    private DateTimeOffset lastForecast;

    public FishCatalog(IDataManager dataManager, IFramework framework)
    {
        this.dataManager = dataManager;
        this.framework = framework;
        // Static resource/version reads occur at construction, outside Draw.
        try
        {
            var directory = Path.GetDirectoryName(Environment.ProcessPath)!;
            conditionBuildMatches = File.ReadAllText(Path.Combine(directory, "ffxivgame.ver")).Trim() == FishingData.Dataset.GameBuild;
        }
        catch (IOException) { conditionBuildMatches = false; }
        catch (UnauthorizedAccessException) { conditionBuildMatches = false; }
        _ = FishingData.Notices;
        _ = FishClassificationData.ByFishId;
        framework.Update += OnUpdate;
    }

    public FishCatalogView View { get { lock (gate) return view; } }
    public bool ConditionBuildMatches => conditionBuildMatches;

    public void RequestLoad(bool reload = false)
    {
        lock (gate)
        {
            if (disposed || (!reload && view.Rows.Count > 0))
                return;
            requested = true;
            forceReload |= reload;
        }
    }

    private void OnUpdate(IFramework _)
    {
        lock (gate)
        {
            if (disposed)
                return;
            if (requested)
            {
                var reload = forceReload;
                requested = false;
                forceReload = false;
                Read(reload);
            }
            var now = DateTimeOffset.UtcNow;
            if (view.Rows.Count > 0 && (now >= nextForecast || now < lastForecast))
            {
                var windows = new Dictionary<uint, FishingWindow>();
                foreach (var row in view.Rows.Where(row => row.Details is { Spot: not null }))
                {
                    var detail = row.Details!;
                    view.Windows.TryGetValue(row.FishParameterId, out var previous);
                    // Keep absolute windows until a boundary. Counts/countdowns are rendered from UTC.
                    // A bounded search with no result is retried each minute, not for every fish each second.
                    var window = now >= lastForecast && previous is not null ? previous.Kind switch
                    {
                        FishingWindowKind.Always => previous,
                        FishingWindowKind.Upcoming when previous.Start > now => previous,
                        FishingWindowKind.Upcoming when previous.End > now => previous with { Kind = FishingWindowKind.Open },
                        FishingWindowKind.Open when previous.End > now => previous,
                        FishingWindowKind.NotFound when lastForecast.ToUnixTimeSeconds() / 60 == now.ToUnixTimeSeconds() / 60 => previous,
                        _ => null,
                    } : null;
                    windows[row.FishParameterId] = window ?? FishingAvailability.Find(now, detail.Rule, detail.Spot!.WeatherRates);
                }
                var changed = windows.Count != view.Windows.Count || windows.Any(pair =>
                    !view.Windows.TryGetValue(pair.Key, out var previous) || previous != pair.Value);
                view = view with { Windows = windows, ForecastRevision = view.ForecastRevision + (changed ? 1 : 0), ForecastAt = now };
                lastForecast = now;
                nextForecast = now.AddSeconds(1);
            }
        }
    }

    // Called only on Framework.Update, including from the guide diagnostics.
    public IReadOnlyList<FishGuideRow> Read(bool reload)
    {
        if (!framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Fish sheets must be read on Framework.Update.");
        lock (gate)
        {
            if (disposed)
                return [];
            if (!reload && view.Rows.Count > 0)
                return view.Rows;
            try
            {
                var sheet = dataManager.GetExcelSheet<FishParameter>()
                    ?? throw new InvalidOperationException("FishParameter sheet unavailable.");
                var spots = LoadSpots();
                var items = dataManager.GetExcelSheet<Item>()!;
                var weathers = dataManager.GetExcelSheet<Weather>()!;
                var actions = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()!;
                var statuses = dataManager.GetExcelSheet<Status>();
                var oceanSpots = LoadOceanSpots();
                var rules = FishingData.Dataset.Fish.ToDictionary(rule => rule.ItemId);
                var notes = dataManager.GetExcelSheet<FishingNoteInfo>()!.Where(note => note.Item.RowId != 0)
                    .GroupBy(note => note.Item.RowId).Where(group => group.Count() == 1)
                    .ToDictionary(group => group.Key, group => group.Single());
                var rows = new List<FishGuideRow>();
                foreach (var fish in sheet)
                {
                    var item = fish.Item;
                    string? name = null;
                    uint iconId = 0;
                    var resolution = fish.RowId == 0 ? FishRowResolution.ZeroRow
                        : item.RowId == 0 ? FishRowResolution.EmptyItemReference
                        : FishRowResolution.UnsupportedReference;
                    if (fish.RowId != 0 && item.RowId != 0 && item.Is<Item>())
                    {
                        resolution = FishRowResolution.MissingReferencedRow;
                        if (item.GetValueOrDefault<Item>() is { } normal)
                        {
                            name = normal.Name.ExtractText();
                            iconId = normal.Icon;
                            resolution = string.IsNullOrWhiteSpace(name) ? FishRowResolution.EmptyName : FishRowResolution.Resolved;
                        }
                    }
                    else if (fish.RowId != 0 && item.RowId != 0 && item.Is<EventItem>())
                    {
                        resolution = FishRowResolution.MissingReferencedRow;
                        if (item.GetValueOrDefault<EventItem>() is { } eventItem)
                        {
                            name = eventItem.Name.ExtractText();
                            iconId = eventItem.Icon;
                            resolution = string.IsNullOrWhiteSpace(name) ? FishRowResolution.EmptyName : FishRowResolution.Resolved;
                        }
                    }
                    var fishSpots = spots.Values.Where(spot => spot.Items.Contains(item.RowId) ||
                        spot.View.Id == fish.FishingSpot.RowId).Select(spot => spot.View).ToArray();
                    FishingDetailView? details = null;
                    if (conditionBuildMatches && rules.TryGetValue(item.RowId, out var rule) && rule.FishParameterId == fish.RowId &&
                        (rule.FishingSpotId == 0 || (spots.TryGetValue(rule.FishingSpotId, out var candidate) &&
                         (candidate.Items.Contains(item.RowId) || fish.FishingSpot.RowId == rule.FishingSpotId))))
                    {
                        spots.TryGetValue(rule.FishingSpotId, out var sourceSpot);
                        FishingItemView ItemView(uint id)
                        {
                            var value = items.GetRowOrDefault(id);
                            return new(id, value?.Name.ExtractText() ?? "", value?.Icon ?? 0);
                        }
                        FishingConditionIcon WeatherView(uint id)
                        {
                            var value = weathers.GetRowOrDefault(id);
                            return new(id, value?.Name.ExtractText() ?? "", (uint)Math.Max(0, value?.Icon ?? 0));
                        }
                        var baits = rule.BaitSteps.Select(step => step.Select(ItemView).ToArray()).ToArray();
                        var currentWeather = rule.WeatherIds.Select(WeatherView).ToArray();
                        var previousWeather = rule.PreviousWeatherIds.Select(WeatherView).ToArray();
                        var predators = rule.Predators.Select(predator => ItemView(predator.ItemId)).ToArray();
                        // Inspected KR Action rows: player fishing skills; icons come from the sheet.
                        FishingConditionIcon? Skill(uint id)
                        {
                            var action = id == 0 ? null : actions.GetRowOrDefault(id);
                            return action is { IsPlayerAction: true } skill && skill.ClassJob.RowId == 18 &&
                                !string.IsNullOrWhiteSpace(skill.Name.ExtractText()) ? new(id, skill.Name.ExtractText(), skill.Icon) : null;
                        }
                        var hookset = Skill(rule.Hookset switch { "Precision" => 4179u, "Powerful" => 4103u, _ => 0 });
                        var folklore = fish.GatheringSubCategory.RowId != 0 ? ItemView(fish.GatheringSubCategory.Value.Item.RowId) : null;
                        if (baits.SelectMany(step => step).Concat(predators).All(value => !string.IsNullOrWhiteSpace(value.Name)) &&
                            (folklore is null || !string.IsNullOrWhiteSpace(folklore.Name)) &&
                            currentWeather.Concat(previousWeather).All(value => !string.IsNullOrWhiteSpace(value.Name)))
                            details = new(rule, sourceSpot.View, baits, currentWeather, previousWeather, predators, hookset,
                                Skill(rule.SnaggingRequired ? 4100u : 0),
                                Skill(rule.Lure switch { "Modest" => 37595u, "Ambitious" => 37594u, _ => 0 }), folklore)
                            {
                                // KR Status 568 and Action 4105 inspected in the installed client on 2026-10-17.
                                Intuition = rule.Predators.Length > 0 && statuses?.GetRowOrDefault(568) is { } intuition
                                    ? new(568, intuition.Name.ExtractText(), intuition.Icon) : null,
                                FishEyes = Skill(4105),
                            };
                    }
                    var grade = FishGrade.Unknown;
                    var timing = FishTimeRestriction.Unknown;
                    if (conditionBuildMatches && fish.RowId > 0 && fish.IsInLog && fish.OceanStars == 0 &&
                        item.Is<Item>() && resolution == FishRowResolution.Resolved)
                    {
                        if (FishClassificationData.Dataset.GameBuild == FishingData.Dataset.GameBuild &&
                            FishClassificationData.ByFishId.TryGetValue(fish.RowId, out var entry) && entry.ItemId == item.RowId)
                            grade = entry.Grade;
                        else if (details is not null)
                            grade = details.Rule.BigFish ? FishGrade.Big : FishGrade.Regular;
                        if (details?.Rule is { ConditionsKnown: true } known)
                            timing = known.StartHour != 0 || known.EndHour != 24 || known.WeatherIds.Length > 0 || known.PreviousWeatherIds.Length > 0
                                ? FishTimeRestriction.Restricted : FishTimeRestriction.Unrestricted;
                        else if (notes.TryGetValue(item.RowId, out var note) && (note.TimeRestriction == 1 || note.WeatherRestriction == 1))
                            timing = FishTimeRestriction.Restricted;
                        // Raw 0/2 restriction flags do not establish unrestricted conditions.
                    }
                    rows.Add(new(fish.RowId, item.RowId, item.RowType?.Name ?? "Unknown", name ?? "", fish.IsInLog)
                    {
                        IconId = iconId,
                        IsOceanFishing = fish.OceanStars > 0,
                        Resolution = resolution,
                        Spots = fishSpots,
                        Locations = fish.OceanStars == 0 ? fishSpots.Select(spot => spot.Location).Distinct().ToArray() : [],
                        Details = details,
                        Grade = grade,
                        TimeRestriction = timing,
                        Ocean = fish.OceanStars > 0 && oceanSpots.TryGetValue(fish.FishingSpot.RowId, out var ocean) ? ocean : null,
                    });
                }
                if (rows.Count == 0)
                    throw new InvalidDataException("FishParameter sheet is empty.");
                view = new(rows.ToArray(), new(T.CatalogLoaded)) {
                    Geography = FishingGeography.FromRows(rows),
                    FishByTerritory = rows.Where(row => row.IsDisplayable && !row.IsOceanFishing)
                        .SelectMany(row => row.Spots.Where(spot => spot.TerritoryId != 0)
                            .Select(spot => (spot.TerritoryId, row.FishParameterId)))
                        .GroupBy(pair => pair.TerritoryId)
                        .ToFrozenDictionary(group => group.Key,
                            group => (IReadOnlySet<uint>)group.Select(pair => pair.FishParameterId).ToFrozenSet()),
                    RelatedDetails = rows.Where(row => row.Details is not null).ToDictionary(row => row.ItemId, row => row.Details!)
                };
                nextForecast = DateTimeOffset.MinValue;
            }
            catch (Exception ex)
            {
                view = new([], new(T.RowsReadError, ex.GetType().Name));
            }
            return view.Rows;
        }
    }

    private Dictionary<uint, OceanFishingView> LoadOceanSpots()
    {
        var result = new Dictionary<uint, OceanFishingView>();
        var routes = dataManager.GetExcelSheet<IKDRoute>()!.Where(route => route.RowId != 0)
            .Select(route => (Name: route.Name.ExtractText(), Spots: route.Spot.ToArray(), Times: route.Time.ToArray())).ToArray();
        foreach (var spot in dataManager.GetExcelSheet<IKDSpot>()!.Where(spot => spot.RowId != 0))
        {
            var visits = routes.SelectMany(route => route.Spots.Select((target, index) => (target, index))
                .Where(value => value.target.RowId == spot.RowId && value.index < route.Times.Length)
                .Select(value => (route.Name, Time: route.Times[value.index].Value.TimeOfDay))).ToArray();
            foreach (var location in new[] { spot.SpotMain, spot.SpotSub })
                if (location.RowId != 0 && location.Value is { } fishingSpot)
                    result[location.RowId] = new(spot.PlaceName.Value.Name.ExtractText(), fishingSpot.PlaceName.Value.Name.ExtractText(),
                        visits.Select(visit => visit.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToArray(),
                        visits.Select(visit => visit.Time).Where(time => time is >= 1 and <= 3).Distinct().Order().ToArray());
        }
        return result;
    }

    private Dictionary<uint, (FishingSpotView View, HashSet<uint> Items)> LoadSpots()
    {
        var result = new Dictionary<uint, (FishingSpotView, HashSet<uint>)>();
        var expansions = dataManager.GetExcelSheet<ExVersion>();
        foreach (var spot in dataManager.GetExcelSheet<FishingSpot>()!)
        {
            if (spot.TerritoryType.RowId == 0 || spot.PlaceNameMain.RowId != 0)
                continue;
            var territory = spot.TerritoryType.Value;
            // ExVersion row 0 is ARR, not an absent reference.
            var expansion = expansions?.GetRowOrDefault(territory.ExVersion.RowId);
            if (territory.Map.RowId == 0)
                continue;
            var map = territory.Map.Value;
            if (map.SizeFactor == 0 || map.TerritoryType.RowId != territory.RowId)
                continue;
            // FishingSpot X/Z are image coordinates; MapLinkPayload expects readable map coordinates.
            // The development importer cross-checks these against the pinned source coordinates.
            var x = 1f + spot.X * 41f / 2048f / (map.SizeFactor / 100f);
            var y = 1f + spot.Z * 41f / 2048f / (map.SizeFactor / 100f);
            var weather = territory.WeatherRate.Value;
            var rates = weather.Weather.Select((value, index) => new WeatherChance(value.RowId, weather.Rate[index]))
                .Where(rate => rate.Chance > 0).ToArray();
            result[spot.RowId] = (new(spot.RowId, spot.PlaceName.Value.Name.ExtractText(),
                territory.PlaceName.Value.Name.ExtractText(), territory.RowId, map.RowId, x, y, rates)
                { ExpansionId = expansion?.RowId, ExpansionName = expansion?.Name.ExtractText() ?? "" },
                spot.Item.Select(value => value.RowId).Where(id => id != 0).ToHashSet());
        }
        return result;
    }

    public string CreateReport(uint? selectedFishParameterId = null)
    {
        var snapshot = View;
        var unresolved = snapshot.Rows.Where(row => !row.IsDisplayable).ToArray();
        var report = new StringBuilder();
        void Append(FormattableString line) => report.AppendLine(FormattableString.Invariant(line));
        report.AppendLine("DuckbillFisher MVP-01/catalog/v5");
        Append($"Plugin: {typeof(Plugin).Assembly.GetName().Version}");
        Append($"Rows: {snapshot.Rows.Count}; displayable: {snapshot.Rows.Count - unresolved.Length}; unresolved: {unresolved.Length}; unresolved in log: {unresolved.Count(row => row.IsInLog)}");
        report.AppendLine($"Status: [{snapshot.Status.Key}] {snapshot.Status.Diagnostic}");
        Append($"Dataset: {FishingData.Dataset.DataVersion}; buildMatch={conditionBuildMatches}; detailRows={snapshot.Rows.Count(row => row.Details is not null)}; rowsWithSpots={snapshot.Rows.Count(row => row.Spots.Count > 0)}");
        Append($"Coverage: oceanAreas={snapshot.Rows.Count(row => row.Ocean is not null)}; spearStored={FishingData.Dataset.Spearfishing.Length}; spearConditionsUnknown={FishingData.SpearUnknown}");
        Append($"Classification: dataset={FishClassificationData.Dataset.DataVersion}; bigIncludingLegendary={snapshot.Rows.Count(row => row.Grade is FishGrade.Big or FishGrade.Legendary)}; legendary={snapshot.Rows.Count(row => row.Grade == FishGrade.Legendary)}; restricted={snapshot.Rows.Count(row => row.TimeRestriction == FishTimeRestriction.Restricted)}");
        Append($"ForecastAt: {snapshot.ForecastAt:O}; horizonPeriods={FishingAvailability.SearchPeriods}; normal time/weather only; no personal readiness inference");
        report.AppendLine($"Source: {FishingData.Dataset.SourceRepository}/tree/{FishingData.Dataset.SourceRevision}");
        report.AppendLine("Detail samples: up to 20, selected fish first, then ordered by FishParameter ID.");
        foreach (var row in snapshot.Rows.Where(row => row.Details is not null)
            .OrderByDescending(row => row.FishParameterId == selectedFishParameterId).ThenBy(row => row.FishParameterId).Take(20))
        {
            var detail = row.Details!;
            snapshot.Windows.TryGetValue(row.FishParameterId, out var window);
            Append($"Detail: FishParameter={row.FishParameterId}; Item={row.ItemId}; spot={detail.Spot?.Id}; territory={detail.Spot?.TerritoryId}; map={detail.Spot?.MapId}; x={detail.Spot?.X:F4}; y={detail.Spot?.Y:F4}; window={window?.Kind}; start={window?.Start:O}; end={window?.End:O}");
        }
        foreach (var group in unresolved.GroupBy(row => row.Resolution).OrderBy(group => group.Key))
            Append($"Resolution: {group.Key}; count: {group.Count()}");
        report.AppendLine("Unresolved row details: up to 80, ordered by FishParameter ID. No character collection data.");
        foreach (var row in unresolved.OrderBy(row => row.FishParameterId).Take(80))
            Append($"FishParameter={row.FishParameterId}; {row.ItemType}={row.ItemId}; IsInLog={row.IsInLog}; resolution={row.Resolution}");
        return report.ToString();
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            framework.Update -= OnUpdate;
            requested = false;
        }
    }
}
