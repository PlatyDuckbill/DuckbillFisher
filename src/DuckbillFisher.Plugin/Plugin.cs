using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/duckbillfisher";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly CommandInfo commandInfo;
    private readonly WindowSystem windows = new("DuckbillFisher");
    private readonly MainWindow mainWindow;
    private readonly FishingConditionObserver fishingObserver;
    private readonly FishGuideDiagnostics fishGuide;
    private readonly FishCatalog fishCatalog;
    private readonly FishingMapService fishingMap;
    private readonly FishPreferences fishPreferences;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, ICondition conditions,
        IClientState clientState, IPlayerState playerState, IDataManager dataManager, IGameGui gameGui, IFramework framework,
        ITextureProvider textureProvider, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        UiText.SetLanguage(UiLanguage.Korean);

        commandInfo = new CommandInfo(OnCommand) { HelpMessage = UiText.Get(TextKey.CommandHelp) };
        if (!commandManager.AddHandler(Command, commandInfo))
            throw new InvalidOperationException(UiText.Get(TextKey.CommandConflict, Command));

        fishingObserver = new FishingConditionObserver(conditions);
        fishCatalog = new FishCatalog(dataManager, framework);
        fishGuide = new FishGuideDiagnostics(clientState, playerState, fishCatalog, gameGui, framework);
        fishingMap = new FishingMapService(gameGui, clientState, framework);
        fishPreferences = new FishPreferences(pluginInterface, clientState, playerState, framework, log);
        mainWindow = new MainWindow(fishingObserver, fishGuide, new FishListPanel(fishCatalog, textureProvider, fishingMap, fishPreferences),
            () => commandInfo.HelpMessage = UiText.Get(TextKey.CommandHelp));
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
        fishingObserver.Dispose();
        fishGuide.Dispose();
        fishCatalog.Dispose();
        fishingMap.Dispose();
        fishPreferences.Dispose();
        windows.RemoveAllWindows();
    }
}
