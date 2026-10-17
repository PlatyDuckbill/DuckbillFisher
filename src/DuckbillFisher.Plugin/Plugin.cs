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
    private readonly FishingActivityProbe fishingObserver;
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
    private readonly FishGuideOpenService guideOpener;
    private readonly FishingTracker fishingTracker;
    private readonly CastingTimerWindow timerWindow;
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

        fishCatalog = new FishCatalog(dataManager, framework);
        var collectionReader = new NativeFishCollectionReader(framework);
        fishGuide = new FishGuideDiagnostics(clientState, playerState, fishCatalog, gameGui, framework, collectionReader);
        fishingMap = new FishingMapService(gameGui, clientState, framework);
        fishPreferences = new FishPreferences(pluginInterface, clientState, playerState, framework, log);
        var activityReader = new NativeFishingActivityReader(framework, collectionReader);
        fishingObserver = new FishingActivityProbe(conditions, clientState, playerState, framework, chat,
            dataManager, fishPreferences, activityReader);
        guideOpener = new FishGuideOpenService(gameGui, clientState, playerState, framework, fishPreferences, fishCatalog, collectionReader);
        fishCollection = new FishCollectionObserver(fishPreferences, fishCatalog, collectionReader, dataManager, gameGui, chat, framework, log);
        fishingTracker = new FishingTracker(clientState, playerState, conditions, framework, chat, dataManager,
            fishPreferences, fishCatalog, fishCollection, activityReader, new FishingHistoryStore(pluginInterface.GetPluginConfigDirectory()));
        itemLinks = new FishingItemLinkService(chat, framework, fishPreferences);
        fishingRegion = new FishingRegionObserver(clientState, conditions, dataManager, framework);
        alertOutput = new SpawnAlertOutput(framework, chat, fishPreferences, collectionReader.IsCompatible, log);
        spawnAlerts = new SpawnAlerts(fishCatalog, fishPreferences, fishCollection, framework, alertOutput, log);
        var detailPanel = new FishDetailPanel(fishPreferences, textureProvider, fishingMap, new FishingConditionPanel(textureProvider, itemLinks));
        detailWindow = new FishDetailWindow(fishCatalog, fishPreferences, fishCollection, detailPanel, guideOpener);
        timerWindow = new CastingTimerWindow(fishingTracker, fishCatalog, fishPreferences, fishCollection, textureProvider, fishingMap, detailWindow.Show);
        compactObserver = new CompactFishObserver(fishCatalog, fishPreferences, framework, fishCollection, fishingRegion);
        compactWindow = new CompactFishWindow(compactObserver, fishPreferences, fishCollection, textureProvider, detailWindow.Show, fishingMap);
        mainWindow = new MainWindow(fishingObserver, fishGuide, new FishListPanel(fishCatalog, textureProvider, detailPanel,
                fishPreferences, fishCollection, fishingRegion, fishingMap, guideOpener),
            new SpawnAlertsPanel(fishPreferences, spawnAlerts, alertOutput, fishCollection, textureProvider, fishCatalog, detailPanel, fishingMap, guideOpener), fishPreferences,
            () => commandInfo.HelpMessage = UiText.Get(TextKey.CommandHelp),
            new FishingHistoryBrowser(fishingTracker, fishCatalog, new FishingHistoryPanel(fishingTracker, textureProvider, detailWindow.Show)));
        mainWindow.ShowCompact = () => compactWindow.IsOpen = true;
        mainWindow.ShowTimer = timerWindow.Toggle;
        compactWindow.ShowMain = mainWindow.Toggle;
        windows.AddWindow(mainWindow);
        windows.AddWindow(compactWindow);
        windows.AddWindow(detailWindow);
        windows.AddWindow(timerWindow);
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += mainWindow.OpenSettings;
    }

    private void OnCommand(string command, string arguments)
    {
        if (arguments.Trim().Equals("compact", StringComparison.OrdinalIgnoreCase)) compactWindow.Toggle();
        else if (arguments.Trim().Equals("timer", StringComparison.OrdinalIgnoreCase)) timerWindow.Toggle();
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
        fishingTracker.Dispose();
        fishGuide.Dispose();
        guideOpener.Dispose();
        fishCatalog.Dispose();
        fishingMap.Dispose();
        fishCollection.Dispose();
        itemLinks.Dispose();
        fishingRegion.Dispose();
        fishPreferences.Dispose();
        windows.RemoveAllWindows();
    }
}
