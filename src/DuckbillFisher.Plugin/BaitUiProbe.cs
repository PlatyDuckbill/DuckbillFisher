using System.Text;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DuckbillFisher.Plugin;

/// <summary>One explicit, bounded KR UI sample. Does not call callbacks, change bait, or write UI nodes.</summary>
internal sealed class BaitUiProbe : IDisposable
{
    private readonly IGameGui gui;
    private readonly IFramework framework;
    private readonly FishPreferences preferences;
    private readonly Func<bool> compatible;
    private sealed record RequestState(int Session, long At);
    private RequestState? pending;
    private string? report;
    private int reportSession = -1;
    private bool disposed;
    public string? Report => Volatile.Read(ref report);
    public bool IsWaiting => Volatile.Read(ref pending) is not null;

    public BaitUiProbe(IGameGui gui, IFramework framework, FishPreferences preferences, Func<bool> compatible)
    {
        this.gui = gui; this.framework = framework; this.preferences = preferences; this.compatible = compatible;
        framework.Update += Update;
    }

    public void Request(int session)
    {
        Volatile.Write(ref report, null);
        Interlocked.Exchange(ref pending, new(session, Stopwatch.GetTimestamp()));
    }

    private unsafe void Update(IFramework _)
    {
        if (disposed) return;
        var personal = preferences.View;
        if (reportSession != personal.Session || !personal.HasCharacter) Volatile.Write(ref report, null);
        reportSession = personal.Session;
        var request = Volatile.Read(ref pending);
        if (request is null) return;
        void Complete(string value)
        {
            if (Interlocked.CompareExchange(ref pending, null, request) == request) Volatile.Write(ref report, value);
        }
        if (!personal.HasCharacter || personal.Session != request.Session || !framework.IsInFrameworkUpdateThread || !compatible())
        { Complete("DuckbillFisher bait-ui/v2\nStatus=Unavailable"); return; }
        if (Stopwatch.GetElapsedTime(request.At).TotalSeconds > 20)
        { Complete("DuckbillFisher bait-ui/v2\nStatus=NotObservedWithin20Seconds; Tried=FishingBait,FishBait"); return; }
        var output = new StringBuilder("DuckbillFisher bait-ui/v2\n");
        output.AppendLine($"Plugin={typeof(BaitUiProbe).Assembly.GetName().Version}; Session={personal.Session}");
        var handle = gui.GetAddonByName("FishingBait");
        var addonName = "FishingBait";
        if (handle.IsNull) { handle = gui.GetAddonByName("FishBait"); addonName = "FishBait"; }
        if (handle.IsNull) return;
        var addon = (AtkUnitBase*)handle.Address;
        if (!addon->IsVisible) return;
        var manager = addon->UldManager;
        if (addon->AtkValuesCount > 1024 || manager.NodeListCount > 512 ||
            (addon->AtkValuesCount > 0 && addon->AtkValues == null) || manager.NodeList == null)
        { output.AppendLine("Status=UnsupportedShape"); Complete(output.ToString()); return; }
        output.AppendLine($"Status=Visible; Addon={addonName}; Values={addon->AtkValuesCount}; Nodes={manager.NodeListCount}; Scale={addon->Scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
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
        output.AppendLine("ReadOnly=True; NoSelectionOrHighlightInference=True");
        Complete(output.ToString());
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        framework.Update -= Update;
        Volatile.Write(ref report, null);
        Interlocked.Exchange(ref pending, null);
    }
}
