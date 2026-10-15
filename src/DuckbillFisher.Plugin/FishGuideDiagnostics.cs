using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record FishGuideDiagnosticView(
    bool HasSample, bool IsLoggedIn, bool? IsLoaded, bool FishGuideVisible, bool FishingNoteVisible,
    bool LoginFallback, int Session, int OpenCount, int RefreshCount, UiMessage LastTrigger, UiMessage Status,
    int InLogCount, uint MaxInLogId, IReadOnlyList<FishGuideRow> Rows, IReadOnlyList<LocalizedLogEntry> History)
{
    public FishBitSample? CollectionSample { get; init; }
}

/// <summary>Public guide/sheet observation, plus an isolated diagnostic reader for a selected fish bit.</summary>
internal sealed class FishGuideDiagnostics : IDisposable
{
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly FishCatalog catalog;
    private readonly IGameGui gameGui;
    private readonly IFramework framework;
    private readonly NativeFishCollectionReader collectionReader;
    private readonly object gate = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Queue<LocalizedLogEntry> history = new();
    private FishGuideDiagnosticView view = new(false, false, null, false, false, false, 0, 0, 0,
        new(T.None), new(T.GuideWaiting), 0, 0, [], []);
    private TimeSpan nextPoll;
    private ulong characterId;
    private bool manualRefresh;
    private UiMessage? pendingTrigger;
    private uint? trackedFishId;
    private uint? pendingFishId;
    private bool disposed;

    public FishGuideDiagnostics(IClientState clientState, IPlayerState playerState, FishCatalog catalog,
        IGameGui gameGui, IFramework framework, NativeFishCollectionReader collectionReader)
    {
        this.clientState = clientState;
        this.playerState = playerState;
        this.catalog = catalog;
        this.gameGui = gameGui;
        this.framework = framework;
        this.collectionReader = collectionReader;
        framework.Update += OnUpdate;
    }

    public FishGuideDiagnosticView View { get { lock (gate) return view; } }

    // UI actions only enqueue work. Sheet/game APIs run on Framework.Update.
    public void RequestRefresh() { lock (gate) { if (!disposed) manualRefresh = true; } }

    public void RequestCollectionRead(uint fishParameterId)
    {
        lock (gate)
        {
            if (disposed)
                return;
            trackedFishId = fishParameterId;
            pendingFishId = fishParameterId;
            view = view with { CollectionSample = null };
        }
    }

    public void SetLoginFallback(bool enabled)
    {
        lock (gate)
        {
            if (disposed || enabled == view.LoginFallback)
                return;
            view = view with { LoginFallback = enabled };
            if (enabled && characterId != 0)
                pendingTrigger = new(T.LoginOptionTrigger);
        }
    }

    private void OnUpdate(IFramework _)
    {
        lock (gate)
        {
            if (disposed || clock.Elapsed < nextPoll)
                return;
            nextPoll = clock.Elapsed + TimeSpan.FromMilliseconds(250);
            try
            {
                Poll();
            }
            catch (Exception ex)
            {
                // Managed public-API failures only; this is not protection against invalid native memory.
                pendingTrigger = null;
                manualRefresh = false;
                pendingFishId = null;
                var status = new UiMessage(T.PublicApiError, ex.GetType().Name);
                if (!view.Status.SameAs(status))
                {
                    AddHistory(status);
                    view = view with { HasSample = false, Rows = [], InLogCount = 0, MaxInLogId = 0, CollectionSample = null, Status = status };
                }
            }
            view = view with { History = history.ToArray() };
        }
    }

    private void Poll()
    {
        var loggedIn = clientState.IsLoggedIn;
        bool? loaded = loggedIn ? playerState.IsLoaded : null;
        var currentCharacter = loaded == true ? playerState.ContentId : 0;
        var guide = loggedIn && IsVisible("FishGuide2");
        var note = loggedIn && IsVisible("FishingNote");
        var oldView = view;

        if (currentCharacter != characterId)
        {
            // Static rows may be reused, but published data/triggers never carry across character sessions.
            characterId = currentCharacter;
            pendingTrigger = null;
            pendingFishId = null;
            trackedFishId = null;
            view = view with
            {
                Session = view.Session + 1, Rows = [], InLogCount = 0, MaxInLogId = 0,
                LastTrigger = new(T.None), OpenCount = 0, RefreshCount = 0,
                CollectionSample = null,
                Status = new(T.GuideWaiting),
            };
            AddHistory(new(currentCharacter == 0 ? T.SessionCleared : T.SessionReady));
            if (currentCharacter != 0 && view.LoginFallback)
                pendingTrigger = new(T.LoginReadyTrigger);
            else if (currentCharacter != 0 && (guide || note))
                pendingTrigger = new(T.AlreadyOpenWhenReady);
        }

        if (!oldView.HasSample || loggedIn != oldView.IsLoggedIn || loaded != oldView.IsLoaded)
        {
            object loadedValue = loaded is bool value ? value : new UiMessage(T.NotQueried);
            AddHistory(new(T.PlayerStateChanged, loggedIn, loadedValue));
        }

        ObserveWindow("FishGuide2", oldView.FishGuideVisible, guide, oldView.HasSample);
        ObserveWindow("FishingNote", oldView.FishingNoteVisible, note, oldView.HasSample);
        view = view with
        {
            HasSample = true, IsLoggedIn = loggedIn, IsLoaded = loaded,
            FishGuideVisible = guide, FishingNoteVisible = note,
        };

        if (!loggedIn || loaded != true || currentCharacter == 0)
        {
            if (manualRefresh)
                AddHistory(new(T.ManualRefreshSkipped));
            manualRefresh = false;
            pendingFishId = null;
            view = view with { Rows = [], InLogCount = 0, MaxInLogId = 0, CollectionSample = null, Status = new(T.WaitingForPlayer) };
            return;
        }

        if (manualRefresh)
            pendingTrigger = new(T.ManualRefreshTrigger);
        if (pendingTrigger is { } trigger)
        {
            var reload = manualRefresh;
            manualRefresh = false;
            pendingTrigger = null;
            RefreshRows(trigger, reload);
        }
        if (pendingFishId is { } fishId)
        {
            pendingFishId = null;
            var row = view.Rows.FirstOrDefault(fish => fish.FishParameterId == fishId);
            var sample = row is null
                ? new FishBitSample(fishId, null, new(T.RowAbsent))
                : collectionReader.Read(row, characterId, guide);
            view = view with { CollectionSample = sample };
            AddHistory(new(T.BitReadLog, fishId, sample.Value?.ToString() ?? "Unknown", sample.Reason));
        }
    }

    private bool IsVisible(string name)
    {
        var addon = gameGui.GetAddonByName(name);
        return !addon.IsNull && addon.IsVisible;
    }

    private void ObserveWindow(string name, bool wasVisible, bool visible, bool hadSample)
    {
        if (!hadSample || wasVisible != visible)
            AddHistory(new(hadSample ? T.WindowStateLog : T.WindowFirstSampleLog, name, new UiMessage(visible ? T.Opened : T.Closed)));
        if (!visible || (hadSample && wasVisible))
            return;
        view = view with { OpenCount = view.OpenCount + 1 };
        pendingTrigger = new(hadSample ? T.GuideOpenedTrigger : T.GuideInitiallyVisibleTrigger, name);
        if (name == "FishGuide2")
            pendingFishId = trackedFishId;
    }

    private void RefreshRows(UiMessage trigger, bool reload)
    {
        try
        {
            var rows = catalog.Read(reload);
            var inLog = rows.Where(row => row.IsInLog).ToArray();
            if (inLog.Length == 0)
                throw new InvalidDataException("No FishParameter rows eligible for the fish log.");
            view = view with
            {
                Rows = rows, InLogCount = inLog.Length,
                MaxInLogId = inLog.Length == 0 ? 0 : inLog.Max(row => row.FishParameterId),
                RefreshCount = view.RefreshCount + 1, LastTrigger = trigger,
                Status = new(T.RowsRead),
            };
            AddHistory(new(T.RowsRefreshLog, view.RefreshCount, trigger, rows.Count, inLog.Length));
            if (view.FishGuideVisible && trackedFishId is not null)
                pendingFishId = trackedFishId;
        }
        catch (Exception ex)
        {
            pendingFishId = null;
            view = view with
            {
                Rows = [], InLogCount = 0, MaxInLogId = 0, LastTrigger = trigger, CollectionSample = null,
                Status = new(T.RowsReadError, ex.GetType().Name),
            };
            AddHistory(view.Status);
        }
    }

    private void AddHistory(UiMessage message)
    {
        if (history.Count == 80)
            history.Dequeue();
        history.Enqueue(new(clock.Elapsed, message));
    }

    public string CreateReport(FishGuideRow? selected)
    {
        lock (gate)
        {
            var report = new StringBuilder();
            void AppendInvariant(FormattableString line) => report.AppendLine(FormattableString.Invariant(line));
            report.AppendLine("DuckbillFisher ENV-03/fish-guide-native/v2");
            AppendInvariant($"Plugin: {typeof(Plugin).Assembly.GetName().Version}; Dalamud: {typeof(IPlayerState).Assembly.GetName().Version}");
            AppendInvariant($"Session: {view.Session}; sampled: {view.HasSample}; IsLoggedIn={view.IsLoggedIn}; IsLoaded={view.IsLoaded?.ToString() ?? "not sampled"}");
            AppendInvariant($"FishGuide2={view.FishGuideVisible}; FishingNote={view.FishingNoteVisible}; loginFallback={view.LoginFallback}");
            AppendInvariant($"Open samples: {view.OpenCount}; refreshes: {view.RefreshCount}; trigger: {view.LastTrigger.Diagnostic}");
            AppendInvariant($"FishParameter rows: {view.Rows.Count}; IsInLog rows: {view.InLogCount}; max IsInLog RowId: {view.MaxInLogId}");
            report.AppendLine(view.Status.Diagnostic);
            AppendInvariant($"Game build: {collectionReader.GameVersion}; native compatibility: {collectionReader.Compatibility.Diagnostic}");
            // Only include a selection that belongs to the currently published snapshot.
            if (selected is not null && view.Rows.Contains(selected))
            {
                AppendInvariant($"Selected: FishParameter={selected.FishParameterId}; {selected.ItemType}={selected.ItemId}; IsInLog={selected.IsInLog}; name={selected.Name}");
                if (view.CollectionSample is { } sample && sample.FishParameterId == selected.FishParameterId)
                    AppendInvariant($"Raw caught bit: {sample.Value?.ToString() ?? "Unknown"}; [{sample.Reason.Key}] {sample.Reason.Diagnostic}");
            }
            report.AppendLine("Collection=Unknown: diagnostic raw bit only. Compare against the game fish log; no automatic filter or readiness inference.");
            report.AppendLine("Visibility sampled every 250ms via public APIs. Times are monotonic seconds since plugin load.");
            foreach (var entry in history)
                report.AppendLine(entry.Diagnostic);
            return report.ToString();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            framework.Update -= OnUpdate;
            clock.Stop();
        }
    }
}
