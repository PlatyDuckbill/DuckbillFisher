using System.Collections.Concurrent;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal enum FishingSoundKind { Bite, BigFish, LegendaryFish }

/// <summary>Consumes live managed observations; never derives catches from saved history or collection scans.</summary>
internal sealed class FishingSoundNotifications : IDisposable
{
    private enum RequestKind { Bite, Catch, Registration, Preview, Reload, Stop }
    private sealed record Request(RequestKind Type, int Session, long At, uint Id = 0, FishingSoundKind Sound = default);
    private readonly FishingTracker tracker;
    private readonly FishCollectionObserver collection;
    private readonly FishCatalog catalog;
    private readonly FishPreferences preferences;
    private readonly SpawnAlertOutput output;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly CustomAlertSound[] sounds;
    private readonly ConcurrentQueue<Request> pending = new();
    private IReadOnlyList<FishGuideRow>? indexedRows;
    private Dictionary<uint, FishGuideRow> byItem = [], byFish = [];
    private int session = -1;
    private long lastPreview;
    private volatile bool disposed;
    private UiMessage? previewResult;

    public FishingSoundNotifications(FishingTracker tracker, FishCollectionObserver collection, FishCatalog catalog,
        FishPreferences preferences, SpawnAlertOutput output, IFramework framework, IPluginLog log)
    {
        this.tracker = tracker; this.collection = collection; this.catalog = catalog;
        this.preferences = preferences; this.output = output; this.framework = framework; this.log = log;
        sounds = [new(log), new(log), new(log)];
        tracker.BiteObserved += OnBite;
        tracker.CatchObserved += OnCatch;
        collection.FishRegistered += OnRegistration;
        framework.Update += Update;
    }

    public UiMessage? PreviewResult => previewResult;
    public CustomSoundView CustomSound(FishingSoundKind kind) => sounds[(int)kind].View;
    public void Preview(FishingSoundKind kind, int expectedSession) => Enqueue(RequestKind.Preview, expectedSession, sound: kind);
    public void Reload(FishingSoundKind kind, int expectedSession) => Enqueue(RequestKind.Reload, expectedSession, sound: kind);
    public void Stop(FishingSoundKind kind, int expectedSession) => Enqueue(RequestKind.Stop, expectedSession, sound: kind);
    private void OnBite(int currentSession) => Enqueue(RequestKind.Bite, currentSession);
    private void OnCatch(int currentSession, uint item) => Enqueue(RequestKind.Catch, currentSession, item);
    private void OnRegistration(int currentSession, uint fish) => Enqueue(RequestKind.Registration, currentSession, fish);

    private void Enqueue(RequestKind type, int expectedSession, uint id = 0, FishingSoundKind sound = default)
    {
        if (!disposed && pending.Count < 64) pending.Enqueue(new(type, expectedSession, Stopwatch.GetTimestamp(), id, sound));
    }

    public static FishingSoundSettings Settings(CastingTimerSettings settings, FishingSoundKind kind) => kind switch
    {
        FishingSoundKind.Bite => settings.BiteSound,
        FishingSoundKind.BigFish => settings.BigFishSound,
        _ => settings.LegendaryFishSound,
    };

    private void Update(IFramework _)
    {
        if (disposed) return;
        var personal = preferences.View;
        if (session != personal.Session)
        {
            session = personal.Session;
            previewResult = null;
            lastPreview = 0;
        }
        foreach (var kind in Enum.GetValues<FishingSoundKind>())
        {
            var settings = Settings(personal.Timer, kind);
            sounds[(int)kind].Configure(personal.HasCharacter && settings.Sound == CustomAlertSound.Choice,
                settings.CustomPath, personal.Session);
        }
        if (!ReferenceEquals(indexedRows, catalog.View.Rows))
        {
            indexedRows = catalog.View.Rows;
            byFish = indexedRows.Where(row => row.IsDisplayable).ToDictionary(row => row.FishParameterId);
            byItem = indexedRows.Where(row => row.IsDisplayable && row.ItemType == "Item")
                .GroupBy(row => row.ItemId).Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single());
        }
        while (pending.TryDequeue(out var request))
        {
            if (!personal.HasCharacter || request.Session != session || Stopwatch.GetElapsedTime(request.At).TotalSeconds > 2) continue;
            try
            {
                var kind = request.Sound;
                if (request.Type is RequestKind.Catch or RequestKind.Registration)
                {
                    var index = request.Type == RequestKind.Catch ? byItem : byFish;
                    if (!index.TryGetValue(request.Id, out var fish) || fish.Grade is not (FishGrade.Big or FishGrade.Legendary)) continue;
                    kind = fish.Grade == FishGrade.Big ? FishingSoundKind.BigFish : FishingSoundKind.LegendaryFish;
                    // Registration is authoritative for first acquisition, including when prior collection was Unknown.
                    // The two event sources are exclusive, so their arrival order cannot double-play a catch.
                    if (Settings(personal.Timer, kind).FirstCatchOnly != (request.Type == RequestKind.Registration)) continue;
                }
                var settings = Settings(personal.Timer, kind);
                var player = sounds[(int)kind];
                if (request.Type == RequestKind.Reload)
                    player.Configure(settings.Sound == CustomAlertSound.Choice, settings.CustomPath, session, force: true);
                else if (request.Type == RequestKind.Stop) player.Stop();
                else
                {
                    if (request.Type == RequestKind.Preview)
                    {
                        if (lastPreview != 0 && Stopwatch.GetElapsedTime(lastPreview).TotalSeconds < 1) continue;
                        lastPreview = Stopwatch.GetTimestamp();
                    }
                    var played = settings.Sound == CustomAlertSound.Choice ? player.TryPlay(session) : output.PlaySound(settings.Sound, session);
                    if (request.Type == RequestKind.Preview)
                        previewResult = new(settings.Sound == CustomAlertSound.Choice
                            ? played ? T.CustomSoundQueued : T.CustomSoundUnavailable
                            : played ? T.AlertSoundPreviewed : T.AlertSoundUnavailable);
                }
            }
            catch (Exception ex) { log.Error(ex, "Could not deliver a fishing sound notification."); }
        }
    }

    public void Dispose()
    {
        disposed = true;
        framework.Update -= Update;
        tracker.BiteObserved -= OnBite;
        tracker.CatchObserved -= OnCatch;
        collection.FishRegistered -= OnRegistration;
        pending.Clear();
        foreach (var player in sounds) player.Dispose();
    }
}
