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
    public void Draw(FishGuideRow? fish, FishCatalogView catalog, FishPreferencesView personal, FishCollectionView observed)
    {
        if (fish is null)
        {
            ImGui.TextWrapped(UiText.Get(T.CatalogDetailPrompt));
            return;
        }
        ImGui.PushID((int)fish.FishParameterId);
        if (ImGui.BeginTable("DetailTitle", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 3);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextColumn();
            UiWidgets.GameIcon(textures, fish.IconId, ImGui.GetFontSize() * 3, fish.DisplayName);
            ImGui.TableNextColumn();
            ImGui.TextWrapped(fish.DisplayName);
            if (fish.IsOceanFishing) ImGui.TextDisabled(UiText.Get(T.OceanLabel));
            else if (fish.Grade is FishGrade.Big or FishGrade.Legendary)
                ImGui.TextDisabled(UiText.Get(fish.Grade == FishGrade.Legendary ? T.GradeLegendaryLabel : T.GradeBigLabel));
            ImGui.EndTable();
        }
        DrawActions(fish, personal, observed);
        if (fish.Details is { } headerDetails)
        {
            if (headerDetails.FishEyes is { } eyes)
            {
                UiWidgets.GameIcon(textures, eyes.IconId, ImGui.GetTextLineHeightWithSpacing() * 1.4f,
                    UiText.Get(headerDetails.Rule.FishEyesSupported ? T.FishEyesAvailable : T.FishEyesUnavailable, eyes.Name));
                ImGui.SameLine();
            }
            ImGui.TextWrapped(UiText.Get(headerDetails.Rule.FishEyesSupported ? T.FishEyesAvailable : T.FishEyesUnavailable,
                headerDetails.FishEyes?.Name ?? UiText.Get(T.ConditionUnknown)));
        }
        if (!personal.HasCharacter) ImGui.TextWrapped(UiText.Get(T.ManualPlayerRequired));
        if (personal.Error is { } error) ImGui.TextWrapped(error.Display);

        UiWidgets.Heading(T.DetailAvailability);
        if (fish.IsOceanFishing) DrawOcean(fish.Ocean);
        else
        {
            catalog.Windows.TryGetValue(fish.FishParameterId, out var window);
            FishingWindowPanel.Draw(window, DateTimeOffset.UtcNow);
        }

        if (fish.Details is { Predators.Length: > 0 } intuitionDetails) conditions.DrawIntuition(intuitionDetails, personal, catalog.RelatedDetails);

        UiWidgets.Heading(T.DetailLocations);
        foreach (var spot in fish.Spots)
        {
            if (!ImGui.BeginTable($"Spot{spot.Id}", 2, ImGuiTableFlags.SizingFixedFit)) continue;
            ImGui.TableSetupColumn("Map", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
            ImGui.TableSetupColumn("Place", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextColumn();
            ImGui.BeginDisabled(!personal.HasCharacter);
            if (UiWidgets.IconButton(FontAwesomeIcon.MapMarkerAlt, "OpenMap", UiText.Get(T.OpenFishingMap))) maps.Request(spot);
            ImGui.EndDisabled();
            ImGui.TableNextColumn();
            ImGui.TextWrapped(UiText.Get(T.SpotLine, spot.Zone, spot.Name, spot.X, spot.Y));
            ImGui.EndTable();
        }
        if (fish.Spots.Count == 0 && !fish.IsOceanFishing) ImGui.TextWrapped(UiText.Get(T.DetailLocationUnknown));
        if (maps.Result is { } mapResult && mapResult.Key != T.MapOpened) ImGui.TextWrapped(mapResult.Display);
        if (!fish.IsOceanFishing)
        {
            UiWidgets.Heading(T.DetailConditions);
            if (fish.Details is { } details) conditions.Draw(details, personal, catalog.RelatedDetails);
            else ImGui.TextWrapped(UiText.Get(T.CatalogDetailsMissing));
        }
        ImGui.PopID();
    }

    private void DrawActions(FishGuideRow fish, FishPreferencesView personal, FishCollectionView observed)
    {
        ImGui.BeginDisabled(!personal.CanEdit || !fish.IsDisplayable);
        var favorite = personal.Favorites.Contains(fish.FishParameterId);
        if (UiWidgets.FavoriteButton("Favorite", favorite))
            preferences.SetFavorite(fish.FishParameterId, !favorite);
        ImGui.EndDisabled();
        ImGui.SameLine();
        var manual = personal.ManualCaught.ContainsKey(fish.FishParameterId);
        ImGui.BeginDisabled(!personal.CanEdit || !personal.HasCharacter || !fish.IsDisplayable);
        ImGui.SetNextItemWidth(Math.Max(ImGui.GetFontSize() * 5,
            ImGui.GetContentRegionAvail().X - (manual ? ImGui.GetFrameHeightWithSpacing() : 0)));
        if (ImGui.BeginCombo("###CollectionState", UiText.Get(FishRowRenderer.CollectionKey(personal, observed, fish.FishParameterId))))
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

    public static void DrawSources()
    {
        ImGui.TextWrapped(UiText.Get(T.DataSourceInfo, FishingData.Dataset.DataVersion, FishingData.Dataset.SourceRevision[..8]));
        ImGui.TextWrapped(UiText.Get(T.SourceCoverageInfo, FishingData.Dataset.Fish.Length, FishingData.Dataset.Spearfishing.Length, FishingData.SpearUnknown));
        if (ImGui.Button(UiText.Label(T.CopyDataLicense, "CopyDataLicense"))) ImGui.SetClipboardText(FishingData.Notices);
    }
}
