using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using DuckbillFisher.Plugin.Localization;
using T = DuckbillFisher.Plugin.Localization.TextKey;

namespace DuckbillFisher.Plugin;

/// <summary>Presentation helpers only: borrowed textures, scaled controls, and contextual help.</summary>
internal static class UiWidgets
{
    public const float BackgroundAlpha = 0.94f;

    public static string SpotLabel(FishingTrackerView view) => view.Spot is { } spot
        ? view.IsLastSpot ? UiText.Get(T.TimerLastSpot, spot.Name) : spot.Name
        : UiText.Get(view.SpotState switch
        {
            FishingSpotState.Unknown => T.TimerUnknownSpot,
            FishingSpotState.AwaitingIdentification => T.TimerSpotIdentifying,
            _ => T.TimerSpotPending,
        });

    public static void FlowNext(float width, float right, float spacing = 8)
    {
        ImGui.SameLine(0, spacing);
        if (ImGui.GetCursorPosX() + width > right) ImGui.NewLine();
    }

    public static void Tooltip(string text)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) return;
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 30);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    public static void Icon(FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextUnformatted(icon.ToIconString());
        ImGui.PopFont();
    }

    public static bool IconButton(FontAwesomeIcon icon, string id, string tooltip, bool active = false, float size = 0)
    {
        if (active) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.8f, 0.32f, 1));
        ImGui.PushFont(UiBuilder.IconFont);
        if (size > 0) ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        var clicked = ImGui.Button($"{icon.ToIconString()}###{id}", new Vector2(size > 0 ? size : ImGui.GetFrameHeight()));
        if (size > 0) ImGui.PopStyleVar();
        ImGui.PopFont();
        if (active) ImGui.PopStyleColor();
        Tooltip(tooltip);
        return clicked;
    }

    public static bool ActionButton(FontAwesomeIcon icon, T key, string id, float width = 0)
    {
        var label = UiText.Get(key);
        if (width <= 0) width = ImGui.GetFrameHeight() + ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2;
        var clicked = ImGui.Button($"###{id}", new Vector2(width, ImGui.GetFrameHeight()));
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var contentWidth = ImGui.GetFrameHeight() + ImGui.CalcTextSize(label).X;
        var origin = min + new Vector2(Math.Max(ImGui.GetStyle().FramePadding.X, (width - contentWidth) / 2), ImGui.GetStyle().FramePadding.Y);
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(ImGuiCol.Text);
        draw.PushClipRect(min, max, true);
        draw.AddText(UiBuilder.IconFont, ImGui.GetFontSize(), origin, color, icon.ToIconString());
        draw.AddText(origin + new Vector2(ImGui.GetFrameHeight(), 0), color, label);
        draw.PopClipRect();
        if (contentWidth + ImGui.GetStyle().FramePadding.X * 2 > width) Tooltip(label);
        return clicked;
    }

    public static bool GameIcon(ITextureProvider textures, uint iconId, float size, string tooltip)
    {
        var available = false;
        // Borrowed wraps belong to Dalamud. Missing textures keep a visible, named fallback.
        if (iconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var texture) &&
            texture.TryGetWrap(out var image, out _))
        {
            var box = new Vector2(size);
            var scale = size / Math.Max(1, Math.Max(image.Width, image.Height));
            var actual = new Vector2(Math.Max(1, MathF.Round(image.Width * scale)), Math.Max(1, MathF.Round(image.Height * scale)));
            var position = ImGui.GetCursorScreenPos() + (box - actual) / 2;
            ImGui.GetWindowDrawList().AddImage(image.Handle, position, position + actual);
            ImGui.Dummy(box);
            available = true;
        }
        else
        {
            var start = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(size));
            ImGui.GetWindowDrawList().AddText(start + new Vector2(size * 0.25f, 0), ImGui.GetColorU32(ImGuiCol.TextDisabled), "?");
        }
        Tooltip(tooltip);
        return available;
    }

    public static bool GameIconButton(ITextureProvider textures, uint iconId, string id, string tooltip, bool active)
    {
        if (active) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.18f, 0.48f, 0.62f, 1));
        var size = ImGui.GetFrameHeight();
        var clicked = ImGui.Button($"###{id}", new Vector2(size));
        if (active) ImGui.PopStyleColor();
        var start = ImGui.GetItemRectMin() + new Vector2(2);
        if (iconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var texture) &&
            texture.TryGetWrap(out var wrap, out _))
            ImGui.GetWindowDrawList().AddImage(wrap.Handle, start, start + new Vector2(size - 4), Vector2.Zero, Vector2.One,
                ImGui.GetColorU32(active ? Vector4.One : new Vector4(1, 1, 1, 0.45f)));
        else ImGui.GetWindowDrawList().AddText(start, ImGui.GetColorU32(ImGuiCol.Text), "?");
        Tooltip(tooltip);
        return clicked;
    }

    public static float MapPinWidth(float height) => height + 4;

    public static bool MapPinButton(string id, string tooltip, float height)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushFont(UiBuilder.IconFont);
        var clicked = ImGui.Button($"{FontAwesomeIcon.MapMarkedAlt.ToIconString()}###{id}", new Vector2(MapPinWidth(height), height));
        ImGui.PopFont();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        Tooltip(tooltip);
        return clicked;
    }

    public static bool FavoriteButton(string id, bool favorite)
    {
        var size = ImGui.GetFrameHeight();
        var clicked = ImGui.Button($"###{id}", new Vector2(size));
        var center = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        var color = ImGui.GetColorU32(favorite ? new Vector4(1, 0.8f, 0.32f, 1) : ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
        Span<Vector2> points = stackalloc Vector2[10];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = -MathF.PI / 2 + i * MathF.PI / 5;
            var radius = size * (i % 2 == 0 ? 0.32f : 0.14f);
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
        var draw = ImGui.GetWindowDrawList();
        for (var i = 0; i < points.Length; i++)
        {
            var next = points[(i + 1) % points.Length];
            if (favorite) draw.AddTriangleFilled(center, points[i], next, color);
            else draw.AddLine(points[i], next, color, 1.3f);
        }
        Tooltip(UiText.Get(favorite ? T.AlertRemoveFavorite : T.FavoriteToggle));
        return clicked;
    }

    public static void Heading(T key)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted(UiText.Get(key));
        ImGui.Spacing();
    }

    public static void GuideBanner(FishCollectionView collection, FishPreferencesView personal, FishGuideOpenService opener)
    {
        if (collection.GuidePrompt(personal.Session, personal.HasCharacter) is not { } prompt) return;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.42f, 0.29f, 0.06f, 0.72f));
        var text = opener.IsSupported
            ? UiText.Get(prompt.Key == T.CollectionGuideOpenRequired ? T.GuideBannerOpen : T.GuideBannerRetry)
            : UiText.Get(T.NativeFeaturesBlocked, opener.CompatibilityReason.Display);
        if (personal.CollectionSavedUtc is { } savedAt && personal.SavedCollection.Count > 0)
            text = UiText.Get(T.CollectionBackupBanner, text, savedAt.ToLocalTime(), personal.SavedCollection.Count);
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() - 24);
        var failure = opener.Failure(personal.Session);
        var height = Math.Max(ImGui.GetFrameHeight(), ImGui.CalcTextSize(text, false, width).Y) + 16
            + ImGui.GetFrameHeightWithSpacing();
        if (failure is not null) height += ImGui.CalcTextSize(failure.Display, false, width).Y + ImGui.GetStyle().ItemSpacing.Y;
        if (ImGui.BeginChild("GuideSyncBanner", new Vector2(0, height), true,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            Icon(FontAwesomeIcon.ExclamationTriangle);
            ImGui.SameLine();
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            Tooltip(opener.IsSupported ? prompt.Display : opener.CompatibilityReason.Display);
            ImGui.BeginDisabled(!opener.IsSupported);
            if (ImGui.Button(UiText.Label(T.OpenFishGuide, "OpenFishGuide"))) opener.Request(personal);
            ImGui.EndDisabled();
            if (!opener.IsSupported) Tooltip(opener.CompatibilityReason.Display);
            if (failure is not null) ImGui.TextWrapped(failure.Display);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    public static void GuideWarning(FishCollectionView collection, FishPreferencesView personal, FishGuideOpenService opener)
    {
        if (collection.GuidePrompt(personal.Session, personal.HasCharacter) is not { } prompt) return;
        var failure = opener.Failure(personal.Session);
        var saved = personal.CollectionSavedUtc is not null && personal.SavedCollection.Count > 0;
        var key = !opener.IsSupported ? T.GuideWarningUnsupported
            : failure is not null || prompt.Key == T.CollectionGuideRetryRequired ? T.GuideWarningRetry
            : saved ? T.GuideWarningSaved : T.GuideWarningRequired;
        var text = UiText.Get(key);
        var detail = saved ? UiText.Get(T.CollectionBackupBanner, prompt.Display,
            personal.CollectionSavedUtc!.Value.ToLocalTime(), personal.SavedCollection.Count) : prompt.Display;
        var hints = new List<string> { detail };
        if (!opener.IsSupported) hints.Add(opener.CompatibilityReason.Display);
        if (failure is not null) hints.Add(failure.Display);
        var hint = string.Join("\n", hints);
        var frame = ImGui.GetFrameHeight();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.42f, 0.29f, 0.06f, 0.72f));
        if (ImGui.BeginChild("GlobalGuideWarning", new Vector2(0, frame), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var available = ImGui.GetContentRegionAvail().X;
            var label = UiText.Get(T.OpenFishGuide);
            var buttonWidth = Math.Min(ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2, available * 0.35f);
            var gap = ImGui.GetStyle().ItemSpacing.X;
            ImGui.AlignTextToFramePadding();
            Icon(FontAwesomeIcon.ExclamationTriangle);
            GuideWarningHint(hint);
            ImGui.SameLine();
            var textWidth = Math.Max(1, available - frame - buttonWidth - gap * 2);
            var origin = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().PushClipRect(origin, origin + new Vector2(textWidth, frame), true);
            ImGui.GetWindowDrawList().AddText(origin + new Vector2(0, ImGui.GetStyle().FramePadding.Y), ImGui.GetColorU32(ImGuiCol.Text), text);
            ImGui.GetWindowDrawList().PopClipRect();
            ImGui.Dummy(new Vector2(textWidth, frame));
            GuideWarningHint(hint);
            ImGui.SameLine();
            ImGui.BeginDisabled(!opener.IsSupported);
            if (ImGui.Button(UiText.Label(T.OpenFishGuide, "OpenFishGuide"), new Vector2(buttonWidth, frame))) opener.Request(personal);
            ImGui.EndDisabled();
            GuideWarningHint(hint);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private static void GuideWarningHint(string message)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) return;
        var width = Math.Min(ImGui.GetFontSize() * 30, ImGui.GetMainViewport().WorkSize.X * 0.8f);
        ImGui.SetNextWindowSize(new Vector2(width + ImGui.GetStyle().WindowPadding.X * 2, 0));
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        ImGui.TextUnformatted(message);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
