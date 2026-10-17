using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Aligned conditions using cached references; links only enqueue explicit user clicks.</summary>
internal sealed class FishingConditionPanel(ITextureProvider textures, FishingItemLinkService itemLinks, BaitStockObserver baitStock, BaitFinder baitFinder)
{
    public void DrawHeading(FishingDetailView details, FishPreferencesView personal,
        IReadOnlyDictionary<uint, FishingDetailView> related)
    {
        ImGui.Spacing();
        ImGui.Separator();
        var firstBaits = details.BaitSteps.FirstOrDefault();
        if (firstBaits is not { Length: > 0 } || firstBaits.Any(item => related.ContainsKey(item.Id)))
        {
            ImGui.TextUnformatted(UiText.Get(T.DetailConditions));
            ImGui.Spacing();
            return;
        }
        if (ImGui.BeginTable("ConditionsHeading", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Find", ImGuiTableColumnFlags.WidthFixed,
                ImGui.CalcTextSize(UiText.Get(T.BaitFind)).X + ImGui.GetStyle().FramePadding.X * 2);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(UiText.Get(T.DetailConditions));
            ImGui.TableNextColumn();
            if (firstBaits.Length == 1) baitFinder.DrawButton(firstBaits[0], personal.Session);
            else
            {
                if (ImGui.SmallButton(UiText.Label(T.BaitFind, "FindBaitChoice"))) ImGui.OpenPopup("FindBaitAlternatives");
                UiWidgets.Tooltip(UiText.Get(T.BaitFindHint));
                if (ImGui.BeginPopup("FindBaitAlternatives"))
                {
                    foreach (var item in firstBaits)
                    {
                        ImGui.PushID((int)item.Id);
                        ImGui.BeginDisabled(!baitFinder.CanRequest(item.Id, personal.Session));
                        if (ImGui.Selectable($"{item.Name}###FindBaitAlternative")) baitFinder.Request(item, personal.Session);
                        ImGui.EndDisabled();
                        ImGui.PopID();
                    }
                    ImGui.EndPopup();
                }
            }
            ImGui.EndTable();
        }
        ImGui.Spacing();
    }

    public void Draw(FishingDetailView details, FishPreferencesView personal,
        IReadOnlyDictionary<uint, FishingDetailView> related)
    {
        var rule = details.Rule;
        DrawBaitPath(details, personal, related);
        baitFinder.DrawStatus(details.BaitSteps.FirstOrDefault() ?? []);
        ImGui.Spacing();
        if (ImGui.BeginTable("Conditions", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 5.5f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            Label(T.DetailHookset);
            UiWidgets.Tooltip(UiText.Get(T.HooksetLabel, details.Hookset?.Name ?? UiText.Get(T.ConditionUnknown)));
            if (details.Hookset is { } hookset)
                UiWidgets.GameIcon(textures, hookset.IconId, IconSize, UiText.Get(T.HooksetLabel, hookset.Name));
            else ImGui.TextWrapped(UiText.Get(rule.Hookset switch
                { "Precision" => T.HooksetPrecision, "Powerful" => T.HooksetPowerful, _ => T.ConditionUnknown }));
            Label(T.DetailTug);
            ImGui.TextUnformatted(rule.Tug switch { "light" => "!", "medium" => "!!", "heavy" => "!!!", _ => UiText.Get(T.ConditionUnknown) });
            Label(T.DetailTime);
            ImGui.TextWrapped(!rule.ConditionsKnown ? UiText.Get(T.ConditionUnknown)
                : rule.StartHour == 0 && rule.EndHour == 24 ? UiText.Get(T.TimeAllDay)
                : UiText.Get(T.TimeConditionLabel, EtClock(rule.StartHour), EtClock(rule.EndHour)));
            if (!rule.ConditionsKnown) UiWidgets.Tooltip(details.ConditionWarning.Display);
            if (rule.ConditionsKnown)
            {
                Label(T.DetailWeatherTransition);
                var right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
                if (details.PreviousWeather.Length > 0)
                {
                    Weather(details.PreviousWeather, T.PreviousWeatherConditionLabel, right);
                    UiWidgets.FlowNext(IconSize * 2 + 12, right, 6);
                    ImGui.TextUnformatted("→");
                    UiWidgets.Tooltip(UiText.Get(T.WeatherTransitionHint));
                    ImGui.SameLine(0, 6);
                }
                Weather(details.Weather, T.WeatherConditionLabel, right);
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

    private void DrawBaitPath(FishingDetailView details, FishPreferencesView personal,
        IReadOnlyDictionary<uint, FishingDetailView> related)
    {
        if (details.BaitSteps.Length == 0) { ImGui.TextDisabled(UiText.Get(T.RowBaitUnknown)); return; }
        var width = ImGui.GetContentRegionAvail().X;
        var right = ImGui.GetCursorPosX() + width;
        var first = true;
        for (var step = 0; step < details.BaitSteps.Length; step++)
            for (var alternative = 0; alternative < details.BaitSteps[step].Length; alternative++)
            {
                var item = details.BaitSteps[step][alternative];
                related.TryGetValue(item.Id, out var detail);
                var name = item.Name;
                if (step == 0 && detail is null)
                {
                    var inventory = baitStock.View;
                    var count = inventory.Session == personal.Session && personal.HasCharacter ? inventory.Quantity(item.Id) : null;
                    name = count is { } quantity ? UiText.Get(T.BaitNameStock, name, quantity) : UiText.Get(T.BaitNameUnknown, name);
                }
                var separator = alternative > 0 ? "/" : "→";
                var separatorWidth = first ? 0 : ImGui.CalcTextSize(separator).X + 8;
                var preferred = IconSize + 8 + Math.Min(ImGui.CalcTextSize(name).X, ImGui.GetFontSize() * 12)
                    + (detail is null ? step > 0 ? ImGui.GetTextLineHeight() + 6 : 0 : IconSize + ImGui.GetFontSize() * 4);
                if (detail is { Rule.ConditionsKnown: true } && (detail.Rule.StartHour != 0 || detail.Rule.EndHour != 24))
                    preferred += ImGui.CalcTextSize(UiText.Get(T.TimeConditionLabel, EtClock(detail.Rule.StartHour), EtClock(detail.Rule.EndHour))).X + 6;
                if (!first)
                {
                    UiWidgets.FlowNext(Math.Min(width, preferred + separatorWidth), right);
                    ImGui.TextDisabled(separator);
                    UiWidgets.Tooltip(UiText.Get(alternative > 0 ? T.ConditionAny : T.BaitPathHeading));
                    ImGui.SameLine(0, 8);
                }
                StepItem(item, name, $"Bait{step}/{item.Id}", personal, detail, step > 0,
                    Math.Max(1, Math.Min(preferred, right - ImGui.GetCursorPosX())));
                if (step == 0 && detail is null) UiWidgets.Tooltip(UiText.Get(T.BaitStockTooltip, name));
                first = false;
            }
    }

    private void StepItem(FishingItemView item, string name, string id, FishPreferencesView personal,
        FishingDetailView? detail, bool annotateMissing, float? maxWidth = null)
    {
        var width = maxWidth ?? ImGui.GetContentRegionAvail().X;
        var minimumConditions = annotateMissing ? ImGui.GetTextLineHeight() : 0;
        if (detail is not null)
        {
            var tug = detail.Rule.Tug switch { "light" => "!", "medium" => "!!", "heavy" => "!!!", _ => UiText.Get(T.ConditionUnknown) };
            minimumConditions = (detail.Hookset is null ? 0 : IconSize + 4) + ImGui.CalcTextSize(tug).X;
            if (!detail.Rule.ConditionsKnown || detail.PreviousWeather.Length > 0 || detail.Weather.Length > 0 ||
                detail.Rule.SnaggingRequired || detail.Rule.Lure.Length > 0 || detail.Predators.Length > 0 ||
                detail.Rule.StartHour != 0 || detail.Rule.EndHour != 24)
                minimumConditions += ImGui.GetTextLineHeight() + 6;
        }
        var spacing = minimumConditions > 0 ? 14 : 8;
        var nameWidth = Math.Max(1, Math.Min(ImGui.CalcTextSize(name).X,
            Math.Min(ImGui.GetFontSize() * 12, width - IconSize - spacing - minimumConditions)));
        ImGui.PushID(id);
        ImGui.BeginGroup();
        Item(item, name, "Item", personal, nameWidth);
        if (detail is not null)
        {
            ImGui.SameLine(0, 6);
            StepConditions(detail, Math.Max(1, width - IconSize - nameWidth - 14));
        }
        else if (annotateMissing)
        {
            ImGui.SameLine(0, 6);
            UiWidgets.Icon(FontAwesomeIcon.InfoCircle);
            UiWidgets.Tooltip(UiText.Get(T.StepConditionsUnknown));
        }
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private void StepConditions(FishingDetailView detail, float width)
    {
        var parts = new List<string>();
        var tug = detail.Rule.Tug switch { "light" => "!", "medium" => "!!", "heavy" => "!!!", _ => UiText.Get(T.ConditionUnknown) };
        parts.Add(UiText.Get(T.StepBite, tug));
        if (detail.Hookset is { } hookset) parts.Add(UiText.Get(T.HooksetLabel, hookset.Name));
        else parts.Add(UiText.Get(T.StepConditionsUnknown));
        string? time = null;
        if (!detail.Rule.ConditionsKnown) parts.Add(detail.ConditionWarning.Display);
        else if (detail.Rule.StartHour != 0 || detail.Rule.EndHour != 24)
        {
            time = UiText.Get(T.TimeConditionLabel, EtClock(detail.Rule.StartHour), EtClock(detail.Rule.EndHour));
            parts.Add(time);
        }
        if (detail.PreviousWeather.Length > 0)
            parts.Add(UiText.Get(T.PreviousWeatherConditionLabel, string.Join(" / ", detail.PreviousWeather.Select(weather => weather.Name))));
        if (detail.Weather.Length > 0)
            parts.Add(UiText.Get(T.WeatherConditionLabel, string.Join(" / ", detail.Weather.Select(weather => weather.Name))));
        if (detail.Rule.SnaggingRequired) parts.Add(UiText.Get(T.RequiredSkill, detail.Snagging?.Name ?? UiText.Get(T.SnaggingRequired)));
        if (detail.Rule.Lure.Length > 0) parts.Add(UiText.Get(T.RequiredSkill, detail.Lure?.Name ??
            UiText.Get(detail.Rule.Lure == "Modest" ? T.LureModest : T.LureAmbitious)));
        if (detail.Predators.Length > 0) parts.Add(UiText.Get(T.IntuitionRequired));
        var extra = !detail.Rule.ConditionsKnown || detail.PreviousWeather.Length > 0 || detail.Weather.Length > 0 ||
            detail.Rule.SnaggingRequired || detail.Rule.Lure.Length > 0 || detail.Predators.Length > 0;
        var used = (detail.Hookset is null ? 0 : IconSize + 4) + ImGui.CalcTextSize(tug).X;
        var showTime = time is not null && used + 6 + ImGui.CalcTextSize(time).X +
            (extra ? ImGui.GetTextLineHeight() + 6 : 0) <= width;
        var tooltip = string.Join("\n", parts);
        if (detail.Hookset is { } hook)
        {
            UiWidgets.GameIcon(textures, hook.IconId, IconSize, tooltip);
            ImGui.SameLine(0, 4);
        }
        ImGui.TextDisabled(tug);
        UiWidgets.Tooltip(tooltip);
        if (showTime)
        {
            ImGui.SameLine(0, 6);
            ImGui.TextDisabled(time!);
            UiWidgets.Tooltip(tooltip);
        }
        if (extra || time is not null && !showTime)
        {
            ImGui.SameLine(0, 6);
            UiWidgets.Icon(FontAwesomeIcon.InfoCircle);
            UiWidgets.Tooltip(tooltip);
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

    private void Item(FishingItemView item, string name, string id, FishPreferencesView personal, float? nameWidth = null)
    {
        ImGui.PushID(id);
        ImGui.BeginDisabled(!personal.HasCharacter);
        UiWidgets.GameIcon(textures, item.IconId, IconSize, UiText.Get(T.ItemLinkHint, item.Name));
        if (personal.HasCharacter && ImGui.IsItemClicked()) itemLinks.Request(item, personal.Session);
        ImGui.SameLine();
        if (nameWidth is { } fixedWidth)
        {
            var position = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().PushClipRect(position, position + new Vector2(fixedWidth, IconSize), true);
            ImGui.GetWindowDrawList().AddText(position + new Vector2(0, (IconSize - ImGui.GetTextLineHeight()) / 2),
                ImGui.GetColorU32(ImGuiCol.Text), name);
            ImGui.GetWindowDrawList().PopClipRect();
            ImGui.Dummy(new Vector2(fixedWidth, IconSize));
        }
        else ImGui.TextWrapped(name);
        if (personal.HasCharacter && ImGui.IsItemClicked()) itemLinks.Request(item, personal.Session);
        UiWidgets.Tooltip(UiText.Get(T.ItemLinkHint, name));
        ImGui.EndDisabled();
        ImGui.PopID();
    }

    private void Skill(FishingConditionIcon? skill, T fallback)
    {
        if (skill is null) ImGui.TextWrapped(UiText.Get(fallback));
        else UiWidgets.GameIcon(textures, skill.IconId, IconSize, UiText.Get(T.RequiredSkill, skill.Name));
    }

    private void Weather(FishingConditionIcon[] options, T label, float right)
    {
        if (options.Length == 0) { ImGui.TextUnformatted(UiText.Get(T.NoWeatherRestriction)); return; }
        for (var i = 0; i < options.Length; i++)
        {
            if (i > 0)
            {
                UiWidgets.FlowNext(IconSize + ImGui.CalcTextSize("/").X + 6, right, 4);
                ImGui.TextDisabled("/");
                UiWidgets.Tooltip(UiText.Get(T.ConditionAny));
                ImGui.SameLine(0, 4);
            }
            UiWidgets.GameIcon(textures, options[i].IconId, IconSize, UiText.Get(label, options[i].Name));
        }
    }

    private static string EtClock(double hour) => $"{(int)hour:00}:{(int)Math.Round(hour % 1 * 60):00}";
}
