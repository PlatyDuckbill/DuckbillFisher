using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace DuckbillFisher.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/duckbillfisher";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly WindowSystem windows = new("DuckbillFisher");
    private readonly MainWindow mainWindow = new();
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;

        if (!commandManager.AddHandler(Command, new CommandInfo(OnCommand)
            { HelpMessage = "DuckbillFisher 창을 여닫습니다." }))
            throw new InvalidOperationException($"Command already registered: {Command}");

        windows.AddWindow(mainWindow);
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
    }

    private void OnCommand(string command, string arguments) => mainWindow.Toggle();

    private void OpenMainUi() => mainWindow.IsOpen = true;

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        commandManager.RemoveHandler(Command);
        windows.RemoveAllWindows();
    }
}
