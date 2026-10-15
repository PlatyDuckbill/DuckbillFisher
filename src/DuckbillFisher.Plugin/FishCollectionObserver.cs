using System.Collections.Frozen;
using System.Diagnostics;
using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using Lumina.Excel.Sheets;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record FishCollectionView(int Revision, int Session, IReadOnlySet<uint> Caught, UiMessage Status)
{
    public int EligibleRows { get; init; }
    public int Scans { get; init; }
    public int RawFalse { get; init; }
    public int Unknown { get; init; }
    public int RegistrationMessages { get; init; }
    public int AppliedRegistrations { get; init; }
    public uint LastLogItem { get; init; }
    public uint LastLogFish { get; init; }
    public bool RegistrationContract { get; init; }
    public bool GuideVisible { get; init; }
    public string LastTrigger { get; init; } = "none";
    public double? SampledAtSeconds { get; init; }
}

/// <summary>Session-local positive observations only. False bits never establish uncaught/readiness.</summary>
internal sealed class FishCollectionObserver : IDisposable
{
    // Inspected KR LogMessage: Item lookup uses lnum1 (first numeric parameter).
    private const uint RegistrationLogId = 1114;
    private const string RegistrationMacro = "어류도감에 <sheet(Item,lnum1,0)>의 정보를 기록했습니다!";
    private readonly FishPreferences preferences;
    private readonly FishCatalog catalog;
    private readonly NativeFishCollectionReader reader;
    private readonly IDataManager dataManager;
    private readonly IGameGui gameGui;
    private readonly IChatGui chat;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly object gate = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Queue<Registration> pending = new();
    private readonly HashSet<uint> caught = [];
    private FishCollectionView view = new(0, 0, FrozenSet<uint>.Empty, new(T.CollectionWaiting));
    private IReadOnlyList<FishGuideRow>? lastRows;
    private FishGuideRow[] eligible = [];
    private Dictionary<uint, uint> fishByItem = [];
    private bool? logContract;
    private bool requested;
    private bool disposed;
    private TimeSpan nextPoll;
    private TimeSpan nextScan;

    public FishCollectionObserver(FishPreferences preferences, FishCatalog catalog, NativeFishCollectionReader reader,
        IDataManager dataManager, IGameGui gameGui, IChatGui chat, IFramework framework, IPluginLog log)
    {
        this.preferences = preferences;
        this.catalog = catalog;
        this.reader = reader;
        this.dataManager = dataManager;
        this.gameGui = gameGui;
        this.chat = chat;
        this.framework = framework;
        this.log = log;
        framework.Update += OnUpdate;
        chat.LogMessage += OnLogMessage;
    }

    public FishCollectionView View { get { lock (gate) return view; } }

    private void OnLogMessage(ILogMessage message)
    {
        // The ILogMessage and its parameters are borrowed and valid only in this callback.
        // Copy only this registration's numeric item ID; never retain chat strings or native wrappers.
        if (message.LogMessageId != RegistrationLogId)
            return;
        var personal = preferences.View;
        if (!personal.HasCharacter || message.ParameterCount < 1 || message.Parameters[0].IsString)
            return;
        var itemId = message.Parameters[0].UIntValue;
        lock (gate)
            if (!disposed && pending.Count < 64)
                pending.Enqueue(new(personal.Session, itemId, clock.Elapsed));
    }

    private void OnUpdate(IFramework _)
    {
        var personal = preferences.View;
        lock (gate)
        {
            if (disposed)
                return;
            if (view.Session != personal.Session)
            {
                caught.Clear();
                view = new(view.Revision + 1, personal.Session, FrozenSet<uint>.Empty, new(T.CollectionWaiting));
                nextScan = TimeSpan.Zero;
            }
            if (!personal.HasCharacter)
            {
                pending.Clear();
                return;
            }
            if (clock.Elapsed < nextPoll)
                return;
            nextPoll = clock.Elapsed + TimeSpan.FromMilliseconds(250);
            try
            {
                Poll(personal);
            }
            catch (Exception ex)
            {
                // Managed API failures only. This cannot make an invalid native pointer recoverable.
                if (view.Status.Key != T.CollectionReadFailed)
                    log.Error(ex, "Could not refresh automatic fish collection observations.");
                var changed = !caught.SetEquals(view.Caught);
                view = view with { Status = new(T.CollectionReadFailed), GuideVisible = false,
                    Caught = changed ? caught.ToFrozenSet() : view.Caught, Revision = view.Revision + (changed ? 1 : 0) };
            }
        }
    }

    private void Poll(FishPreferencesView personal)
    {
        if (!reader.IsCompatible)
        {
            pending.Clear();
            view = view with { Status = reader.Compatibility };
            return;
        }
        if (!requested)
        {
            catalog.RequestLoad();
            requested = true;
        }
        var rows = catalog.View.Rows;
        var rowsChanged = !ReferenceEquals(rows, lastRows);
        if (rowsChanged)
        {
            eligible = rows.Where(NativeFishCollectionReader.Supports).ToArray();
            // Ambiguous item mappings are excluded from event-based observations.
            fishByItem = eligible.GroupBy(row => row.ItemId).Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single().FishParameterId);
            lastRows = rows;
            nextScan = TimeSpan.Zero;
        }
        if (eligible.Length == 0)
        {
            view = view with { Status = new(T.CollectionWaiting) };
            return;
        }
        if (logContract is null)
        {
            var sheet = dataManager.GetExcelSheet<LogMessage>();
            logContract = sheet is not null && sheet.TryGetRow(RegistrationLogId, out var row) &&
                row.Text.ToMacroString() == RegistrationMacro;
        }
        var changed = false;
        var trigger = "none";
        while (pending.TryDequeue(out var registration))
        {
            // Queued data must belong to this exact ready session and arrive promptly.
            if (registration.Session != personal.Session || clock.Elapsed - registration.At > TimeSpan.FromSeconds(5))
                continue;
            view = view with { RegistrationMessages = view.RegistrationMessages + 1, LastLogItem = registration.ItemId, LastLogFish = 0 };
            if (logContract != true || !fishByItem.TryGetValue(registration.ItemId, out var fishId))
                continue;
            changed |= caught.Add(fishId);
            trigger = "RegistrationLog";
            view = view with { AppliedRegistrations = view.AppliedRegistrations + 1, LastLogFish = fishId };
        }
        var addon = gameGui.GetAddonByName("FishGuide2");
        var visible = !addon.IsNull && addon.IsVisible;
        if (visible && (!view.GuideVisible || clock.Elapsed >= nextScan))
        {
            var rawFalse = 0;
            var unknown = 0;
            foreach (var row in eligible)
            {
                var sample = reader.Read(row, personal.CharacterId, true);
                if (sample.Value == true)
                    changed |= caught.Add(row.FishParameterId);
                else if (sample.Value == false)
                    rawFalse++;
                else
                    unknown++;
            }
            nextScan = clock.Elapsed + TimeSpan.FromSeconds(1);
            trigger = "GuideScan";
            view = view with { Scans = view.Scans + 1, RawFalse = rawFalse, Unknown = unknown };
        }
        view = view with
        {
            Revision = view.Revision + (changed ? 1 : 0), Caught = changed ? caught.ToFrozenSet() : view.Caught,
            EligibleRows = eligible.Length, GuideVisible = visible, RegistrationContract = logContract == true,
            Status = new(logContract == true ? T.CollectionPositiveOnly : T.CollectionLogUnavailable),
            LastTrigger = trigger == "none" ? view.LastTrigger : trigger,
            SampledAtSeconds = trigger == "none" ? view.SampledAtSeconds : clock.Elapsed.TotalSeconds,
        };
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            pending.Clear();
            caught.Clear();
        }
        chat.LogMessage -= OnLogMessage;
        framework.Update -= OnUpdate;
    }

    private sealed record Registration(int Session, uint ItemId, TimeSpan At);
}
