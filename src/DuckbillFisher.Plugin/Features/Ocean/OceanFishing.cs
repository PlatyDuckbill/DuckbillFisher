using System.Diagnostics;
using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using Lumina.Excel.Sheets;

namespace DuckbillFisher.Plugin;

internal sealed record OceanStop(uint Id, string Name, uint FishingSpotId, uint SpectralSpotId, byte Time);
internal sealed record OceanRoute(uint Id, string Name, uint TerritoryId, uint ContentId, OceanStop[] Stops);
internal sealed record OceanDeparture(DateTimeOffset Starts, bool Ruby, OceanRoute Route);
internal sealed record OceanGuidance(uint SpotId, byte Time, FishingItemView Bait, FishingItemView Fish, string Tug, int Double, int Triple);
internal enum OceanDataState { Loading, Ready, Unavailable }
[Flags]
internal enum OceanConsumer { Main = 1, Timer = 2, FishList = 4 }
internal enum OceanFishListState { Loading, Ready, OffVoyage, Unavailable, Moving, Waiting, Finished }
internal sealed record OceanFishListView(OceanFishListState State, string Area = "", bool Spectral = false,
    uint FishingSpotId = 0)
{
    public FishGuideRow[] Rows { get; init; } = [];
}
internal sealed record OceanVoyageView(int Session, OceanDataState DataState, OceanNativeSample Sample, long SampledAt,
    IReadOnlyDictionary<uint, OceanRoute> Routes, OceanDeparture[] Departures,
    IReadOnlyDictionary<(uint Spot, byte Time), OceanGuidance> Guidance)
{
    public bool DataReady => DataState == OceanDataState.Ready;
    public bool IsOceanTerritory { get; init; }
    public OceanFishListView FishList { get; init; } = new(OceanFishListState.Loading);
}

/// <summary>Loads local sheets/resources once, then copies live state on Framework. No HTTP and no Draw-time data reads.</summary>
internal sealed class OceanFishing : IDisposable
{
    private readonly IDataManager data;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly ICondition conditions;
    private readonly IFramework framework;
    private readonly FishPreferences preferences;
    private readonly FishCatalog catalog;
    private readonly IPluginLog log;
    private readonly NativeOceanFishingReader reader;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly Dictionary<uint, OceanRoute> routes = [];
    private readonly Dictionary<(uint, byte), OceanGuidance> guidance = [];
    private (uint Indigo, uint Ruby)[] table = [];
    private OceanVoyageView view = new(-1, OceanDataState.Loading, new(OceanReadState.PlayerUnavailable), 0,
        new Dictionary<uint, OceanRoute>(), [], new Dictionary<(uint, byte), OceanGuidance>());
    private bool loaded;
    private bool dataReady;
    private int consumers;
    private IReadOnlyList<FishGuideRow>? indexedRows;
    private bool catalogRequested;
    private Dictionary<uint, FishGuideRow[]> fishBySpot = [];
    private bool disposed;
    private long sampledAt;
    private DateTimeOffset nextRegistration;
    private OceanDeparture[] departures = [];

    public OceanFishing(IDataManager data, IClientState client, IPlayerState player, ICondition conditions,
        IFramework framework, FishPreferences preferences, FishCatalog catalog, IPluginLog log, Func<DateTimeOffset>? utcNow = null)
    {
        this.data = data; this.client = client; this.player = player; this.conditions = conditions;
        this.framework = framework; this.preferences = preferences; this.log = log;
        this.catalog = catalog;
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        reader = new(framework);
        framework.Update += Update;
    }

    public OceanVoyageView View
    {
        get
        {
            var current = Volatile.Read(ref view);
            return current.Session != preferences.View.Session || current.SampledAt == 0 ||
                Stopwatch.GetElapsedTime(current.SampledAt).TotalSeconds > 3
                ? current with { Sample = new(OceanReadState.Stale), FishList = new(OceanFishListState.Unavailable) } : current;
        }
    }

    public void SetActive(OceanConsumer consumer, bool value)
    {
        if (value) Interlocked.Or(ref consumers, (int)consumer);
        else Interlocked.And(ref consumers, ~(int)consumer);
    }

    private OceanFishListView FishList(OceanNativeSample sample)
    {
        if (sample.State == OceanReadState.OffVoyage) return new(OceanFishListState.OffVoyage);
        if (sample.State != OceanReadState.Ready) return new(OceanFishListState.Unavailable);
        if (sample.Phase != OceanPhase.Fishing) return new(sample.Phase switch
        {
            OceanPhase.Waiting => OceanFishListState.Waiting,
            OceanPhase.Finished => OceanFishListState.Finished,
            _ => OceanFishListState.Moving,
        });
        if (!routes.TryGetValue(sample.RouteId, out var route) || sample.Zone < 0 || sample.Zone >= route.Stops.Length)
            return new(OceanFishListState.Unavailable);
        var stop = route.Stops[sample.Zone];
        var id = sample.Spectral ? stop.SpectralSpotId : stop.FishingSpotId;
        if (indexedRows is not { Count: > 0 }) return new(catalog.View.Status.Key == Localization.TextKey.RowsReadError
            ? OceanFishListState.Unavailable : OceanFishListState.Loading);
        return fishBySpot.TryGetValue(id, out var rows)
            ? new(OceanFishListState.Ready, stop.Name, sample.Spectral, id) { Rows = rows }
            : new(OceanFishListState.Unavailable);
    }

    public string CreateReport()
    {
        var current = View;
        var s = current.Sample;
        var route = current.Routes.GetValueOrDefault(s.RouteId);
        return FormattableString.Invariant($"Format=ocean-beta/v1\nDataState={current.DataState}; GuidanceRows={current.Guidance.Count}\nState={s.State}; Phase={s.Phase}; Route={s.RouteId}; Zone={s.Zone}; Spectral={s.Spectral}\nContent={s.ContentId}; ExpectedContent={route?.ContentId}; Territory={route?.TerritoryId}\nDuration={s.Duration}; ContentSeconds={s.ContentSeconds}; TimeOffset={s.TimeOffset}; ZoneSeconds={s.RemainingSeconds}\nSampleAgeMs={(current.SampledAt == 0 ? -1 : Stopwatch.GetElapsedTime(current.SampledAt).TotalMilliseconds):F0}\nNextRegistrationUtc={current.Departures.FirstOrDefault()?.Starts:O}");
    }

    private void Load()
    {
        // The one-shot resource/sheet failure affects only this Beta feature.
        loaded = true;
        try
        {
            var routeSheet = data.GetExcelSheet<IKDRoute>()!;
            foreach (var row in routeSheet.Where(row => row.RowId != 0))
            {
                var spots = row.Spot.ToArray();
                var times = row.Time.ToArray();
                if (spots.Length != 3 || times.Length != 3 || spots.Any(s => s.RowId == 0) || times.Any(t => t.Value.TimeOfDay is < 1 or > 3))
                    throw new InvalidDataException("OceanRouteShape");
                var stops = spots.Select((s, i) => new OceanStop(s.RowId, s.Value.PlaceName.Value.Name.ExtractText(),
                    s.Value.SpotMain.RowId, s.Value.SpotSub.RowId, times[i].Value.TimeOfDay)).ToArray();
                var instance = row.Instance.Value;
                routes.Add(row.RowId, new(row.RowId, row.Name.ExtractText(), instance.TerritoryType.RowId,
                    instance.Content.RowId, stops));
            }
            var rows = data.GetExcelSheet<IKDRouteTable>()!.OrderBy(row => row.RowId).ToArray();
            // Stable schema calls the second uint Unknown0. Its Ruby-route meaning was checked
            // against both installed clients and the experimental schema; no raw offset is used.
            if (rows.Length != 144 || rows.Where((row, i) => row.RowId != i).Any()) throw new InvalidDataException("OceanTableShape");
            table = rows.Select(row => (row.Route.RowId, row.Unknown0)).ToArray();
            if (table.Any(pair => !routes.ContainsKey(pair.Indigo) || !routes.ContainsKey(pair.Ruby)))
                throw new InvalidDataException("OceanTableRoute");
            dataReady = true;
        }
        catch (Exception e)
        {
            routes.Clear(); table = []; dataReady = false;
            log.Warning("OceanDataUnavailable: {Code}", e.GetType().Name);
        }
        if (!dataReady) return;
        try
        {
            using var stream = typeof(OceanFishing).Assembly.GetManifestResourceStream("DuckbillFisher.OceanGuidance")!;
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("OceanGuidanceSchema");
            var items = data.GetExcelSheet<Item>()!;
            var fish = data.GetExcelSheet<FishParameter>()!.Where(f => f.OceanStars > 0)
                .ToDictionary(f => f.Item.RowId, f => f.FishingSpot.RowId);
            var spots = routes.Values.SelectMany(r => r.Stops).DistinctBy(s => s.Id).ToDictionary(s => s.Id);
            foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                var id = entry.GetProperty("spotId").GetUInt32();
                var time = entry.GetProperty("time").GetByte();
                var baitId = entry.GetProperty("baitItemId").GetUInt32();
                var fishId = entry.GetProperty("fishItemId").GetUInt32();
                var tug = entry.GetProperty("tug").GetString()!;
                var twice = entry.GetProperty("double").GetInt32();
                var thrice = entry.GetProperty("triple").GetInt32();
                if (!spots.TryGetValue(id, out var spot) || time > 3 ||
                    !fish.TryGetValue(fishId, out var location) || location != (time == 0 ? spot.FishingSpotId : spot.SpectralSpotId) ||
                    tug is not ("!" or "!!" or "!!!") || twice is < 1 or > 4 || thrice is < 1 or > 7)
                    continue; // Missing client facts stay uncollected, never guessed.
                var baitItem = items.GetRowOrDefault(baitId);
                var fishItem = items.GetRowOrDefault(fishId);
                if (baitItem is not { } bait || bait.ItemUICategory.RowId != FishingGameIds.BaitCategory || fishItem is not { } target) continue;
                guidance.Add((id, time), new(id, time, new(baitId, bait.Name.ExtractText(), bait.Icon),
                    new(fishId, target.Name.ExtractText(), target.Icon), tug, twice, thrice));
            }
        }
        catch (Exception e) { guidance.Clear(); log.Warning("OceanGuidanceUnavailable: {Code}", e.GetType().Name); }
    }

    private void Update(IFramework _)
    {
        if (disposed) return;
        var personal = preferences.View;
        if (Volatile.Read(ref consumers) == 0)
        {
            if (view.Session != personal.Session) Volatile.Write(ref view, view with { Session = personal.Session,
                Sample = new(OceanReadState.PlayerUnavailable), SampledAt = 0,
                IsOceanTerritory = false, FishList = new(OceanFishListState.Unavailable) });
            return;
        }
        if (sampledAt != 0 && Stopwatch.GetElapsedTime(sampledAt).TotalMilliseconds < 200 && view.Session == personal.Session) return;
        sampledAt = Stopwatch.GetTimestamp();
        if (!loaded) Load();
        var catalogRows = catalog.View.Rows;
        if (!ReferenceEquals(indexedRows, catalogRows))
        {
            indexedRows = catalogRows;
            fishBySpot = catalogRows.Where(f => f.IsDisplayable && f.IsOceanFishing && f.OceanFishingSpotId != 0)
                .GroupBy(f => f.OceanFishingSpotId).ToDictionary(g => g.Key,
                    g => g.OrderBy(f => f.Name, ClientNameOrder.Comparer).ToArray());
        }
        if (!catalogRequested) { catalogRequested = true; catalog.RequestLoad(); }
        var starts = OceanSchedule.RegistrationStarts(utcNow());
        if (dataReady && (departures.Length == 0 || nextRegistration != starts[0]))
        {
            nextRegistration = starts[0];
            departures = starts.SelectMany(start =>
            {
                var row = table[OceanSchedule.TableIndex(start)];
                return new[] { new OceanDeparture(start, false, routes[row.Indigo]), new OceanDeparture(start, true, routes[row.Ruby]) };
            }).ToArray();
        }
        var sample = !dataReady || !personal.HasCharacter || !client.IsLoggedIn || !player.IsLoaded ||
            player.ContentId != personal.CharacterId || conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51]
            ? new OceanNativeSample(OceanReadState.PlayerUnavailable)
            : reader.Read(personal.CharacterId, client.TerritoryType, routes);
        // A Framework callback cannot publish a departed character's state to a new session.
        if (preferences.View.Session != personal.Session) return;
        Volatile.Write(ref view, new(personal.Session, dataReady ? OceanDataState.Ready : OceanDataState.Unavailable,
            sample, sampledAt, routes, departures, guidance)
        {
            IsOceanTerritory = personal.HasCharacter && client.IsLoggedIn && routes.Values.Any(r => r.TerritoryId == client.TerritoryType),
            FishList = FishList(sample),
        });
    }

    public void Dispose()
    {
        disposed = true; Interlocked.Exchange(ref consumers, 0);
        framework.Update -= Update;
        Volatile.Write(ref view, view with { Sample = new(OceanReadState.PlayerUnavailable), SampledAt = 0,
            IsOceanTerritory = false, FishList = new(OceanFishListState.Unavailable) });
    }
}
