using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class BaitPreparationWindow : Window
{
    private readonly BaitPreparationPanel panel;

    public BaitPreparationWindow(BaitPreparationPanel panel) : base("DuckbillFisher###DuckbillFisher.BaitPreparation")
    {
        this.panel = panel;
        Size = new Vector2(540, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        BgAlpha = UiWidgets.BackgroundAlpha;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 280), MaximumSize = new Vector2(float.MaxValue),
        };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public override void PreDraw() => WindowName = $"{UiText.Get(T.BaitPreparation)}###DuckbillFisher.BaitPreparation";
    public override void Draw() => panel.Draw();
}
