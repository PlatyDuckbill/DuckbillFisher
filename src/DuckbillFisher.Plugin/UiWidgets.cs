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
        var text = UiText.Get(prompt.Key == T.CollectionGuideOpenRequired ? T.GuideBannerOpen : T.GuideBannerRetry);
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
            Tooltip(prompt.Display);
            ImGui.BeginDisabled(!opener.IsSupported);
            if (ImGui.Button(UiText.Label(T.OpenFishGuide, "OpenFishGuide"))) opener.Request(personal);
            ImGui.EndDisabled();
            if (!opener.IsSupported) Tooltip(UiText.Get(T.GuideOpenUnavailable));
            if (failure is not null) ImGui.TextWrapped(failure.Display);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }
}
