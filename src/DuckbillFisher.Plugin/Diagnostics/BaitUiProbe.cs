using System.Diagnostics;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Explicit bounded observation only. Copying progress never reads game memory.</summary>
internal sealed class BaitUiProbe : IDisposable
{
    private readonly IGameGui gui;
    private readonly IFramework framework;
    private readonly IAddonLifecycle lifecycle;
    private readonly FishPreferences preferences;
    private readonly Func<bool> compatible;
    private readonly NativeBaitUiReader baitReader;
    private readonly BaitFinder finder;
    private readonly object gate = new();
    private readonly HashSet<string> observedNames = new(StringComparer.Ordinal);
    private sealed record RequestState(int Session, long At, uint Target);
    private RequestState? pending;
    private string? report;
    private string status = "Idle";
    private int reportSession = -1;
    private int polls;
    private long firstVisibleAt;
    private bool disposed;

    public BaitUiProbe(IGameGui gui, IFramework framework, IAddonLifecycle lifecycle,
        FishPreferences preferences, Func<bool> compatible, NativeBaitUiReader baitReader, BaitFinder finder)
    {
        this.gui = gui; this.framework = framework; this.lifecycle = lifecycle;
        this.preferences = preferences; this.compatible = compatible;
        this.baitReader = baitReader;
        this.finder = finder;
        framework.Update += Update;
        lifecycle.RegisterListener(AddonEvent.PostSetup, ObserveAddonName);
        lifecycle.RegisterListener(AddonEvent.PostRefresh, ObserveAddonName);
    }

    // Deadline checks use managed clocks only, so the UI can also finish a stalled observation.
    private void ExpirePending()
    {
        if (pending is not { } request || Stopwatch.GetElapsedTime(request.At).TotalSeconds < 20) return;
        report = Header(request.Session) + $"Status=NotObservedWithin20Seconds; LastLookup={status}\n";
        status = "NotObservedWithin20Seconds";
        pending = null;
    }

    public bool IsWaiting { get { lock (gate) { ExpirePending(); return pending is not null; } } }
    public int SecondsRemaining { get { lock (gate) { ExpirePending(); return pending is { } request
        ? Math.Max(0, (int)Math.Ceiling(20 - Stopwatch.GetElapsedTime(request.At).TotalSeconds)) : 0; } } }

    public UiMessage StatusMessage
    {
        get
        {
            lock (gate)
            {
                ExpirePending();
                return new(status switch
                {
                    "Captured" => T.BaitProbeCaptured,
                    "NotObservedWithin20Seconds" => T.BaitProbeNotObserved,
                    "Unavailable" => T.BaitProbeUnavailable,
                    "UnsupportedShape" => T.BaitProbeUnsupportedShape,
                    "Cancelled" => T.BaitProbeCancelled,
                    _ => T.BaitProbeIdle,
                });
            }
        }
    }

    public string Report
    {
        get
        {
            lock (gate)
            {
                ExpirePending();
                var output = new StringBuilder(report ?? Header(reportSession));
                output.AppendLine($"Progress={status}; Pending={pending is not null}; FrameworkPolls={polls}");
                if (pending is { } request)
                    output.AppendLine($"ElapsedMilliseconds={(long)Stopwatch.GetElapsedTime(request.At).TotalMilliseconds}");
                output.AppendLine("ObservedAddonNames=" + string.Join(',', observedNames.Order(StringComparer.Ordinal)));
                return output.ToString();
            }
        }
    }

    private static string Header(int session) => "DuckbillFisher bait-ui/v7\n" +
        $"Plugin={typeof(BaitUiProbe).Assembly.GetName().Version}; ProbeBuild={typeof(BaitUiProbe).Module.ModuleVersionId.ToString()[..8]}; Session={session}\n";

    public void Request(int session)
    {
        lock (gate)
        {
            ExpirePending();
            if (disposed || pending is not null) return;
            reportSession = session;
            report = null;
            polls = 0;
            firstVisibleAt = 0;
            observedNames.Clear();
            status = "Queued";
            var target = finder.View;
            pending = new(session, Stopwatch.GetTimestamp(), target.Session == session ? target.Item : 0);
        }
    }

    public void Cancel()
    {
        lock (gate)
        {
            if (pending is null) return;
            pending = null;
            status = "Cancelled";
        }
    }

    private void ObserveAddonName(AddonEvent _, AddonArgs args)
    {
        lock (gate)
        {
            if (disposed || pending is null) return;
            // Public lifecycle names identify the UI; do not retain the borrowed addon address.
            var name = args.AddonName;
            if (observedNames.Count < 16 || name.Contains("Bait", StringComparison.OrdinalIgnoreCase))
                if (name.Length is > 0 and <= 80 && observedNames.Count < 24) observedNames.Add(name);
        }
    }

    private unsafe void Update(IFramework _)
    {
        lock (gate)
        {
            if (disposed) return;
            var personal = preferences.View;
            if (reportSession != personal.Session)
            {
                report = null;
                observedNames.Clear();
                reportSession = personal.Session;
                status = "SessionChanged";
                polls = 0;
            }
            ExpirePending();
            if (pending is not { } request) return;
            polls++;
            void Finish(string code, string? data = null)
            {
                status = code;
                report = data ?? Header(personal.Session) + $"Status={code}\n";
                pending = null;
            }
            if (!personal.HasCharacter || personal.Session != request.Session || !framework.IsInFrameworkUpdateThread || !compatible())
            { Finish("Unavailable"); return; }
            var names = new[] { "Bait" }.Concat(observedNames.Where(name =>
                name.Contains("Bait", StringComparison.OrdinalIgnoreCase))).Distinct().ToArray();
            status = "WaitingForAddon";
            foreach (var name in names)
            {
                var handle = gui.GetAddonByName(name);
                if (handle.IsNull) continue;
                var addon = (AtkUnitBase*)handle.Address;
                if (!addon->IsVisible) { status = "AddonHidden"; continue; }
                // Wait for initial tooltip attachments; retain only the managed timestamp.
                if (firstVisibleAt == 0) firstVisibleAt = Stopwatch.GetTimestamp();
                if (Stopwatch.GetElapsedTime(firstVisibleAt).TotalMilliseconds < 250) return;
                var manager = addon->UldManager;
                if (addon->AtkValuesCount > 1024 || manager.NodeListCount > 512 ||
                    (addon->AtkValuesCount > 0 && addon->AtkValues == null) || manager.NodeList == null)
                { Finish("UnsupportedShape"); return; }
                var output = new StringBuilder(Header(personal.Session));
                output.AppendLine($"Status=Visible; Addon={name}; Values={addon->AtkValuesCount}; Nodes={manager.NodeListCount}; Scale={addon->Scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                var values = new List<string>();
                for (var i = 0; i < addon->AtkValuesCount; i++)
                {
                    var value = addon->AtkValues[i];
                    if (value.Type == AtkValueType.UInt && value.UInt != 0) values.Add($"{i}:u:{value.UInt}");
                    else if (value.Type == AtkValueType.Int && value.Int != 0) values.Add($"{i}:i:{value.Int}");
                }
                output.AppendLine("Numeric=" + string.Join(',', values.Take(200)));
                output.AppendLine($"NumericOmitted={Math.Max(0, values.Count - 200)}");
                var nodes = new List<string>();
                for (var i = 0; i < manager.NodeListCount; i++)
                {
                    var node = manager.NodeList[i];
                    if (node == null || node->NodeId is < 17 or > 41) continue;
                    nodes.Add($"{node->NodeId}:{(uint)node->Type}:{node->Width}x{node->Height}");
                }
                output.AppendLine("GridNodes=" + string.Join(',', nodes));
                var binding = baitReader.Read(request.Target, diagnostics: true);
                var state = request.Target == 0 && binding.State == BaitUiState.BindingUnavailable ? "ObservedOnly" : binding.State.ToString();
                output.AppendLine(FormattableString.Invariant($"BindingState={state}; TargetItem={request.Target}; Matches={binding.Matches}; Node={binding.MatchedNode}; Rect={binding.Position.X:0.##},{binding.Position.Y:0.##},{binding.Size.X:0.##},{binding.Size.Y:0.##}; TooltipBindings={binding.Bindings}"));
                if (binding.Page is { } page)
                    output.AppendLine(FormattableString.Invariant($"Pages={page.State}; Check={page.Check}; Entries={page.Entries}; Current={page.Current}; Target={page.Target}; Total={page.Total}; Compared={page.Compared}; Theme={page.Theme}; AccentSource={page.AccentSource}; Marker={page.MarkerVisible}; PageRect={page.Position.X:0.##},{page.Position.Y:0.##},{page.Size.X:0.##},{page.Size.Y:0.##}"));
                output.AppendLine("ReadOnly=True; NoSelectionOrHighlightInference=True");
                Finish("Captured", output.ToString());
                return;
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            pending = null;
            report = null;
            observedNames.Clear();
            status = "Disposed";
        }
        framework.Update -= Update;
        lifecycle.UnregisterListener(AddonEvent.PostSetup, ObserveAddonName);
        lifecycle.UnregisterListener(AddonEvent.PostRefresh, ObserveAddonName);
    }
}
