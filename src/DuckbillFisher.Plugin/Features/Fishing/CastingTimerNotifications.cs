using Dalamud.Plugin.Services;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

internal sealed class CastingTimerNotifications : IDisposable
{
    private readonly FishingTracker tracker;
    private readonly FishPreferences preferences;
    private readonly SpawnAlertOutput output;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly CastTimerAlerts alerts = new();

    public CastingTimerNotifications(FishingTracker tracker, FishPreferences preferences, SpawnAlertOutput output,
        IFramework framework, IPluginLog log)
    {
        this.tracker = tracker; this.preferences = preferences; this.output = output;
        this.framework = framework; this.log = log;
        framework.Update += Update;
    }

    private void Update(IFramework _)
    {
        var personal = preferences.View;
        var sample = tracker.View;
        var active = personal.HasCharacter && sample.Session == personal.Session && sample.State == FishingTrackerState.Ready &&
            sample.Active is { Outcome: FishingOutcome.Pending, HookSeconds: null };
        var due = alerts.Evaluate(personal.Session, active ? sample.Active?.Id : null,
            active ? sample.CastStartedAt : null, tracker.Now, personal.Timer.Alerts);
        if (due.Count == 0) return;
        try { output.PrintTimer(due, personal); }
        catch (Exception ex) { log.Error(ex, "Could not deliver casting timer alerts; crossed deadlines will not be replayed."); }
    }

    public void Dispose() => framework.Update -= Update;
}
