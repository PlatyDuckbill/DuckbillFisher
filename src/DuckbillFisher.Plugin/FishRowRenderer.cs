using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
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
    private CultureInfo? culture;
    public static float Height => ImGui.GetTextLineHeightWithSpacing() * 5 + 8;

    public bool Draw(FishGuideRow fish, float height, bool selected, string state, string place,
        FishingWindow? forecast, DateTimeOffset now, bool usesOtherRegion = false)
    {
        if (culture != UiText.Culture)
        {
            summaries.Clear();
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
        var clicked = ImGui.Selectable("###FishRow", selected, ImGuiSelectableFlags.None, new Vector2(width, height));
        var next = ImGui.GetCursorPos();
        var iconSize = ImGui.GetTextLineHeightWithSpacing() * 3;
        ImGui.SetCursorPos(start + new Vector2(4, 4));
        // The host owns these immediate textures; never dispose the borrowed wrap.
        if (fish.IconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(fish.IconId), out var icon) &&
            icon.TryGetWrap(out var wrap, out _))
            ImGui.Image(wrap.Handle, new Vector2(iconSize));
        else
            ImGui.Dummy(new Vector2(iconSize));
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
            : forecast is not null ? FormatWindow(forecast, now) : UiText.Get(T.ForecastUnknown);
        Line(2, usesOtherRegion ? UiText.Get(T.RegionForecastElsewhere, window) : window);
        if (!summaries.TryGetValue(fish.FishParameterId, out var cached) || !ReferenceEquals(cached.Row, fish))
            summaries[fish.FishParameterId] = cached = (fish, FishRowSummary.Create(fish));
        Line(3, cached.Summary.Bait);
        Line(4, usesOtherRegion ? UiText.Get(T.RegionForecastElsewhere, cached.Summary.Conditions) : cached.Summary.Conditions, true);
        ImGui.SetCursorPos(next);
        ImGui.PopID();
        return clicked;
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
