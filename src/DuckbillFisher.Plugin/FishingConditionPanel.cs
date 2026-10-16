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
    public void Draw(FishingDetailView details, FishPreferencesView personal)
    {
        var rule = details.Rule;
        ImGui.TextUnformatted(UiText.Get(T.BaitPathHeading));
        for (var index = 0; index < details.BaitPath.Length; index++)
            DrawItem(details.BaitPath[index], UiText.Get(T.BaitStepLabel, index + 1, details.BaitPath[index].Name),
                $"BaitStep{index}", personal);
        if (itemLinks.Result is { } result)
            ImGui.TextWrapped(result.Display);

        ImGui.Separator();
        if (details.Hookset is { } hookset)
        {
            DrawIcon(hookset.IconId);
            ImGui.SameLine();
        }
        ImGui.TextWrapped(UiText.Get(T.HooksetLabel, details.Hookset?.Name ??
            UiText.Get(rule.Hookset == "Precision" ? T.HooksetPrecision : T.HooksetPowerful)));
        ImGui.TextWrapped(UiText.Get(T.TugLabel, rule.Tug switch { "light" => "!", "medium" => "!!", _ => "!!!" }));
        ImGui.TextWrapped(rule.StartHour == 0 && rule.EndHour == 24 ? UiText.Get(T.TimeAllDay)
            : UiText.Get(T.TimeConditionLabel, EtClock(rule.StartHour), EtClock(rule.EndHour)));
        DrawWeather(T.WeatherHeading, T.WeatherConditionLabel, details.Weather);
        DrawWeather(T.PreviousWeatherHeading, T.PreviousWeatherConditionLabel, details.PreviousWeather);

        if (details.Predators.Length > 0)
        {
            ImGui.TextWrapped(UiText.Get(T.IntuitionHeading));
            for (var index = 0; index < details.Predators.Length; index++)
                DrawItem(details.Predators[index], UiText.Get(T.PredatorLabel, details.Predators[index].Name, rule.Predators[index].Count),
                    $"Predator{index}", personal);
            if (rule.IntuitionSeconds is { } seconds)
                ImGui.TextWrapped(UiText.Get(T.IntuitionDuration, seconds));
        }
        if (rule.FolkloreRequired)
            ImGui.TextWrapped(UiText.Get(T.FolkloreRequired));
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
