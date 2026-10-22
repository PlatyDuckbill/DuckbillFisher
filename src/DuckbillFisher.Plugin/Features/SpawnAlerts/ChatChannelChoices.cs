using System.Globalization;
using Dalamud.Game.Text;
using DuckbillFisher.Plugin.Localization;

namespace DuckbillFisher.Plugin;

/// <summary>Common local display channels; legacy names remain readable in delivery history.</summary>
internal static class ChatChannelChoices
{
    public static readonly XivChatType[] All =
    [
        XivChatType.SystemMessage, XivChatType.Echo, XivChatType.Say, XivChatType.Party,
        XivChatType.Alliance, XivChatType.FreeCompany,
        XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4,
        XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8,
        XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3, XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6, XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8,
    ];
    private static readonly Dictionary<XivChatType, TextKey> Keys = Enum.GetValues<XivChatType>().Where(IsKnown).Distinct().ToDictionary(type => type,
        type => Enum.TryParse<TextKey>($"Chat{type}", out var key) ? key : TextKey.ChatChannelFallback);
    private static CultureInfo? culture;
    private static string[] names = [];

    public static bool Supports(XivChatType type) => All.Contains(type);
    public static bool IsKnown(XivChatType type) => type != XivChatType.None && Enum.IsDefined(type);
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
