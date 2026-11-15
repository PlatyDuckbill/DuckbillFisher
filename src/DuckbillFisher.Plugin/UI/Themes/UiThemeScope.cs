namespace DuckbillFisher.Plugin;

internal sealed class UiThemeScope : IDisposable
{
    [ThreadStatic] private static UiThemeProfile? current;
    public static UiThemeProfile? Current => current;
    private readonly UiThemeProfile? previous = current;
    private readonly GameThemeScope scope;
    public UiThemeScope(UiTheme theme, int brightness = GameThemePalette.DefaultBrightness, int depth = GameThemePalette.DefaultDepth, int transparency = GameThemePalette.DefaultTransparency)
    {
        current = UiThemes.Find(theme, brightness, depth, transparency);
        scope = new GameThemeScope(current.IsLight, brightness, depth, transparency);
    }
    public void Dispose() { scope.Dispose(); current = previous; }
}
