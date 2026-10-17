using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal enum BaitFindState { Idle, Waiting, Highlighted, BindingUnavailable, Unavailable, Cancelled, Expired, Closed, Selected }
internal sealed record BaitFindView(int Session, uint Item, string Name, BaitFindState State, long SampledAt = 0,
    Vector2 Position = default, Vector2 Size = default);

/// <summary>One explicit request, no persistence, no selection calls, and no pointer retained across callbacks.</summary>
internal sealed class BaitFinder : IDisposable
{
    private readonly IFramework framework;
    private readonly IAddonLifecycle lifecycle;
    private readonly FishPreferences preferences;
    private readonly BaitStockObserver stock;
    private readonly NativeBaitUiReader reader;
    private readonly Func<bool> compatible;
    private readonly Func<uint?> selected;
    private readonly object gate = new();
    private BaitFindView view = new(-1, 0, "", BaitFindState.Idle);
    private long requestedAt;
    private bool active;
    private bool wasOpen;
    private bool disposed;
    public BaitFindView View { get { lock (gate) { Expire(); return view; } } }
    public bool Active { get { lock (gate) { Expire(); return active; } } }

    public BaitFinder(IFramework framework, IAddonLifecycle lifecycle, FishPreferences preferences,
        BaitStockObserver stock, NativeBaitUiReader reader, Func<bool> compatible, Func<uint?> selected)
    {
        this.framework = framework; this.lifecycle = lifecycle; this.preferences = preferences;
        this.stock = stock; this.reader = reader; this.compatible = compatible; this.selected = selected;
        framework.Update += Update;
        lifecycle.RegisterListener(AddonEvent.PreFinalize, "Bait", OnClose);
        lifecycle.RegisterListener(AddonEvent.PostSetup, "Bait", OnSetup);
    }

    public bool CanRequest(uint item, int session) => !disposed && compatible() && preferences.View.HasCharacter &&
        preferences.View.Session == session && stock.View.Session == session && stock.View.Quantity(item) is > 0;

    public void Request(FishingItemView item, int session)
    {
        lock (gate)
        {
            if (!CanRequest(item.Id, session)) return;
            view = new(session, item.Id, item.Name, BaitFindState.Waiting);
            requestedAt = Stopwatch.GetTimestamp(); active = true; wasOpen = false;
        }
    }

    public void Cancel() { lock (gate) { Finish(BaitFindState.Cancelled); } }
    private void Finish(BaitFindState state) { active = false; view = view with { State = state, SampledAt = 0 }; }
    private void Expire()
    {
        if (view.Session != preferences.View.Session || !preferences.View.HasCharacter)
        { active = false; view = new(preferences.View.Session, 0, "", BaitFindState.Idle); }
        if (active && Stopwatch.GetElapsedTime(requestedAt).TotalSeconds >= 60) Finish(BaitFindState.Expired);
    }
    private void OnClose(AddonEvent _, AddonArgs args) { lock (gate) { if (active) Finish(BaitFindState.Closed); } }
    private void OnSetup(AddonEvent _, AddonArgs args)
    { lock (gate) { if (active && wasOpen) Finish(BaitFindState.Closed); } }

    private void Update(IFramework _)
    {
        lock (gate)
        {
            if (disposed) return;
            Expire();
            if (!active) return;
            var personal = preferences.View;
            var inventory = stock.View;
            if (!compatible() || inventory.Session != personal.Session || inventory.State != BaitStockState.Ready ||
                inventory.Quantity(view.Item) is not > 0)
            { Finish(BaitFindState.Unavailable); return; }
            if (selected() == view.Item) { Finish(BaitFindState.Selected); return; }
            var result = reader.Read(view.Item);
            if (result.State == BaitUiState.NotOpen)
            {
                if (wasOpen) Finish(BaitFindState.Closed);
                else view = view with { State = BaitFindState.Waiting, SampledAt = 0 };
                return;
            }
            wasOpen = true;
            if (result.State is BaitUiState.Unavailable or BaitUiState.UnsupportedShape)
            { Finish(BaitFindState.Unavailable); return; }
            view = view with { State = result.State == BaitUiState.Found ? BaitFindState.Highlighted : BaitFindState.BindingUnavailable,
                SampledAt = Stopwatch.GetTimestamp(), Position = result.Position, Size = result.Size };
        }
    }

    public void DrawOverlay()
    {
        var current = View;
        if (current.State != BaitFindState.Highlighted || Stopwatch.GetElapsedTime(current.SampledAt).TotalSeconds > 0.15) return;
        var viewport = ImGui.GetMainViewport();
        var start = viewport.Pos + current.Position;
        if (start.X < viewport.Pos.X || start.Y < viewport.Pos.Y ||
            start.X + current.Size.X > viewport.Pos.X + viewport.Size.X || start.Y + current.Size.Y > viewport.Pos.Y + viewport.Size.Y) return;
        var draw = ImGui.GetForegroundDrawList(viewport);
        draw.AddRect(start, start + current.Size, ImGui.GetColorU32(new Vector4(1, 0.83f, 0.25f, 1)), 5, ImDrawFlags.None, 3);
    }

    public void DrawButton(FishingItemView item, int session)
    {
        ImGui.BeginDisabled(!CanRequest(item.Id, session));
        if (ImGui.SmallButton(UiText.Label(T.BaitFind, "FindBait"))) Request(item, session);
        ImGui.EndDisabled();
        UiWidgets.Tooltip(UiText.Get(T.BaitFindHint));
    }

    public void DrawStatus(IReadOnlyList<FishingItemView>? scope = null)
    {
        var current = View;
        if (current.State == BaitFindState.Idle || scope is not null && !scope.Any(item => item.Id == current.Item)) return;
        var message = UiText.Get(current.State switch
        {
            BaitFindState.Waiting => T.BaitFindWaiting, BaitFindState.Highlighted => T.BaitFindHighlighted,
            BaitFindState.BindingUnavailable => T.BaitFindBindingUnavailable, BaitFindState.Selected => T.BaitFindSelected,
            BaitFindState.Expired => T.BaitFindExpired, BaitFindState.Closed => T.BaitFindClosed,
            BaitFindState.Cancelled => T.BaitFindCancelled, _ => T.BaitFindUnavailable,
        }, current.Name);
        if (!Active) { ImGui.TextWrapped(message); return; }
        if (!ImGui.BeginTable("BaitFindStatus", 2, ImGuiTableFlags.SizingFixedFit)) return;
        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Cancel", ImGuiTableColumnFlags.WidthFixed,
            ImGui.CalcTextSize(UiText.Get(T.BaitFindCancel)).X + ImGui.GetStyle().FramePadding.X * 2);
        ImGui.TableNextColumn();
        ImGui.TextWrapped(message);
        ImGui.TableNextColumn();
        if (ImGui.SmallButton(UiText.Label(T.BaitFindCancel, "CancelFindBait"))) Cancel();
        ImGui.EndTable();
    }

    public void Dispose()
    {
        lock (gate) { disposed = true; active = false; view = new(-1, 0, "", BaitFindState.Idle); }
        framework.Update -= Update;
        lifecycle.UnregisterListener(AddonEvent.PreFinalize, "Bait", OnClose);
        lifecycle.UnregisterListener(AddonEvent.PostSetup, "Bait", OnSetup);
    }
}
