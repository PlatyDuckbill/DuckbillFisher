namespace DuckbillFisher.Plugin;

// 0/1/2 are legacy saved identifiers; 3–5 stay retired. Only Smoke and Pearl are offered.
public enum UiTheme { Original = 0, Graphite = 1, Paper = 2, Smoke = 6, Pearl = 7 }
internal static class UiThemeSettings
{
    public static UiTheme Normalize(UiTheme theme) => theme is UiTheme.Paper or UiTheme.Pearl ? UiTheme.Pearl : UiTheme.Smoke;
}
