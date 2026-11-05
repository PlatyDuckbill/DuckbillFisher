using System.Diagnostics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class OceanFishingPanel(OceanFishing ocean, FishPreferences preferences,
    BaitStockObserver stock, BaitFinder finder, ITextureProvider textures)
{
    private DateTimeOffset selectedStart;
    private bool selectedRuby;
    private int session = -1;
    private readonly List<FishingItemView> visibleBaits = [];

    public void SetActive(bool value) => ocean.SetActive(value);

    public void Draw()
    {
        if (!ImGui.BeginChild("OceanBetaContent")) { ImGui.EndChild(); return; }
        var personal = preferences.View;
        if (session != personal.Session) { session = personal.Session; selectedStart = default; selectedRuby = false; }
        var view = ocean.View;
        visibleBaits.Clear();
        if (!view.DataReady)
        {
            ImGui.TextWrapped(UiText.Get(view.DataState == OceanDataState.Loading ? T.OceanDataLoading : T.OceanDataUnavailable));
            DrawDiagnostics(personal.DeveloperMode);
            ImGui.EndChild(); return;
        }
        var aboard = view.Sample.State == OceanReadState.Ready && view.Routes.TryGetValue(view.Sample.RouteId, out _);
        if (aboard)
        {
            var route = view.Routes[view.Sample.RouteId];
            DrawVoyage(view, route);
            ImGui.Spacing();
            if (ImGui.CollapsingHeader(UiText.Label(T.OceanSchedulePreview, "OceanSchedulePreview"))) DrawSchedule(view);
        }
        else
        {
            if (view.Sample.State != OceanReadState.OffVoyage && personal.HasCharacter)
            {
                ImGui.TextWrapped(UiText.Get(T.OceanStateUnavailable));
                UiWidgets.Tooltip($"OceanReadState={view.Sample.State}");
                ImGui.Spacing();
            }
            DrawRegistration(view);
            DrawSchedule(view);
        }
        finder.DrawStatus(visibleBaits);
        ImGui.Spacing();
        ImGui.TextDisabled(UiText.Get(T.OceanBetaHint));
        DrawDiagnostics(personal.DeveloperMode);
        ImGui.EndChild();
    }

    private void DrawDiagnostics(bool developerMode)
    {
        if (developerMode && ImGui.CollapsingHeader(UiText.Label(T.OceanDiagnostics, "OceanDiagnostics")))
        {
            var report = ocean.CreateReport();
            if (ImGui.SmallButton(UiText.Label(T.OceanCopyDiagnostics, "CopyOceanDiagnostics"))) ImGui.SetClipboardText(report);
            ImGui.TextUnformatted(report);
        }
    }

    private static string Time(byte time) => UiText.Get(time switch
        { 1 => T.OceanTimeNight, 2 => T.OceanTimeDay, 3 => T.OceanTimeSunset, _ => T.ConditionUnknown });
    private static string RouteName(OceanDeparture departure) => UiText.Get(T.OceanDepartureName,
        UiText.Get(departure.Ruby ? T.OceanRuby : T.OceanIndigo), departure.Route.Name);
    private static string Countdown(double seconds)
    {
        var total = (int)Math.Clamp(Math.Ceiling(seconds), 0, int.MaxValue);
        return total >= 3600 ? $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}" : $"{total / 60:00}:{total % 60:00}";
    }

    private static void Heading(string state, string title, string timeLabel, string value)
    {
        if (!ImGui.BeginTable("OceanHeading", 2, ImGuiTableFlags.SizingStretchProp)) return;
        ImGui.TableSetupColumn("Route", ImGuiTableColumnFlags.WidthStretch, 2);
        ImGui.TableSetupColumn("Clock", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableNextColumn();
        ImGui.TextDisabled(state);
        ImGui.TextWrapped(title);
        ImGui.TableNextColumn();
        ImGui.TextWrapped(timeLabel);
        ImGui.TextUnformatted(value);
        ImGui.EndTable();
    }

    private static void DrawRoute(OceanRoute route, int current = -1)
    {
        ImGui.Spacing();
        if (!ImGui.BeginTable("OceanRoute", 3, ImGuiTableFlags.SizingStretchSame)) return;
        for (var i = 0; i < route.Stops.Length; i++)
        {
            ImGui.TableNextColumn();
            if (current < 0) ImGui.TextDisabled(UiText.Get(T.OceanStopOrdinal, i + 1));
            else ImGui.TextDisabled(UiText.Get(i < current ? T.OceanPassed : i == current ? T.OceanCurrent : T.OceanNext));
            var stop = route.Stops[i];
            ImGui.TextWrapped(UiText.Get(T.OceanStopName, stop.Name, Time(stop.Time)));
            ImGui.Separator();
        }
        ImGui.EndTable();
    }

    private void DrawVoyage(OceanVoyageView view, OceanRoute route)
    {
        var sample = view.Sample;
        var fishing = sample.Phase == OceanPhase.Fishing;
        var status = sample.Phase switch
        {
            OceanPhase.Fishing => sample.Spectral ? T.OceanSpectral : T.OceanNormal,
            OceanPhase.Waiting => T.OceanWaiting,
            OceanPhase.Finished => T.OceanFinished,
            _ => T.OceanMoving,
        };
        var seconds = sample.RemainingSeconds.HasValue
            ? Math.Max(0, sample.RemainingSeconds.Value - Stopwatch.GetElapsedTime(view.SampledAt).TotalSeconds) : (double?)null;
        Heading(UiText.Get(T.OceanAboardState, UiText.Get(status)), route.Name,
            UiText.Get(fishing ? T.OceanZoneRemaining : status), seconds.HasValue ? Countdown(seconds.Value)
                : fishing ? UiText.Get(T.ConditionUnknown) : "—");
        DrawRoute(route, fishing ? sample.Zone : -1);
        if (!fishing) return; // Transition zone indices are not interpreted as the next fishing stop.
        ImGui.Spacing();
        ImGui.TextUnformatted(UiText.Get(T.OceanBasicGuide));
        var stop = route.Stops[sample.Zone];
        view.Guidance.TryGetValue((stop.Id, 0), out var normal);
        view.Guidance.TryGetValue((stop.Id, stop.Time), out var spectral);
        DrawBait(normal?.Bait, sample.Spectral ? T.OceanNormal : T.OceanRecommended, !sample.Spectral, "NormalBait");
        DrawBait(spectral?.Bait, sample.Spectral ? T.OceanRecommended : T.OceanSpectralBait, sample.Spectral, "SpectralBait");
        var target = sample.Spectral ? spectral : normal;
        if (target is not null)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(UiText.Get(sample.Spectral ? T.OceanScoreTarget : T.OceanTriggerTarget,
                target.Fish.Name, target.Tug, target.Double, target.Triple));
            UiWidgets.Tooltip(UiText.Get(T.OceanTargetHint));
        }
    }

    private static void DrawRegistration(OceanVoyageView view)
    {
        if (view.Departures.Length == 0) return;
        var start = view.Departures[0].Starts;
        var now = DateTimeOffset.UtcNow;
        var open = now >= start && now < start + OceanSchedule.RegistrationDuration;
        Heading(UiText.Get(open ? T.OceanRegistrationOpen : T.OceanSchedule),
            UiText.Get(open ? T.OceanCurrentRegistration : T.OceanNextRegistration, start.ToLocalTime().ToString("HH:mm")),
            UiText.Get(open ? T.OceanRegistrationEnds : T.OceanRegistrationStarts),
            Countdown(((open ? start + OceanSchedule.RegistrationDuration : start) - now).TotalSeconds));
    }

    private void DrawSchedule(OceanVoyageView view)
    {
        ImGui.Spacing();
        var selected = view.Departures.FirstOrDefault(d => d.Starts == selectedStart && d.Ruby == selectedRuby)
            ?? view.Departures.FirstOrDefault();
        if (selected is null) return;
        // Keep the preview's own stable departure identity, independent from observed voyage state.
        selectedStart = selected.Starts; selectedRuby = selected.Ruby;
        if (ImGui.BeginTable("OceanDepartures", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn(UiText.Label(T.OceanRegistrationLocal, "RegistrationTime"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 9);
            ImGui.TableSetupColumn(UiText.Label(T.OceanDepartureColumn, "DepartureRoute"), ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn(UiText.Label(T.OceanRegistrationStatus, "RegistrationStatus"), ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 6);
            ImGui.TableHeadersRow();
            for (var i = 0; i < view.Departures.Length; i++)
            {
                var departure = view.Departures[i];
                ImGui.PushID(i);
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                var label = departure.Starts.ToLocalTime().ToString("MM/dd HH:mm");
                if (ImGui.Selectable($"{label}###Departure", departure.Starts == selectedStart && departure.Ruby == selectedRuby,
                    ImGuiSelectableFlags.SpanAllColumns))
                { selected = departure; selectedStart = departure.Starts; selectedRuby = departure.Ruby; }
                ImGui.TableNextColumn(); ImGui.TextWrapped(RouteName(departure));
                ImGui.TableNextColumn();
                var now = DateTimeOffset.UtcNow;
                ImGui.TextUnformatted(now >= departure.Starts && now < departure.Starts + OceanSchedule.RegistrationDuration
                    ? UiText.Get(T.OceanRegistrationOpen) : Countdown((departure.Starts - now).TotalSeconds));
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        ImGui.TextDisabled(UiText.Get(T.OceanPreview));
        ImGui.TextWrapped(RouteName(selected));
        DrawRoute(selected.Route);
        ImGui.Spacing();
        ImGui.TextUnformatted(UiText.Get(T.OceanPreparationBaits));
        var baits = selected.Route.Stops.SelectMany(s => new[]
        {
            view.Guidance.GetValueOrDefault((s.Id, (byte)0))?.Bait,
            view.Guidance.GetValueOrDefault((s.Id, s.Time))?.Bait,
        }).Where(b => b is not null).Cast<FishingItemView>().DistinctBy(b => b.Id).ToArray();
        foreach (var bait in baits) DrawBait(bait, T.OceanBasicBait, false, $"Prep{bait.Id}");
        if (baits.Length == 0) ImGui.TextWrapped(UiText.Get(T.OceanGuidanceMissing));
    }

    private void DrawBait(FishingItemView? bait, T label, bool active, string id)
    {
        ImGui.PushID(id);
        if (ImGui.BeginTable("OceanBaitRow", 4, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 8);
            ImGui.TableSetupColumn("Bait", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Stock", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 7);
            ImGui.TableSetupColumn("Find", ImGuiTableColumnFlags.WidthFixed,
                ImGui.CalcTextSize(UiText.Get(T.BaitFind)).X + ImGui.GetStyle().FramePadding.X * 2);
            ImGui.TableNextRow();
            if (active) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.Header));
            ImGui.TableNextColumn(); ImGui.TextWrapped(UiText.Get(label));
            ImGui.TableNextColumn();
            if (bait is null) ImGui.TextWrapped(UiText.Get(T.OceanGuidanceMissing));
            else
            {
                visibleBaits.Add(bait);
                UiWidgets.GameIcon(textures, bait.IconId, ImGui.GetTextLineHeight(), bait.Name);
                ImGui.SameLine(); ImGui.TextWrapped(bait.Name);
                ImGui.TableNextColumn();
                var inventory = stock.View;
                var quantity = inventory.Session == preferences.View.Session ? inventory.Quantity(bait.Id) : null;
                ImGui.TextWrapped(quantity.HasValue ? UiText.Get(T.OceanBaitStock, quantity.Value) : UiText.Get(T.ConditionUnknown));
                ImGui.TableNextColumn(); finder.DrawButton(bait, preferences.View.Session);
            }
            ImGui.EndTable();
        }
        ImGui.PopID();
    }
}
