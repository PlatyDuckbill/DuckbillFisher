using System.Diagnostics;
using System.Globalization;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record SpawnAlertDisplay(uint FishId, uint ItemId, string Name, FishingSpotView Spot, DateTimeOffset Start, int LeadMinutes);
internal sealed record SpawnAlertDelivery(DateTimeOffset SentAt, SpawnAlertDisplay Fish, XivChatType Channel,
    int Sound, bool SoundPlayed);
internal enum FavoriteAlertState { Disabled, WaitingCharacter, Caught, Unknown, Unsupported, Always, Pending, Armed }
internal sealed record FavoriteAlertView(uint FishId, string? Name, int LeadMinutes,
    FavoriteAlertState State, DateTimeOffset? Start)
{
    public FishGuideRow? Row { get; init; }
    public FishingWindow? Window { get; init; }
}
internal sealed record SpawnAlertsView(int Session, UiMessage Status, int Eligible, int Armed, int Sent, int Batches,
    int Rebases, IReadOnlyList<SpawnAlertDelivery> History)
{
    public SpawnAlertDisplay? Next { get; init; }
    public IReadOnlyList<FavoriteAlertView> Favorites { get; init; } = [];
}

/// <summary>Framework adapter: plans with existing forecasts, reserves locally, then queues local chat only.</summary>
internal sealed class SpawnAlerts : IDisposable
{
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly FishCollectionObserver collection;
    private readonly IFramework framework;
    private readonly SpawnAlertOutput output;
    private readonly IPluginLog log;
    private readonly SpawnAlertScheduler scheduler = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly object gate = new();
    private sealed record Plan(FishingWindow Window, int Lead);
    private readonly Dictionary<uint, Plan> plans = [];
    private readonly Dictionary<uint, (int Lead, TimeSpan At)> retryAfter = [];
    private IReadOnlyList<FishGuideRow>? lastRows;
    private Dictionary<uint, FishGuideRow> rowsById = [];
    private SpawnAlertsView view = new(-1, new(T.AlertsDisabled), 0, 0, 0, 0, 0, []);
    private TimeSpan nextPoll;
    private int nextPlanIndex;
    private bool disposed;

    public SpawnAlerts(FishCatalog catalog, FishPreferences preferences, FishCollectionObserver collection,
        IFramework framework, SpawnAlertOutput output, IPluginLog log)
    {
        this.catalog = catalog;
        this.preferences = preferences;
        this.collection = collection;
        this.framework = framework;
        this.output = output;
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
                nextPlanIndex = 0;
                var settings = preferences.View;
                scheduler.Evaluate(DateTimeOffset.UtcNow, clock.Elapsed, view.Session, false, settings.SpawnAlertLeadMinutes,
                    settings.SpawnAlertAudience, []);
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
            nextPlanIndex = 0;
            view = new(personal.Session, new(T.AlertsWaitingCharacter), 0, 0, 0, 0, 0, []);
        }
        if (!ReferenceEquals(lastRows, data.Rows))
        {
            lastRows = data.Rows;
            rowsById = data.Rows.ToDictionary(row => row.FishParameterId);
            plans.Clear();
            retryAfter.Clear();
            nextPlanIndex = 0;
        }
        var status = !personal.SpawnAlertsEnabled ? T.AlertsDisabled : !personal.HasCharacter ? T.AlertsWaitingCharacter :
            !personal.CanEdit || personal.Error is not null ? T.AlertsStorageUnavailable :
            !catalog.ConditionBuildMatches || data.ForecastAt is not { } forecastAt ||
                (now - forecastAt).TotalSeconds is < 0 or > 5 ? T.AlertsForecastUnavailable : T.AlertsWatching;
        var active = status == T.AlertsWatching;
        if (scheduler.RequiresRebase(now, elapsed, personal.Session, active))
        {
            // A rollback may introduce an earlier future window than the cached plan.
            // Long interruptions also invalidate retry delays from the previous timeline.
            plans.Clear();
            retryAfter.Clear();
            nextPlanIndex = 0;
        }
        var candidates = new List<SpawnAlertCandidate>();
        var plansBuilt = 0;
        if (active)
        {
            var favorites = personal.Favorites.ToArray();
            var first = favorites.Length == 0 ? 0 : nextPlanIndex % favorites.Length;
            for (var offset = 0; offset < favorites.Length; offset++)
            {
                var index = (first + offset) % favorites.Length;
                var id = favorites[index];
                if (!rowsById.TryGetValue(id, out var row) || !row.IsDisplayable || row.IsOceanFishing ||
                    row.Details?.Spot is null || !SpawnAlertScheduler.Includes(personal.SpawnAlertAudience, personal.Collection(id, observed)))
                    continue;
                var currentWindow = data.NormalWindows.GetValueOrDefault(id);
                if (currentWindow?.Kind == FishingWindowKind.Always)
                    continue;
                var lead = personal.AlertLead(id);
                if (plans.TryGetValue(id, out var old) && old.Lead != lead)
                    plans.Remove(id);
                if (!plans.TryGetValue(id, out var plan))
                {
                    if (retryAfter.TryGetValue(id, out var retry) && retry.Lead == lead && elapsed < retry.At)
                        continue;
                    // Rotate new work so early retries cannot starve later favorites.
                    // Still inspect every existing plan for crossed deadlines in this poll.
                    if (plansBuilt >= 8)
                        continue;
                    plansBuilt++;
                    nextPlanIndex = (index + 1) % favorites.Length;
                    var window = SpawnAlertScheduler.FindFutureWindow(now, lead, currentWindow,
                        personal.ReservedAlertWindows.GetValueOrDefault(id), at => data.Forecast.Find(at, row.ItemId));
                    if (window is null)
                    {
                        retryAfter[id] = (lead, elapsed + TimeSpan.FromMinutes(1));
                        continue;
                    }
                    retryAfter.Remove(id);
                    plans[id] = plan = new(window, lead);
                }
                candidates.Add(new(id, true, personal.Collection(id, observed), plan.Window, lead));
            }
        }
        var evaluation = scheduler.Evaluate(now, elapsed, personal.Session, active, personal.SpawnAlertLeadMinutes,
            personal.SpawnAlertAudience, candidates);
        var retained = candidates.Select(fish => fish.FishId).ToHashSet();
        foreach (var id in retryAfter.Keys.ToArray())
            if (!active || !personal.Favorites.Contains(id) ||
                !SpawnAlertScheduler.Includes(personal.SpawnAlertAudience, personal.Collection(id, observed)))
                retryAfter.Remove(id);
        // Evaluate the crossing before expiring a plan. Replanning first would discard a due alert.
        foreach (var id in plans.Keys.ToArray())
            if (!retained.Contains(id) || plans[id].Window.Start is not { } start || start.AddMinutes(-plans[id].Lead) <= now)
                plans.Remove(id);
        var next = plans.OrderBy(pair => pair.Value.Window.Start?.AddMinutes(-pair.Value.Lead)).ThenBy(pair => pair.Key).FirstOrDefault();
        view = view with
        {
            Status = new(status), Eligible = candidates.Count, Armed = evaluation.Armed,
            Rebases = view.Rebases + (evaluation.Rebased ? 1 : 0),
            Next = next.Value?.Window.Start is { } nextStart ? Display(next.Key, nextStart, next.Value.Lead) : null,
            Favorites = FavoriteRows(personal, observed, data),
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
        var sentAt = DateTimeOffset.UtcNow;
        if (sentAt < now || sentAt - now > TimeSpan.FromSeconds(10))
            return; // A slow save or clock jump must not replay a stale batch.
        var batch = reserved.OrderBy(alert => alert.Start).ThenBy(alert => alert.FishId)
            .Where(alert => alert.Start > sentAt)
            .Select(alert => Display(alert.FishId, alert.Start, personal.AlertLead(alert.FishId))).ToArray();
        if (batch.Length == 0)
            return;
        // One chat message per poll, regardless of how many fish share the deadline.
        var soundPlayed = output.Print(batch, sentAt, personal);
        var history = batch.Reverse().Select(fish => new SpawnAlertDelivery(sentAt, fish, personal.SpawnAlertChannel,
            personal.SpawnAlertSound, soundPlayed)).Concat(view.History).Take(200).ToArray();
        view = view with { Sent = view.Sent + batch.Length, Batches = view.Batches + 1, History = history };
    }

    private SpawnAlertDisplay Display(uint id, DateTimeOffset start, int lead)
    {
        var row = rowsById[id];
        return new(id, row.ItemId, row.Name, row.Details!.Spot!, start, lead);
    }

    private IReadOnlyList<FavoriteAlertView> FavoriteRows(FishPreferencesView personal, FishCollectionView observed, FishCatalogView data)
    {
        // Names/order change only with membership or catalog rows. Reuse both the sorted
        // snapshot and each unchanged entry so the UI does not rebuild its search each second.
        if (view.Favorites.Count != personal.Favorites.Count || view.Favorites.Any(fish =>
            !personal.Favorites.Contains(fish.FishId) || !ReferenceEquals(fish.Row, rowsById.GetValueOrDefault(fish.FishId))))
            return personal.Favorites.Select(id => MakeRow(id, null))
                .OrderBy(fish => fish.Name, StringComparer.Ordinal).ThenBy(fish => fish.FishId).ToArray();

        FavoriteAlertView[]? changed = null;
        for (var index = 0; index < view.Favorites.Count; index++)
        {
            var previous = view.Favorites[index];
            var next = MakeRow(previous.FishId, previous);
            if (ReferenceEquals(previous, next)) continue;
            changed ??= view.Favorites.ToArray();
            changed[index] = next;
        }
        return changed ?? view.Favorites;

        FavoriteAlertView MakeRow(uint id, FavoriteAlertView? previous)
        {
            rowsById.TryGetValue(id, out var row);
            plans.TryGetValue(id, out var plan);
            var collectionStatus = personal.Collection(id, observed);
            var uncaughtOnly = personal.SpawnAlertAudience == SpawnAlertAudience.UncaughtOnly;
            var state = !personal.HasCharacter ? FavoriteAlertState.WaitingCharacter :
                uncaughtOnly && collectionStatus == CollectionStatus.Caught ? FavoriteAlertState.Caught :
                uncaughtOnly && collectionStatus == CollectionStatus.Unknown ? FavoriteAlertState.Unknown :
                row is not { IsDisplayable: true, IsOceanFishing: false, Details.Spot: not null } ? FavoriteAlertState.Unsupported :
                data.NormalWindows.GetValueOrDefault(id)?.Kind == FishingWindowKind.Always ? FavoriteAlertState.Always :
                !personal.SpawnAlertsEnabled ? FavoriteAlertState.Disabled :
                plan is null ? FavoriteAlertState.Pending : FavoriteAlertState.Armed;
            var lead = personal.AlertLead(id);
            var start = plan?.Window.Start;
            var window = data.NormalWindows.GetValueOrDefault(id);
            if (previous is not null && previous.LeadMinutes == lead && previous.State == state &&
                previous.Start == start && ReferenceEquals(previous.Window, window))
                return previous;
            return new FavoriteAlertView(id, row?.Name, lead, state, start)
            {
                Row = row, Window = window,
            };
        }
    }

    public string CreateReport()
    {
        var snapshot = View;
        var settings = preferences.View;
        return string.Create(CultureInfo.InvariantCulture,
            $"DuckbillFisher MVP-03/alerts/v6\nPlugin={typeof(Plugin).Assembly.GetName().Version}; Enabled={settings.SpawnAlertsEnabled}; LeadMinutes={settings.SpawnAlertLeadMinutes}; Channel={settings.SpawnAlertChannel}; Channels={ChatChannelChoices.All.Length}; Sound={settings.SpawnAlertSound}; SoundReady={output.SoundAvailable}; Session={snapshot.Session}; Status={snapshot.Status.Key}\nAudience={settings.SpawnAlertAudience}; RemoveOnRegistration={settings.RemoveFavoriteOnRegistration}; Favorites={snapshot.Favorites.Count}; CustomLeads={settings.FishAlertLeadMinutes.Count}; History={snapshot.History.Count}; Eligible={snapshot.Eligible}; Armed={snapshot.Armed}; Sent={snapshot.Sent}; Batches={snapshot.Batches}; Rebases={snapshot.Rebases}\nNextFish={snapshot.Next?.FishId}; NextLead={snapshot.Next?.LeadMinutes}; NextStart={snapshot.Next?.Start:O}; DueAt={snapshot.Next?.Start.AddMinutes(-snapshot.Next.LeadMinutes):O}\nDedup=PersistedWindowStartPerCharacter; CatchUp=False; PersonalReadiness=False");
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
