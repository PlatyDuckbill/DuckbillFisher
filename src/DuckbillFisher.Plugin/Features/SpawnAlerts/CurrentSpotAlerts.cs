using Dalamud.Plugin.Services;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

/// <summary>Only a current, identified fishing session can produce local appearance notices.
/// Uses managed observations already validated by FishingTracker; never reads native memory.</summary>
internal sealed class CurrentSpotAlerts : IDisposable
{
    private readonly FishingTracker tracker;
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly SpawnAlertOutput output;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly TimeProvider clock;
    private readonly Dictionary<uint, (FishingWindow Window, DateTimeOffset Retry)> windows = [];
    private IReadOnlyList<FishGuideRow>? rows;
    private uint spotId;
    private int session = -1;
    private long lastPoll;
    private DateTimeOffset lastUtc;
    private bool disposed, failed;

    public CurrentSpotAlerts(FishingTracker tracker, FishCatalog catalog, FishPreferences preferences,
        FishCollectionObserver collection, SpawnAlertOutput output, IFramework framework, IPluginLog log,
        TimeProvider? clock = null)
    {
        this.tracker = tracker; this.catalog = catalog; this.preferences = preferences;
        this.collection = collection; this.output = output; this.framework = framework; this.log = log;
        this.clock = clock ?? TimeProvider.System;
        framework.Update += Update;
    }

    private void Update(IFramework _)
    {
        if (disposed || lastPoll != 0 && clock.GetElapsedTime(lastPoll) < TimeSpan.FromSeconds(1)) return;
        lastPoll = clock.GetTimestamp();
        try { Poll(); failed = false; }
        catch (Exception ex)
        {
            if (!failed) log.Error(ex, "Could not update current fishing-hole appearance alerts.");
            failed = true; windows.Clear();
        }
    }

    private void Poll()
    {
        var personal = preferences.View;
        var current = tracker.View;
        var now = clock.GetUtcNow();
        var data = catalog.View;
        if (personal.Session != session || !ReferenceEquals(rows, data.Rows) || now < lastUtc)
        { windows.Clear(); session = personal.Session; rows = data.Rows; }
        lastUtc = now;
        if (!personal.CurrentSpotAlertsEnabled || !personal.HasCharacter || !personal.CanEdit || personal.Error is not null ||
            !catalog.ConditionBuildMatches || current.Session != session || !current.HasSession || current.IsLastSpot ||
            current.State != FishingTrackerState.Ready || current.SpotState != FishingSpotState.Known ||
            current.Native?.Availability != FishingReadState.Ready || current.Spot is not { } spot)
        { spotId = 0; windows.Clear(); return; }
        if (spotId != spot.Id) { spotId = spot.Id; windows.Clear(); }
        var observed = collection.View;
        var batch = new List<SpawnAlertDisplay>();
        foreach (var fish in current.AllSpotFish)
        {
            if (fish.Grade is not (FishGrade.Big or FishGrade.Legendary) || fish.IsOceanFishing ||
                personal.Collection(fish.FishParameterId, observed) != CollectionStatus.Uncaught ||
                fish.Details is not { Spot: { } location } detail || location.Id != spot.Id) continue;
            if (!windows.TryGetValue(fish.FishParameterId, out var saved) || saved.Retry <= now)
            {
                var found = FishingAvailability.Find(now, detail.Rule, location.WeatherRates);
                var retry = found.Kind == FishingWindowKind.Open ? found.End : found.Start;
                saved = (found, retry ?? now.AddMinutes(1));
                windows[fish.FishParameterId] = saved;
            }
            var window = saved.Window;
            // Own time/weather only. An always-available Intuition target has no shared spawn start.
            if (window.Start is not { } start || start > now || window.End is not { } end || end <= now) continue;
            batch.Add(new(fish.FishParameterId, fish.ItemId, fish.Name, spot, start, 0)
            {
                End = end, OccurrenceEnd = end, AlreadyOpen = now - start > TimeSpan.FromSeconds(5),
                RequiresIntuition = detail.Rule.Predators.Length > 0,
            });
        }
        if (batch.Count > 0) output.Print(batch, now, personal);
    }

    public void Dispose() { disposed = true; framework.Update -= Update; windows.Clear(); }
}
