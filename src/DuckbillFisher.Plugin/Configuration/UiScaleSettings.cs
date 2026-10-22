namespace DuckbillFisher.Plugin;

internal static class UiScaleSettings
{
    public const int DefaultPercent = 100;
    public static readonly int[] Choices = [80, 90, 100, 110, 125, 150, 175, 200];
    public static int Normalize(int percent) => Choices.Contains(percent) ? percent : DefaultPercent;
}
