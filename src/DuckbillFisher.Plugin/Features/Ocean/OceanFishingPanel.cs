using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

internal sealed class OceanFishingPanel(OceanFishing ocean, FishPreferences preferences,
    BaitStockObserver stock, BaitFinder finder, ITextureProvider textures, Action toggleSpotFish)
{
    private DateTimeOffset selectedStart;
    private bool selectedRuby;
    private int session = -1;
    private readonly List<FishingItemView> visibleBaits = [];

    public void SetActive(bool value) => ocean.SetActive(OceanConsumer.Main, value);

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
            if (UiWidgets.Disclosure(UiText.Label(T.OceanSchedulePreview, "OceanSchedulePreview"))) DrawSchedule(view);
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
        if (developerMode && UiWidgets.Disclosure(UiText.Label(T.OceanDiagnostics, "OceanDiagnostics")))
        {
            var report = ocean.CreateReport();
            if (UiWidgets.SmallButton(UiText.Label(T.OceanCopyDiagnostics, "CopyOceanDiagnostics"))) ImGui.SetClipboardText(report);
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

    private void DrawRoute(OceanRoute route, int current = -1, OceanFishListView? fishList = null)
    {
        ImGui.Spacing();
        if (!ImGui.BeginTable("OceanRoute", 3, ImGuiTableFlags.SizingStretchSame)) return;
        for (var i = 0; i < route.Stops.Length; i++)
        {
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (current < 0) ImGui.TextDisabled(UiText.Get(T.OceanStopOrdinal, i + 1));
            else ImGui.TextDisabled(UiText.Get(i < current ? T.OceanPassed : i == current ? T.OceanCurrent : T.OceanNext));
            if (i == current && fishList is not null)
            {
                ImGui.SameLine();
                ImGui.BeginDisabled(fishList.State != OceanFishListState.Ready);
                if (UiWidgets.SmallButton(UiText.Label(T.TimerSpotFishButton, "OceanSpotFish"))) toggleSpotFish();
                ImGui.EndDisabled();
                UiWidgets.Tooltip(fishList.State == OceanFishListState.Ready
                    ? UiText.Get(T.OceanFishConditionsHint) : UiWidgets.OceanSpotLabel(fishList));
            }
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
        DrawRoute(route, fishing ? sample.Zone : -1, view.FishList);
        if (!fishing) return; // Transition zone indices are not interpreted as the next fishing stop.
        ImGui.Spacing();
        ImGui.TextUnformatted(UiText.Get(T.OceanBasicGuide));
        var stop = route.Stops[sample.Zone];
        if (view.Recommendation.DataReady)
        {
            DrawRecommendation(view, stop);
            return;
        }
        ImGui.TextDisabled(UiText.Get(T.OceanRecommendationFallback));
        view.Guidance.TryGetValue((stop.Id, 0), out var normal);
        view.Guidance.TryGetValue((stop.Id, stop.Time), out var spectral);
        DrawBait(normal?.Bait, T.OceanNormal, false, "NormalBait");
        DrawBait(spectral?.Bait, T.OceanSpectralBait, false, "SpectralBait");
        var target = sample.Spectral ? spectral : normal;
        if (target is not null)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(UiText.Get(sample.Spectral ? T.OceanScoreTarget : T.OceanTriggerTarget,
                target.Fish.Name, target.Tug, target.Double, target.Triple));
            UiWidgets.Tooltip(UiText.Get(T.OceanTargetHint));
        }
    }

    private void DrawRecommendation(OceanVoyageView view, OceanStop stop)
    {
        DrawGoalSelector(view);
        var recommendation = view.Recommendation;
        ImGui.TextDisabled(UiText.Get(T.OceanRecommendationWeather,
            recommendation.Weather.Length > 0 ? recommendation.Weather : UiText.Get(T.ConditionUnknown)));
        if (recommendation.Primary is { } primary)
        {
            DrawBait(primary.Bait, T.OceanRecommended, true, "RecommendedBait", primary.Kind != OceanBaitKind.Mooch);
            DrawTarget(primary);
            DrawMoochPath(primary);
        }
        else ImGui.TextWrapped(UiText.Get(T.OceanNoRecommendation));
        if (view.Plans.TryGetValue((stop.Id, stop.Time), out var plan))
        {
            var other = view.Sample.Spectral ? plan.Normal : plan.Spectral;
            if (other is not null && other.Bait.Id != recommendation.Primary?.Bait.Id)
            {
                DrawBait(other.Bait, view.Sample.Spectral ? T.OceanNormal : T.OceanSpectralBait, false, "OtherPoolBait", other.Kind != OceanBaitKind.Mooch);
                DrawMoochPath(other);
            }
        }
        foreach (var candidate in recommendation.IntuitionCandidates)
        {
            ImGui.PushID((int)candidate.Fish.Id);
            DrawBait(candidate.Bait, T.OceanIntuitionBait, false, "IntuitionBait", candidate.Kind != OceanBaitKind.Mooch);
            DrawTarget(candidate);
            DrawMoochPath(candidate);
            ImGui.PopID();
        }
        if (recommendation.Intuition is null) ImGui.TextDisabled(UiText.Get(T.OceanIntuitionUnknown));
    }

    private static void DrawMoochPath(OceanBaitChoiceView choice)
    {
        if (choice.Kind == OceanBaitKind.Mooch && choice.BaitPath.Length > 1)
            ImGui.TextWrapped(UiText.Get(T.OceanMoochPath, string.Join(" → ", choice.BaitPath.Select(b => b.Name))));
    }

    private void DrawGoalSelector(OceanVoyageView view)
    {
        if (!view.Recommendation.DataReady) return;
        var selected = DrawGoalControl(view.Goal);
        if (selected != view.Goal) ocean.SetGoal(selected, preferences.View.Session);
    }

    private static OceanFishingGoal DrawGoalControl(OceanFishingGoal current)
    {
        ImGui.TextUnformatted(UiText.Get(T.OceanGoal));
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(Math.Max(1, Math.Min(width, ImGui.GetFontSize() * 23)));
        var selected = current;
        if (ImGui.BeginCombo("###OceanGoal", GoalName(current)))
        {
            foreach (var goal in Enum.GetValues<OceanFishingGoal>())
            {
                ImGui.PushID((int)goal);
                if (ImGui.Selectable($"{GoalName(goal)}###Goal", goal == current)) selected = goal;
                if (goal == current) ImGui.SetItemDefaultFocus();
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        UiWidgets.Tooltip(UiText.Get(T.OceanGoalHint));
        return selected;
    }

    private static string GoalName(OceanFishingGoal goal) => UiText.Get(goal switch
    {
        OceanFishingGoal.Shark => T.OceanGoalShark, OceanFishingGoal.Jellyfish => T.OceanGoalJellyfish,
        OceanFishingGoal.Fugu => T.OceanGoalFugu, OceanFishingGoal.Crab => T.OceanGoalCrab,
        OceanFishingGoal.Shrimp => T.OceanGoalShrimp, OceanFishingGoal.Shellfish => T.OceanGoalShellfish,
        OceanFishingGoal.Squid => T.OceanGoalSquid, OceanFishingGoal.Manta => T.OceanGoalManta,
        OceanFishingGoal.Octopus => T.OceanGoalOctopus, OceanFishingGoal.Seadragon => T.OceanGoalSeadragon,
        OceanFishingGoal.Mantis => T.OceanGoalMantis, OceanFishingGoal.Prehistoric => T.OceanGoalPrehistoric,
        _ => T.OceanGoalDefault,
    });

    private static void DrawTarget(OceanBaitChoiceView target)
    {
        var text = target.Reason switch
        {
            OceanBaitReason.SpectralTrigger => UiText.Get(T.OceanRecommendationTrigger, target.Fish.Name, target.Tug),
            OceanBaitReason.IntuitionCandidate => UiText.Get(T.OceanRecommendationIntuition, target.Fish.Name, target.Tug),
            OceanBaitReason.SpeciesGoal => UiText.Get(T.OceanRecommendationGoal, target.Fish.Name, target.Tug),
            _ => UiText.Get(T.OceanRecommendationScore, target.Fish.Name, target.Tug, target.Points),
        };
        if (target.Reason != OceanBaitReason.IntuitionCandidate && target.Availability == OceanCandidateState.Unconfirmed)
            text = UiText.Get(T.OceanRecommendationUnconfirmed, text);
        ImGui.TextWrapped(text);
        UiWidgets.Tooltip(UiText.Get(target.Reason == OceanBaitReason.IntuitionCandidate ? T.OceanIntuitionBaitHint
            : target.Reason == OceanBaitReason.SpeciesGoal ? T.OceanGoalHint : T.OceanRecommendationHint));
        if (target.Reason != OceanBaitReason.IntuitionCandidate) HookYieldText.Draw(target.Fish.Id, standalone: false);
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
            ImGui.TableSetupColumn(UiText.Label(T.OceanIndigo, "IndigoRoute"), ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn(UiText.Label(T.OceanRuby, "RubyRoute"), ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            var now = DateTimeOffset.UtcNow;
            foreach (var group in view.Departures.GroupBy(d => d.Starts))
            {
                ImGui.PushID(group.Key.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                ImGui.TextUnformatted(group.Key.ToLocalTime().ToString("MM/dd HH:mm"));
                ImGui.TextDisabled(now >= group.Key && now < group.Key + OceanSchedule.RegistrationDuration
                    ? UiText.Get(T.OceanRegistrationOpen) : Countdown((group.Key - now).TotalSeconds));
                foreach (var ruby in new[] { false, true })
                {
                    ImGui.TableNextColumn();
                    var departure = group.FirstOrDefault(d => d.Ruby == ruby);
                    if (departure is null) { ImGui.TextUnformatted("—"); continue; }
                    ImGui.PushID(ruby ? "Ruby" : "Indigo");
                    if (DepartureChoice(departure.Route.Name, departure.Starts == selectedStart && departure.Ruby == selectedRuby))
                    { selected = departure; selectedStart = departure.Starts; selectedRuby = departure.Ruby; }
                    ImGui.PopID();
                }
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        ImGui.TextDisabled(UiText.Get(T.OceanPreview));
        var themed = UiThemeScope.Current is not null;
        if (themed) ImGui.PushStyleColor(ImGuiCol.Text, UiThemeScope.Current!.Accent);
        ImGui.TextWrapped(RouteName(selected));
        if (themed) ImGui.PopStyleColor();
        DrawRoute(selected.Route);
        DrawGoalSelector(view);
        if (view.Recommendation.DataReady && view.Goal != OceanFishingGoal.Default)
        {
            var matched = false;
            foreach (var stop in selected.Route.Stops)
                if (view.Plans.TryGetValue((stop.Id, stop.Time), out var goalPlan))
                    foreach (var (choice, phase) in new[] { (goalPlan.Normal, T.OceanNormal), (goalPlan.Spectral, T.OceanSpectral) })
                        if (choice is not null)
                        {
                            matched = true;
                            var previewText = UiText.Get(T.OceanGoalPreview, stop.Name, UiText.Get(phase), choice.Fish.Name,
                                string.Join(" → ", choice.BaitPath.Select(b => b.Name)));
                            if (choice.Availability == OceanCandidateState.Unconfirmed)
                                previewText = UiText.Get(T.OceanRecommendationUnconfirmed, previewText);
                            ImGui.TextWrapped(previewText);
                        }
            if (!matched) ImGui.TextWrapped(UiText.Get(T.OceanGoalPreviewEmpty));
        }
        ImGui.Spacing();
        ImGui.TextUnformatted(UiText.Get(T.OceanPreparationBaits));
        var baits = selected.Route.Stops.SelectMany(s => view.Plans.TryGetValue((s.Id, s.Time), out var plan) ? plan.Baits : new[]
            {
                view.Guidance.GetValueOrDefault((s.Id, (byte)0))?.Bait,
                view.Guidance.GetValueOrDefault((s.Id, s.Time))?.Bait,
            }.Where(b => b is not null).Cast<FishingItemView>()).DistinctBy(b => b.Id).ToArray();
        foreach (var bait in baits) DrawBait(bait, T.OceanBasicBait, false, $"Prep{bait.Id}");
        if (baits.Length == 0) ImGui.TextWrapped(UiText.Get(T.OceanGuidanceMissing));
        var special = selected.Route.Stops.SelectMany(s => view.Plans.GetValueOrDefault((s.Id, s.Time))?.IntuitionBaits ?? [])
            .Where(b => !baits.Any(n => n.Id == b.Id)).DistinctBy(b => b.Id).ToArray();
        if (special.Length > 0 && UiWidgets.Disclosure(UiText.Label(T.OceanOptionalBaits, "OceanOptionalBaits")))
        {
            ImGui.TextWrapped(UiText.Get(T.OceanOptionalBaitsHint));
            foreach (var bait in special) DrawBait(bait, T.OceanIntuitionBait, false, $"Optional{bait.Id}");
        }
    }

    private static bool DepartureChoice(string label, bool selected)
    {
        var origin = ImGui.GetCursorPos();
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var height = Math.Max(ImGui.GetTextLineHeightWithSpacing() + ImGui.GetTextLineHeight(),
            ImGui.CalcTextSize(label, false, width).Y);
        var clicked = ImGui.Selectable("###Departure", selected, ImGuiSelectableFlags.None, new Vector2(0, height));
        if (selected && UiThemeScope.Current is { } theme && ImGui.IsItemVisible())
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(new(min.X + 2, max.Y - 1), new(max.X - 2, max.Y - 1),
                ImGui.GetColorU32(theme.Accent), 2);
        }
        var next = ImGui.GetCursorPos();
        // Keep the native selectable hit area/keyboard behavior while wrapping long client names.
        ImGui.SetCursorPos(origin);
        ImGui.TextWrapped(label);
        ImGui.SetCursorPos(next);
        return clicked;
    }

    private void DrawBait(FishingItemView? bait, T label, bool active, string id, bool inventoryBait = true)
    {
        ImGui.PushID(id);
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 30;
        if (ImGui.BeginTable("OceanBaitRow", narrow ? 1 : 4, ImGuiTableFlags.SizingFixedFit))
        {
            if (!narrow) ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 8);
            ImGui.TableSetupColumn("Bait", ImGuiTableColumnFlags.WidthStretch);
            if (!narrow)
            {
                ImGui.TableSetupColumn("Stock", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 7);
                ImGui.TableSetupColumn("Find", ImGuiTableColumnFlags.WidthFixed,
                    ImGui.CalcTextSize(UiText.Get(T.BaitFind)).X + ImGui.GetStyle().FramePadding.X * 2);
            }
            ImGui.TableNextRow();
            if (active) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.Header));
            ImGui.TableNextColumn(); ImGui.TextWrapped(UiText.Get(label));
            if (!narrow) ImGui.TableNextColumn();
            if (bait is null) ImGui.TextWrapped(UiText.Get(T.OceanGuidanceMissing));
            else
            {
                if (inventoryBait) visibleBaits.Add(bait);
                UiWidgets.GameIcon(textures, bait.IconId, ImGui.GetTextLineHeight(), bait.Name);
                ImGui.SameLine(); ImGui.TextWrapped(bait.Name);
                if (!narrow) ImGui.TableNextColumn();
                var inventory = stock.View;
                var quantity = inventoryBait && inventory.Session == preferences.View.Session ? inventory.Quantity(bait.Id) : null;
                var stockText = !inventoryBait ? UiText.Get(T.OceanMoochBait) : quantity == 0 ? UiText.Get(T.OceanBaitMissing)
                    : quantity.HasValue ? UiText.Get(T.OceanBaitStock, quantity.Value) : UiText.Get(T.OceanBaitStockUnknown);
                ImGui.TextWrapped(stockText);
                if (quantity == 0 && inventoryBait && label == T.OceanBasicBait) UiWidgets.Tooltip(UiText.Get(T.OceanBaitBuyHint));
                if (narrow)
                {
                    var buttonWidth = ImGui.CalcTextSize(UiText.Get(T.BaitFind)).X + ImGui.GetStyle().FramePadding.X * 2;
                    if (ImGui.CalcTextSize(stockText).X + ImGui.GetStyle().ItemSpacing.X + buttonWidth <= ImGui.GetContentRegionAvail().X)
                        ImGui.SameLine();
                }
                else ImGui.TableNextColumn();
                if (inventoryBait) finder.DrawButton(bait, preferences.View.Session);
            }
            ImGui.EndTable();
        }
        ImGui.PopID();
    }
}
