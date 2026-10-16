using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class SpawnAlertsPanel(FishPreferences preferences, SpawnAlerts alerts, SpawnAlertOutput output,
    FishCollectionObserver collection, ITextureProvider textures)
{
    private readonly FishRowRenderer rows = new(textures);
    private uint? selectedId;
    private int lastSession = -1;
    private IReadOnlyList<FavoriteAlertView>? lastFavorites;
    private int lastFavoritesRevision = -1;
    private FavoriteAlertView[] matches = [];
    private string search = "";
    public void Draw()
    {
        if (ImGui.BeginChild("SpawnAlertsContent"))
            DrawContent();
        ImGui.EndChild();
    }

    private void DrawContent()
    {
        var settings = preferences.View;
        if (lastSession != settings.Session)
        {
            selectedId = null;
            lastSession = settings.Session;
        }
        var view = alerts.View;
        if (view.Session != settings.Session)
            view = new(settings.Session, new(T.AlertsWaitingCharacter), 0, 0, 0, 0, 0, []);
        var enabled = settings.SpawnAlertsEnabled;
        var minutes = settings.SpawnAlertLeadMinutes;
        var channel = Array.IndexOf(ChatChannelChoices.All, settings.SpawnAlertChannel);
        var sound = settings.SpawnAlertSound;
        var uncaughtOnly = settings.SpawnAlertAudience == SpawnAlertAudience.UncaughtOnly;
        var removeOnRegistration = settings.RemoveFavoriteOnRegistration;
        var changed = false;
        ImGui.TextWrapped(UiText.Get(T.AlertsIntro));
        ImGui.BeginDisabled(!settings.CanEdit);
        changed |= ImGui.Checkbox(UiText.Label(T.AlertsEnable, "SpawnAlertsEnabled"), ref enabled);
        changed |= ImGui.Checkbox(UiText.Label(T.AlertUncaughtOnly, "AlertUncaughtOnly"), ref uncaughtOnly);
        changed |= ImGui.Checkbox(UiText.Label(T.AlertAutoRemove, "AlertAutoRemove"), ref removeOnRegistration);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.Get(T.AlertAutoRemoveHint));
        ImGui.SetNextItemWidth(180);
        changed |= ImGui.InputInt(UiText.Label(T.AlertsLead, "SpawnAlertLead"), ref minutes);
        ImGui.SetNextItemWidth(180);
        var channelNames = ChatChannelChoices.Names;
        changed |= ImGui.Combo(UiText.Label(T.AlertChannel, "SpawnAlertChannel"), ref channel, channelNames, channelNames.Length);
        ImGui.SetNextItemWidth(180);
        var sounds = Enumerable.Range(0, 17).Select(value => value == 0 ? UiText.Get(T.AlertSoundNone) : UiText.Get(T.AlertSoundNumber, value)).ToArray();
        changed |= ImGui.Combo(UiText.Label(T.AlertSound, "SpawnAlertSound"), ref sound, sounds, sounds.Length);
        if (changed)
            preferences.SetSpawnAlerts(enabled, minutes, ChatChannelChoices.All[channel], sound,
                uncaughtOnly ? SpawnAlertAudience.UncaughtOnly : SpawnAlertAudience.AllFavorites, removeOnRegistration);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(sound == 0 || !output.SoundAvailable || !settings.HasCharacter);
        if (ImGui.Button(UiText.Label(T.AlertSoundPreview, "PreviewAlertSound")))
            output.RequestPreview(sound, settings.Session);
        ImGui.EndDisabled();
        if (output.PreviewResult is { } preview)
            ImGui.TextWrapped(preview.Display);
        if (sound != 0 && !output.SoundAvailable)
            ImGui.TextWrapped(UiText.Get(T.AlertSoundUnavailable));
        ImGui.TextWrapped(UiText.Get(T.AlertsLeadRange, SpawnAlertScheduler.MinLeadMinutes, SpawnAlertScheduler.MaxLeadMinutes));
        if (ImGui.CollapsingHeader(UiText.Label(T.AlertsRulesHeading, "SpawnAlertRules")))
            ImGui.TextWrapped(UiText.Get(T.AlertsPolicy));
        ImGui.Separator();
        ImGui.TextWrapped(view.Status.Display);
        if (settings.Error is { } error)
            ImGui.TextWrapped(error.Display);
        ImGui.TextWrapped(UiText.Get(T.AlertsCounts, view.Eligible, view.Armed, view.Sent));
        if (view.Next is { } next)
            ImGui.TextWrapped(UiText.Get(T.AlertsNext, next.Name, next.Start.AddMinutes(-next.LeadMinutes).ToLocalTime(),
                next.Start.ToLocalTime()));
        if (ImGui.Button(UiText.Label(T.AlertsCopy, "CopySpawnAlerts")))
            ImGui.SetClipboardText(alerts.CreateReport());
        ImGui.Separator();
        if (ImGui.BeginTabBar("SpawnAlertViews"))
        {
            if (ImGui.BeginTabItem(UiText.Label(T.AlertFavorites, "AlertFavorites")))
            {
                DrawFavorites(view, settings);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(UiText.Label(T.AlertHistory, "AlertHistory")))
            {
                DrawHistory(view);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawFavorites(SpawnAlertsView view, FishPreferencesView settings)
    {
        var changed = ImGui.InputText(UiText.Label(T.FishSearch, "AlertFavoriteSearch"), ref search, 100);
        if (changed || !ReferenceEquals(lastFavorites, view.Favorites) || lastFavoritesRevision != settings.Revision)
        {
            lastFavorites = view.Favorites;
            lastFavoritesRevision = settings.Revision;
            var term = search.Trim();
            matches = view.Favorites.Where(fish => settings.Favorites.Contains(fish.FishId) &&
                (term.Length == 0 || fish.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
                fish.FishId.ToString(System.Globalization.CultureInfo.InvariantCulture) == term)).ToArray();
            if (selectedId is { } selected && !settings.Favorites.Contains(selected))
                selectedId = null;
        }
        ImGui.TextWrapped(UiText.Get(T.AlertFavoritesHint));
        var baseHeight = FishRowRenderer.Height;
        var rowHeight = baseHeight + ImGui.GetFrameHeightWithSpacing() + ImGui.GetTextLineHeightWithSpacing() + 8;
        var observed = collection.View;
        var now = DateTimeOffset.UtcNow;
        var clipper = ImGui.ImGuiListClipper();
        try
        {
            clipper.Begin(matches.Length, rowHeight);
            while (clipper.Step())
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var fish = matches[i];
                    ImGui.PushID((int)fish.FishId);
                    var rowY = ImGui.GetCursorPosY();
                    if (fish.Row is { } row)
                    {
                        var state = UiText.Get(T.FavoriteRowState, new UiMessage(FishRowRenderer.CollectionKey(settings, observed, fish.FishId)));
                        var place = row.Ocean is { } ocean ? UiText.Get(T.OceanArea, ocean.Area) : row.Spots.FirstOrDefault()?.Name
                            ?? UiText.Get(row.IsDisplayable ? T.CatalogRowInfo : T.CatalogUnresolvedRow, row.FishParameterId);
                        if (rows.Draw(row, baseHeight, selectedId == fish.FishId, state, place, fish.Window, now))
                            selectedId = fish.FishId;
                    }
                    else
                        ImGui.TextUnformatted(UiText.Get(T.AlertFavoriteName, UiText.Get(T.MissingName), fish.FishId));
                    ImGui.SetCursorPosY(rowY + baseHeight + 4);
                    var custom = settings.FishAlertLeadMinutes.ContainsKey(fish.FishId);
                    var lead = settings.AlertLead(fish.FishId);
                    ImGui.BeginDisabled(!settings.CanEdit);
                    if (ImGui.Checkbox(UiText.Label(T.AlertCustomLead, "CustomLead"), ref custom))
                        preferences.SetFishAlertLead(fish.FishId, custom ? lead : null);
                    ImGui.SameLine();
                    ImGui.BeginDisabled(!custom);
                    ImGui.SetNextItemWidth(110);
                    if (ImGui.InputInt(UiText.Label(T.AlertFishMinutes, "FishLead"), ref lead))
                        preferences.SetFishAlertLead(fish.FishId, lead);
                    ImGui.EndDisabled();
                    ImGui.SameLine();
                    if (ImGui.SmallButton(UiText.Label(T.AlertRemoveFavorite, "RemoveAlertFavorite")))
                        preferences.SetFavorite(fish.FishId, false);
                    ImGui.EndDisabled();
                    ImGui.TextDisabled(UiText.Get(StateKey(fish.State)));
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(fish.LeadMinutes == settings.AlertLead(fish.FishId) && fish.Start is { } start
                        ? UiText.Get(T.AlertFavoriteNext, start.AddMinutes(-fish.LeadMinutes).ToLocalTime(), start.ToLocalTime())
                        : UiText.Get(T.AlertNoReservation));
                    ImGui.Separator();
                    ImGui.SetCursorPosY(rowY + rowHeight);
                    ImGui.PopID();
                }
        }
        finally { clipper.Destroy(); }
    }

    private void DrawHistory(SpawnAlertsView view)
    {
        ImGui.TextWrapped(UiText.Get(T.AlertHistoryHint));
        for (var i = 0; i < view.History.Count; i++)
        {
            var entry = view.History[i];
            var line = UiText.Get(T.AlertHistoryLine, entry.SentAt.ToLocalTime(), entry.Fish.Name,
                SpawnAlertOutput.Remaining(entry.Fish.Start - entry.SentAt), ChatChannelChoices.Name(entry.Channel),
                entry.Sound == 0 ? new UiMessage(T.AlertSoundNone) : new UiMessage(entry.SoundPlayed ? T.AlertSoundNumber : T.AlertSoundSkipped, entry.Sound));
            ImGui.TextUnformatted(line);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(line);
        }
    }

    private static T StateKey(FavoriteAlertState state) => state switch
    {
        FavoriteAlertState.Disabled => T.AlertsDisabled,
        FavoriteAlertState.WaitingCharacter => T.AlertsWaitingCharacter,
        FavoriteAlertState.Caught => T.AlertExcludedCaught,
        FavoriteAlertState.Unknown => T.AlertExcludedUnknown,
        FavoriteAlertState.Unsupported => T.AlertUnsupported,
        FavoriteAlertState.Always => T.AlertAlways,
        FavoriteAlertState.Armed => T.AlertArmed,
        _ => T.AlertPending,
    };
}
