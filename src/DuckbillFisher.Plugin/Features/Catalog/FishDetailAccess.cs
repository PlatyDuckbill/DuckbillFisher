using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Shared, indexed entry points. Item menus are added only for uniquely resolved fish items.</summary>
internal sealed class FishDetailAccess : Window, IDisposable
{
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly IContextMenu menus;
    private readonly IFramework framework;
    private readonly Action<FishGuideRow> show;
    private readonly NativeChatItemContextReader chatItems;
    private readonly IPluginLog log;
    private (ChatItemContextState State, bool Fish)? lastChatResult;
    private IReadOnlyList<FishGuideRow>? rows;
    private IReadOnlyDictionary<uint, FishGuideRow> byItem = new Dictionary<uint, FishGuideRow>();
    private FishGuideRow[] candidates = [];
    private readonly object gate = new();
    private (string Text, int Session)? request;
    private (uint Id, int Session)? selection;
    private int session = -1;
    private string search = "";

    public FishDetailAccess(FishCatalog catalog, FishPreferences preferences, IContextMenu menus,
        IFramework framework, IPluginLog log, Action<FishGuideRow> show) : base("DuckbillFisher###DuckbillFisher.DetailSearch")
    {
        this.catalog = catalog; this.preferences = preferences; this.menus = menus; this.framework = framework; this.show = show;
        this.log = log; chatItems = new(framework);
        Size = new(420, 320); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new() { MinimumSize = new(280, 160), MaximumSize = new(float.MaxValue) };
        BgAlpha = UiWidgets.BackgroundAlpha;
        menus.OnMenuOpened += OnMenuOpened;
        framework.Update += Update;
    }

    public void Search(string value)
    {
        lock (gate) request = (value.Trim().Trim('"').Trim(), preferences.View.Session);
        catalog.RequestLoad();
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        uint itemId;
        if (args.Target is MenuTargetInventory { TargetItem: { } item }) itemId = item.BaseItemId;
        else
        {
            var context = chatItems.Read(args);
            var baseId = ItemUtil.GetBaseId(context.RawItemId).Item1;
            var result = (context.State, context.State == ChatItemContextState.Ready && ItemUtil.IsNormalItem(baseId) && byItem.ContainsKey(baseId));
            var chatAddon = args.AddonName?.StartsWith("ChatLog", StringComparison.Ordinal) == true;
            if ((chatAddon || context.State is not (ChatItemContextState.NotDefaultMenu or ChatItemContextState.OtherOwner)) && lastChatResult != result)
            {
                lastChatResult = result;
                log.Debug("FishDetailContext/v1: State={State}; FishMatch={FishMatch}", result.State, result.Item2);
            }
            if (!result.Item2) return;
            itemId = baseId;
        }
        if (!byItem.TryGetValue(itemId, out var fish)) return;
        var expectedSession = preferences.View.Session;
        var id = fish.FishParameterId;
        args.AddMenuItem(new MenuItem
        {
            Name = new SeStringBuilder().AddText(UiText.Get(T.FishContextDetail)).Build(),
            OnClicked = _ => { lock (gate) selection = (id, expectedSession); },
        });
    }

    private void Update(IFramework _)
    {
        lock (gate) UpdatePending();
    }

    private void UpdatePending()
    {
        var current = catalog.View;
        var personal = preferences.View;
        if (session != personal.Session)
        {
            session = personal.Session; selection = null; candidates = []; IsOpen = false;
        }
        if (!ReferenceEquals(rows, current.Rows))
        {
            rows = current.Rows;
            byItem = rows.Where(f => f.IsDisplayable && f.ItemType == "Item").GroupBy(f => f.ItemId)
                .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        }
        if (selection is { } chosen)
        {
            selection = null;
            if (chosen.Session == session && current.RowsById.TryGetValue(chosen.Id, out var fish)) show(fish);
        }
        if (request is not { } query || rows.Count == 0) return;
        request = null;
        if (query.Session != session) return;
        var text = query.Text;
        search = text;
        var exact = rows.Where(f => f.IsDisplayable && f.Name.Equals(text, StringComparison.OrdinalIgnoreCase)).ToArray();
        candidates = text.Length == 0 ? [] : exact.Length > 0 ? exact : rows.Where(f => f.IsDisplayable &&
            f.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.Name, ClientNameOrder.Comparer).ToArray();
        if (candidates.Length == 1) { IsOpen = false; show(candidates[0]); }
        else IsOpen = true;
    }

    public override void PreDraw() => WindowName = $"{UiText.Get(T.FishSearchTitle)}###DuckbillFisher.DetailSearch";
    public override void Draw()
    {
        ImGui.TextWrapped(UiText.Get(T.FishSearchResults, search, candidates.Length));
        if (ImGui.BeginChild("DetailSearchResults", Vector2.Zero))
            foreach (var fish in candidates)
            {
                if (ImGui.Selectable($"{fish.DisplayName}###SearchFish{fish.FishParameterId}"))
                { lock (gate) selection = (fish.FishParameterId, session); IsOpen = false; }
            }
        ImGui.EndChild();
    }

    public void Dispose()
    {
        menus.OnMenuOpened -= OnMenuOpened; framework.Update -= Update;
        lock (gate) { selection = null; request = null; candidates = []; }
    }
}
