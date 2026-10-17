using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using DuckbillFisher.Core;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Shared catalog/favorite row presentation. Uses cached managed data and borrowed textures only.</summary>
internal sealed class FishRowRenderer(ITextureProvider textures)
{
    private static readonly Vector4 OpenBackground = new(0.10f, 0.48f, 0.30f, 0.28f);
    private readonly Dictionary<uint, (FishGuideRow Row, FishRowSummary Summary)> summaries = [];
    private readonly Dictionary<uint, (RowToken[] Bait, RowToken[] Conditions)> tokens = [];
    private CultureInfo? culture;
    public static float Height => ImGui.GetTextLineHeightWithSpacing() * 5 + 8;
    public static float CompactHeight => ImGui.GetTextLineHeightWithSpacing() * 4 + 8;

    public bool Draw(FishGuideRow fish, float height, bool selected, string state, string place,
        FishingWindow? forecast, DateTimeOffset now, bool usesOtherRegion = false, bool favorite = false, bool compact = false)
    {
        if (culture != UiText.Culture)
        {
            summaries.Clear();
            tokens.Clear();
            culture = UiText.Culture;
        }
        ImGui.PushID((int)fish.FishParameterId);
        var start = ImGui.GetCursorPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (!fish.IsOceanFishing && forecast is { Kind: FishingWindowKind.Open } &&
            (forecast.Start is null || forecast.Start <= now) && (forecast.End is null || now < forecast.End))
        {
            var screen = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(screen, screen + new Vector2(width, height), ImGui.GetColorU32(OpenBackground));
        }
        var clicked = ImGui.Selectable("###FishRow", selected, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(width, height));
        var next = ImGui.GetCursorPos();
        var iconSize = ImGui.GetTextLineHeightWithSpacing() * 3;
        ImGui.SetCursorPos(start + new Vector2(4, 4));
        // The host owns these immediate textures; never dispose the borrowed wrap.
        if (fish.IconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(fish.IconId), out var icon) &&
            icon.TryGetWrap(out var wrap, out _))
            ImGui.Image(wrap.Handle, new Vector2(iconSize));
        else
            ImGui.Dummy(new Vector2(iconSize));
        UiWidgets.Tooltip(fish.DisplayName);
        if (favorite)
        {
            ImGui.SetCursorPos(start + new Vector2(4, iconSize + 8));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.8f, 0.32f, 1));
            UiWidgets.Icon(FontAwesomeIcon.Star);
            ImGui.PopStyleColor();
            UiWidgets.Tooltip(UiText.Get(T.FavoriteToggle));
        }
        var textWidth = Math.Max(1, width - iconSize - 14);
        void Line(int index, string text, bool disabled = false)
        {
            ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4 + ImGui.GetTextLineHeightWithSpacing() * index));
            LineText(text, textWidth, disabled);
        }
        var name = fish.IsOceanFishing ? UiText.Get(T.OceanRowLabel, fish.DisplayName)
            : fish.Grade is FishGrade.Big or FishGrade.Legendary
                ? UiText.Get(T.RowGradeLabel, new UiMessage(fish.Grade == FishGrade.Legendary ? T.GradeLegendaryLabel : T.GradeBigLabel), fish.DisplayName)
                : fish.DisplayName;
        Line(0, name);
        Line(1, UiText.Get(T.RowStateLocation, state, place), true);
        var window = fish.IsOceanFishing ? UiText.Get(T.RowOceanWindow)
            : forecast is not null ? CompactWindow(forecast, now) : UiText.Get(T.ForecastUnknown);
        ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4 + ImGui.GetTextLineHeightWithSpacing() * 2));
        UiWidgets.Icon(FontAwesomeIcon.Clock);
        ImGui.SameLine();
        LineText(usesOtherRegion ? UiText.Get(T.RegionForecastElsewhere, window) : window,
            Math.Max(1, textWidth - ImGui.GetFrameHeight()), false);
        UiWidgets.Tooltip(forecast is null ? window : FormatWindow(forecast, now));
        if (!summaries.TryGetValue(fish.FishParameterId, out var cached) || !ReferenceEquals(cached.Row, fish))
        {
            summaries[fish.FishParameterId] = cached = (fish, FishRowSummary.Create(fish));
            tokens[fish.FishParameterId] = BuildTokens(fish);
        }
        var icons = tokens[fish.FishParameterId];
        ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4 + ImGui.GetTextLineHeightWithSpacing() * 3));
        DrawTokens(icons.Bait, textWidth, cached.Summary.Bait);
        if (!compact)
        {
            ImGui.SetCursorPos(start + new Vector2(iconSize + 14, 4 + ImGui.GetTextLineHeightWithSpacing() * 4));
            DrawTokens(icons.Conditions, textWidth,
                usesOtherRegion ? UiText.Get(T.RegionForecastElsewhere, cached.Summary.Conditions) : cached.Summary.Conditions);
        }
        ImGui.SetCursorPos(next);
        ImGui.PopID();
        return clicked;
    }

    private sealed record RowToken(uint Icon, string Text, string Hint);

    private static (RowToken[] Bait, RowToken[] Conditions) BuildTokens(FishGuideRow fish)
    {
        if (fish.Details is not { } details || fish.IsOceanFishing) return ([], []);
        var bait = new List<RowToken>();
        for (var i = 0; i < details.BaitSteps.Length; i++)
        {
            if (i > 0) bait.Add(new(0, "→", UiText.Get(T.BaitPathHeading)));
            var options = details.BaitSteps[i];
            if (options.Length > 1) bait.Add(new(0, "(", UiText.Get(T.ConditionAny)));
            for (var j = 0; j < options.Length; j++)
            {
                if (j > 0) bait.Add(new(0, "/", UiText.Get(T.ConditionAny)));
                bait.Add(new(options[j].IconId, "", options[j].Name));
            }
            if (options.Length > 1) bait.Add(new(0, ")", UiText.Get(T.ConditionAny)));
        }
        var conditions = new List<RowToken>();
        if (!details.Rule.ConditionsKnown)
            conditions.Add(new(0, UiText.Get(T.ConditionUnknown), UiText.Get(T.ConditionsPartial)));
        else
        {
            if (details.PreviousWeather.Length > 0)
            {
                AddWeather(details.PreviousWeather, T.PreviousWeatherConditionLabel);
                conditions.Add(new(0, "→", UiText.Get(T.WeatherTransitionHint)));
            }
            AddWeather(details.Weather, T.WeatherConditionLabel);
            if (details.PreviousWeather.Length > 0 && details.Weather.Length == 0)
                conditions.Add(new(0, UiText.Get(T.NoWeatherRestriction), UiText.Get(T.NoWeatherRestriction)));
        }
        if (details.Hookset is { } hook)
            conditions.Add(new(hook.IconId, "", UiText.Get(T.HooksetLabel, hook.Name)));
        if (details.Rule.ConditionsKnown && (details.Rule.StartHour != 0 || details.Rule.EndHour != 24))
            conditions.Add(new(0, UiText.Get(T.RowEtRange, EtClock(details.Rule.StartHour), EtClock(details.Rule.EndHour)), UiText.Get(T.DetailTime)));
        if (details.Predators.Length > 0) conditions.Add(new(0, UiText.Get(T.RowIntuition), UiText.Get(T.IntuitionHeading)));
        return (bait.ToArray(), conditions.ToArray());

        void AddWeather(FishingConditionIcon[] options, T label)
        {
            for (var i = 0; i < options.Length; i++)
            {
                if (i > 0) conditions.Add(new(0, "/", UiText.Get(T.ConditionAny)));
                conditions.Add(new(options[i].IconId, "", UiText.Get(label, options[i].Name)));
            }
        }
    }

    private void DrawTokens(RowToken[] content, float width, string fullText)
    {
        if (content.Length == 0) { LineText(fullText, width, true); return; }
        var start = ImGui.GetCursorPosX();
        var size = ImGui.GetTextLineHeight();
        for (var i = 0; i < content.Length; i++)
        {
            var token = content[i];
            var needed = token.Text.Length == 0 ? size : ImGui.CalcTextSize(token.Text).X;
            if (ImGui.GetCursorPosX() - start + needed + size > width) break;
            if (token.Text.Length == 0) UiWidgets.GameIcon(textures, token.Icon, size, token.Hint);
            else { ImGui.TextUnformatted(token.Text); UiWidgets.Tooltip(token.Hint); }
            ImGui.SameLine(0, 3);
        }
        UiWidgets.Icon(FontAwesomeIcon.InfoCircle);
        UiWidgets.Tooltip(fullText);
    }

    private static string EtClock(double hour) => $"{(int)hour:00}:{(int)Math.Round(hour % 1 * 60):00}";

    public static string CompactWindow(FishingWindow window, DateTimeOffset now)
    {
        var target = window.Kind == FishingWindowKind.Open ? window.End : window.Start;
        if (target is null || window.Kind is not (FishingWindowKind.Open or FishingWindowKind.Upcoming)) return FormatWindow(window, now);
        var time = target > now ? target.Value - now : TimeSpan.Zero;
        return UiText.Get(window.Kind == FishingWindowKind.Open ? T.RowEndsIn : T.RowStartsIn,
            UiText.Get(T.RowCountdown, Math.Floor(time.TotalHours), time.Minutes, time.Seconds));
    }

    private static void LineText(string text, float width, bool disabled)
    {
        var position = ImGui.GetCursorScreenPos();
        ImGui.PushClipRect(position, position + new Vector2(width, ImGui.GetTextLineHeight()), true);
        if (disabled) ImGui.TextDisabled(text);
        else ImGui.TextUnformatted(text);
        ImGui.PopClipRect();
        if (ImGui.IsItemHovered() && ImGui.CalcTextSize(text).X > width)
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    public static T CollectionKey(FishPreferencesView personal, FishCollectionView observed, uint id) =>
        personal.ManualCaught.TryGetValue(id, out var caught) ? caught ? T.CollectionManualCaught : T.CollectionManualUncaught :
        personal.Collection(id, observed) switch
        {
            CollectionStatus.Caught => T.CollectionAutomaticCaught,
            CollectionStatus.Uncaught => T.CollectionAutomaticUncaught,
            _ => T.CollectionUnknown,
        };

    public static string FormatWindow(FishingWindow window, DateTimeOffset now)
    {
        string Remaining(DateTimeOffset time)
        {
            var duration = time > now ? time - now : TimeSpan.Zero;
            return UiText.Get(T.DurationShort, Math.Floor(duration.TotalHours), duration.Minutes, duration.Seconds);
        }
        return window.Kind switch
        {
            FishingWindowKind.Always => UiText.Get(T.ForecastAlways),
            FishingWindowKind.Open when window.End is { } end => UiText.Get(T.ForecastOpen, Remaining(end)),
            FishingWindowKind.Open => UiText.Get(T.ForecastOpenNoEnd),
            FishingWindowKind.Upcoming when window.Start is { } start => UiText.Get(T.ForecastUpcoming,
                Remaining(start), start.ToLocalTime().ToString("MM-dd HH:mm", UiText.Culture)),
            FishingWindowKind.NotFound => UiText.Get(T.ForecastNotFound),
            _ => UiText.Get(T.ForecastUnknown),
        };
    }
}
