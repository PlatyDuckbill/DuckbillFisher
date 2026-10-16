using Dalamud.Bindings.ImGui;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class SpawnAlertsPanel(FishPreferences preferences, SpawnAlerts alerts, SpawnAlertOutput output, FishingMapService maps)
{
    private static readonly SpawnAlertChannel[] Channels = [SpawnAlertChannel.System, SpawnAlertChannel.Say, SpawnAlertChannel.CrossWorldLinkshell1];
    private IReadOnlyList<FavoriteAlertView>? lastFavorites;
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
        var view = alerts.View;
        if (view.Session != settings.Session)
            view = new(settings.Session, new(T.AlertsWaitingCharacter), 0, 0, 0, 0, 0, []);
        var enabled = settings.SpawnAlertsEnabled;
        var minutes = settings.SpawnAlertLeadMinutes;
        var channel = Array.IndexOf(Channels, settings.SpawnAlertChannel);
        var sound = settings.SpawnAlertSound;
        var changed = false;
        ImGui.TextWrapped(UiText.Get(T.AlertsIntro));
        ImGui.BeginDisabled(!settings.CanEdit);
        changed |= ImGui.Checkbox(UiText.Label(T.AlertsEnable, "SpawnAlertsEnabled"), ref enabled);
        ImGui.SetNextItemWidth(180);
        changed |= ImGui.InputInt(UiText.Label(T.AlertsLead, "SpawnAlertLead"), ref minutes);
        ImGui.SetNextItemWidth(180);
        var channelNames = Channels.Select(value => UiText.Get(ChannelKey(value))).ToArray();
        changed |= ImGui.Combo(UiText.Label(T.AlertChannel, "SpawnAlertChannel"), ref channel, channelNames, channelNames.Length);
        ImGui.SetNextItemWidth(180);
        var sounds = Enumerable.Range(0, 17).Select(value => value == 0 ? UiText.Get(T.AlertSoundNone) : UiText.Get(T.AlertSoundNumber, value)).ToArray();
        changed |= ImGui.Combo(UiText.Label(T.AlertSound, "SpawnAlertSound"), ref sound, sounds, sounds.Length);
        if (changed)
            preferences.SetSpawnAlerts(enabled, minutes, Channels[channel], sound);
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
        if (changed || !ReferenceEquals(lastFavorites, view.Favorites))
        {
            lastFavorites = view.Favorites;
            var term = search.Trim();
            matches = view.Favorites.Where(fish => term.Length == 0 || fish.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
                fish.FishId.ToString(System.Globalization.CultureInfo.InvariantCulture) == term).ToArray();
        }
        ImGui.TextWrapped(UiText.Get(T.AlertFavoritesHint));
        var rowHeight = ImGui.GetTextLineHeightWithSpacing() * 3 + ImGui.GetFrameHeightWithSpacing() + 8;
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
                    ImGui.TextUnformatted(UiText.Get(T.AlertFavoriteName, fish.Name ?? UiText.Get(T.MissingName), fish.FishId));
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(fish.Name ?? UiText.Get(T.MissingName));
                    ImGui.TextUnformatted(UiText.Get(StateKey(fish.State)));
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
                    ImGui.EndDisabled();
                    ImGui.TextUnformatted(fish.LeadMinutes == settings.AlertLead(fish.FishId) && fish.Start is { } start
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
            ImGui.PushID(i);
            ImGui.TextWrapped(UiText.Get(T.AlertHistoryLine, entry.SentAt.ToLocalTime(), entry.Fish.Name,
                SpawnAlertOutput.Remaining(entry.Fish.Start - entry.SentAt)));
            ImGui.TextWrapped(UiText.Get(T.AlertHistoryDetails, new UiMessage(ChannelKey(entry.Channel)),
                entry.Sound == 0 ? new UiMessage(T.AlertSoundNone) : new UiMessage(entry.SoundPlayed ? T.AlertSoundNumber : T.AlertSoundSkipped, entry.Sound),
                entry.Fish.Start.ToLocalTime()));
            if (ImGui.Button(UiText.Label(T.OpenFishingMap, "HistoryMap")))
                maps.Request(entry.Fish.Spot);
            ImGui.SameLine();
            ImGui.TextUnformatted(entry.Fish.Spot.Zone);
            ImGui.Separator();
            ImGui.PopID();
        }
    }

    private static T ChannelKey(SpawnAlertChannel channel) => channel switch
    {
        SpawnAlertChannel.Say => T.AlertChannelSay,
        SpawnAlertChannel.CrossWorldLinkshell1 => T.AlertChannelCwls,
        _ => T.AlertChannelSystem,
    };

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
