using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class SpawnAlertsPanel(FishPreferences preferences, SpawnAlerts alerts, SpawnAlertOutput output,
    FishCollectionObserver collection, ITextureProvider textures, FishCatalog catalog, FishDetailPanel detailPanel, FishingMapService maps,
    FishGuideOpenService guideOpener, ManagedSoundLibrary soundLibrary) : IDisposable
{
    private readonly FileDialogManager soundFiles = new();
    private int soundDialogSession = -1;
    private bool disposed;
    private readonly FishRowRenderer rows = new(textures, maps);
    private uint? selectedId;
    private int lastSession = -1;
    private IReadOnlyList<FavoriteAlertView>? lastFavorites;
    private int lastFavoritesRevision = -1;
    private FavoriteAlertView[] matches = [];
    private bool hasStateRows;
    private string search = "";
    private FishGuideRow? selectedRow;
    private bool resetDetailScroll;
    public bool DeveloperMode { get; set; }
    public void Draw() => DrawContent();

    private void DrawContent()
    {
        var settings = preferences.View;
        if (soundDialogSession != settings.Session) { soundFiles.Reset(); soundDialogSession = settings.Session; }
        if (lastSession != settings.Session)
        {
            selectedId = null;
            selectedRow = null;
            lastSession = settings.Session;
        }
        var view = alerts.View;
        if (view.Session != settings.Session)
            view = new(settings.Session, new(T.AlertsWaitingCharacter), 0, 0, 0, 0, 0, []);
        if (view.Status.Key != T.AlertsWatching)
            ImGui.TextWrapped(view.Status.Display);
        if (settings.Error is { } error)
            ImGui.TextWrapped(error.Display);
        UiWidgets.GuideBanner(collection.View, settings, guideOpener);
        ImGui.TextWrapped(UiText.Get(T.AlertsCounts, view.Eligible, view.Armed, view.Sent));
        if (view.Next is { } next && ImGui.IsItemHovered())
            ImGui.SetTooltip(UiText.Get(T.AlertsNext, next.Name, next.Start.AddMinutes(-next.LeadMinutes).ToLocalTime(),
                next.Start.ToLocalTime()));
        if (DeveloperMode && ImGui.CollapsingHeader(UiText.Label(T.AlertDiagnosticsHeading, "SpawnAlertDiagnostics")))
        {
            ImGui.TextWrapped(view.Status.Display);
            if (ImGui.Button(UiText.Label(T.AlertsCopy, "CopySpawnAlerts")))
                ImGui.SetClipboardText(alerts.CreateReport());
        }
        ImGui.Separator();
        if (ImGui.BeginTabBar("SpawnAlertViews", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (ImGui.BeginTabItem(UiText.Label(T.AlertFavorites, "AlertFavorites")))
            {
                DrawFavorites(view, settings);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(UiText.Label(T.AlertSettingsHeading, "AlertSettings")))
            {
                if (ImGui.BeginChild("SpawnAlertSettingsContent")) DrawSettings(settings);
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(UiText.Label(T.AlertHistory, "AlertHistory")))
            {
                if (ImGui.BeginChild("AlertHistoryRows")) DrawHistory(view);
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        soundFiles.Draw();
    }

    private void DrawSettings(FishPreferencesView settings)
    {
        var enabled = settings.SpawnAlertsEnabled;
        var minutes = settings.SpawnAlertLeadMinutes;
        var channel = Math.Max(0, Array.IndexOf(ChatChannelChoices.All, settings.SpawnAlertChannel));
        var sound = settings.SpawnAlertSound;
        var uncaughtOnly = settings.SpawnAlertAudience == SpawnAlertAudience.UncaughtOnly;
        var removeOnRegistration = settings.RemoveFavoriteOnRegistration;
        ImGui.BeginDisabled(!settings.CanEdit);
        var changed = false;
        var style = ImGui.GetStyle();
        var background = style.Colors[(int)ImGuiCol.Header];
        background.W = enabled ? 0.32f : 0.12f;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, background);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, style.FramePadding + new System.Numerics.Vector2(2, 3));
        var masterHeight = ImGui.GetFrameHeight() + style.WindowPadding.Y * 2 + style.ChildBorderSize * 2;
        if (ImGui.BeginChild("SpawnAlertsMaster", new System.Numerics.Vector2(0, masterHeight), true,
            ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            changed = ImGui.Checkbox(UiText.Label(T.AlertsEnable, "SpawnAlertsEnabled"), ref enabled);
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        changed |= ImGui.Checkbox(UiText.Label(T.AlertUncaughtOnly, "AlertUncaughtOnly"), ref uncaughtOnly);
        changed |= ImGui.Checkbox(UiText.Label(T.AlertAutoRemove, "AlertAutoRemove"), ref removeOnRegistration);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.Get(T.AlertAutoRemoveHint));
        ImGui.Spacing();
        if (ImGui.BeginTable("AlertOutputSettings", 2, ImGuiTableFlags.SizingFixedFit))
        {
            var labelWidth = new[] { T.AlertsLead, T.AlertChannel, T.AlertSound }
                .Max(key => ImGui.CalcTextSize(UiText.Get(key)).X);
            ImGui.TableSetupColumn("Setting", ImGuiTableColumnFlags.WidthFixed, labelWidth);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(UiText.Get(T.AlertsLead));
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            changed |= ImGui.InputInt("###SpawnAlertLead", ref minutes);
            UiWidgets.Tooltip(UiText.Get(T.AlertsLeadRange, SpawnAlertScheduler.MinLeadMinutes, SpawnAlertScheduler.MaxLeadMinutes));
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(UiText.Get(T.AlertChannel));
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var channelNames = ChatChannelChoices.Names;
            changed |= ImGui.Combo("###SpawnAlertChannel", ref channel, channelNames, channelNames.Length);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(UiText.Get(T.AlertSound));
            ImGui.TableNextColumn();
            var previewWidth = ImGui.CalcTextSize(UiText.Get(T.AlertSoundPreview)).X + ImGui.GetStyle().FramePadding.X * 2;
            ImGui.SetNextItemWidth(Math.Max(1, ImGui.GetContentRegionAvail().X - previewWidth - ImGui.GetStyle().ItemSpacing.X));
            var sounds = Enumerable.Range(0, CustomAlertSound.Choice + 1).Select(value => value == 0 ? UiText.Get(T.AlertSoundNone)
                : value == CustomAlertSound.Choice ? UiText.Get(T.CustomSound) : UiText.Get(T.AlertSoundNumber, value)).ToArray();
            changed |= ImGui.Combo("###SpawnAlertSound", ref sound, sounds, sounds.Length);
            ImGui.SameLine();
            ImGui.BeginDisabled(sound == 0 || !output.SoundAvailable || !settings.HasCharacter || soundLibrary.Busy(SoundSlot.SpawnAlert));
            if (ImGui.Button(UiText.Label(T.AlertSoundPreview, "PreviewAlertSound"))) output.RequestPreview(sound, settings.Session);
            ImGui.EndDisabled();
            ImGui.EndTable();
        }
        if (sound == CustomAlertSound.Choice)
        {
            if (settings.CustomAlertSoundPath.Length > 0) ImGui.TextWrapped(settings.CustomAlertSoundPath);
            ImGui.BeginDisabled(soundLibrary.Busy(SoundSlot.SpawnAlert));
            if (ImGui.Button(UiText.Label(T.CustomSoundChoose, "CustomSoundChoose")))
            {
                var expectedSession = settings.Session;
                soundFiles.OpenFileDialog(UiText.Get(T.CustomSoundChoose), ".wav,.WAV", (bool ok, string path) =>
                {
                    if (!disposed && ok && preferences.View.Session == expectedSession) soundLibrary.ImportFile(SoundSlot.SpawnAlert, path, expectedSession);
                });
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(soundLibrary.Busy(SoundSlot.SpawnAlert));
            if (ImGui.Button(UiText.Label(T.CustomSoundReload, "CustomSoundReload"))) output.ReloadCustomSound();
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button(UiText.Label(T.CustomSoundStop, "CustomSoundStop"))) output.StopCustomSound();
            if (soundLibrary.Message(SoundSlot.SpawnAlert) is { } importMessage) ImGui.TextWrapped(importMessage.Display);
            ImGui.TextWrapped(UiText.Get(T.ManagedSoundHint));
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(UiText.Get(T.CustomSoundHint, CustomAlertSound.MaxSeconds, CustomAlertSound.MaxMegabytes));
            ImGui.PopStyleColor();
            var status = output.CustomSound.State;
            ImGui.TextWrapped(UiText.Get(status switch
            {
                CustomSoundState.Ready => T.CustomSoundReady,
                CustomSoundState.Loading => T.CustomSoundLoading,
                CustomSoundState.NoFile => T.CustomSoundNoFile,
                CustomSoundState.FileUnavailable => T.CustomSoundFileUnavailable,
                CustomSoundState.InvalidFile => T.CustomSoundInvalid,
                CustomSoundState.LimitExceeded => T.CustomSoundLimit,
                CustomSoundState.AudioUnavailable => T.CustomSoundUnavailable,
                _ => T.CustomSoundLoginRequired,
            }));
        }
        ImGui.EndDisabled();
        if (output.PreviewResult is { } preview)
            ImGui.TextWrapped(preview.Display);
        if (sound is > 0 and < CustomAlertSound.Choice && !output.SoundAvailable)
            ImGui.TextWrapped(UiText.Get(T.AlertSoundUnavailable));
        if (changed)
            preferences.SetSpawnAlerts(enabled, minutes, ChatChannelChoices.All[channel], sound,
                uncaughtOnly ? SpawnAlertAudience.UncaughtOnly : SpawnAlertAudience.AllFavorites, removeOnRegistration);

        if (ImGui.CollapsingHeader(UiText.Label(T.AlertsRulesHeading, "SpawnAlertRules")))
        {
            ImGui.TextWrapped(UiText.Get(T.AlertsIntro));
            ImGui.TextWrapped(UiText.Get(T.AlertsPolicy));
        }
    }

    private void DrawFavorites(SpawnAlertsView view, FishPreferencesView settings)
    {
        ImGui.SetNextItemWidth(-1);
        var changed = ImGui.InputTextWithHint("###AlertFavoriteSearch", UiText.Get(T.SearchPlaceholder), ref search, 100);
        if (changed || !ReferenceEquals(lastFavorites, view.Favorites) || lastFavoritesRevision != settings.Revision)
        {
            lastFavorites = view.Favorites;
            lastFavoritesRevision = settings.Revision;
            var term = search.Trim();
            matches = view.Favorites.Where(fish => settings.Favorites.Contains(fish.FishId) &&
                (term.Length == 0 || fish.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
                fish.Row?.Ocean?.Area.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
                fish.Row?.Spots.Any(spot => spot.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    spot.Zone.Contains(term, StringComparison.OrdinalIgnoreCase)) == true ||
                fish.FishId.ToString(System.Globalization.CultureInfo.InvariantCulture) == term)).ToArray();
            hasStateRows = matches.Any(fish => fish.State != FavoriteAlertState.Armed);
            if (selectedId is { } selected && !settings.Favorites.Contains(selected))
                selectedId = null;
            selectedRow = selectedId is { } id ? view.Favorites.FirstOrDefault(fish => fish.FishId == id)?.Row : null;
        }
        ImGui.TextDisabled(UiText.Get(T.AlertFavoritesCount, matches.Length));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(UiText.Get(T.AlertFavoritesHint));
        if (!ImGui.BeginTable("FavoritesLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("List", ImGuiTableColumnFlags.WidthStretch, 1.15f);
        ImGui.TableSetupColumn("Detail", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableNextColumn();
        if (ImGui.BeginChild("FavoriteRows")) DrawFavoriteRows(settings);
        ImGui.EndChild();
        ImGui.TableNextColumn();
        if (ImGui.BeginChild("FavoriteDetail"))
        {
            if (resetDetailScroll) { ImGui.SetScrollY(0); resetDetailScroll = false; }
            detailPanel.Draw(selectedRow, catalog.View, settings, collection.View);
        }
        ImGui.EndChild();
        ImGui.EndTable();
    }

    private void DrawFavoriteRows(FishPreferencesView settings)
    {
        if (matches.Length == 0) ImGui.TextWrapped(UiText.Get(T.FavoritesEmpty));
        var baseHeight = FishRowRenderer.GetHeight(ImGui.GetContentRegionAvail().X);
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 20;
        var rowHeight = baseHeight + ImGui.GetFrameHeightWithSpacing() * (narrow ? 2 : 1) +
            (hasStateRows ? ImGui.GetTextLineHeightWithSpacing() : 0) + 8;
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
                        var state = UiText.Get(FishRowRenderer.CollectionKey(settings, observed, fish.FishId));
                        var place = row.Ocean is { } ocean ? UiText.Get(T.OceanArea, ocean.Area) : row.Spots.FirstOrDefault()?.Name
                            ?? UiText.Get(row.IsDisplayable ? T.CatalogRowInfo : T.CatalogUnresolvedRow, row.FishParameterId);
                        if (rows.Draw(row, baseHeight, selectedId == fish.FishId, state, place, fish.Window, now,
                            spot: row.Spots.FirstOrDefault(), canOpenMap: settings.HasCharacter))
                        {
                            resetDetailScroll |= selectedId != fish.FishId;
                            selectedId = fish.FishId;
                            selectedRow = row;
                        }
                    }
                    else
                        ImGui.TextUnformatted(UiText.Get(T.AlertFavoriteName, UiText.Get(T.MissingName), fish.FishId));
                    ImGui.SetCursorPosY(rowY + baseHeight + 4);
                    var custom = settings.FishAlertLeadMinutes.ContainsKey(fish.FishId);
                    var lead = settings.AlertLead(fish.FishId);
                    ImGui.BeginDisabled(!settings.CanEdit);
                    if (ImGui.Checkbox(UiText.Label(T.AlertCustomLead, "CustomLead"), ref custom))
                        preferences.SetFishAlertLead(fish.FishId, custom ? lead : null);
                    if (!narrow) ImGui.SameLine();
                    ImGui.BeginDisabled(!custom);
                    ImGui.SetNextItemWidth(ImGui.GetFontSize() * 5.5f);
                    if (ImGui.InputInt(UiText.Label(T.AlertFishMinutes, "FishLead"), ref lead))
                        preferences.SetFishAlertLead(fish.FishId, lead);
                    ImGui.EndDisabled();
                    ImGui.SameLine();
                    if (UiWidgets.FavoriteButton("RemoveAlertFavorite", true))
                        preferences.SetFavorite(fish.FishId, false);
                    ImGui.EndDisabled();
                    if (fish.State != FavoriteAlertState.Armed)
                    {
                        ImGui.TextDisabled(UiText.Get(StateKey(fish.State)));
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip(fish.LeadMinutes == settings.AlertLead(fish.FishId) && fish.Start is { } start
                            ? UiText.Get(T.AlertFavoriteNext, start.AddMinutes(-fish.LeadMinutes).ToLocalTime(), start.ToLocalTime())
                            : UiText.Get(T.AlertNoReservation));
                    }
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
                entry.Sound == CustomAlertSound.Choice ? new UiMessage(entry.SoundPlayed ? T.CustomSoundQueued : T.CustomSoundSkipped)
                : entry.Sound == 0 ? new UiMessage(T.AlertSoundNone) : new UiMessage(entry.SoundPlayed ? T.AlertSoundNumber : T.AlertSoundSkipped, entry.Sound));
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

    public void Dispose() { disposed = true; soundFiles.Reset(); }
}
