using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DuckbillFisher.Plugin;

internal sealed class MainWindow : Window
{
    private static readonly string[] Markers = ["찌 던짐", "입질 대기", "입질 확인", "낚아챔", "취소/회수", "낚시 종료", "생미끼 시전", "지역 이동", "로그아웃"];
    private readonly FishingConditionObserver observer;
    private readonly FishGuideDiagnostics fishGuide;
    private IReadOnlyList<FishGuideRow>? lastRows;
    private FishGuideRow[] matches = [];
    private FishGuideRow? selectedFish;
    private string search = "";
    private int lastSession = -1;
    private int selectedMarker;

    public MainWindow(FishingConditionObserver observer, FishGuideDiagnostics fishGuide) : base("DuckbillFisher###DuckbillFisher.Main")
    {
        this.observer = observer;
        this.fishGuide = fishGuide;
        Size = new Vector2(820, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(520, 400), MaximumSize = new Vector2(float.MaxValue) };
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("DiagnosticsTabs"))
        {
            if (ImGui.BeginTabItem("수첩 연동 확인"))
            {
                DrawFishGuide();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("낚시 상태 관찰"))
            {
                DrawFishingObservation();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawFishGuide()
    {
        var view = fishGuide.View;
        if (!ReferenceEquals(lastRows, view.Rows) || lastSession != view.Session)
        {
            lastRows = view.Rows;
            lastSession = view.Session;
            selectedFish = null;
            UpdateMatches(view.Rows);
        }

        ImGui.TextWrapped("게임의 어류도감을 연 상태에서 아래 어종 이름을 클릭하면 실제 수집 비트를 조회합니다. 아직 검증 중이며 자동 필터에는 반영하지 않습니다.");
        ImGui.TextWrapped(view.Status);
        ImGui.Separator();
        ImGui.TextUnformatted($"로그인: {view.IsLoggedIn} | 캐릭터 준비: {view.IsLoaded?.ToString() ?? "조회 전"}");
        ImGui.TextUnformatted($"어류도감: {(view.FishGuideVisible ? "열림" : "닫힘")} | 낚시 수첩: {(view.FishingNoteVisible ? "열림" : "닫힘")}");
        ImGui.TextUnformatted($"열림 표본 {view.OpenCount}회 | 목록 갱신 {view.RefreshCount}회 | 마지막 계기: {view.LastTrigger}");
        var fallback = view.LoginFallback;
        if (ImGui.Checkbox("로그인 준비 후에도 갱신", ref fallback))
            fishGuide.SetLoginFallback(fallback);
        if (ImGui.Button("어종 목록 새로 읽기"))
            fishGuide.RequestRefresh();
        ImGui.SameLine();
        if (ImGui.Button("수첩 진단 복사"))
            ImGui.SetClipboardText(fishGuide.CreateReport(selectedFish));
        ImGui.TextUnformatted($"전체 행 {view.Rows.Count} | 수첩 포함 {view.InLogCount} | 수첩 포함 최대 ID {view.MaxInLogId}");

        ImGui.SetNextItemWidth(300);
        if (ImGui.InputText("어종 이름 또는 FishParameter ID", ref search, 100))
            UpdateMatches(view.Rows);
        ImGui.TextUnformatted("수첩에 포함된 검색 결과 중 최대 30종을 표시합니다.");
        if (ImGui.BeginChild("FishMatches", new Vector2(0, 120), true))
        {
            foreach (var fish in matches)
            {
                if (ImGui.Selectable($"{fish.Name} (ID {fish.FishParameterId})###Fish{fish.FishParameterId}",
                    selectedFish == fish))
                {
                    selectedFish = fish;
                    fishGuide.RequestCollectionRead(fish.FishParameterId);
                }
            }
        }
        ImGui.EndChild();
        if (selectedFish is { } selected)
        {
            ImGui.TextWrapped($"{selected.Name} | FishParameter {selected.FishParameterId} | {selected.ItemType} {selected.ItemId} | IsInLog={selected.IsInLog} | 수집 여부: Unknown");
            if (view.CollectionSample is { } sample && sample.FishParameterId == selected.FishParameterId)
                ImGui.TextWrapped($"마지막 수집 비트: {sample.Value?.ToString() ?? "Unknown"} | {sample.Reason}");
            else
                ImGui.TextUnformatted("수집 비트 조회 대기 중입니다.");
            if (ImGui.Button("선택 어종 수집 비트 다시 읽기"))
                fishGuide.RequestCollectionRead(selected.FishParameterId);
        }
        if (ImGui.CollapsingHeader("준비 상태와 수첩 열림 기록"))
        {
            if (ImGui.BeginChild("FishGuideHistory", new Vector2(0, 130), true, ImGuiWindowFlags.HorizontalScrollbar))
            {
                for (var i = view.History.Count - 1; i >= 0; i--)
                    ImGui.TextUnformatted(view.History[i]);
            }
            ImGui.EndChild();
        }
    }

    private void UpdateMatches(IReadOnlyList<FishGuideRow> rows)
    {
        var term = search.Trim();
        matches = rows.Where(row => row.IsInLog &&
                (term.Length == 0 || row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 row.FishParameterId.ToString(System.Globalization.CultureInfo.InvariantCulture) == term))
            .Take(30).ToArray();
    }

    private void DrawFishingObservation()
    {
        ImGui.TextUnformatted("ENV-03 낚시 상태 관찰");
        ImGui.Separator();
        ImGui.TextWrapped("직접 낚시하면서 세 상태의 변화를 관찰합니다. 시전·입질 자동 판정은 아직 제공하지 않습니다.");
        var view = observer.View;
        ImGui.BeginDisabled(view.IsRecording);
        if (ImGui.Button("새 관찰 시작"))
            observer.Start();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!view.IsRecording);
        if (ImGui.Button("관찰 중지"))
            observer.Stop();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("기록 복사"))
            ImGui.SetClipboardText(observer.CreateReport());

        // A button may have changed the cached snapshot in this frame.
        view = observer.View;
        ImGui.TextUnformatted($"{(view.IsRecording ? "관찰 중" : "중지됨")} | 기록 {view.Entries.Count}/200 | 오래된 기록 제외 {view.DroppedCount}");
        ImGui.TextWrapped("새 관찰은 이전 기록을 지웁니다. 닫아도 관찰은 계속되며, 언로드하면 기록이 사라집니다.");
        ImGui.BeginDisabled(!view.IsRecording);
        ImGui.SetNextItemWidth(180);
        ImGui.Combo("수동 메모", ref selectedMarker, Markers, Markers.Length);
        ImGui.SameLine();
        if (ImGui.Button("메모 추가"))
            observer.Mark(Markers[selectedMarker]);
        ImGui.EndDisabled();
        ImGui.TextWrapped("메모 시간은 버튼을 누른 시각입니다. 실제 게임 동작을 감지한 시각이 아닙니다.");
        if (view.Entries.Count > 0)
        {
            var last = view.Entries[^1].Conditions;
            ImGui.TextWrapped($"마지막 표본: Fishing={last.Fishing} | Gathering={last.ExecutingGatheringAction} | Casting={last.Casting}");
        }
        ImGui.TextUnformatted("아래는 최신 기록부터 표시합니다. 복사한 보고서는 시간순입니다.");
        if (ImGui.BeginChild("ObservationHistory", new Vector2(0, -40), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            for (var i = view.Entries.Count - 1; i >= 0; i--)
                ImGui.TextUnformatted(FishingConditionObserver.FormatEntry(view.Entries[i]));
        }
        ImGui.EndChild();
        ImGui.Spacing();
        if (ImGui.Button("닫기"))
            IsOpen = false;
    }
}
