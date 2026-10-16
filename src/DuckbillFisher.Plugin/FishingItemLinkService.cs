using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Explicit item clicks produce local game links through public chat APIs.</summary>
internal sealed class FishingItemLinkService : IDisposable
{
    private readonly IChatGui chat;
    private readonly IFramework framework;
    private readonly FishPreferences preferences;
    private readonly object gate = new();
    private (FishingItemView Item, int Session)? pending;
    private UiMessage? result;
    private bool disposed;

    public FishingItemLinkService(IChatGui chat, IFramework framework, FishPreferences preferences)
    {
        this.chat = chat;
        this.framework = framework;
        this.preferences = preferences;
        framework.Update += OnUpdate;
    }

    public UiMessage? Result { get { lock (gate) return result; } }

    public void Request(FishingItemView item, int session)
    {
        lock (gate)
            if (!disposed && item.Id != 0 && !string.IsNullOrWhiteSpace(item.Name))
                pending = (item, session);
    }

    private void OnUpdate(IFramework _)
    {
        lock (gate)
        {
            if (disposed || pending is not { } request)
                return;
            pending = null;
            var personal = preferences.View;
            if (!personal.HasCharacter || personal.Session != request.Session)
            {
                result = new(T.ItemLinkPlayerRequired);
                return;
            }
            try
            {
                chat.Print(SeString.CreateItemLink(request.Item.Id, false, request.Item.Name), "DuckbillFisher");
                result = new(T.ItemLinkPrinted, request.Item.Name);
            }
            catch (Exception ex)
            {
                result = new(T.ItemLinkError, ex.GetType().Name);
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            pending = null;
        }
        framework.Update -= OnUpdate;
    }
}
