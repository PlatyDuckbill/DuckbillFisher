using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DuckbillFisher.Plugin;

internal sealed class MainWindow : Window
{
    private static readonly string[] Markers = ["찌 던짐", "입질 대기", "입질 확인", "낚아챔", "취소/회수", "낚시 종료", "생미끼 시전", "지역 이동", "로그아웃"];
    private readonly FishingConditionObserver observer;
    private int selectedMarker;

    public MainWindow(FishingConditionObserver observer) : base("DuckbillFisher###DuckbillFisher.Main")
    {
        this.observer = observer;
        Size = new Vector2(820, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(520, 400), MaximumSize = new Vector2(float.MaxValue) };
    }

    public override void Draw()
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
