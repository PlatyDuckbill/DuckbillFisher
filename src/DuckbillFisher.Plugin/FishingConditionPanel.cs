using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Aligned conditions using cached references; links only enqueue explicit user clicks.</summary>
internal sealed class FishingConditionPanel(ITextureProvider textures, FishingItemLinkService itemLinks)
{
    public void Draw(FishingDetailView details, FishPreferencesView personal,
        IReadOnlyDictionary<uint, FishingDetailView> related)
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
                    related.TryGetValue(item.Id, out var intermediate);
                    StepItem(item, item.Name, $"Bait{step}/{item.Id}", personal, intermediate, step > 0);
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
        if (rule.FolkloreRequired)
        {
            UiWidgets.Heading(T.DetailFolklore);
            if (details.Folklore is { } book) Item(book, book.Name, "Folklore", personal);
            else ImGui.TextWrapped(UiText.Get(T.FolkloreRequired));
        }

    }

    public void DrawIntuition(FishingDetailView details, FishPreferencesView personal,
        IReadOnlyDictionary<uint, FishingDetailView> related)
    {
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.TableRowBg, new System.Numerics.Vector4(0.30f, 0.18f, 0.45f, 0.55f));
        if (ImGui.BeginTable("IntuitionHighlight", 1, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            UiWidgets.GameIcon(textures, details.Intuition?.IconId ?? 0, IconSize, UiText.Get(T.IntuitionRequired));
            ImGui.SameLine();
            ImGui.TextUnformatted(UiText.Get(T.IntuitionRequired));
            ImGui.TextDisabled(UiText.Get(T.IntuitionAllRequired));
            for (var index = 0; index < details.Predators.Length; index++)
            {
                var item = details.Predators[index];
                related.TryGetValue(item.Id, out var prey);
                StepItem(item, UiText.Get(T.PredatorLabel, item.Name, details.Rule.Predators[index].Count),
                    $"Predator{index}", personal, prey, true);
            }
            ImGui.TextWrapped(details.Rule.IntuitionSeconds is { } seconds
                ? UiText.Get(T.IntuitionDuration, seconds) : UiText.Get(T.IntuitionDurationUnknown));
            ImGui.EndTable();
        }
        ImGui.PopStyleColor();
    }

    private void StepItem(FishingItemView item, string name, string id, FishPreferencesView personal,
        FishingDetailView? detail, bool annotateMissing)
    {
        if (detail is null && !annotateMissing) { Item(item, name, id, personal); return; }
        ImGui.PushID(id);
        if (ImGui.BeginTable("Ingredient", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            Item(item, name, "Item", personal);
            ImGui.TableNextColumn();
            if (detail is null)
            {
                UiWidgets.Icon(FontAwesomeIcon.InfoCircle);
                UiWidgets.Tooltip(UiText.Get(T.StepConditionsUnknown));
            }
            else StepConditions(detail);
            ImGui.EndTable();
        }
        ImGui.PopID();
    }

    private void StepConditions(FishingDetailView detail)
    {
        var start = ImGui.GetCursorPos();
        var screen = ImGui.GetCursorScreenPos();
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var infoWidth = ImGui.GetTextLineHeight();
        var parts = new List<string>();
        var tug = detail.Rule.Tug switch { "light" => "!", "medium" => "!!", "heavy" => "!!!", _ => UiText.Get(T.ConditionUnknown) };
        parts.Add(UiText.Get(T.StepBite, tug));
        ImGui.PushClipRect(screen, screen + new Vector2(Math.Max(1, width - infoWidth - 6), IconSize), true);
        if (detail.Hookset is { } hookset)
        {
            UiWidgets.GameIcon(textures, hookset.IconId, IconSize, UiText.Get(T.HooksetLabel, hookset.Name));
            parts.Add(UiText.Get(T.HooksetLabel, hookset.Name));
            ImGui.SameLine(0, 4);
        }
        else parts.Add(UiText.Get(T.StepConditionsUnknown));
        ImGui.TextDisabled(tug);
        if (!detail.Rule.ConditionsKnown) parts.Add(UiText.Get(T.ConditionsPartial));
        else if (detail.Rule.StartHour != 0 || detail.Rule.EndHour != 24)
        {
            var time = UiText.Get(T.TimeConditionLabel, EtClock(detail.Rule.StartHour), EtClock(detail.Rule.EndHour));
            parts.Add(time);
            ImGui.SameLine(0, 6);
            ImGui.TextDisabled(time);
        }
        ImGui.PopClipRect();
        if (detail.PreviousWeather.Length > 0)
            parts.Add(UiText.Get(T.PreviousWeatherConditionLabel, string.Join(" / ", detail.PreviousWeather.Select(weather => weather.Name))));
        if (detail.Weather.Length > 0)
            parts.Add(UiText.Get(T.WeatherConditionLabel, string.Join(" / ", detail.Weather.Select(weather => weather.Name))));
        if (detail.Rule.SnaggingRequired) parts.Add(UiText.Get(T.RequiredSkill, detail.Snagging?.Name ?? UiText.Get(T.SnaggingRequired)));
        if (detail.Rule.Lure.Length > 0) parts.Add(UiText.Get(T.RequiredSkill, detail.Lure?.Name ??
            UiText.Get(detail.Rule.Lure == "Modest" ? T.LureModest : T.LureAmbitious)));
        if (detail.Predators.Length > 0) parts.Add(UiText.Get(T.IntuitionRequired));
        ImGui.SetCursorPos(start + new Vector2(Math.Max(0, width - infoWidth), 0));
        UiWidgets.Icon(FontAwesomeIcon.InfoCircle);
        UiWidgets.Tooltip(string.Join("\n", parts));
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
