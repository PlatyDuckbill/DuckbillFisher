using System.Diagnostics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using FFXIVClientStructs.FFXIV.Client.UI;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Local chat payloads and the inspected KR chat sound call. No chat commands or server sends.</summary>
internal sealed class SpawnAlertOutput : IDisposable
{
    private readonly IFramework framework;
    private readonly IChatGui chat;
    private readonly FishPreferences preferences;
    private readonly bool nativeCompatible;
    private readonly IPluginLog log;
    private readonly object gate = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private (int Effect, int Session)? preview;
    private TimeSpan nextPreview;
    private int session = -1;
    private bool disposed;
    private bool soundAvailable;
    private UiMessage? previewResult;

    public SpawnAlertOutput(IFramework framework, IChatGui chat, FishPreferences preferences, bool nativeCompatible, IPluginLog log)
    {
        this.framework = framework;
        this.chat = chat;
        this.preferences = preferences;
        this.nativeCompatible = nativeCompatible;
        this.log = log;
        framework.Update += OnUpdate;
    }

    public bool SoundAvailable { get { lock (gate) return soundAvailable; } }
    public UiMessage? PreviewResult { get { lock (gate) return previewResult; } }

    public void RequestPreview(int effect, int expectedSession)
    {
        lock (gate)
            if (!disposed && effect is >= 1 and <= 16 && clock.Elapsed >= nextPreview)
            {
                preview = (effect, expectedSession);
                nextPreview = clock.Elapsed + TimeSpan.FromSeconds(1);
            }
    }

    private void OnUpdate(IFramework _)
    {
        var personal = preferences.View;
        lock (gate)
        {
            if (disposed)
                return;
            if (session != personal.Session)
            {
                preview = null;
                previewResult = null;
                session = personal.Session;
            }
            soundAvailable = personal.HasCharacter && NativeSoundReady();
            if (preview is not { } request)
                return;
            preview = null;
            try
            {
                previewResult = new(request.Session == personal.Session && PlaySound(request.Effect)
                    ? T.AlertSoundPreviewed : T.AlertSoundUnavailable);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Could not preview the selected alert sound.");
                previewResult = new(T.AlertSoundUnavailable);
            }
        }
    }

    public bool Print(IReadOnlyList<SpawnAlertDisplay> batch, DateTimeOffset now, FishPreferencesView settings)
    {
        if (!framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Alert output must run on Framework.Update.");
        var message = new SeStringBuilder();
        var first = true;
        foreach (var fish in batch.Take(3))
        {
            if (!first)
                message.AddText("\n");
            first = false;
            message.AddText(UiText.Get(T.AlertCompactMessage, fish.Name, Remaining(fish.Start - now)));
            foreach (var payload in SeString.CreateMapLink(fish.Spot.TerritoryId, fish.Spot.MapId, fish.Spot.X, fish.Spot.Y).Payloads)
                message.Add(payload);
        }
        if (batch.Count > 3)
            message.AddText(UiText.Get(T.AlertExtraHistory, batch.Count - 3));
        chat.Print(new XivChatEntry
        {
            Type = settings.SpawnAlertChannel switch
            {
                SpawnAlertChannel.Say => XivChatType.Say,
                SpawnAlertChannel.CrossWorldLinkshell1 => XivChatType.CrossLinkShell1,
                _ => XivChatType.SystemMessage,
            },
            Name = "DuckbillFisher", Message = message.Build(), Silent = true,
        });
        if (settings.SpawnAlertSound == 0)
            return false;
        try { return PlaySound(settings.SpawnAlertSound); }
        catch (Exception ex)
        {
            // Managed API failures only; chat already queued must remain in delivery history.
            log.Error(ex, "Could not play the alert sound; local chat was already queued.");
            return false;
        }
    }

    public static string Remaining(TimeSpan time)
    {
        var seconds = Math.Max(0, (int)Math.Ceiling(time.TotalSeconds));
        return seconds >= 3600
            ? UiText.Get(T.AlertRemainingHours, seconds / 3600, seconds / 60 % 60, seconds % 60)
            : UiText.Get(T.AlertRemainingMinutes, seconds / 60, seconds % 60);
    }

    private unsafe bool NativeSoundReady() => nativeCompatible && framework.IsInFrameworkUpdateThread &&
        UIGlobals.MemberFunctionPointers.PlaySoundEffect != null;

    private bool PlaySound(int effect)
    {
        // Never use catch to recover from an invalid native address. Identity + resolved static function only.
        if (effect is < 1 or > 16 || !preferences.View.HasCharacter || !NativeSoundReady())
            return false;
        UIGlobals.PlayChatSoundEffect((uint)effect);
        return true;
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            preview = null;
        }
        framework.Update -= OnUpdate;
    }
}
