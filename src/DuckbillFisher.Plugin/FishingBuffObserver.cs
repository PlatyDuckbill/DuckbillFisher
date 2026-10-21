using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace DuckbillFisher.Plugin;

internal enum FishingBuffReadState { NotObserved, Ready, Unavailable }
internal sealed record FishingBuffView(int Session, FishingBuffReadState State, TimeSpan At, TimeSpan? PatienceEnd,
    TimeSpan? IntuitionEnd, uint PatienceIcon, uint IntuitionIcon)
{
    public static FishingBuffView Empty { get; } = new(-1, FishingBuffReadState.NotObserved, default, null, null, 0, 0);
    public bool Ready => State == FishingBuffReadState.Ready;
}

/// <summary>Public status API, copied on Framework.Update. No game objects survive the callback.</summary>
internal sealed class FishingBuffObserver : IDisposable
{
    private readonly IObjectTable objects;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly ICondition conditions;
    private readonly IFramework framework;
    private readonly IDataManager data;
    private readonly FishPreferences preferences;
    private readonly Func<bool> compatible;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private FishingBuffView view = FishingBuffView.Empty;
    private TimeSpan nextUpdate;
    private uint patienceIcon, intuitionIcon;
    private bool initialized, disposed;
    private volatile bool active;

    public FishingBuffObserver(IObjectTable objects, IClientState client, IPlayerState player, ICondition conditions,
        IFramework framework, IDataManager data, FishPreferences preferences, Func<bool> compatible)
    {
        this.objects = objects; this.client = client; this.player = player; this.conditions = conditions;
        this.framework = framework; this.data = data; this.preferences = preferences; this.compatible = compatible;
        framework.Update += Update;
    }

    public TimeSpan Now => clock.Elapsed;
    public FishingBuffView View
    {
        get
        {
            var sample = Volatile.Read(ref view);
            var personal = preferences.View;
            return active && personal.HasCharacter && sample.Session == personal.Session && Now - sample.At <= TimeSpan.FromSeconds(1)
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
        nextUpdate = now + TimeSpan.FromMilliseconds(200);
        var personal = preferences.View;
        if (!compatible() || !personal.HasCharacter || !client.IsLoggedIn || !player.IsLoaded ||
            player.ContentId != personal.CharacterId || player.ClassJob.RowId != 18 ||
            conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51])
        { PublishUnavailable(personal.Session, now); return; }
        if (!initialized)
        {
            initialized = true;
            var sheet = data.GetExcelSheet<Status>();
            patienceIcon = sheet?.GetRowOrDefault(850)?.Icon ?? 0;
            intuitionIcon = sheet?.GetRowOrDefault(568)?.Icon ?? 0;
        }
        var local = objects.LocalPlayer;
        if (local is null || local.ClassJob.RowId != 18 || patienceIcon == 0 || intuitionIcon == 0)
        { PublishUnavailable(personal.Session, now); return; }
        TimeSpan? patience = null, intuition = null;
        var valid = true;
        foreach (var status in local.StatusList)
        {
            var id = status.StatusId;
            if (id is not (850 or 568)) continue;
            var seconds = status.RemainingTime;
            if (!float.IsFinite(seconds) || seconds < 0 || seconds > int.MaxValue) { valid = false; break; }
            var end = now + TimeSpan.FromSeconds(seconds);
            if (id == 850) patience = end;
            else intuition = end;
        }
        Volatile.Write(ref view, new(personal.Session, valid ? FishingBuffReadState.Ready : FishingBuffReadState.Unavailable,
            now, valid ? patience : null, valid ? intuition : null, patienceIcon, intuitionIcon));
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
