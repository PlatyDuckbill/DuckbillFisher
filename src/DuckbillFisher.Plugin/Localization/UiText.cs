using System.Globalization;
using System.Resources;

namespace DuckbillFisher.Plugin.Localization;

public enum UiLanguage { Korean, English }

internal static class UiText
{
    private static readonly ResourceManager Resources = new("DuckbillFisher.Plugin.Localization.Strings", typeof(UiText).Assembly);
    private static CultureInfo culture = CultureInfo.GetCultureInfo("ko");

    // Never change the host game's CurrentCulture or CurrentUICulture.
    public static CultureInfo Culture => Volatile.Read(ref culture);
    public static UiLanguage Language => Culture.TwoLetterISOLanguageName == "ko" ? UiLanguage.Korean : UiLanguage.English;

    /// <summary>
    /// Interface language for a first run without saved settings. Korean on the KR client or a Korean Dalamud UI,
    /// English otherwise. The Korean ClientLanguage value exists only in KR Dalamud builds, so the build compiled
    /// against global references compares the runtime enum name, not a symbol. Saved settings always win.
    /// </summary>
    public static UiLanguage DefaultFor(Dalamud.Game.ClientLanguage client, string? dalamudUiLanguage) =>
        client.ToString() == "Korean" || string.Equals(dalamudUiLanguage, "ko", StringComparison.OrdinalIgnoreCase)
            ? UiLanguage.Korean : UiLanguage.English;

    public static void SetLanguage(UiLanguage language) =>
        Volatile.Write(ref culture, CultureInfo.GetCultureInfo(language == UiLanguage.Korean ? "ko" : "en"));

    public static string Get(TextKey key, params object[] arguments) => Format(key, Culture, arguments);

    public static string Label(TextKey key, string id) => $"{Get(key)}###{id}";

    public static string Format(TextKey key, CultureInfo targetCulture, params object[] arguments)
    {
        var template = Resources.GetString(key.ToString(), targetCulture)
            ?? Resources.GetString(key.ToString(), CultureInfo.InvariantCulture) ?? key.ToString();
        if (arguments.Length == 0)
            return template;
        var resolved = arguments.Select(value => value is UiMessage message ? message.Resolve(targetCulture) : value).ToArray();
        return string.Format(targetCulture, template, resolved);
    }
}

/// <summary>Store a message's identity and values, so cached state survives a UI language change.</summary>
internal sealed record UiMessage(TextKey Key, params object[] Arguments)
{
    public string Display => Resolve(UiText.Culture);
    public string Diagnostic => Resolve(CultureInfo.InvariantCulture);
    public string Resolve(CultureInfo culture) => UiText.Format(Key, culture, Arguments);
    public bool SameAs(UiMessage other) => Key == other.Key && Arguments.SequenceEqual(other.Arguments);
}

internal sealed record LocalizedLogEntry(TimeSpan Elapsed, UiMessage Message)
{
    public string Display => string.Create(CultureInfo.InvariantCulture, $"+{Elapsed.TotalSeconds:F3}s {Message.Display}");
    public string Diagnostic => string.Create(CultureInfo.InvariantCulture, $"+{Elapsed.TotalSeconds:F3}s [{Message.Key}] {Message.Diagnostic}");
}
