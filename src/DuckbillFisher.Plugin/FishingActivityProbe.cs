using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

internal enum FishingLogActor { Local, Other, NoSource, Unknown }

internal sealed record FishingProbeView(FishingObservationView Observation, FishingNativeSample? Native,
    int Session, int Logs, int RejectedLogs, int QueueDropped, int ValidContracts, int TotalContracts,
    uint Territory, DateTimeOffset? StartedUtc, bool LastStartHadCharacter);

/// <summary>
/// Explicit diagnostic recording only. Does not infer casts, results, collection or session completion.
/// Public callback data is copied immediately; all native sampling runs on Framework.Update.
/// </summary>
internal sealed class FishingActivityProbe : IDisposable
{
    private const int QueueCapacity = 64;
    private const int ReportTail = 40;
    private readonly ICondition conditions;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IFramework framework;
    private readonly IChatGui chat;
    private readonly IDataManager data;
    private readonly FishPreferences preferences;
    private readonly NativeFishingActivityReader reader;
    private readonly object gate = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly FishingObservation observation = new();
    private readonly Queue<PendingLog> pending = new();
    private readonly HashSet<uint> validLogs = [];
    private FishingProbeView view;
    private FishingNativeSample? lastNative;
    private FishingConditionSnapshot? lastFlags;
    private bool? requested;
    private bool recording;
    private bool disposed;
    private bool contractsLoaded;
    private int session;
    private ulong character;
    private string localName = "";
    private ushort homeWorld;
    private uint territory;
    private uint lastJob;
    private TimeSpan lastUpdate;
    private int logCount;
    private int rejected;
    private int dropped;
    private DateTimeOffset? startedUtc;
    private bool startHadCharacter;

    public FishingActivityProbe(ICondition conditions, IClientState client, IPlayerState player,
        IFramework framework, IChatGui chat, IDataManager data, FishPreferences preferences,
        NativeFishingActivityReader reader)
    {
        this.conditions = conditions;
        this.client = client;
        this.player = player;
        this.framework = framework;
        this.chat = chat;
        this.data = data;
        this.preferences = preferences;
        this.reader = reader;
        view = Snapshot();
        framework.Update += OnUpdate;
        chat.LogMessage += OnLogMessage;
        conditions.ConditionChange += OnConditionChange;
    }

    public FishingProbeView View { get { lock (gate) return view; } }
    public void Start() { lock (gate) if (!disposed) requested = true; }
    public void Stop() { lock (gate) if (!disposed) requested = false; }

    public void Mark(string code)
    {
        lock (gate)
        {
            if (!recording || disposed) return;
            Record(clock.Elapsed, $"MARK (manual): {code}");
            Publish();
        }
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag is not (ConditionFlag.Fishing or ConditionFlag.ExecutingGatheringAction or ConditionFlag.Casting)) return;
        lock (gate)
        {
            if (disposed || !recording) return;
            Record(clock.Elapsed, $"FLAG {flag}={value}");
            Publish();
        }
    }

    private void OnLogMessage(ILogMessage message)
    {
        lock (gate)
        {
            if (disposed || !recording || !validLogs.Contains(message.LogMessageId)) return;
            var personal = preferences.View;
            // A callback before the next Framework session update must not inherit the old identity.
            if (!personal.DeveloperMode || !personal.HasCharacter || personal.Session != session ||
                personal.CharacterId != character || !client.IsLoggedIn || !player.IsLoaded ||
                player.ContentId != character || conditions[ConditionFlag.BetweenAreas] ||
                conditions[ConditionFlag.BetweenAreas51]) { rejected++; return; }
            if (message.ParameterCount is < 0 or > 8) { rejected++; return; }
            var actor = FishingLogActor.NoSource;
            if (message.SourceEntity is { } source)
            {
                var sourceName = source.Name.ExtractText();
                actor = source.IsPlayer && localName.Length > 0 && homeWorld != 0 &&
                    sourceName.Length > 0 && source.HomeWorldId != 0
                    ? source.HomeWorldId == homeWorld && sourceName == localName
                        ? FishingLogActor.Local : FishingLogActor.Other
                    : FishingLogActor.Unknown;
            }
            // Preserve positional numeric slots; never retain borrowed strings, names or wrappers.
            var numbers = new uint?[message.ParameterCount];
            for (var i = 0; i < numbers.Length; i++)
                if (!message.Parameters[i].IsString) numbers[i] = message.Parameters[i].UIntValue;
            if (pending.Count == QueueCapacity) { dropped++; return; }
            pending.Enqueue(new(session, clock.Elapsed, message.LogMessageId, actor, client.TerritoryType, numbers, Flags()));
        }
    }

    private void OnUpdate(IFramework _)
    {
        var personal = preferences.View;
        lock (gate)
        {
            if (disposed) return;
            var now = clock.Elapsed;
            var changed = false;
            if (requested is bool enabled)
            {
                requested = null;
                if (enabled && personal.DeveloperMode && !recording)
                {
                    EnsureContracts();
                    recording = true;
                    session = personal.Session;
                    character = personal.HasCharacter ? personal.CharacterId : 0;
                    startHadCharacter = character != 0;
                    startedUtc = DateTimeOffset.UtcNow;
                    logCount = rejected = dropped = 0;
                    territory = lastJob = 0;
                    localName = "";
                    homeWorld = 0;
                    lastNative = null;
                    lastFlags = null;
                    pending.Clear();
                    lastUpdate = now;
                    observation.Start(now, Flags());
                    Record(now, $"PROBE_START Session={session}; HasCharacter={startHadCharacter}; Contracts={validLogs.Count}/{FishingLogContracts.Count}");
                    changed = true;
                }
                else if (!enabled)
                {
                    if (personal.HasCharacter && personal.Session == session && personal.CharacterId == character &&
                        client.IsLoggedIn && player.IsLoaded && player.ContentId == character)
                        DrainPending(now);
                    Finish(now, "USER_STOP");
                }
            }
            if (!recording) return;
            if (!personal.DeveloperMode) { Finish(now, "DEVELOPER_DISABLED"); return; }
            if (personal.Session != session || !personal.HasCharacter || personal.CharacterId != character ||
                !client.IsLoggedIn || !player.IsLoaded || player.ContentId != character)
            { Finish(now, "CHARACTER_BOUNDARY"); return; }
            if (conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51])
            { Finish(now, "AREA_TRANSITION"); return; }
            if (now - lastUpdate > TimeSpan.FromSeconds(5))
            { Finish(now, "OBSERVATION_GAP"); return; }
            lastUpdate = now;
            localName = player.CharacterName;
            homeWorld = (ushort)player.HomeWorld.RowId;
            var currentTerritory = client.TerritoryType;
            var job = player.ClassJob.RowId;
            if (territory != currentTerritory || lastJob != job)
            {
                territory = currentTerritory;
                lastJob = job;
                Record(now, $"CONTEXT Territory={territory}; Job={job}");
                changed = true;
            }
            var current = reader.Read(character, job == 18);
            var flags = Flags();
            if (current != lastNative || flags != lastFlags)
            {
                lastNative = current;
                lastFlags = flags;
                Record(now, FormatNative(current));
                changed = true;
            }
            changed |= DrainPending(now);
            if (changed || view.RejectedLogs != rejected || view.QueueDropped != dropped) Publish();
        }
    }

    private bool DrainPending(TimeSpan now)
    {
        var changed = pending.Count > 0;
        while (pending.TryDequeue(out var item))
        {
            if (item.Session != session || item.Territory != territory || now - item.At > TimeSpan.FromSeconds(2))
            { rejected++; continue; }
            logCount++;
            var values = string.Join(',', item.Numbers.Select(value => value?.ToString(CultureInfo.InvariantCulture) ?? "string"));
            observation.Record(item.At, $"LOG Id={item.Id}; Actor={item.Actor}; Territory={item.Territory}; Params=[{values}]", item.Flags);
        }
        return changed;
    }

    private void EnsureContracts()
    {
        if (contractsLoaded) return;
        contractsLoaded = true;
        // This is a managed sheet lookup and may fail independently of native availability.
        try { FishingLogContracts.Validate(data, validLogs); }
        catch (Exception) { validLogs.Clear(); }
    }

    private FishingConditionSnapshot Flags() => new(conditions[ConditionFlag.Fishing],
        conditions[ConditionFlag.ExecutingGatheringAction], conditions[ConditionFlag.Casting]);

    private void Record(TimeSpan at, string description) => observation.Record(at, description, Flags());
    private void Finish(TimeSpan now, string code)
    {
        if (!recording) return;
        Record(now, code);
        observation.Stop(now, Flags());
        recording = false;
        pending.Clear();
        character = 0;
        localName = "";
        homeWorld = 0;
        Publish();
    }
    private FishingProbeView Snapshot() => new(observation.GetView(), lastNative, session, logCount, rejected,
        dropped, validLogs.Count, FishingLogContracts.Count, territory, startedUtc, startHadCharacter);
    private void Publish() => view = Snapshot();

    private static string FormatNative(FishingNativeSample sample) => string.Create(CultureInfo.InvariantCulture,
        $"NATIVE Read={sample.Availability}; Phase={sample.Phase}; Equipped={sample.EquippedBait}; BaitFlags={sample.BaitFlags}; SwimIndex={sample.SelectedSwimbait}; Swim=[{sample.Swimbait0},{sample.Swimbait1},{sample.Swimbait2}]; AtHole={sample.AtFishingHole}");

    public string CreateReport()
    {
        lock (gate)
        {
            var v = view;
            var report = new StringBuilder();
            report.AppendLine("DuckbillFisher MVP-02/casting-probe/v1");
            report.AppendLine($"Plugin={typeof(Plugin).Assembly.GetName().Version}; Session={v.Session}; Recording={v.Observation.IsRecording}; StartHadCharacter={v.LastStartHadCharacter}");
            report.AppendLine($"StartedUtc={v.StartedUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "none"}; Territory={v.Territory}");
            report.AppendLine($"Contracts={v.ValidContracts}/{v.TotalContracts}; Logs={v.Logs}; Rejected={v.RejectedLogs}; QueueDropped={v.QueueDropped}; HistoryDropped={v.Observation.DroppedCount}");
            report.AppendLine("Raw observations only. Actor=NoSource is not proof of self. No cast/result/collection inference. MARK is manual.");
            if (v.Native is { } sample) report.AppendLine(FormatNative(sample));
            report.AppendLine($"LastEvents={Math.Min(ReportTail, v.Observation.Entries.Count)}; F=Fishing; G=ExecutingGatheringAction; C=Casting; monotonic seconds since START.");
            foreach (var entry in v.Observation.Entries.TakeLast(ReportTail))
                report.AppendLine(FishingConditionObserver.FormatEntry(entry));
            return report.ToString();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            recording = false;
            requested = null;
            pending.Clear();
            localName = "";
            character = 0;
            framework.Update -= OnUpdate;
            chat.LogMessage -= OnLogMessage;
            conditions.ConditionChange -= OnConditionChange;
            clock.Stop();
        }
    }

    private sealed record PendingLog(int Session, TimeSpan At, uint Id, FishingLogActor Actor,
        uint Territory, uint?[] Numbers, FishingConditionSnapshot Flags);
}
