namespace DuckbillFisher.Plugin;

// Persisted identifiers. Values 3–5 are retired and must not be reused.
public enum UiTheme { Original = 0, Graphite = 1, Paper = 2 }

internal static class UiThemeSettings
{
    public static UiTheme Normalize(UiTheme theme) => Enum.IsDefined(theme) ? theme : UiTheme.Original;
}
