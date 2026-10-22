using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;

namespace DuckbillFisher.Plugin;

/// <summary>Only uses public condition APIs. No native reads or inferred cast/bite events.</summary>
internal sealed class FishingConditionObserver : IDisposable
{
    private readonly ICondition conditions;
    private readonly object gate = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly FishingObservation observation = new();
    private FishingObservationView view;
    private DateTimeOffset? startedAtUtc;
    private bool disposed;

    public FishingConditionObserver(ICondition conditions)
    {
        this.conditions = conditions;
        view = observation.GetView();
        conditions.ConditionChange += OnConditionChange;
    }

    // A cached managed snapshot: drawing does not read game state or rebuild history.
    public FishingObservationView View
    {
        get { lock (gate) return view; }
    }

    public void Start()
    {
        lock (gate)
        {
            if (disposed || observation.IsRecording)
                return;

            startedAtUtc = DateTimeOffset.UtcNow;
            observation.Start(clock.Elapsed, ReadConditions());
            view = observation.GetView();
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            if (disposed || !observation.IsRecording)
                return;

            observation.Stop(clock.Elapsed, ReadConditions());
            view = observation.GetView();
        }
    }

    public void Mark(string label)
    {
        lock (gate)
        {
            if (disposed || !observation.IsRecording)
                return;

            observation.Record(clock.Elapsed, $"MARK (manual): {label}", ReadConditions());
            view = observation.GetView();
        }
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag is not (ConditionFlag.Fishing or ConditionFlag.ExecutingGatheringAction or ConditionFlag.Casting))
            return;

        lock (gate)
        {
            if (disposed || !observation.IsRecording)
                return;

            // Preserve every callback, including repeated values, to reveal possible duplicate subscriptions.
            observation.Record(clock.Elapsed, $"CHANGE {flag}={value}", ReadConditions());
            view = observation.GetView();
        }
    }

    private FishingConditionSnapshot ReadConditions() => new(
        conditions[ConditionFlag.Fishing],
        conditions[ConditionFlag.ExecutingGatheringAction],
        conditions[ConditionFlag.Casting]);

    public string CreateReport()
    {
        lock (gate)
        {
            var report = new StringBuilder();
            report.AppendLine("DuckbillFisher ENV-03/public-conditions/v1");
            report.AppendLine($"Plugin: {typeof(Plugin).Assembly.GetName().Version}; Dalamud: {typeof(ICondition).Assembly.GetName().Version}");
            report.AppendLine($"Started UTC: {startedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "not started"}");
            report.AppendLine($"Recording: {view.IsRecording}; retained: {view.Entries.Count}; dropped: {view.DroppedCount}");
            report.AppendLine("Times are monotonic seconds since START. MARK timestamps are user clicks, not action detection.");
            report.AppendLine("F=Fishing; G=ExecutingGatheringAction; C=Casting. Values are raw API flags, not fish collection status.");
            foreach (var entry in view.Entries)
                report.AppendLine(FormatEntry(entry));
            return report.ToString();
        }
    }

    public static string FormatEntry(FishingObservationEntry entry) => string.Create(
        CultureInfo.InvariantCulture,
        $"#{entry.Sequence:D4} +{entry.Elapsed.TotalSeconds:F3}s {entry.Event} | F={entry.Conditions.Fishing} G={entry.Conditions.ExecutingGatheringAction} C={entry.Conditions.Casting}");

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;

            disposed = true;
            conditions.ConditionChange -= OnConditionChange;
            clock.Stop();
        }
    }
}
