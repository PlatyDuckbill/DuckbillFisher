using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Aligned conditions using cached references; links only enqueue explicit user clicks.</summary>
internal sealed class FishingConditionPanel(ITextureProvider textures, FishingItemLinkService itemLinks)
{
    public void Draw(FishingDetailView details, FishPreferencesView personal,
        IReadOnlyDictionary<uint, FishingDetailView> related, bool includeRelated = true)
    {
        var rule = details.Rule;
        if (ImGui.BeginTable("Conditions", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 5.5f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            Label(T.DetailBait);
            if (details.BaitSteps.Length == 0) ImGui.TextUnformatted(UiText.Get(T.ConditionUnknown));
            for (var step = 0; step < details.BaitSteps.Length; step++)
            {
                if (step > 0) ImGui.TextUnformatted("↓");
                for (var alternative = 0; alternative < details.BaitSteps[step].Length; alternative++)
                {
                    if (alternative > 0) ImGui.TextDisabled(UiText.Get(T.ConditionOr));
                    var item = details.BaitSteps[step][alternative];
                    Item(item, item.Name, $"Bait{step}/{item.Id}", personal);
                }
            }
            Label(T.DetailHookset);
            UiWidgets.Tooltip(UiText.Get(T.HooksetLabel, details.Hookset?.Name ?? UiText.Get(T.ConditionUnknown)));
            if (details.Hookset is { } hookset)
                UiWidgets.GameIcon(textures, hookset.IconId, IconSize, UiText.Get(T.HooksetLabel, hookset.Name));
            else ImGui.TextWrapped(UiText.Get(rule.Hookset switch
                { "Precision" => T.HooksetPrecision, "Powerful" => T.HooksetPowerful, _ => T.ConditionUnknown }));
            Label(T.DetailTug);
            ImGui.TextUnformatted(rule.Tug switch { "light" => "!", "medium" => "!!", "heavy" => "!!!", _ => UiText.Get(T.ConditionUnknown) });
            Label(T.DetailTime);
            ImGui.TextWrapped(!rule.ConditionsKnown ? UiText.Get(T.ConditionsPartial)
                : rule.StartHour == 0 && rule.EndHour == 24 ? UiText.Get(T.TimeAllDay)
                : UiText.Get(T.TimeConditionLabel, EtClock(rule.StartHour), EtClock(rule.EndHour)));
            if (rule.ConditionsKnown)
            {
                Label(T.DetailPreviousWeather);
                Weather(details.PreviousWeather, T.PreviousWeatherConditionLabel);
                Label(T.DetailWeather);
                Weather(details.Weather, T.WeatherConditionLabel);
            }
            if (rule.SnaggingRequired) { Label(T.DetailRequired); Skill(details.Snagging, T.SnaggingRequired); }
            if (rule.Lure.Length > 0) { Label(T.DetailRequired); Skill(details.Lure, rule.Lure == "Modest" ? T.LureModest : T.LureAmbitious); }
            ImGui.EndTable();
        }
        if (itemLinks.Result is { } result && result.Key != T.ItemLinkPrinted) ImGui.TextWrapped(result.Display);
        if (details.Predators.Length > 0)
        {
            UiWidgets.Heading(T.IntuitionHeading);
            for (var index = 0; index < details.Predators.Length; index++)
                Item(details.Predators[index], UiText.Get(T.PredatorLabel, details.Predators[index].Name, rule.Predators[index].Count), $"Predator{index}", personal);
            ImGui.TextWrapped(rule.IntuitionSeconds is { } seconds
                ? UiText.Get(T.IntuitionDuration, seconds) : UiText.Get(T.IntuitionDurationUnknown));
        }
        if (rule.FolkloreRequired)
        {
            UiWidgets.Heading(T.DetailFolklore);
            if (details.Folklore is { } book) Item(book, book.Name, "Folklore", personal);
            else ImGui.TextWrapped(UiText.Get(T.FolkloreRequired));
        }
        if (includeRelated)
        {
            foreach (var item in details.BaitSteps.SelectMany(step => step).Concat(details.Predators).DistinctBy(item => item.Id))
            {
                if (!related.TryGetValue(item.Id, out var detail)) continue;
                if (!ImGui.CollapsingHeader($"{UiText.Get(T.RelatedConditions, item.Name)}###Related{item.Id}")) continue;
                ImGui.PushID((int)item.Id);
                Draw(detail, personal, related, includeRelated: false);
                ImGui.PopID();
            }
        }
        if (ImGui.CollapsingHeader(UiText.Label(T.DetailAdditional, "AdditionalConditions")))
        {
            ImGui.TextWrapped(UiText.Get(T.FishEyesSupport, new UiMessage(rule.FishEyesSupported ? T.ConditionSupported : T.ConditionUnsupported)));
            ImGui.TextWrapped(UiText.Get(T.SourcePatchLabel, rule.Patch));
            if (details.Spot is { } spot) ImGui.TextWrapped(UiText.Get(T.RecommendedSpot, spot.Zone, spot.Name));
        }
    }

    private static float IconSize => ImGui.GetTextLineHeightWithSpacing() * 1.35f;
    private static void Label(T key)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextDisabled(UiText.Get(key));
        ImGui.TableNextColumn();
    }

    private void Item(FishingItemView item, string name, string id, FishPreferencesView personal)
    {
        ImGui.PushID(id);
        ImGui.BeginDisabled(!personal.HasCharacter);
        UiWidgets.GameIcon(textures, item.IconId, IconSize, UiText.Get(T.ItemLinkHint, item.Name));
        if (personal.HasCharacter && ImGui.IsItemClicked()) itemLinks.Request(item, personal.Session);
        ImGui.SameLine();
        ImGui.TextWrapped(name);
        if (personal.HasCharacter && ImGui.IsItemClicked()) itemLinks.Request(item, personal.Session);
        UiWidgets.Tooltip(UiText.Get(T.ItemLinkHint, item.Name));
        ImGui.EndDisabled();
        ImGui.PopID();
    }

    private void Skill(FishingConditionIcon? skill, T fallback)
    {
        if (skill is null) ImGui.TextWrapped(UiText.Get(fallback));
        else UiWidgets.GameIcon(textures, skill.IconId, IconSize, UiText.Get(T.RequiredSkill, skill.Name));
    }

    private void Weather(FishingConditionIcon[] options, T label)
    {
        if (options.Length == 0) { ImGui.TextUnformatted(UiText.Get(T.NoWeatherRestriction)); return; }
        for (var i = 0; i < options.Length; i++)
        {
            if (i > 0 && ImGui.GetContentRegionAvail().X > IconSize * 3) ImGui.SameLine();
            UiWidgets.GameIcon(textures, options[i].IconId, IconSize, UiText.Get(label, options[i].Name));
        }
        if (options.Length > 1) ImGui.TextDisabled(UiText.Get(T.ConditionAny));
    }

    private static string EtClock(double hour) => $"{(int)hour:00}:{(int)Math.Round(hour % 1 * 60):00}";
}
