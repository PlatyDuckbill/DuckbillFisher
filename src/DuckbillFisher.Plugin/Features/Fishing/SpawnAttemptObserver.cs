using Dalamud.Plugin.Services;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

/// <summary>Counts participation, not target selection or successful preparation. Uses copied tracker facts only.</summary>
internal sealed class SpawnAttemptObserver : IDisposable
{
    private readonly FishingTracker tracker;
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly IFramework framework;
    private readonly TimeProvider clock;
    private readonly Dictionary<uint, (FishingWindow Window, DateTimeOffset Retry)> windows = [];
    private IReadOnlyList<FishGuideRow>? rows;
    private long lastPoll;
    private long lastSaveFailure;
    private uint spotId;
    private int session = -1;
    private DateTimeOffset lastUtc;

    public SpawnAttemptObserver(FishingTracker tracker, FishCatalog catalog, FishPreferences preferences,
        FishCollectionObserver collection, IFramework framework, TimeProvider? clock = null)
    {
        this.tracker = tracker; this.catalog = catalog; this.preferences = preferences;
        this.collection = collection; this.framework = framework; this.clock = clock ?? TimeProvider.System;
        framework.Update += Update;
    }

    private void Update(IFramework _)
    {
        if (lastPoll != 0 && clock.GetElapsedTime(lastPoll) < TimeSpan.FromMilliseconds(250)) return;
        lastPoll = clock.GetTimestamp();
        var personal = preferences.View;
        var current = tracker.View;
        var data = catalog.View;
        var now = clock.GetUtcNow();
        if (session != personal.Session || !ReferenceEquals(rows, data.Rows) || spotId != current.Spot?.Id || now < lastUtc)
        { windows.Clear(); session = personal.Session; rows = data.Rows; spotId = current.Spot?.Id ?? 0; }
        lastUtc = now;
        // An actual, still-pending cast must overlap the occurrence. Rod-ready/retained timers do not qualify.
        if (!personal.HasCharacter || !personal.CanEdit || !catalog.ConditionDataAvailable || current.Session != session ||
            current.State != FishingTrackerState.Ready || current.IsLastSpot || current.SpotState != FishingSpotState.Known ||
            current.Active is not { Outcome: FishingOutcome.Pending, StartedUtc: not null } ||
            current.Native?.IsLineActive != true || current.Spot is not { } spot) return;
        if (lastSaveFailure != 0 && clock.GetElapsedTime(lastSaveFailure) < TimeSpan.FromSeconds(5)) return;
        var batch = new List<(uint, DateTimeOffset)>();
        foreach (var fish in current.AllSpotFish)
        {
            if (fish.Grade is not (FishGrade.Big or FishGrade.Legendary) || fish.IsOceanFishing ||
                personal.Collection(fish.FishParameterId, collection.View) != CollectionStatus.Uncaught ||
                fish.Details is not { Spot: { } location } details || location.Id != spot.Id) continue;
            if (!windows.TryGetValue(fish.FishParameterId, out var saved) || saved.Retry <= now)
            {
                var found = FishingAvailability.Find(now, details.Rule, location.WeatherRates);
                saved = (found, (found.Kind == FishingWindowKind.Open ? found.End : found.Start) ?? now.AddMinutes(1));
                windows[fish.FishParameterId] = saved;
            }
            var window = saved.Window;
            // Always/unknown/no-end occurrences have no stable key. Own time/weather only, independent of Fish Eyes UI.
            if (window.Kind != FishingWindowKind.Open || window.Start is not { } start || start > now || window.End is not { } end || end <= now ||
                personal.PersonalRecords.GetValueOrDefault(fish.FishParameterId)?.LastAttemptOccurrenceEnd >= end.ToUnixTimeMilliseconds()) continue;
            batch.Add((fish.FishParameterId, end));
        }
        if (batch.Count > 0) lastSaveFailure = preferences.RecordSpawnAttempts(session, personal.SettingsRestoreRevision, batch) ? 0 : clock.GetTimestamp();
    }

    public void Dispose() { framework.Update -= Update; windows.Clear(); }
}
