using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Renders cached condition references. Sheets and game actions stay outside Draw.</summary>
internal sealed class FishingConditionPanel(ITextureProvider textures, FishingItemLinkService itemLinks)
{
    public void Draw(FishingDetailView details, FishPreferencesView personal,
        IReadOnlyDictionary<uint, FishingDetailView> related, bool includeRelated = true)
    {
        var rule = details.Rule;
        ImGui.TextUnformatted(UiText.Get(T.BaitPathHeading));
        for (var index = 0; index < details.BaitSteps.Length; index++)
        {
            if (details.BaitSteps[index].Length > 1)
                ImGui.TextWrapped(UiText.Get(T.BaitAlternatives, index + 1));
            foreach (var item in details.BaitSteps[index])
            {
                DrawItem(item, UiText.Get(T.BaitStepLabel, index + 1, item.Name), $"BaitStep{index}/{item.Id}", personal);
                if (includeRelated) DrawRelated(item, related, personal, $"BaitRelated{index}/{item.Id}");
            }
        }
        if (itemLinks.Result is { } result)
            ImGui.TextWrapped(result.Display);

        ImGui.Separator();
        if (details.Hookset is { } hookset)
        {
            DrawIcon(hookset.IconId);
            ImGui.SameLine();
        }
        ImGui.TextWrapped(UiText.Get(T.HooksetLabel, details.Hookset?.Name ?? UiText.Get(rule.Hookset switch
            { "Precision" => T.HooksetPrecision, "Powerful" => T.HooksetPowerful, _ => T.ConditionUnknown })));
        ImGui.TextWrapped(UiText.Get(T.TugLabel, rule.Tug switch { "light" => "!", "medium" => "!!", "heavy" => "!!!", _ => UiText.Get(T.ConditionUnknown) }));
        if (rule.ConditionsKnown)
        {
            ImGui.TextWrapped(rule.StartHour == 0 && rule.EndHour == 24 ? UiText.Get(T.TimeAllDay)
                : UiText.Get(T.TimeConditionLabel, EtClock(rule.StartHour), EtClock(rule.EndHour)));
            DrawWeather(T.WeatherHeading, T.WeatherConditionLabel, details.Weather);
            DrawWeather(T.PreviousWeatherHeading, T.PreviousWeatherConditionLabel, details.PreviousWeather);
        }
        else
            ImGui.TextWrapped(UiText.Get(T.ConditionsPartial));
        if (rule.SnaggingRequired)
            DrawRequiredSkill(details.Snagging, T.SnaggingRequired);
        if (rule.Lure.Length > 0)
            DrawRequiredSkill(details.Lure, rule.Lure == "Modest" ? T.LureModest : T.LureAmbitious);
        ImGui.TextWrapped(UiText.Get(T.FishEyesSupport,
            new UiMessage(rule.FishEyesSupported ? T.ConditionSupported : T.ConditionUnsupported)));
        if (rule.BigFish) ImGui.TextUnformatted(UiText.Get(T.BigFishLabel));
        ImGui.TextWrapped(UiText.Get(T.SourcePatchLabel, rule.Patch));
        if (details.Spot is { } spot)
            ImGui.TextWrapped(UiText.Get(T.RecommendedSpot, spot.Zone, spot.Name));

        if (details.Predators.Length > 0)
        {
            ImGui.TextWrapped(UiText.Get(T.IntuitionHeading));
            for (var index = 0; index < details.Predators.Length; index++)
            {
                DrawItem(details.Predators[index], UiText.Get(T.PredatorLabel, details.Predators[index].Name, rule.Predators[index].Count),
                    $"Predator{index}", personal);
                if (includeRelated) DrawRelated(details.Predators[index], related, personal, $"PredatorRelated{index}");
            }
            if (rule.IntuitionSeconds is { } seconds)
                ImGui.TextWrapped(UiText.Get(T.IntuitionDuration, seconds));
            else
                ImGui.TextWrapped(UiText.Get(T.IntuitionDurationUnknown));
        }
        if (rule.FolkloreRequired)
        {
            ImGui.TextWrapped(UiText.Get(T.FolkloreRequired));
            if (details.Folklore is { } book)
                DrawItem(book, UiText.Get(T.FolkloreItem, book.Name), "FolkloreBook", personal);
        }
    }

    private void DrawRequiredSkill(FishingConditionIcon? skill, T fallback)
    {
        if (skill is not null) { DrawIcon(skill.IconId); ImGui.SameLine(); }
        ImGui.TextWrapped(UiText.Get(T.RequiredSkill, skill?.Name ?? UiText.Get(fallback)));
    }

    private void DrawRelated(FishingItemView item, IReadOnlyDictionary<uint, FishingDetailView> related,
        FishPreferencesView personal, string id)
    {
        if (!related.TryGetValue(item.Id, out var detail)) return;
        if (ImGui.CollapsingHeader($"{UiText.Get(T.RelatedConditions, item.Name)}###{id}"))
        {
            ImGui.PushID(id);
            Draw(detail, personal, related, includeRelated: false);
            ImGui.PopID();
        }
    }

    private void DrawItem(FishingItemView item, string label, string id, FishPreferencesView personal)
    {
        ImGui.PushID(id);
        ImGui.BeginDisabled(!personal.HasCharacter);
        DrawIcon(item.IconId);
        if (personal.HasCharacter && ImGui.IsItemClicked(ImGuiMouseButton.Left))
            itemLinks.Request(item, personal.Session);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(UiText.Get(T.ItemLinkHint, item.Name));
        ImGui.SameLine();
        if (ImGui.SmallButton($"{label}###ItemLink"))
            itemLinks.Request(item, personal.Session);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(UiText.Get(T.ItemLinkHint, item.Name));
        ImGui.PopID();
    }

    private void DrawWeather(T heading, T unrestrictedLabel, FishingConditionIcon[] conditions)
    {
        if (conditions.Length == 0)
        {
            ImGui.TextWrapped(UiText.Get(unrestrictedLabel, new UiMessage(T.NoWeatherRestriction)));
            return;
        }
        ImGui.TextUnformatted(UiText.Get(heading));
        foreach (var condition in conditions)
        {
            DrawIcon(condition.IconId);
            ImGui.SameLine();
            ImGui.TextUnformatted(condition.Name);
        }
    }

    private void DrawIcon(uint iconId)
    {
        var size = new Vector2(ImGui.GetTextLineHeightWithSpacing() * 1.5f);
        // The shared texture wrap belongs to Dalamud and must not be disposed here.
        if (iconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var icon) &&
            icon.TryGetWrap(out var wrap, out _))
            ImGui.Image(wrap.Handle, size);
        else
            ImGui.Dummy(size);
    }

    private static string EtClock(double hour) => $"{(int)hour:00}:{(int)Math.Round(hour % 1 * 60):00}";
}
