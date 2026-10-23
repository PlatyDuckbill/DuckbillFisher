using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/duckbillfisher";
    private const string ShortCommand = "/dbfs";
    private readonly bool shortCommandRegistered;
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
    private readonly SpawnAlertsPanel spawnAlertsPanel;
    private readonly CompactFishObserver compactObserver;
    private readonly CompactFishWindow compactWindow;
    private readonly FishDetailWindow detailWindow;
    private readonly FishDetailWindow compareDetailWindow;
    private readonly FishGuideOpenService guideOpener;
    private readonly FishingTracker fishingTracker;
    private readonly CastingTimerWindow timerWindow;
    private readonly FishingBuffObserver fishingBuffs;
    private readonly CastingTimerNotifications timerNotifications;
    private readonly FishingSoundNotifications fishingSounds;
    private readonly CastingTimerSettingsWindow timerSettings;
    private readonly BaitStockObserver baitStock;
    private readonly BaitPreparation baitPreparation;
    private readonly BaitUiProbe baitUiProbe;
    private readonly BaitFinder baitFinder;
    private readonly CollectionProgress collectionProgress;
    private readonly SpawnSchedule schedule;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, ICondition conditions,
        IClientState clientState, IPlayerState playerState, IDataManager dataManager, IGameGui gameGui, IFramework framework,
        ITextureProvider textureProvider, IPluginLog log, IChatGui chat, IGameInventory inventory, IAddonLifecycle addonLifecycle,
        IObjectTable objectTable)
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
            fishPreferences, fishCatalog, fishCollection, activityReader,
            new FishingHistoryStore(pluginInterface.GetPluginConfigDirectory(), (writes, deletes, blocked) =>
                log.Warning("FishingHistoryDrainIncomplete: Writes={Writes}; Deletes={Deletes}; ReadBlocked={ReadBlocked}", writes, deletes, blocked)));
        itemLinks = new FishingItemLinkService(chat, framework, fishPreferences);
        fishingRegion = new FishingRegionObserver(clientState, conditions, dataManager, framework);
        alertOutput = new SpawnAlertOutput(framework, chat, fishPreferences, collectionReader.IsCompatible, log);
        timerNotifications = new CastingTimerNotifications(fishingTracker, fishPreferences, alertOutput, framework, log);
        fishingSounds = new FishingSoundNotifications(fishingTracker, fishCollection, fishCatalog, fishPreferences, alertOutput, framework, log);
        spawnAlerts = new SpawnAlerts(fishCatalog, fishPreferences, fishCollection, framework, alertOutput, log);
        baitStock = new BaitStockObserver(inventory, clientState, playerState, conditions, framework, fishPreferences, () => collectionReader.IsCompatible);
        var preparationQuery = new CatalogQueryState(fishPreferences.View.Catalog);
        baitPreparation = new BaitPreparation(fishCatalog, fishPreferences, fishCollection, fishingRegion, preparationQuery, framework);
        var baitReader = new NativeBaitUiReader(gameGui, framework, () => collectionReader.IsCompatible, dataManager);
        baitFinder = new BaitFinder(framework, addonLifecycle, fishPreferences, baitStock, baitReader,
            () => collectionReader.IsCompatible, () =>
            {
                var personal = fishPreferences.View;
                if (!personal.HasCharacter || !clientState.IsLoggedIn || !playerState.IsLoaded || playerState.ContentId != personal.CharacterId)
                    return null;
                var sample = activityReader.Read(personal.CharacterId, playerState.ClassJob.RowId == FishingGameIds.FisherJob);
                return sample.Availability == FishingReadState.Ready ? sample.EquippedBait : null;
            });
        baitUiProbe = new BaitUiProbe(gameGui, framework, addonLifecycle, fishPreferences, () => collectionReader.IsCompatible, baitReader, baitFinder);
        var preparationPanel = new BaitPreparationPanel(baitPreparation, baitStock, fishPreferences, textureProvider, itemLinks, baitUiProbe);
        var preparationWindow = new BaitPreparationWindow(preparationPanel);
        var detailPanel = new FishDetailPanel(fishPreferences, textureProvider, fishingMap, new FishingConditionPanel(textureProvider, itemLinks, baitStock, baitFinder));
        detailWindow = new FishDetailWindow(fishCatalog, fishPreferences, fishCollection, detailPanel, guideOpener);
        compareDetailWindow = new FishDetailWindow(fishCatalog, fishPreferences, fishCollection, detailPanel, guideOpener, comparison: true);
        detailPanel.OpenRelated = (origin, itemId) =>
        {
            if (!fishCatalog.View.RelatedRows.TryGetValue(itemId, out var fish)) return;
            if (compareDetailWindow.IsOpen && compareDetailWindow.SelectedId == origin) compareDetailWindow.Navigate(fish);
            else compareDetailWindow.Show(fish);
        };
        var spotFishWindow = new SpotFishWindow(fishingTracker, fishCatalog, fishPreferences, fishCollection, textureProvider, fishingMap, detailWindow.Show);
        fishingBuffs = new FishingBuffObserver(objectTable, clientState, playerState, conditions, framework, dataManager,
            fishPreferences, fishingTracker, () => collectionReader.IsCompatible);
        timerWindow = new CastingTimerWindow(fishingTracker, fishingBuffs, textureProvider, detailWindow.Show, spotFishWindow.Toggle);
        timerSettings = new CastingTimerSettingsWindow(fishPreferences, alertOutput, fishingSounds);
        timerWindow.ShowSettings = () => timerSettings.IsOpen = true;
        compactObserver = new CompactFishObserver(fishCatalog, fishPreferences, framework, fishCollection, fishingRegion);
        compactWindow = new CompactFishWindow(compactObserver, fishPreferences, fishCollection, textureProvider, detailWindow.Show, fishingMap);
        spawnAlertsPanel = new SpawnAlertsPanel(fishPreferences, spawnAlerts, alertOutput, fishCollection, textureProvider,
            fishCatalog, detailPanel, fishingMap, guideOpener);
        collectionProgress = new CollectionProgress(fishCatalog, fishPreferences, fishCollection, framework);
        schedule = new SpawnSchedule(fishCatalog, fishPreferences, fishCollection, fishingRegion, preparationQuery, framework, log);
        var collectionFishWindow = new CollectionProgressFishWindow(collectionProgress, fishCatalog, fishPreferences,
            fishCollection, textureProvider, fishingMap, detailWindow.Show);
        mainWindow = new MainWindow(fishingObserver, fishGuide, new FishListPanel(fishCatalog, textureProvider, detailPanel,
                fishPreferences, fishCollection, fishingRegion, fishingMap, guideOpener, preparationQuery, () => preparationWindow.IsOpen = true, detailWindow.Show),
            spawnAlertsPanel, fishPreferences,
            () => commandInfo.HelpMessage = UiText.Get(TextKey.CommandHelp),
            new FishingHistoryBrowser(fishingTracker, fishCatalog, new FishingHistoryPanel(fishingTracker, textureProvider, detailWindow.Show)),
            new CollectionProgressPanel(collectionProgress, fishCatalog, fishPreferences, fishCollection, guideOpener, collectionFishWindow.Show),
            new SpawnSchedulePanel(schedule, fishCatalog, fishPreferences, textureProvider, detailWindow.Show, fishingMap));
        mainWindow.ShowCompact = compactWindow.Toggle;
        mainWindow.ShowTimer = timerWindow.Toggle;
        timerSettings.ShowOutputSettings = mainWindow.OpenSettings;
        var changelogWindow = new ChangelogWindow(fishPreferences);
        mainWindow.ShowChangelog = () => changelogWindow.IsOpen = true;
        mainWindow.ShowUnreadChangelog = changelogWindow.ShowIfUnread;
        compactWindow.ShowMain = mainWindow.Toggle;
        windows.AddWindow(mainWindow);
        windows.AddWindow(compactWindow);
        windows.AddWindow(detailWindow);
        windows.AddWindow(compareDetailWindow);
        windows.AddWindow(timerWindow);
        windows.AddWindow(timerSettings);
        windows.AddWindow(spotFishWindow);
        windows.AddWindow(collectionFishWindow);
        windows.AddWindow(preparationWindow);
        windows.AddWindow(changelogWindow);
        pluginInterface.UiBuilder.Draw += DrawWindows;
        pluginInterface.UiBuilder.Draw += baitFinder.DrawOverlay;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += mainWindow.OpenSettings;
        shortCommandRegistered = commandManager.AddHandler(ShortCommand, commandInfo);
        if (!shortCommandRegistered)
            log.Warning("{CommandConflict}", UiText.Get(TextKey.CommandConflict, ShortCommand));
    }

    private void OnCommand(string command, string arguments)
    {
        var action = arguments.Trim();
        if (action.Equals("compact", StringComparison.OrdinalIgnoreCase) || action.Equals("cl", StringComparison.OrdinalIgnoreCase)) compactWindow.Toggle();
        else if (action.Equals("timer", StringComparison.OrdinalIgnoreCase) || action.Equals("ct", StringComparison.OrdinalIgnoreCase)) timerWindow.Toggle();
        else mainWindow.Toggle();
    }

    private void OpenMainUi() => mainWindow.IsOpen = true;

    private void DrawWindows() => UiScale.Draw(windows, fishPreferences.View.UiScalePercent);

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        pluginInterface.UiBuilder.Draw -= DrawWindows;
        pluginInterface.UiBuilder.Draw -= baitFinder.DrawOverlay;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi -= mainWindow.OpenSettings;
        commandManager.RemoveHandler(Command);
        if (shortCommandRegistered) commandManager.RemoveHandler(ShortCommand);
        compactObserver.Dispose();
        collectionProgress.Dispose();
        schedule.Dispose();
        baitPreparation.Dispose();
        baitStock.Dispose();
        baitUiProbe.Dispose();
        baitFinder.Dispose();
        spawnAlerts.Dispose();
        timerNotifications.Dispose();
        timerSettings.Dispose();
        fishingSounds.Dispose();
        spawnAlertsPanel.Dispose();
        alertOutput.Dispose();
        fishingObserver.Dispose();
        fishingTracker.Dispose();
        fishingBuffs.Dispose();
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
