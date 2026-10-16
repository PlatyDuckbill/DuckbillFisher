using System.Diagnostics;
using System.Globalization;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record SpawnAlertDisplay(uint FishId, string Name, string Place, DateTimeOffset Start);
internal sealed record SpawnAlertsView(int Session, UiMessage Status, int Eligible, int Armed, int Sent, int Batches,
    int Rebases, IReadOnlyList<SpawnAlertDisplay> LastBatch)
{
    public SpawnAlertDisplay? Next { get; init; }
}

/// <summary>Framework adapter: plans with existing forecasts, reserves locally, then queues local chat only.</summary>
internal sealed class SpawnAlerts : IDisposable
{
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly IFramework framework;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly SpawnAlertScheduler scheduler = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly object gate = new();
    private readonly Dictionary<uint, FishingWindow> plans = [];
    private readonly Dictionary<uint, TimeSpan> retryAfter = [];
    private IReadOnlyList<FishGuideRow>? lastRows;
    private Dictionary<uint, FishGuideRow> rowsById = [];
    private SpawnAlertsView view = new(-1, new(T.AlertsDisabled), 0, 0, 0, 0, 0, []);
    private TimeSpan nextPoll;
    private int lastLead;
    private bool disposed;

    public SpawnAlerts(FishCatalog catalog, FishPreferences preferences, FishCollectionObserver collection,
        IFramework framework, IChatGui chat, IPluginLog log)
    {
        this.catalog = catalog;
        this.preferences = preferences;
        this.collection = collection;
        this.framework = framework;
        this.chat = chat;
        this.log = log;
        framework.Update += OnUpdate;
    }

    public SpawnAlertsView View { get { lock (gate) return view; } }

    private void OnUpdate(IFramework _)
    {
        lock (gate)
        {
            if (disposed || clock.Elapsed < nextPoll && view.Session == preferences.View.Session)
                return;
            nextPoll = clock.Elapsed + TimeSpan.FromSeconds(1);
            try { Poll(DateTimeOffset.UtcNow, clock.Elapsed); }
            catch (Exception ex)
            {
                if (view.Status.Key != T.AlertsOutputFailed)
                    log.Error(ex, "Could not update spawn alerts; no pending notifications will be replayed.");
                plans.Clear();
                retryAfter.Clear();
                scheduler.Evaluate(DateTimeOffset.UtcNow, clock.Elapsed, view.Session, false, lastLead, []);
                view = view with { Status = new(T.AlertsOutputFailed), Armed = 0, Next = null };
            }
        }
    }

    private void Poll(DateTimeOffset now, TimeSpan elapsed)
    {
        var personal = preferences.View;
        var observed = collection.View;
        var data = catalog.View;
        if (view.Session != personal.Session)
        {
            plans.Clear();
            retryAfter.Clear();
            view = new(personal.Session, new(T.AlertsWaitingCharacter), 0, 0, 0, 0, 0, []);
        }
        if (!ReferenceEquals(lastRows, data.Rows))
        {
            lastRows = data.Rows;
            rowsById = data.Rows.ToDictionary(row => row.FishParameterId);
            plans.Clear();
            retryAfter.Clear();
        }
        if (lastLead != personal.SpawnAlertLeadMinutes)
        {
            plans.Clear();
            retryAfter.Clear();
        }
        lastLead = personal.SpawnAlertLeadMinutes;
        var status = !personal.SpawnAlertsEnabled ? T.AlertsDisabled : !personal.HasCharacter ? T.AlertsWaitingCharacter :
            !personal.CanEdit || personal.Error is not null ? T.AlertsStorageUnavailable :
            !catalog.ConditionBuildMatches || data.ForecastAt is not { } forecastAt ||
                (now - forecastAt).TotalSeconds is < 0 or > 5 ? T.AlertsForecastUnavailable : T.AlertsWatching;
        var active = status == T.AlertsWatching;
        var candidates = new List<SpawnAlertCandidate>();
        var plansBuilt = 0;
        if (active)
        {
            foreach (var id in personal.Favorites)
            {
                if (!rowsById.TryGetValue(id, out var row) || !row.IsDisplayable || row.IsOceanFishing ||
                    row.Details?.Spot is null || personal.Collection(id, observed) != CollectionStatus.Uncaught)
                    continue;
                if (!plans.TryGetValue(id, out var plan))
                {
                    if (retryAfter.TryGetValue(id, out var retry) && elapsed < retry)
                        continue;
                    // Spread a large favorite list across updates; existing deadlines are still checked.
                    if (plansBuilt >= 8)
                        continue;
                    plansBuilt++;
                    plan = SpawnAlertScheduler.FindFutureWindow(now, lastLead, data.Windows.GetValueOrDefault(id),
                        row.Details.Rule, row.Details.Spot.WeatherRates, personal.ReservedAlertWindows.GetValueOrDefault(id));
                    if (plan is null)
                    {
                        retryAfter[id] = elapsed + TimeSpan.FromMinutes(1);
                        continue;
                    }
                    retryAfter.Remove(id);
                    plans[id] = plan;
                }
                candidates.Add(new(id, true, CollectionStatus.Uncaught, plan));
            }
        }
        var evaluation = scheduler.Evaluate(now, elapsed, personal.Session, active, lastLead, candidates);
        var retained = candidates.Select(fish => fish.FishId).ToHashSet();
        foreach (var id in retryAfter.Keys.ToArray())
            if (!active || !personal.Favorites.Contains(id) || personal.Collection(id, observed) != CollectionStatus.Uncaught)
                retryAfter.Remove(id);
        // Evaluate the crossing before expiring a plan. Replanning first would discard a due alert.
        foreach (var id in plans.Keys.ToArray())
            if (!retained.Contains(id) || plans[id].Start is not { } start || start.AddMinutes(-lastLead) <= now)
                plans.Remove(id);
        var next = plans.OrderBy(pair => pair.Value.Start).FirstOrDefault();
        view = view with
        {
            Status = new(status), Eligible = candidates.Count, Armed = evaluation.Armed,
            Rebases = view.Rebases + (evaluation.Rebased ? 1 : 0),
            Next = next.Value?.Start is { } nextStart ? Display(next.Key, nextStart) : null,
        };
        if (evaluation.Due.Count == 0)
            return;
        var reserved = preferences.ReserveAlerts(personal.Session, evaluation.Due);
        if (reserved is null)
        {
            view = view with { Status = new(T.AlertsStorageUnavailable) };
            return;
        }
        if (reserved.Count == 0)
            return;
        var batch = reserved.OrderBy(alert => alert.Start).ThenBy(alert => alert.FishId)
            .Select(alert => Display(alert.FishId, alert.Start)).ToArray();
        // One chat message per poll, regardless of how many fish share the deadline.
        var names = string.Join(" / ", batch.Take(3).Select(fish =>
            UiText.Get(T.AlertEntry, fish.Name, fish.Place, fish.Start.ToLocalTime())));
        if (batch.Length > 3)
            names = UiText.Get(T.AlertMore, names, batch.Length - 3);
        chat.Print(UiText.Get(T.AlertMessage, names), "DuckbillFisher");
        view = view with { Sent = view.Sent + batch.Length, Batches = view.Batches + 1, LastBatch = batch };
    }

    private SpawnAlertDisplay Display(uint id, DateTimeOffset start)
    {
        var row = rowsById[id];
        return new(id, row.Name, row.Details!.Spot!.Name, start);
    }

    public string CreateReport()
    {
        var snapshot = View;
        var settings = preferences.View;
        return string.Create(CultureInfo.InvariantCulture,
            $"DuckbillFisher MVP-03/alerts/v1\nPlugin={typeof(Plugin).Assembly.GetName().Version}; Enabled={settings.SpawnAlertsEnabled}; LeadMinutes={settings.SpawnAlertLeadMinutes}; Channel=LocalChat; Session={snapshot.Session}; Status={snapshot.Status.Key}\nEligible={snapshot.Eligible}; Armed={snapshot.Armed}; Sent={snapshot.Sent}; Batches={snapshot.Batches}; Rebases={snapshot.Rebases}\nNextFish={snapshot.Next?.FishId}; NextStart={snapshot.Next?.Start:O}; DueAt={snapshot.Next?.Start.AddMinutes(-settings.SpawnAlertLeadMinutes):O}\nDedup=PersistedWindowStartPerCharacter; CatchUp=False; PersonalReadiness=False");
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            plans.Clear();
            retryAfter.Clear();
        }
        framework.Update -= OnUpdate;
    }
}
