using Dalamud.Bindings.ImGui;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class SpawnAlertsPanel(FishPreferences preferences, SpawnAlerts alerts)
{
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
        ImGui.TextWrapped(UiText.Get(T.AlertsIntro));
        ImGui.BeginDisabled(!settings.CanEdit);
        if (ImGui.Checkbox(UiText.Label(T.AlertsEnable, "SpawnAlertsEnabled"), ref enabled))
            preferences.SetSpawnAlerts(enabled, minutes);
        ImGui.SetNextItemWidth(180);
        if (ImGui.InputInt(UiText.Label(T.AlertsLead, "SpawnAlertLead"), ref minutes))
            preferences.SetSpawnAlerts(enabled, minutes);
        ImGui.EndDisabled();
        ImGui.TextWrapped(UiText.Get(T.AlertsLeadRange, SpawnAlertScheduler.MinLeadMinutes, SpawnAlertScheduler.MaxLeadMinutes));
        if (ImGui.CollapsingHeader(UiText.Label(T.AlertsRulesHeading, "SpawnAlertRules")))
            ImGui.TextWrapped(UiText.Get(T.AlertsPolicy));
        ImGui.Separator();
        ImGui.TextWrapped(view.Status.Display);
        if (settings.Error is { } error)
            ImGui.TextWrapped(error.Display);
        ImGui.TextWrapped(UiText.Get(T.AlertsCounts, view.Eligible, view.Armed, view.Sent));
        if (view.Next is { } next)
            ImGui.TextWrapped(UiText.Get(T.AlertsNext, next.Name, next.Start.AddMinutes(-settings.SpawnAlertLeadMinutes).ToLocalTime(),
                next.Start.ToLocalTime()));
        if (ImGui.Button(UiText.Label(T.AlertsCopy, "CopySpawnAlerts")))
            ImGui.SetClipboardText(alerts.CreateReport());
        ImGui.Separator();
        ImGui.TextUnformatted(UiText.Get(T.AlertsLastBatch));
        foreach (var fish in view.LastBatch)
            ImGui.TextWrapped(UiText.Get(T.AlertEntry, fish.Name, fish.Place, fish.Start.ToLocalTime()));
    }
}
