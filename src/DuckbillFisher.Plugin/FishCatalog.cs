using System.Text;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using Lumina.Excel.Sheets;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal enum FishRowResolution
{
    Unknown,
    Resolved,
    ZeroRow,
    EmptyItemReference,
    UnsupportedReference,
    MissingReferencedRow,
    EmptyName,
}

internal sealed record FishGuideRow(uint FishParameterId, uint ItemId, string ItemType, string Name, bool IsInLog)
{
    public uint IconId { get; init; }
    public FishRowResolution Resolution { get; init; }
    public bool IsDisplayable => Resolution == FishRowResolution.Resolved;
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? UiText.Get(T.MissingName) : Name;
}

internal sealed record FishCatalogView(IReadOnlyList<FishGuideRow> Rows, UiMessage Status);

/// <summary>Character-independent sheet data shared by the list and collection diagnostics.</summary>
internal sealed class FishCatalog : IDisposable
{
    private readonly IDataManager dataManager;
    private readonly IFramework framework;
    private readonly object gate = new();
    private FishCatalogView view = new([], new(T.CatalogWaiting));
    private bool requested;
    private bool forceReload;
    private bool disposed;

    public FishCatalog(IDataManager dataManager, IFramework framework)
    {
        this.dataManager = dataManager;
        this.framework = framework;
        framework.Update += OnUpdate;
    }

    public FishCatalogView View { get { lock (gate) return view; } }

    public void RequestLoad(bool reload = false)
    {
        lock (gate)
        {
            if (disposed || (!reload && view.Rows.Count > 0))
                return;
            requested = true;
            forceReload |= reload;
        }
    }

    private void OnUpdate(IFramework _)
    {
        lock (gate)
        {
            if (disposed || !requested)
                return;
            var reload = forceReload;
            requested = false;
            forceReload = false;
            Read(reload);
        }
    }

    // Called only on Framework.Update, including from the guide diagnostics.
    public IReadOnlyList<FishGuideRow> Read(bool reload)
    {
        if (!framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Fish sheets must be read on Framework.Update.");
        lock (gate)
        {
            if (disposed)
                return [];
            if (!reload && view.Rows.Count > 0)
                return view.Rows;
            try
            {
                var sheet = dataManager.GetExcelSheet<FishParameter>()
                    ?? throw new InvalidOperationException("FishParameter sheet unavailable.");
                var rows = new List<FishGuideRow>();
                foreach (var fish in sheet)
                {
                    var item = fish.Item;
                    string? name = null;
                    uint iconId = 0;
                    var resolution = fish.RowId == 0 ? FishRowResolution.ZeroRow
                        : item.RowId == 0 ? FishRowResolution.EmptyItemReference
                        : FishRowResolution.UnsupportedReference;
                    if (fish.RowId != 0 && item.RowId != 0 && item.Is<Item>())
                    {
                        resolution = FishRowResolution.MissingReferencedRow;
                        if (item.GetValueOrDefault<Item>() is { } normal)
                        {
                            name = normal.Name.ExtractText();
                            iconId = normal.Icon;
                            resolution = string.IsNullOrWhiteSpace(name) ? FishRowResolution.EmptyName : FishRowResolution.Resolved;
                        }
                    }
                    else if (fish.RowId != 0 && item.RowId != 0 && item.Is<EventItem>())
                    {
                        resolution = FishRowResolution.MissingReferencedRow;
                        if (item.GetValueOrDefault<EventItem>() is { } eventItem)
                        {
                            name = eventItem.Name.ExtractText();
                            iconId = eventItem.Icon;
                            resolution = string.IsNullOrWhiteSpace(name) ? FishRowResolution.EmptyName : FishRowResolution.Resolved;
                        }
                    }
                    rows.Add(new(fish.RowId, item.RowId, item.RowType?.Name ?? "Unknown", name ?? "", fish.IsInLog)
                    {
                        IconId = iconId,
                        Resolution = resolution,
                    });
                }
                if (rows.Count == 0)
                    throw new InvalidDataException("FishParameter sheet is empty.");
                view = new(rows.ToArray(), new(T.CatalogLoaded));
            }
            catch (Exception ex)
            {
                view = new([], new(T.RowsReadError, ex.GetType().Name));
            }
            return view.Rows;
        }
    }

    public string CreateReport()
    {
        var snapshot = View;
        var unresolved = snapshot.Rows.Where(row => !row.IsDisplayable).ToArray();
        var report = new StringBuilder();
        void Append(FormattableString line) => report.AppendLine(FormattableString.Invariant(line));
        report.AppendLine("DuckbillFisher MVP-01/catalog/v1");
        Append($"Plugin: {typeof(Plugin).Assembly.GetName().Version}");
        Append($"Rows: {snapshot.Rows.Count}; displayable: {snapshot.Rows.Count - unresolved.Length}; unresolved: {unresolved.Length}; unresolved in log: {unresolved.Count(row => row.IsInLog)}");
        report.AppendLine($"Status: [{snapshot.Status.Key}] {snapshot.Status.Diagnostic}");
        foreach (var group in unresolved.GroupBy(row => row.Resolution).OrderBy(group => group.Key))
            Append($"Resolution: {group.Key}; count: {group.Count()}");
        report.AppendLine("Unresolved row details: up to 80, ordered by FishParameter ID. No character collection data.");
        foreach (var row in unresolved.OrderBy(row => row.FishParameterId).Take(80))
            Append($"FishParameter={row.FishParameterId}; {row.ItemType}={row.ItemId}; IsInLog={row.IsInLog}; resolution={row.Resolution}");
        return report.ToString();
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            framework.Update -= OnUpdate;
            requested = false;
        }
    }
}
