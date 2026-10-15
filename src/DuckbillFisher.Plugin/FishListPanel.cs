using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Displays cached static rows. It does not read or infer character collection status.</summary>
internal sealed class FishListPanel(FishCatalog catalog, ITextureProvider textures, FishingMapService mapService)
{
    private static readonly StringComparer NameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("ko-KR"), true);
    private IReadOnlyList<FishGuideRow>? lastRows;
    private FishGuideRow[] matches = [];
    private string search = "";
    private bool inLogOnly = true;
    private bool showUnresolved;
    private int unresolvedCount;
    private bool requested;
    private bool resetScroll;
    private int sort;
    private uint? selectedId;
    private FishGuideRow? selectedFish;
    private int detailCoverage;
    private bool detailsOnly;
    private int lastForecastRevision;

    public void Draw()
    {
        if (!requested)
        {
            catalog.RequestLoad();
            requested = true;
        }
        var view = catalog.View;
        var changed = !ReferenceEquals(lastRows, view.Rows);
        if (changed)
        {
            detailCoverage = view.Rows.Count(row => row.Details is not null);
            selectedFish = selectedId is { } selected ? view.Rows.FirstOrDefault(row => row.FishParameterId == selected) : null;
        }
        lastRows = view.Rows;
        ImGui.TextWrapped(view.Status.Display);
        ImGui.SetNextItemWidth(280);
        changed |= ImGui.InputText(UiText.Label(T.FishSearch, "CatalogSearch"), ref search, 100);
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogInLogOnly, "CatalogInLogOnly"), ref inLogOnly);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        string[] sortNames = [UiText.Get(T.CatalogSortName), UiText.Get(T.CatalogSortId), UiText.Get(T.CatalogSortWindow)];
        changed |= ImGui.Combo(UiText.Label(T.CatalogSort, "CatalogSort"), ref sort, sortNames, sortNames.Length);
        if (ImGui.Button(UiText.Label(T.ReloadFish, "ReloadCatalog")))
            catalog.RequestLoad(reload: true);
        ImGui.SameLine();
        if (ImGui.Button(UiText.Label(T.CopyCatalogReport, "CopyCatalogReport")))
            ImGui.SetClipboardText(catalog.CreateReport());
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogShowUnresolved, "CatalogShowUnresolved"), ref showUnresolved);
        ImGui.SameLine();
        changed |= ImGui.Checkbox(UiText.Label(T.CatalogDetailsOnly, "CatalogDetailsOnly"), ref detailsOnly);

        // Rebuild only for data or user input changes, never on every frame or scroll step.
        if (changed || (sort == 2 && lastForecastRevision != view.ForecastRevision))
            UpdateMatches(view, changed);
        lastForecastRevision = view.ForecastRevision;
        ImGui.TextUnformatted(UiText.Get(T.CatalogCount, matches.Length, view.Rows.Count));
        if (unresolvedCount > 0)
            ImGui.TextWrapped(UiText.Get(T.CatalogUnresolvedCount, unresolvedCount));
        ImGui.TextWrapped(UiText.Get(T.CatalogDetailsPending));
        ImGui.Separator();

        if (ImGui.BeginTable("CatalogLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableNextColumn();
            DrawRows();
            ImGui.TableNextColumn();
            if (ImGui.BeginChild("CatalogDetail", new Vector2(0, 0), false))
                DrawDetails(view);
            ImGui.EndChild();
            ImGui.EndTable();
        }
    }

    private void DrawRows()
    {
        if (ImGui.BeginChild("CatalogRows", new Vector2(0, 0), true))
        {
            if (resetScroll)
            {
                ImGui.SetScrollY(0);
                resetScroll = false;
            }
            if (matches.Length == 0)
                ImGui.TextWrapped(UiText.Get(T.CatalogNoMatches));
            var rowHeight = ImGui.GetTextLineHeight() * 3.2f;
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                clipper.Begin(matches.Length, rowHeight + ImGui.GetStyle().ItemSpacing.Y);
                while (clipper.Step())
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        DrawRow(matches[i], rowHeight);
            }
            finally
            {
                clipper.Destroy();
            }
        }
        ImGui.EndChild();
    }

    private void UpdateMatches(FishCatalogView view, bool reset)
    {
        var term = search.Trim();
        var scope = view.Rows.Where(row => !inLogOnly || row.IsInLog).ToArray();
        unresolvedCount = scope.Count(row => !row.IsDisplayable);
        var filtered = scope.Where(row => (showUnresolved || row.IsDisplayable) && (!detailsOnly || row.Details is not null) &&
            (term.Length == 0 || row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
             row.Spots.Any(spot => spot.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || spot.Zone.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
             row.FishParameterId.ToString(CultureInfo.InvariantCulture) == term));
        matches = (sort switch
        {
            0 => filtered.OrderBy(row => row.Name, NameComparer).ThenBy(row => row.FishParameterId),
            2 => filtered.OrderBy(row => WindowSortKey(view, row.FishParameterId)).ThenBy(row => row.FishParameterId),
            _ => filtered.OrderBy(row => row.FishParameterId),
        }).ToArray();
        resetScroll |= reset;
    }

    private void DrawRow(FishGuideRow fish, float height)
    {
        ImGui.PushID((int)fish.FishParameterId);
        var start = ImGui.GetCursorPos();
        if (ImGui.Selectable("###CatalogRow", selectedId == fish.FishParameterId,
                ImGuiSelectableFlags.None, new Vector2(ImGui.GetContentRegionAvail().X, height)))
        {
            selectedId = fish.FishParameterId;
            selectedFish = fish;
        }
        var next = ImGui.GetCursorPos();
        var iconSize = height - 8;
        ImGui.SetCursorPos(start + new Vector2(4, 4));
        // Shared immediate textures are owned by Dalamud; do not dispose the borrowed wrap.
        if (fish.IconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(fish.IconId), out var icon)
            && icon.TryGetWrap(out var wrap, out _))
            ImGui.Image(wrap.Handle, new Vector2(iconSize));
        else
            ImGui.Dummy(new Vector2(iconSize));
        ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4));
        ImGui.TextUnformatted(fish.DisplayName);
        ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4 + ImGui.GetTextLineHeightWithSpacing()));
        if (fish.Spots.FirstOrDefault() is { } spot)
            ImGui.TextUnformatted(spot.Name);
        else
            ImGui.TextDisabled(UiText.Get(fish.IsDisplayable ? T.CatalogRowInfo : T.CatalogUnresolvedRow, fish.FishParameterId));
        ImGui.SetCursorPos(next);
        ImGui.PopID();
    }

    private static DateTimeOffset WindowSortKey(FishCatalogView view, uint id) =>
        view.Windows.TryGetValue(id, out var window) ? window.Kind switch
        {
            FishingWindowKind.Open or FishingWindowKind.Always => DateTimeOffset.MinValue,
            FishingWindowKind.Upcoming => window.Start ?? DateTimeOffset.MaxValue,
            _ => DateTimeOffset.MaxValue,
        } : DateTimeOffset.MaxValue;

    private void DrawDetails(FishCatalogView view)
    {
        var selected = selectedFish;
        ImGui.TextWrapped(UiText.Get(T.CatalogDetailCoverage, detailCoverage));
        if (selected is null)
            ImGui.TextWrapped(UiText.Get(T.CatalogDetailPrompt));
        else
        {
            ImGui.TextUnformatted(selected.DisplayName);
            ImGui.Separator();
            foreach (var spot in selected.Spots)
            {
                ImGui.TextWrapped(UiText.Get(T.SpotLine, spot.Zone, spot.Name, spot.X, spot.Y));
                if (ImGui.Button(UiText.Label(T.OpenFishingMap, $"OpenFishingMap{spot.Id}")))
                    mapService.Request(spot);
            }
            if (mapService.Result is { } mapResult)
                ImGui.TextWrapped(mapResult.Display);
            if (selected.Details is not { } details)
                ImGui.TextWrapped(UiText.Get(T.CatalogDetailsMissing));
            else
            {
                var rule = details.Rule;
                ImGui.Separator();
                ImGui.TextWrapped(UiText.Get(T.BaitPathLabel, string.Join(" → ", details.BaitNames)));
                ImGui.TextWrapped(UiText.Get(T.HooksetLabel, new UiMessage(rule.Hookset == "Precision" ? T.HooksetPrecision : T.HooksetPowerful)));
                ImGui.TextWrapped(UiText.Get(T.TugLabel, rule.Tug switch { "light" => "!", "medium" => "!!", _ => "!!!" }));
                ImGui.TextWrapped(rule.StartHour == 0 && rule.EndHour == 24 ? UiText.Get(T.TimeAllDay)
                    : UiText.Get(T.TimeConditionLabel, EtClock(rule.StartHour), EtClock(rule.EndHour)));
                ImGui.TextWrapped(UiText.Get(T.WeatherConditionLabel, WeatherNames(details.WeatherNames)));
                ImGui.TextWrapped(UiText.Get(T.PreviousWeatherConditionLabel, WeatherNames(details.PreviousWeatherNames)));
                if (rule.Predators.Length > 0)
                {
                    var requirements = rule.Predators.Select((predator, index) => UiText.Get(T.PredatorLabel, details.PredatorNames[index], predator.Count));
                    ImGui.TextWrapped(UiText.Get(T.IntuitionConditions, string.Join(", ", requirements)));
                    if (rule.IntuitionSeconds is { } seconds)
                        ImGui.TextWrapped(UiText.Get(T.IntuitionDuration, seconds));
                }
                if (rule.FolkloreRequired)
                    ImGui.TextWrapped(UiText.Get(T.FolkloreRequired));
                ImGui.Separator();
                if (view.Windows.TryGetValue(selected.FishParameterId, out var window))
                    DrawWindow(window);
                else
                    ImGui.TextWrapped(UiText.Get(T.ForecastUnknown));
                ImGui.TextWrapped(UiText.Get(T.ForecastScope));
            }
        }
        ImGui.Separator();
        if (ImGui.CollapsingHeader(UiText.Label(T.DataSourceLabel, "DataSource")))
        {
            ImGui.TextWrapped(UiText.Get(T.DataSourceInfo, FishingData.Dataset.DataVersion, FishingData.Dataset.SourceRevision[..8]));
            if (ImGui.Button(UiText.Label(T.CopyDataLicense, "CopyDataLicense")))
                ImGui.SetClipboardText(FishingData.Notices);
        }
    }

    private static string EtClock(double hour) => $"{(int)hour:00}:{(int)Math.Round(hour % 1 * 60):00}";
    private static string WeatherNames(string[] names) => names.Length == 0 ? UiText.Get(T.NoWeatherRestriction) : string.Join(" / ", names);

    private static void DrawWindow(FishingWindow window)
    {
        var now = DateTimeOffset.UtcNow;
        string Remaining(DateTimeOffset time)
        {
            var duration = time > now ? time - now : TimeSpan.Zero;
            return UiText.Get(T.DurationShort, Math.Floor(duration.TotalHours), duration.Minutes, duration.Seconds);
        }
        var message = window.Kind switch
        {
            FishingWindowKind.Always => UiText.Get(T.ForecastAlways),
            FishingWindowKind.Open when window.End is { } end => UiText.Get(T.ForecastOpen, Remaining(end)),
            FishingWindowKind.Open => UiText.Get(T.ForecastOpenNoEnd),
            FishingWindowKind.Upcoming when window.Start is { } start => UiText.Get(T.ForecastUpcoming,
                Remaining(start), start.ToLocalTime().ToString("MM-dd HH:mm", UiText.Culture)),
            FishingWindowKind.NotFound => UiText.Get(T.ForecastNotFound),
            _ => UiText.Get(T.ForecastUnknown),
        };
        ImGui.TextWrapped(message);
        if (window.Kind == FishingWindowKind.Upcoming && window.End is { } finish)
            ImGui.TextWrapped(UiText.Get(T.ForecastEnd, finish.ToLocalTime().ToString("MM-dd HH:mm", UiText.Culture)));
    }
}
