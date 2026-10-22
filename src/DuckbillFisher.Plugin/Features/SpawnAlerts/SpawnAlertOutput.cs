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
    private readonly CustomAlertSound customSound;
    private readonly object gate = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private (int Effect, int Session)? preview;
    private TimeSpan nextPreview;
    private int session = -1;
    private bool disposed;
    private bool soundAvailable;
    private bool alertsEnabled;
    private UiMessage? previewResult;

    public SpawnAlertOutput(IFramework framework, IChatGui chat, FishPreferences preferences, bool nativeCompatible, IPluginLog log)
    {
        this.framework = framework;
        this.chat = chat;
        this.preferences = preferences;
        this.nativeCompatible = nativeCompatible;
        this.log = log;
        customSound = new(log);
        framework.Update += OnUpdate;
    }

    public bool SoundAvailable { get { lock (gate) return soundAvailable; } }
    public UiMessage? PreviewResult { get { lock (gate) return previewResult; } }
    public CustomSoundView CustomSound => customSound.View;

    public void ReloadCustomSound()
    {
        var settings = preferences.View;
        customSound.Configure(settings.HasCharacter && settings.SpawnAlertSound == CustomAlertSound.Choice,
            settings.CustomAlertSoundPath, settings.Session, force: true);
    }

    public void StopCustomSound() => customSound.Stop();

    public void RequestPreview(int effect, int expectedSession)
    {
        lock (gate)
            if (!disposed && effect is >= 1 and <= CustomAlertSound.Choice && clock.Elapsed >= nextPreview)
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
            if (alertsEnabled && !personal.SpawnAlertsEnabled) customSound.Stop();
            alertsEnabled = personal.SpawnAlertsEnabled;
            customSound.Configure(personal.HasCharacter && personal.SpawnAlertSound == CustomAlertSound.Choice,
                personal.CustomAlertSoundPath, personal.Session);
            soundAvailable = personal.HasCharacter && (personal.SpawnAlertSound == CustomAlertSound.Choice
                ? customSound.View.State == CustomSoundState.Ready : NativeSoundReady());
            if (preview is not { } request)
                return;
            preview = null;
            try
            {
                previewResult = new(request.Session == personal.Session && PlaySound(request.Effect, request.Session)
                    ? request.Effect == CustomAlertSound.Choice ? T.CustomSoundQueued : T.AlertSoundPreviewed
                    : request.Effect == CustomAlertSound.Choice ? T.CustomSoundUnavailable : T.AlertSoundUnavailable);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Could not preview the selected alert sound.");
                previewResult = new(request.Effect == CustomAlertSound.Choice ? T.CustomSoundUnavailable : T.AlertSoundUnavailable);
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
            foreach (var payload in SeString.CreateItemLink(fish.ItemId, false, fish.Name).Payloads)
                message.Add(payload);
            message.AddText(UiText.Get(T.AlertLinkedRemaining, Remaining(fish.Start - now)));
            foreach (var payload in SeString.CreateMapLink(fish.Spot.TerritoryId, fish.Spot.MapId, fish.Spot.X, fish.Spot.Y).Payloads)
                message.Add(payload);
        }
        if (batch.Count > 3)
            message.AddText(UiText.Get(T.AlertExtraHistory, batch.Count - 3));
        chat.Print(new XivChatEntry
        {
            Type = settings.SpawnAlertChannel,
            Name = "DuckbillFisher", Message = message.Build(), Silent = true,
        });
        var soundPlayed = false;
        try { soundPlayed = PlaySound(settings.SpawnAlertSound, settings.Session); }
        catch (Exception ex)
        {
            // Managed API failures only; chat already queued must remain in delivery history.
            log.Error(ex, "Could not play the alert sound; local chat was already queued.");
        }
        return soundPlayed;
    }

    public static string Remaining(TimeSpan time)
    {
        var minutes = Math.Max(0, (int)Math.Ceiling(time.TotalMinutes));
        return minutes >= 60
            ? UiText.Get(T.AlertRemainingHours, minutes / 60, minutes % 60)
            : UiText.Get(T.AlertRemainingMinutes, minutes);
    }

    private unsafe bool NativeSoundReady() => nativeCompatible && framework.IsInFrameworkUpdateThread &&
        UIGlobals.MemberFunctionPointers.PlaySoundEffect != null;

    private bool PlaySound(int effect, int expectedSession)
    {
        var personal = preferences.View;
        if (!personal.HasCharacter || personal.Session != expectedSession) return false;
        if (effect == CustomAlertSound.Choice) return customSound.TryPlay(expectedSession);
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
        customSound.Dispose();
    }
}
