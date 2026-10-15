using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DuckbillFisher.Plugin;

internal sealed class MainWindow : Window
{
    public MainWindow() : base("DuckbillFisher###DuckbillFisher.Main")
    {
        Size = new Vector2(460, 180);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("DuckbillFisher 개발 준비 버전");
        ImGui.Separator();
        ImGui.TextWrapped("낚시 정보와 타이머를 준비하고 있습니다.");
        ImGui.TextWrapped("/duckbillfisher 명령으로 이 창을 다시 열 수 있습니다.");
        ImGui.Spacing();
        if (ImGui.Button("닫기"))
            IsOpen = false;
    }
}
