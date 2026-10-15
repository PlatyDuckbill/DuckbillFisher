using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using Lumina.Excel.Sheets;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed record FishGuideRow(uint FishParameterId, uint ItemId, string ItemType, string Name, bool IsInLog)
{
    public uint IconId { get; init; }
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
                    if (item.Is<Item>() && item.GetValueOrDefault<Item>() is { } normal)
                    {
                        name = normal.Name.ExtractText();
                        iconId = normal.Icon;
                    }
                    else if (item.Is<EventItem>() && item.GetValueOrDefault<EventItem>() is { } eventItem)
                    {
                        name = eventItem.Name.ExtractText();
                        iconId = eventItem.Icon;
                    }
                    rows.Add(new(fish.RowId, item.RowId, item.RowType?.Name ?? "Unknown", name ?? "", fish.IsInLog)
                    {
                        IconId = iconId,
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
