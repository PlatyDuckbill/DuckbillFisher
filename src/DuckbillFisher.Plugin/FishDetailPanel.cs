using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Shared detail body for the main panel and the single independent detail window.</summary>
internal sealed class FishDetailPanel(FishPreferences preferences, ITextureProvider textures,
    FishingMapService maps, FishingConditionPanel conditions)
{
    public Action<uint, uint>? OpenRelated { get; set; }

    public void Draw(FishGuideRow? fish, FishCatalogView catalog, FishPreferencesView personal, FishCollectionView observed,
        Action<FishGuideRow>? showDetail = null)
    {
        if (fish is null)
        {
            ImGui.TextWrapped(UiText.Get(T.CatalogDetailPrompt));
            return;
        }
        ImGui.PushID((int)fish.FishParameterId);
        var patch = fish.Details is { } patchDetails ? PatchText(patchDetails.Rule) : "";
        if (ImGui.BeginTable("DetailTitle", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 3);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextColumn();
            UiWidgets.GameIcon(textures, fish.IconId, ImGui.GetFontSize() * 3, fish.DisplayName);
            ImGui.TableNextColumn();
            if (ImGui.BeginTable("DetailName", patch.Length > 0 ? 2 : 1, ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                if (patch.Length > 0) ImGui.TableSetupColumn("Patch", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize(patch).X);
                ImGui.TableNextColumn();
                ImGui.TextWrapped(fish.DisplayName);
                if (patch.Length > 0)
                {
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(patch).X));
                    ImGui.TextDisabled(patch);
                }
                ImGui.EndTable();
            }
            DrawActions(fish, personal, observed, showDetail);
            ImGui.EndTable();
        }
        if (!personal.HasCharacter) ImGui.TextWrapped(UiText.Get(T.ManualPlayerRequired));
        if (personal.Error is { } error) ImGui.TextWrapped(error.Display);

        UiWidgets.Heading(T.DetailAvailability);
        if (fish.IsOceanFishing) DrawOcean(fish.Ocean);
        else
        {
            var now = DateTimeOffset.UtcNow;
            catalog.Windows.TryGetValue(fish.FishParameterId, out var window);
            var reason = fish.Details is { Rule.ConditionsKnown: false } partial ? partial.ConditionWarning : null;
            catalog.NormalWindows.TryGetValue(fish.FishParameterId, out var normal);
            catalog.FishEyesWindows.TryGetValue(fish.FishParameterId, out var eyes);
            if (FishingWindowPanel.Different(normal, eyes))
            {
                ImGui.PushID("NormalForecast");
                ImGui.TextUnformatted(UiText.Get(T.ForecastNormal));
                FishingWindowPanel.Draw(normal, now, reason);
                ImGui.PopID();
                ImGui.PushID("FishEyesForecast");
                ImGui.TextUnformatted(UiText.Get(T.ForecastWithFishEyes));
                FishingWindowPanel.Draw(eyes, now, reason);
                ImGui.PopID();
            }
            else FishingWindowPanel.Draw(window, now, reason);
            if (fish.Details is { Predators.Length: > 0 })
            {
                if (window?.Issue is not (null or FishingForecastIssue.None))
                    ImGui.TextWrapped(FishingWindowPanel.IntuitionNotice(window));
                FishingWindowPanel.DrawBreakdown(window, fish, textures, now, catalog.FishEyesEnabled);
            }
        }

        void Open(uint itemId) => OpenRelated?.Invoke(fish.FishParameterId, itemId);
        if (fish.Details is { Predators.Length: > 0 } intuitionDetails) conditions.DrawIntuition(intuitionDetails, personal, catalog.RelatedDetails, Open);

        UiWidgets.Heading(T.DetailLocations);
        foreach (var spot in fish.Spots)
        {
            if (!ImGui.BeginTable($"Spot{spot.Id}", 2, ImGuiTableFlags.SizingFixedFit)) continue;
            ImGui.TableSetupColumn("Map", ImGuiTableColumnFlags.WidthFixed, UiWidgets.MapPinWidth(ImGui.GetFrameHeight()));
            ImGui.TableSetupColumn("Place", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextColumn();
            ImGui.BeginDisabled(!personal.HasCharacter);
            if (UiWidgets.MapPinButton("OpenMap", UiText.Get(personal.HasCharacter ? T.OpenFishingMap : T.MapPlayerRequired), ImGui.GetFrameHeight())) maps.Request(spot);
            ImGui.EndDisabled();
            ImGui.TableNextColumn();
            ImGui.TextWrapped(UiText.Get(T.SpotLine, spot.Zone, spot.Name, spot.X, spot.Y));
            ImGui.EndTable();
        }
        if (fish.Spots.Count == 0 && !fish.IsOceanFishing) ImGui.TextWrapped(UiText.Get(T.DetailLocationUnknown));
        if (maps.Result is { } mapResult && mapResult.Key != T.MapOpened) ImGui.TextWrapped(mapResult.Display);
        if (!fish.IsOceanFishing)
        {
            if (fish.Details is { } details)
            {
                conditions.DrawHeading(details, personal, catalog.RelatedDetails);
                conditions.Draw(details, personal, catalog.RelatedDetails, Open);
            }
            else
            {
                UiWidgets.Heading(T.DetailConditions);
                ImGui.TextWrapped(UiText.Get(T.CatalogDetailsMissing));
            }
        }
        ImGui.PopID();
    }

    private void DrawActions(FishGuideRow fish, FishPreferencesView personal, FishCollectionView observed, Action<FishGuideRow>? showDetail)
    {
        var grade = fish.IsOceanFishing ? UiText.Get(T.OceanLabel)
            : fish.Grade is FishGrade.Big or FishGrade.Legendary
                ? UiText.Get(fish.Grade == FishGrade.Legendary ? T.GradeLegendaryLabel : T.GradeBigLabel) : "";
        var openLabel = UiText.Get(T.OpenIndependentDetail);
        var openWidth = ImGui.CalcTextSize(openLabel).X + ImGui.GetStyle().FramePadding.X * 2;
        var state = UiText.Get(FishRowRenderer.CollectionKey(personal, observed, fish.FishParameterId));
        var iconOnly = ImGui.GetContentRegionAvail().X < openWidth + ImGui.CalcTextSize(grade).X +
            ImGui.CalcTextSize(state).X + ImGui.GetFrameHeight() * 3 + ImGui.GetStyle().ItemSpacing.X * 4;
        if (!ImGui.BeginTable("DetailActions", (grade.Length > 0 ? 3 : 2) + (showDetail is null ? 0 : 1), ImGuiTableFlags.SizingFixedFit)) return;
        if (grade.Length > 0) ImGui.TableSetupColumn("Grade", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize(grade).X + 4);
        ImGui.TableSetupColumn("Favorite", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("Collection", ImGuiTableColumnFlags.WidthStretch);
        if (showDetail is not null) ImGui.TableSetupColumn("SeparateDetail", ImGuiTableColumnFlags.WidthFixed, iconOnly ? ImGui.GetFrameHeight() : openWidth);
        if (grade.Length > 0)
        {
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(grade);
        }
        ImGui.TableNextColumn();
        ImGui.BeginDisabled(!personal.CanEdit || !fish.IsDisplayable);
        var favorite = personal.Favorites.Contains(fish.FishParameterId);
        if (UiWidgets.FavoriteButton("Favorite", favorite)) preferences.SetFavorite(fish.FishParameterId, !favorite);
        ImGui.EndDisabled();
        ImGui.TableNextColumn();
        var manual = personal.ManualCaught.ContainsKey(fish.FishParameterId);
        ImGui.BeginDisabled(!personal.CanEdit || !personal.HasCharacter || !fish.IsDisplayable);
        var desiredWidth = ImGui.CalcTextSize(state).X + ImGui.GetFrameHeight() + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SetNextItemWidth(Math.Max(1, Math.Min(desiredWidth,
            ImGui.GetContentRegionAvail().X - (manual ? ImGui.GetFrameHeightWithSpacing() : 0))));
        if (ImGui.BeginCombo("###CollectionState", state))
        {
            if (ImGui.Selectable(UiText.Label(T.MarkCaught, "Caught"))) preferences.SetManualCaught(fish.FishParameterId, true, personal.Session);
            if (ImGui.Selectable(UiText.Label(T.MarkUncaught, "Uncaught"))) preferences.SetManualCaught(fish.FishParameterId, false, personal.Session);
            if (manual && ImGui.Selectable(UiText.Label(T.ClearManualCollection, "ClearManual")))
                preferences.SetManualCaught(fish.FishParameterId, null, personal.Session);
            ImGui.EndCombo();
        }
        UiWidgets.Tooltip(UiText.Get(T.CollectionControlHint));
        if (manual)
        {
            ImGui.SameLine();
            if (UiWidgets.IconButton(FontAwesomeIcon.Undo, "ClearManual", UiText.Get(T.ClearManualCollection)))
                preferences.SetManualCaught(fish.FishParameterId, null, personal.Session);
        }
        ImGui.EndDisabled();
        if (showDetail is not null)
        {
            ImGui.TableNextColumn();
            var clicked = iconOnly
                ? UiWidgets.IconButton(FontAwesomeIcon.ExternalLinkAlt, "OpenIndependentDetail", openLabel)
                : ImGui.Button(UiText.Label(T.OpenIndependentDetail, "OpenIndependentDetail"));
            if (clicked) showDetail(fish);
        }
        ImGui.EndTable();
    }
    private static void DrawOcean(OceanFishingView? ocean)
    {
        if (ocean is null) ImGui.TextWrapped(UiText.Get(T.OceanArea, new UiMessage(T.ConditionUnknown)));
        else
        {
            ImGui.TextWrapped(UiText.Get(T.OceanArea, ocean.Area));
            ImGui.TextWrapped(UiText.Get(T.OceanSpot, ocean.FishingSpot));
            if (ocean.RouteNames.Length > 0) ImGui.TextWrapped(UiText.Get(T.OceanRoutes, string.Join(" / ", ocean.RouteNames)));
            var times = ocean.VisitTimes.SequenceEqual(new byte[] { 1, 2, 3 })
                ? string.Join(" / ", new[] { T.OceanTimeDay, T.OceanTimeSunset, T.OceanTimeNight }.Select(key => UiText.Get(key)))
                : UiText.Get(T.ConditionUnknown);
            ImGui.TextWrapped(UiText.Get(T.OceanVisitTimes, times));
        }
        ImGui.TextWrapped(UiText.Get(T.OceanScope));
    }

    private static string PatchText(FishingRule rule)
    {
        var version = rule.PatchVersion.Length > 0 ? rule.PatchVersion : rule.Patch > 0
            ? rule.Patch.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) : "";
        return version.Length > 0 ? UiText.Get(T.PatchLabel, version) : "";
    }

    public static void DrawSources()
    {
        if (FishingData.Failure is { } failure)
            ImGui.TextWrapped(UiText.Get(T.ConditionDataInvalid, failure.Code, failure.Path));
        else
        {
            ImGui.TextWrapped(UiText.Get(T.DataSourceInfo, FishingData.Dataset.DataVersion, FishingData.Dataset.SourceRevision[..8]));
            foreach (var source in FishingData.Dataset.SupplementalSources)
                ImGui.TextWrapped(UiText.Get(T.DataSupplementSourceInfo, source.Name, source.Revision[..Math.Min(8, source.Revision.Length)]));
            ImGui.TextWrapped(UiText.Get(T.SourceCoverageInfo, FishingData.Dataset.Fish.Length, FishingData.Dataset.Spearfishing.Length, FishingData.SpearUnknown));
        }
        if (ImGui.Button(UiText.Label(T.CopyDataLicense, "CopyDataLicense"))) ImGui.SetClipboardText(FishingData.Notices);
    }
}
