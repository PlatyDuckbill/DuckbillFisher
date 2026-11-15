namespace DuckbillFisher.Plugin;

internal sealed class UiThemeScope : IDisposable
{
    [ThreadStatic] private static UiThemeProfile? current;
    public static UiThemeProfile? Current => current;
    private readonly UiThemeProfile? previous = current;
    private readonly GameThemeScope scope;
    public UiThemeScope(UiTheme theme)
    {
        current = UiThemes.Find(theme);
        scope = new GameThemeScope(current.IsLight);
    }
    public void Dispose() { scope.Dispose(); current = previous; }
}
