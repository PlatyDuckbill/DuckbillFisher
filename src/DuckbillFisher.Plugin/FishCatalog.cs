using System.Text;
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
    public FishRowResolution Resolution { get; init; }
    public bool IsDisplayable => Resolution == FishRowResolution.Resolved;
    public IReadOnlyList<FishingSpotView> Spots { get; init; } = [];
    public FishingDetailView? Details { get; init; }
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? UiText.Get(T.MissingName) : Name;
}

internal sealed record FishCatalogView(IReadOnlyList<FishGuideRow> Rows, UiMessage Status)
{
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
        framework.Update += OnUpdate;
    }

    public FishCatalogView View { get { lock (gate) return view; } }

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
                var windows = view.Rows.Where(row => row.Details is not null).ToDictionary(
                    row => row.FishParameterId,
                    row => FishingAvailability.Find(now, row.Details!.Rule, row.Details.Spot.WeatherRates));
                view = view with { Windows = windows, ForecastRevision = view.ForecastRevision + 1, ForecastAt = now };
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
                var rules = FishingData.Dataset.Fish.ToDictionary(rule => rule.ItemId);
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
                    if (conditionBuildMatches && rules.TryGetValue(item.RowId, out var rule) &&
                        rule.FishParameterId == fish.RowId && spots.TryGetValue(rule.FishingSpotId, out var sourceSpot) &&
                        (sourceSpot.Items.Contains(item.RowId) || fish.FishingSpot.RowId == rule.FishingSpotId))
                    {
                        string ItemName(uint id) => items.GetRowOrDefault(id)?.Name.ExtractText() ?? "";
                        string WeatherName(uint id) => weathers.GetRowOrDefault(id)?.Name.ExtractText() ?? "";
                        var baits = rule.BaitPath.Select(ItemName).ToArray();
                        var currentWeather = rule.WeatherIds.Select(WeatherName).ToArray();
                        var previousWeather = rule.PreviousWeatherIds.Select(WeatherName).ToArray();
                        var predators = rule.Predators.Select(predator => ItemName(predator.ItemId)).ToArray();
                        if (baits.Concat(currentWeather).Concat(previousWeather).Concat(predators).All(value => !string.IsNullOrWhiteSpace(value)))
                            details = new(rule, sourceSpot.View, baits, currentWeather, previousWeather, predators);
                    }
                    rows.Add(new(fish.RowId, item.RowId, item.RowType?.Name ?? "Unknown", name ?? "", fish.IsInLog)
                    {
                        IconId = iconId,
                        Resolution = resolution,
                        Spots = fishSpots,
                        Details = details,
                    });
                }
                if (rows.Count == 0)
                    throw new InvalidDataException("FishParameter sheet is empty.");
                view = new(rows.ToArray(), new(T.CatalogLoaded));
                nextForecast = DateTimeOffset.MinValue;
            }
            catch (Exception ex)
            {
                view = new([], new(T.RowsReadError, ex.GetType().Name));
            }
            return view.Rows;
        }
    }

    private Dictionary<uint, (FishingSpotView View, HashSet<uint> Items)> LoadSpots()
    {
        var result = new Dictionary<uint, (FishingSpotView, HashSet<uint>)>();
        foreach (var spot in dataManager.GetExcelSheet<FishingSpot>()!)
        {
            if (spot.TerritoryType.RowId == 0 || spot.PlaceNameMain.RowId != 0)
                continue;
            var territory = spot.TerritoryType.Value;
            if (territory.Map.RowId == 0)
                continue;
            var map = territory.Map.Value;
            if (map.SizeFactor == 0 || map.TerritoryType.RowId != territory.RowId)
                continue;
            // FishingSpot X/Z are image coordinates; MapLinkPayload expects readable map coordinates.
            // Cross-checked against the pinned Fish Tracker coordinates for all ten initial fish.
            var x = 1f + spot.X * 41f / 2048f / (map.SizeFactor / 100f);
            var y = 1f + spot.Z * 41f / 2048f / (map.SizeFactor / 100f);
            var weather = territory.WeatherRate.Value;
            var rates = weather.Weather.Select((value, index) => new WeatherChance(value.RowId, weather.Rate[index]))
                .Where(rate => rate.Chance > 0).ToArray();
            result[spot.RowId] = (new(spot.RowId, spot.PlaceName.Value.Name.ExtractText(),
                territory.PlaceName.Value.Name.ExtractText(), territory.RowId, map.RowId, x, y, rates),
                spot.Item.Select(value => value.RowId).Where(id => id != 0).ToHashSet());
        }
        return result;
    }

    public string CreateReport()
    {
        var snapshot = View;
        var unresolved = snapshot.Rows.Where(row => !row.IsDisplayable).ToArray();
        var report = new StringBuilder();
        void Append(FormattableString line) => report.AppendLine(FormattableString.Invariant(line));
        report.AppendLine("DuckbillFisher MVP-01/catalog/v2");
        Append($"Plugin: {typeof(Plugin).Assembly.GetName().Version}");
        Append($"Rows: {snapshot.Rows.Count}; displayable: {snapshot.Rows.Count - unresolved.Length}; unresolved: {unresolved.Length}; unresolved in log: {unresolved.Count(row => row.IsInLog)}");
        report.AppendLine($"Status: [{snapshot.Status.Key}] {snapshot.Status.Diagnostic}");
        Append($"Dataset: {FishingData.Dataset.DataVersion}; buildMatch={conditionBuildMatches}; detailRows={snapshot.Rows.Count(row => row.Details is not null)}; rowsWithSpots={snapshot.Rows.Count(row => row.Spots.Count > 0)}");
        Append($"ForecastAt: {snapshot.ForecastAt:O}; horizonPeriods={FishingAvailability.SearchPeriods}; normal time/weather only; no personal readiness inference");
        report.AppendLine($"Source: {FishingData.Dataset.SourceRepository}/tree/{FishingData.Dataset.SourceRevision}");
        foreach (var row in snapshot.Rows.Where(row => row.Details is not null))
        {
            var detail = row.Details!;
            snapshot.Windows.TryGetValue(row.FishParameterId, out var window);
            Append($"Detail: FishParameter={row.FishParameterId}; Item={row.ItemId}; spot={detail.Spot.Id}; territory={detail.Spot.TerritoryId}; map={detail.Spot.MapId}; x={detail.Spot.X:F4}; y={detail.Spot.Y:F4}; window={window?.Kind}; start={window?.Start:O}; end={window?.End:O}");
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
