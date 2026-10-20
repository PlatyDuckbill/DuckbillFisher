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
internal sealed class FishRowRenderer(ITextureProvider textures, FishingMapService maps)
{
    private static readonly Vector4 OpenBackground = new(0.10f, 0.48f, 0.30f, 0.14f);
    private static readonly Vector4 OpenAccent = new(0.55f, 0.88f, 0.68f, 1);
    private readonly Dictionary<uint, (FishGuideRow Row, FishRowSummary Summary)> summaries = [];
    private readonly Dictionary<uint, (RowToken[] Bait, RowToken[] Conditions)> tokens = [];
    private CultureInfo? culture;
    private static bool IsWide(float width) => width >= ImGui.GetFontSize() * 23;
    private static int LineCount(float width) => IsWide(width) ? 3 : 4;
    public static float GetHeight(float width) => ImGui.GetTextLineHeightWithSpacing() * LineCount(width) + 8;

    public bool Draw(FishGuideRow fish, float height, bool selected, string state, string place,
        FishingWindow? forecast, DateTimeOffset now, bool usesOtherRegion = false, bool favorite = false, bool compact = false,
        FishingSpotView? spot = null, bool canOpenMap = false, CollectionStatus? collectionStatus = null)
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
        var wide = IsWide(width);
        var line = ImGui.GetTextLineHeightWithSpacing();
        var open = !fish.IsOceanFishing && forecast is { Kind: FishingWindowKind.Open } &&
            (forecast.Start is null || forecast.Start <= now) && (forecast.End is null || now < forecast.End);
        if (open)
        {
            var screen = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(screen, screen + new Vector2(width, height), ImGui.GetColorU32(OpenBackground));
        }
        var clicked = ImGui.Selectable("###FishRow", selected, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(width, height));
        if (open)
        {
            var screen = ImGui.GetItemRectMin();
            ImGui.GetWindowDrawList().AddRectFilled(screen, screen + new Vector2(2, height), ImGui.GetColorU32(OpenAccent));
        }
        var next = ImGui.GetCursorPos();
        var iconSize = line * (wide || compact ? 2 : 3);
        var informationHeight = line * LineCount(width) - ImGui.GetStyle().ItemSpacing.Y;
        var iconY = 4 + Math.Max(0, (informationHeight - iconSize) / 2);
        ImGui.SetCursorPos(start + new Vector2(4, iconY));
        UiWidgets.GameIcon(textures, fish.IconId, iconSize, fish.DisplayName);
        if (fish.Details is { Predators.Length: > 0 } intuition)
        {
            var badge = MathF.Round(ImGui.GetTextLineHeightWithSpacing() * 1.15f);
            ImGui.SetCursorPos(start + new Vector2(4 + iconSize - badge, iconY));
            UiWidgets.GameIcon(textures, intuition.Intuition?.IconId ?? 0, badge, FishingWindowPanel.IntuitionNotice(forecast));
        }
        if (favorite)
        {
            ImGui.SetCursorPos(start + new Vector2(6, iconY + iconSize - ImGui.GetTextLineHeight()));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.8f, 0.32f, 1));
            UiWidgets.Icon(FontAwesomeIcon.Star);
            ImGui.PopStyleColor();
            UiWidgets.Tooltip(UiText.Get(T.FavoriteToggle));
        }
        var textX = iconSize + 14;
        var textWidth = Math.Max(1, width - textX - 4);
        var name = fish.IsOceanFishing ? UiText.Get(T.OceanRowLabel, fish.DisplayName)
            : fish.Grade is FishGrade.Big or FishGrade.Legendary
                ? UiText.Get(T.RowGradeLabel, new UiMessage(fish.Grade == FishGrade.Legendary ? T.GradeLegendaryLabel : T.GradeBigLabel), fish.DisplayName)
                : fish.DisplayName;
        var stateWidth = Math.Min(ImGui.CalcTextSize(state).X, textWidth * 0.45f);
        ImGui.SetCursorPos(start + new Vector2(textX, 4));
        LineText(name, Math.Max(1, textWidth - stateWidth - 10), false);
        ImGui.SetCursorPos(start + new Vector2(textX + textWidth - stateWidth, 4));
        var collectionColor = collectionStatus switch
        {
            CollectionStatus.Caught => new Vector4(0.50f, 0.85f, 0.65f, 1),
            CollectionStatus.Uncaught => new Vector4(1, 0.57f, 0.52f, 1),
            _ => (Vector4?)null,
        };
        if (collectionColor is { } color) ImGui.PushStyleColor(ImGuiCol.Text, color);
        LineText(state, stateWidth, collectionColor is null);
        if (collectionColor is not null) ImGui.PopStyleColor();

        // Location is clipped first; the timer keeps its own width on the same line.
        var time = fish.IsOceanFishing ? UiText.Get(T.RowOceanWindow)
            : forecast is not null ? CompactWindow(forecast, now) : UiText.Get(T.ForecastUnavailable);
        var intuitionForecast = !fish.IsOceanFishing && fish.Details is { Predators.Length: > 0 };
        if (intuitionForecast) time = UiText.Get(T.RowIntuitionForecast, time);
        var timerWidth = Math.Min(ImGui.CalcTextSize(time).X, textWidth * 0.53f);
        var pinSize = MathF.Round(ImGui.GetTextLineHeightWithSpacing() - 1);
        var pinWidth = UiWidgets.MapPinWidth(pinSize);
        ImGui.SetCursorPos(start + new Vector2(textX, 4 + line));
        ImGui.BeginDisabled(!canOpenMap || spot is null);
        var pinClicked = UiWidgets.MapPinButton("RowMap", !canOpenMap ? UiText.Get(T.MapPlayerRequired) : spot is null
            ? UiText.Get(T.MapUnavailable) : UiText.Get(T.OpenFishingMap), pinSize);
        var pinHovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
        ImGui.EndDisabled();
        if (pinClicked && spot is not null) maps.Request(spot);
        ImGui.SetCursorPos(start + new Vector2(textX + pinWidth + 5, 4 + line));
        LineText(place, Math.Max(1, textWidth - timerWidth - pinWidth - 16), true);
        ImGui.SetCursorPos(start + new Vector2(textX + textWidth - timerWidth, 4 + line));
        if (open) ImGui.PushStyleColor(ImGuiCol.Text, OpenAccent);
        LineText(time, timerWidth, false);
        if (open) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 30);
            ImGui.TextUnformatted(usesOtherRegion ? UiText.Get(T.RegionForecastElsewhere, time)
                : forecast is null ? time : FormatWindow(forecast, now));
            if (intuitionForecast) ImGui.TextUnformatted(FishingWindowPanel.IntuitionNotice(forecast));
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        if (!summaries.TryGetValue(fish.FishParameterId, out var cached) || !ReferenceEquals(cached.Row, fish))
        {
            summaries[fish.FishParameterId] = cached = (fish, FishRowSummary.Create(fish));
            tokens[fish.FishParameterId] = BuildTokens(fish);
        }
        var icons = tokens[fish.FishParameterId];
        var baitWidth = wide ? BaitWidth(icons.Bait, icons.Conditions, cached.Summary, textWidth) : textWidth;
        ImGui.SetCursorPos(start + new Vector2(textX, 4 + line * 2));
        DrawTokens(icons.Bait, baitWidth, cached.Summary.Bait);
        ImGui.SetCursorPos(start + new Vector2(textX + (wide ? baitWidth + 6 : 0), 4 + line * (wide ? 2 : 3)));
        DrawTokens(icons.Conditions, wide ? Math.Max(1, textWidth - baitWidth - 6) : textWidth,
            usesOtherRegion ? UiText.Get(T.RegionForecastElsewhere, cached.Summary.Conditions) : cached.Summary.Conditions,
            forceMore: usesOtherRegion);
        ImGui.SetCursorPos(next);
        ImGui.PopID();
        // A map click is its own action and must not also open/select the detail view.
        return clicked && !pinClicked && !pinHovered;
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
        if (details.Hookset is { } hook)
            bait.Add(new(hook.IconId, "", UiText.Get(T.HooksetLabel, hook.Name)));
        else
        {
            var hookName = UiText.Get(details.Rule.Hookset switch
                { "Precision" => T.HooksetPrecision, "Powerful" => T.HooksetPowerful, _ => T.ConditionUnknown });
            bait.Add(new(0, hookName, UiText.Get(T.HooksetLabel, hookName)));
        }
        var tug = details.Rule.Tug switch { "light" => "!", "medium" => "!!", "heavy" => "!!!", _ => null };
        if (tug is not null) bait.Add(new(0, tug, UiText.Get(T.StepBite, tug)));
        var conditions = new List<RowToken>();
        if (!details.Rule.ConditionsKnown)
            conditions.Add(new(0, UiText.Get(T.ConditionUnknown), details.ConditionWarning.Display));
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
        if (details.Rule.ConditionsKnown && (details.Rule.StartHour != 0 || details.Rule.EndHour != 24))
            conditions.Add(new(0, UiText.Get(T.RowEtRange, EtClock(details.Rule.StartHour), EtClock(details.Rule.EndHour)), UiText.Get(T.DetailTime)));
        if (details.Rule.SnaggingRequired)
            conditions.Add(new(details.Snagging?.IconId ?? 0, details.Snagging is null ? UiText.Get(T.SnaggingRequired) : "",
                UiText.Get(T.RequiredSkill, details.Snagging?.Name ?? UiText.Get(T.SnaggingRequired))));
        if (details.Rule.Lure.Length > 0)
            conditions.Add(new(details.Lure?.IconId ?? 0, details.Lure is null ? UiText.Get(T.ConditionUnknown) : "",
                UiText.Get(T.RequiredSkill, details.Lure?.Name ?? UiText.Get(T.ConditionUnknown))));
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

    private static float TokenWidth(RowToken[] content) => content.Sum(token =>
        token.Text.Length == 0 ? ImGui.GetTextLineHeight() : ImGui.CalcTextSize(token.Text).X) + Math.Max(0, content.Length - 1) * 3;

    private static float BaitWidth(RowToken[] bait, RowToken[] conditions, FishRowSummary summary, float width)
    {
        var baitNatural = bait.Length == 0 ? ImGui.CalcTextSize(summary.Bait).X : TokenWidth(bait);
        var conditionNatural = conditions.Length == 0 ? ImGui.CalcTextSize(summary.Conditions).X : TokenWidth(conditions);
        var available = Math.Max(1, width - 6);
        if (baitNatural + conditionNatural <= available) return baitNatural;
        var minimum = Math.Min(baitNatural, ImGui.GetTextLineHeight() * 2);
        var maximum = Math.Max(minimum, available - Math.Min(conditionNatural, ImGui.GetFontSize() * 7));
        return Math.Clamp(available * baitNatural / Math.Max(1, baitNatural + conditionNatural), minimum, maximum);
    }

    private void DrawTokens(RowToken[] content, float width, string fullText, bool forceMore = false)
    {
        var start = ImGui.GetCursorPos();
        var markerWidth = ImGui.CalcTextSize("…").X;
        var natural = content.Length == 0 ? ImGui.CalcTextSize(fullText).X : TokenWidth(content);
        var more = forceMore || natural > width;
        var available = more ? Math.Max(0, width - markerWidth - 3) : width;
        var used = 0f;
        if (content.Length == 0) LineText(fullText, Math.Max(1, available), true);
        for (var i = 0; i < content.Length; i++)
        {
            var token = content[i];
            var needed = token.Text.Length == 0 ? ImGui.GetTextLineHeight() : ImGui.CalcTextSize(token.Text).X;
            var nextWidth = used + (i > 0 ? 3 : 0) + needed;
            if (nextWidth > available) break;
            if (i > 0) ImGui.SameLine(0, 3);
            if (token.Text.Length == 0) UiWidgets.GameIcon(textures, token.Icon, ImGui.GetTextLineHeight(), token.Hint);
            else { ImGui.TextUnformatted(token.Text); UiWidgets.Tooltip(token.Hint); }
            used = nextWidth;
        }
        if (more)
        {
            ImGui.SetCursorPos(start + new Vector2(Math.Min(available + 3, Math.Max(0, width - markerWidth)), 0));
            LineText("…", markerWidth, false);
            UiWidgets.Tooltip(fullText);
        }
    }

    private static string EtClock(double hour) => $"{(int)hour:00}:{(int)Math.Round(hour % 1 * 60):00}";

    public static string CompactWindow(FishingWindow window, DateTimeOffset now)
    {
        if (window.Kind == FishingWindowKind.NotFound)
            return UiText.Get(T.RowForecastNotFound, FishingAvailability.SearchDays);
        var target = window.Kind == FishingWindowKind.Open ? window.End : window.Start;
        if (target is null || window.Kind is not (FishingWindowKind.Open or FishingWindowKind.Upcoming))
            return UiText.Get(window.Kind switch
            {
                FishingWindowKind.Always => T.ForecastAlways,
                FishingWindowKind.Open => T.WindowOpen,
                _ => T.ForecastUnavailable,
            });
        var time = target > now ? target.Value - now : TimeSpan.Zero;
        return UiText.Get(window.Kind == FishingWindowKind.Open ? T.RowEndsIn : T.RowStartsIn,
            Countdown(time));
    }

    public static string Countdown(TimeSpan duration, bool compact = true) => duration.TotalDays >= 1
        ? UiText.Get(T.DurationDays, Math.Floor(duration.TotalDays), duration.Hours, duration.Minutes, duration.Seconds)
        : UiText.Get(compact ? T.RowCountdown : T.DurationShort, Math.Floor(duration.TotalHours), duration.Minutes, duration.Seconds);

    private static void LineText(string text, float width, bool disabled)
    {
        var position = ImGui.GetCursorScreenPos();
        width = Math.Max(1, width);
        var size = new Vector2(width, ImGui.GetTextLineHeight());
        ImGui.PushClipRect(position, position + size, true);
        ImGui.GetWindowDrawList().AddText(position, ImGui.GetColorU32(disabled ? ImGuiCol.TextDisabled : ImGuiCol.Text), text);
        ImGui.PopClipRect();
        ImGui.Dummy(size);
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
        if (window.Issue == FishingForecastIssue.FishEyesSupportUnknown) return UiText.Get(T.ForecastFishEyesUnknown);
        string Remaining(DateTimeOffset time)
        {
            var duration = time > now ? time - now : TimeSpan.Zero;
            return Countdown(duration, compact: false);
        }
        return window.Kind switch
        {
            FishingWindowKind.Always => UiText.Get(T.ForecastAlways),
            FishingWindowKind.Open when window.End is { } end => UiText.Get(T.ForecastOpen, Remaining(end)),
            FishingWindowKind.Open => UiText.Get(T.ForecastOpenNoEnd),
            FishingWindowKind.Upcoming when window.Start is { } start => UiText.Get(T.ForecastUpcoming,
                Remaining(start), start.ToLocalTime().ToString("MM-dd HH:mm", UiText.Culture)),
            FishingWindowKind.NotFound => UiText.Get(T.ForecastNotFound, FishingAvailability.SearchDays),
            _ => UiText.Get(T.ForecastUnknown),
        };
    }
}
