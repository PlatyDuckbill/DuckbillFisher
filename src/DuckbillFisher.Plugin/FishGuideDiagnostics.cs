using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace DuckbillFisher.Plugin;

internal sealed record FishGuideRow(uint FishParameterId, uint ItemId, string ItemType, string Name, bool IsInLog);

internal sealed record FishGuideDiagnosticView(
    bool HasSample, bool IsLoggedIn, bool? IsLoaded, bool FishGuideVisible, bool FishingNoteVisible,
    bool LoginFallback, int Session, int OpenCount, int RefreshCount, string LastTrigger, string Status,
    int InLogCount, uint MaxInLogId, IReadOnlyList<FishGuideRow> Rows, IReadOnlyList<string> History)
{
    public FishBitSample? CollectionSample { get; init; }
}

/// <summary>Public guide/sheet observation, plus an isolated diagnostic reader for a selected fish bit.</summary>
internal sealed class FishGuideDiagnostics : IDisposable
{
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IDataManager dataManager;
    private readonly IGameGui gameGui;
    private readonly IFramework framework;
    private readonly NativeFishCollectionReader collectionReader;
    private readonly object gate = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Queue<string> history = new();
    private FishGuideDiagnosticView view = new(false, false, null, false, false, false, 0, 0, 0,
        "없음", "수첩을 열면 어종 목록을 읽습니다. 수집 여부는 아직 확인 전입니다.", 0, 0, [], []);
    private FishGuideRow[]? cachedRows;
    private TimeSpan nextPoll;
    private ulong characterId;
    private bool manualRefresh;
    private string? pendingTrigger;
    private uint? trackedFishId;
    private uint? pendingFishId;
    private bool disposed;

    public FishGuideDiagnostics(IClientState clientState, IPlayerState playerState, IDataManager dataManager,
        IGameGui gameGui, IFramework framework)
    {
        this.clientState = clientState;
        this.playerState = playerState;
        this.dataManager = dataManager;
        this.gameGui = gameGui;
        this.framework = framework;
        collectionReader = new NativeFishCollectionReader(framework);
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
                pendingTrigger = "로그인 후 갱신 옵션 켜기";
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
                var status = $"Unknown: 공개 API 확인 실패 ({ex.GetType().Name}).";
                if (view.Status != status)
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
                LastTrigger = "없음", OpenCount = 0, RefreshCount = 0,
                CollectionSample = null,
                Status = "수첩을 열면 어종 목록을 읽습니다. 수집 여부는 아직 확인 전입니다.",
            };
            AddHistory(currentCharacter == 0 ? "세션 해제: 이전 표시 폐기" : "캐릭터 준비: 새 세션");
            if (currentCharacter != 0 && view.LoginFallback)
                pendingTrigger = "로그인 후 준비 확인";
            else if (currentCharacter != 0 && (guide || note))
                pendingTrigger = "캐릭터 준비 시 수첩이 이미 열려 있음";
        }

        if (!oldView.HasSample || loggedIn != oldView.IsLoggedIn || loaded != oldView.IsLoaded)
            AddHistory($"상태: IsLoggedIn={loggedIn}, IsLoaded={loaded?.ToString() ?? "조회 안 함"}");

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
                AddHistory("수동 갱신 생략: 로그인과 캐릭터 준비 필요");
            manualRefresh = false;
            pendingFishId = null;
            view = view with { Rows = [], InLogCount = 0, MaxInLogId = 0, CollectionSample = null, Status = "Unknown: 로그인과 캐릭터 준비를 기다립니다." };
            return;
        }

        if (manualRefresh)
            pendingTrigger = "수동 새로 읽기";
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
                ? new FishBitSample(fishId, null, "Unknown: 현재 어종 목록에 없는 ID입니다.")
                : collectionReader.Read(row, characterId, guide);
            view = view with { CollectionSample = sample };
            AddHistory($"수집 비트 조회: FishParameter={fishId}; raw={sample.Value?.ToString() ?? "Unknown"}; {sample.Reason}");
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
            AddHistory($"{name}: {(visible ? "표시" : "닫힘")}{(!hadSample ? " (첫 표본)" : "")}");
        if (!visible || (hadSample && wasVisible))
            return;
        view = view with { OpenCount = view.OpenCount + 1 };
        pendingTrigger = hadSample ? $"수첩 열림: {name}" : $"로드 시 이미 표시됨: {name}";
        if (name == "FishGuide2")
            pendingFishId = trackedFishId;
    }

    private void RefreshRows(string trigger, bool reload)
    {
        try
        {
            if (cachedRows is null || reload)
            {
                var sheet = dataManager.GetExcelSheet<FishParameter>()
                    ?? throw new InvalidOperationException("FishParameter sheet unavailable.");
                var rows = new List<FishGuideRow>();
                foreach (var fish in sheet)
                {
                    var item = fish.Item;
                    var itemType = item.RowType?.Name ?? "Unknown";
                    var name = item.Is<Item>() ? item.GetValueOrDefault<Item>()?.Name.ExtractText()
                        : item.Is<EventItem>() ? item.GetValueOrDefault<EventItem>()?.Name.ExtractText() : null;
                    rows.Add(new FishGuideRow(fish.RowId, item.RowId, itemType,
                        string.IsNullOrWhiteSpace(name) ? "(참조 행/이름 확인 불가)" : name, fish.IsInLog));
                }
                cachedRows = rows.ToArray();
            }
            var inLog = cachedRows.Where(row => row.IsInLog).ToArray();
            if (inLog.Length == 0)
                throw new InvalidDataException("No FishParameter rows eligible for the fish log.");
            view = view with
            {
                Rows = cachedRows, InLogCount = inLog.Length,
                MaxInLogId = inLog.Length == 0 ? 0 : inLog.Max(row => row.FishParameterId),
                RefreshCount = view.RefreshCount + 1, LastTrigger = trigger,
                Status = "어종 목록 읽기 완료 · 수집 여부 Unknown (확인 전)",
            };
            AddHistory($"갱신 #{view.RefreshCount}: {trigger}; 전체 {cachedRows.Length} / IsInLog {inLog.Length}");
            if (view.FishGuideVisible && trackedFishId is not null)
                pendingFishId = trackedFishId;
        }
        catch (Exception ex)
        {
            cachedRows = null;
            pendingFishId = null;
            view = view with
            {
                Rows = [], InLogCount = 0, MaxInLogId = 0, LastTrigger = trigger, CollectionSample = null,
                Status = $"Unknown: 어종 목록 읽기 실패 ({ex.GetType().Name}). 수동 새로 읽기로 재시도하세요.",
            };
            AddHistory(view.Status);
        }
    }

    private void AddHistory(string message)
    {
        if (history.Count == 80)
            history.Dequeue();
        history.Enqueue(string.Create(CultureInfo.InvariantCulture, $"+{clock.Elapsed.TotalSeconds:F3}s {message}"));
    }

    public string CreateReport(FishGuideRow? selected)
    {
        lock (gate)
        {
            var report = new StringBuilder();
            report.AppendLine("DuckbillFisher ENV-03/fish-guide-native/v1");
            report.AppendLine($"Plugin: {typeof(Plugin).Assembly.GetName().Version}; Dalamud: {typeof(IPlayerState).Assembly.GetName().Version}");
            report.AppendLine($"Session: {view.Session}; sampled: {view.HasSample}; IsLoggedIn={view.IsLoggedIn}; IsLoaded={view.IsLoaded?.ToString() ?? "not sampled"}");
            report.AppendLine($"FishGuide2={view.FishGuideVisible}; FishingNote={view.FishingNoteVisible}; loginFallback={view.LoginFallback}");
            report.AppendLine($"Open samples: {view.OpenCount}; refreshes: {view.RefreshCount}; trigger: {view.LastTrigger}");
            report.AppendLine($"FishParameter rows: {view.Rows.Count}; IsInLog rows: {view.InLogCount}; max IsInLog RowId: {view.MaxInLogId}");
            report.AppendLine(view.Status);
            report.AppendLine($"Game build: {collectionReader.GameVersion}; native compatibility: {collectionReader.Compatibility}");
            // Only include a selection that belongs to the currently published snapshot.
            if (selected is not null && view.Rows.Contains(selected))
            {
                report.AppendLine($"Selected: FishParameter={selected.FishParameterId}; {selected.ItemType}={selected.ItemId}; IsInLog={selected.IsInLog}; name={selected.Name}");
                if (view.CollectionSample is { } sample && sample.FishParameterId == selected.FishParameterId)
                    report.AppendLine($"Raw caught bit: {sample.Value?.ToString() ?? "Unknown"}; {sample.Reason}");
            }
            report.AppendLine("Collection=Unknown: diagnostic raw bit only. Compare against the game fish log; no automatic filter or readiness inference.");
            report.AppendLine("Visibility sampled every 250ms via public APIs. Times are monotonic seconds since plugin load.");
            foreach (var entry in history)
                report.AppendLine(entry);
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
