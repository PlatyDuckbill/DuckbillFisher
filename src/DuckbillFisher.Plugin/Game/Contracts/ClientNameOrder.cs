using System.Globalization;
using Dalamud.Game;

namespace DuckbillFisher.Plugin;

/// <summary>
/// Sort order for game names, which come from the client's sheet language. The UI language never changes it.
/// The Korean value exists only in KR Dalamud builds, so the runtime enum name is compared.
/// </summary>
internal static class ClientNameOrder
{
    private static StringComparer comparer = StringComparer.Create(CultureInfo.InvariantCulture, true);

    public static StringComparer Comparer => Volatile.Read(ref comparer);

    public static void Initialize(ClientLanguage language) => Volatile.Write(ref comparer, StringComparer.Create(
        CultureInfo.GetCultureInfo(language.ToString() switch
        {
            "Japanese" => "ja-JP", "English" => "en-US", "German" => "de-DE", "French" => "fr-FR", "Korean" => "ko-KR",
            _ => "",
        }), true));
}
