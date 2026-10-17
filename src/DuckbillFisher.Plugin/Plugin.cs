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
    private readonly FishCollectionObserver fishCollection;
    private readonly FishingItemLinkService itemLinks;
    private readonly FishingRegionObserver fishingRegion;
    private readonly SpawnAlerts spawnAlerts;
    private readonly SpawnAlertOutput alertOutput;
    private readonly CompactFishObserver compactObserver;
    private readonly CompactFishWindow compactWindow;
    private readonly FishDetailWindow detailWindow;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, ICondition conditions,
        IClientState clientState, IPlayerState playerState, IDataManager dataManager, IGameGui gameGui, IFramework framework,
        ITextureProvider textureProvider, IPluginLog log, IChatGui chat)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        UiText.SetLanguage(UiLanguage.Korean);

        commandInfo = new CommandInfo(OnCommand) { HelpMessage = UiText.Get(TextKey.CommandHelp) };
        if (!commandManager.AddHandler(Command, commandInfo))
            throw new InvalidOperationException(UiText.Get(TextKey.CommandConflict, Command));

        fishingObserver = new FishingConditionObserver(conditions);
        fishCatalog = new FishCatalog(dataManager, framework);
        var collectionReader = new NativeFishCollectionReader(framework);
        fishGuide = new FishGuideDiagnostics(clientState, playerState, fishCatalog, gameGui, framework, collectionReader);
        fishingMap = new FishingMapService(gameGui, clientState, framework);
        fishPreferences = new FishPreferences(pluginInterface, clientState, playerState, framework, log);
        fishCollection = new FishCollectionObserver(fishPreferences, fishCatalog, collectionReader, dataManager, gameGui, chat, framework, log);
        itemLinks = new FishingItemLinkService(chat, framework, fishPreferences);
        fishingRegion = new FishingRegionObserver(clientState, conditions, dataManager, framework);
        alertOutput = new SpawnAlertOutput(framework, chat, fishPreferences, collectionReader.IsCompatible, log);
        spawnAlerts = new SpawnAlerts(fishCatalog, fishPreferences, fishCollection, framework, alertOutput, log);
        var detailPanel = new FishDetailPanel(fishPreferences, textureProvider, fishingMap, new FishingConditionPanel(textureProvider, itemLinks));
        detailWindow = new FishDetailWindow(fishCatalog, fishPreferences, fishCollection, detailPanel);
        compactObserver = new CompactFishObserver(fishCatalog, fishPreferences, framework, fishCollection, fishingRegion);
        compactWindow = new CompactFishWindow(compactObserver, fishPreferences, fishCollection, textureProvider, detailWindow.Show, fishingMap);
        mainWindow = new MainWindow(fishingObserver, fishGuide, new FishListPanel(fishCatalog, textureProvider, detailPanel,
                fishPreferences, fishCollection, fishingRegion, fishingMap),
            new SpawnAlertsPanel(fishPreferences, spawnAlerts, alertOutput, fishCollection, textureProvider, fishCatalog, detailPanel, fishingMap), fishPreferences,
            () => commandInfo.HelpMessage = UiText.Get(TextKey.CommandHelp));
        mainWindow.ShowCompact = () => compactWindow.IsOpen = true;
        compactWindow.ShowMain = mainWindow.Toggle;
        windows.AddWindow(mainWindow);
        windows.AddWindow(compactWindow);
        windows.AddWindow(detailWindow);
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += mainWindow.OpenSettings;
    }

    private void OnCommand(string command, string arguments)
    {
        if (arguments.Trim().Equals("compact", StringComparison.OrdinalIgnoreCase)) compactWindow.Toggle();
        else mainWindow.Toggle();
    }

    private void OpenMainUi() => mainWindow.IsOpen = true;

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi -= mainWindow.OpenSettings;
        commandManager.RemoveHandler(Command);
        compactObserver.Dispose();
        spawnAlerts.Dispose();
        alertOutput.Dispose();
        fishingObserver.Dispose();
        fishGuide.Dispose();
        fishCatalog.Dispose();
        fishingMap.Dispose();
        fishCollection.Dispose();
        itemLinks.Dispose();
        fishingRegion.Dispose();
        fishPreferences.Dispose();
        windows.RemoveAllWindows();
    }
}
