using System.Globalization;
using Dalamud.Game.Text;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

/// <summary>Every channel defined by the installed SDK, except the absence of a channel.</summary>
internal static class ChatChannelChoices
{
    public static readonly XivChatType[] All = Enum.GetValues<XivChatType>().Where(Supports).Distinct().ToArray();
    private static readonly Dictionary<XivChatType, TextKey> Keys = All.ToDictionary(type => type,
        type => Enum.TryParse<TextKey>($"Chat{type}", out var key) ? key : TextKey.ChatChannelFallback);
    private static CultureInfo? culture;
    private static string[] names = [];

    public static bool Supports(XivChatType type) => type != XivChatType.None && Enum.IsDefined(type);
    public static string Name(XivChatType type) => Keys.TryGetValue(type, out var key) && key != TextKey.ChatChannelFallback
        ? UiText.Get(key) : UiText.Get(TextKey.ChatChannelFallback, type.ToString());

    public static string[] Names
    {
        get
        {
            if (culture != UiText.Culture)
            {
                culture = UiText.Culture;
                names = All.Select(Name).ToArray();
            }
            return names;
        }
    }
}
