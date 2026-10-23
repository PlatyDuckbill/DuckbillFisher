using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using Lumina.Excel.Sheets;

namespace DuckbillFisher.Plugin;

internal enum FishingBuffReadState { NotObserved, Ready, Unavailable }
internal sealed record FishingLureView(Guid CastId, uint StatusId, int Stacks, string Name, uint Icon, TimeSpan? StartedAt);
internal sealed record FishingBuffView(int Session, FishingBuffReadState State, TimeSpan At, TimeSpan? PatienceEnd,
    TimeSpan? IntuitionEnd, uint PatienceIcon, uint IntuitionIcon)
{
    public static FishingBuffView Empty { get; } = new(-1, FishingBuffReadState.NotObserved, default, null, null, 0, 0);
    public bool Ready => State == FishingBuffReadState.Ready;
    public FishingLureView? Lure { get; init; }
    public bool LureReady { get; init; }
    public Guid? LureCastId { get; init; }
}

/// <summary>Public status API, copied on Framework.Update. No game objects survive the callback.</summary>
internal sealed class FishingBuffObserver : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan SampleMaxAge = TimeSpan.FromSeconds(1);
    private readonly IObjectTable objects;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly ICondition conditions;
    private readonly IFramework framework;
    private readonly IDataManager data;
    private readonly FishPreferences preferences;
    private readonly FishingTracker tracker;
    private readonly Func<bool> compatible;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private FishingBuffView view = FishingBuffView.Empty;
    private TimeSpan nextUpdate;
    private uint patienceIcon, intuitionIcon;
    private uint giganticIcon, smallIcon;
    private string giganticName = "", smallName = "";
    private bool initialized, disposed;
    private volatile bool active;

    public FishingBuffObserver(IObjectTable objects, IClientState client, IPlayerState player, ICondition conditions,
        IFramework framework, IDataManager data, FishPreferences preferences, FishingTracker tracker, Func<bool> compatible)
    {
        this.objects = objects; this.client = client; this.player = player; this.conditions = conditions;
        this.framework = framework; this.data = data; this.preferences = preferences; this.compatible = compatible;
        this.tracker = tracker;
        framework.Update += Update;
    }

    public TimeSpan Now => clock.Elapsed;
    public FishingBuffView View
    {
        get
        {
            var sample = Volatile.Read(ref view);
            var personal = preferences.View;
            return active && personal.HasCharacter && sample.Session == personal.Session && Now - sample.At <= SampleMaxAge
                ? sample : FishingBuffView.Empty;
        }
    }

    public void SetActive(bool value)
    {
        Volatile.Write(ref view, FishingBuffView.Empty);
        active = value;
    }

    private void Update(IFramework _)
    {
        if (disposed || !active || Now < nextUpdate) return;
        var now = Now;
        nextUpdate = now + PollInterval;
        var personal = preferences.View;
        if (!compatible() || !personal.HasCharacter || !client.IsLoggedIn || !player.IsLoaded ||
            player.ContentId != personal.CharacterId || player.ClassJob.RowId != FishingGameIds.FisherJob ||
            conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51])
        { PublishUnavailable(personal.Session, now); return; }
        if (!initialized)
        {
            initialized = true;
            var sheet = data.GetExcelSheet<Status>();
            patienceIcon = sheet?.GetRowOrDefault(FishingGameIds.PatienceStatus)?.Icon ?? 0;
            intuitionIcon = sheet?.GetRowOrDefault(FishingGameIds.IntuitionStatus)?.Icon ?? 0;
            var actions = data.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            var gigantic = actions?.GetRowOrDefault(FishingGameIds.GiganticLureAction);
            var small = actions?.GetRowOrDefault(FishingGameIds.SmallLureAction);
            giganticIcon = gigantic?.Icon ?? 0;
            smallIcon = small?.Icon ?? 0;
            giganticName = gigantic?.Name.ExtractText() ?? "";
            smallName = small?.Name.ExtractText() ?? "";
        }
        var local = objects.LocalPlayer;
        if (local is null || local.ClassJob.RowId != FishingGameIds.FisherJob || patienceIcon == 0 || intuitionIcon == 0)
        { PublishUnavailable(personal.Session, now); return; }
        TimeSpan? patience = null, intuition = null;
        var valid = true;
        uint lureStatus = 0;
        var lureStacks = 0;
        var lureValid = giganticIcon != 0 && smallIcon != 0 && giganticName.Length > 0 && smallName.Length > 0;
        foreach (var status in local.StatusList)
        {
            var id = status.StatusId;
            if (id is FishingGameIds.GiganticLureStatus or FishingGameIds.SmallLureStatus)
            {
                var stacks = status.Param;
                if (lureStatus != 0 || stacks is < 1 or > 3) lureValid = false;
                lureStatus = id;
                lureStacks = stacks;
                continue;
            }
            if (id is not (FishingGameIds.PatienceStatus or FishingGameIds.IntuitionStatus)) continue;
            var seconds = status.RemainingTime;
            if (!float.IsFinite(seconds) || seconds < 0 || seconds > int.MaxValue) { valid = false; continue; }
            var end = now + TimeSpan.FromSeconds(seconds);
            if (id == FishingGameIds.PatienceStatus) patience = end;
            else intuition = end;
        }
        var previous = Volatile.Read(ref view);
        var cast = tracker.View;
        var castId = cast.Session == personal.Session && cast.State == FishingTrackerState.Ready &&
            cast.Active is { Outcome: FishingOutcome.Pending, HookSeconds: null } && cast.CastStartedAt is not null
                ? cast.Active.Id : (Guid?)null;
        FishingLureView? lure = null;
        if (lureValid && lureStatus != 0 && castId is { } currentCast)
        {
            var continuous = previous.Session == personal.Session && previous.LureReady && previous.LureCastId == currentCast &&
                now - previous.At <= SampleMaxAge;
            var prior = previous.Lure;
            // First sample with an already present effect has no known start. Only a newly observed stack starts a timer.
            var increased = continuous && (prior is null || prior.CastId == currentCast && prior.StatusId == lureStatus && lureStacks > prior.Stacks);
            var started = increased ? now : continuous && prior?.CastId == currentCast && prior.StatusId == lureStatus && prior.Stacks == lureStacks
                ? prior.StartedAt : null;
            lure = new(currentCast, lureStatus, lureStacks,
                lureStatus == FishingGameIds.GiganticLureStatus ? giganticName : smallName,
                lureStatus == FishingGameIds.GiganticLureStatus ? giganticIcon : smallIcon, started);
        }
        Volatile.Write(ref view, new(personal.Session, valid ? FishingBuffReadState.Ready : FishingBuffReadState.Unavailable,
            now, valid ? patience : null, valid ? intuition : null, patienceIcon, intuitionIcon)
            { LureReady = lureValid && (lureStatus == 0 || castId is not null), Lure = lure, LureCastId = castId });
    }

    private void PublishUnavailable(int session, TimeSpan now) => Volatile.Write(ref view,
        new(session, FishingBuffReadState.Unavailable, now, null, null, patienceIcon, intuitionIcon));

    public void Dispose()
    {
        disposed = true; active = false;
        framework.Update -= Update;
        Volatile.Write(ref view, FishingBuffView.Empty);
    }
}
