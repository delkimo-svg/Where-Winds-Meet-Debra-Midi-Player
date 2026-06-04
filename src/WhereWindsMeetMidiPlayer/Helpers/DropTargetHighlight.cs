using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class DropTargetHighlight
{
    public static void Apply(Border border, bool active)
    {
        if (active)
        {
            border.SetResourceReference(Border.BorderBrushProperty, "Brush.DropTargetActiveBorder");
            border.SetResourceReference(Border.BackgroundProperty, "Brush.DropTargetActiveBackground");
            border.BorderThickness = new Thickness(2);
            return;
        }

        border.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        border.SetResourceReference(Border.BackgroundProperty, "Brush.CardBackground");
        border.BorderThickness = new Thickness(1);
    }

    public static Brush ResolveBrush(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;

    public static Brush ActiveBorderBrush => ResolveBrush("Brush.DropTargetActiveBorder", new SolidColorBrush(Color.FromRgb(184, 74, 104)));

    public static Brush ActiveBackgroundBrush =>
        ResolveBrush("Brush.DropTargetActiveBackground", new SolidColorBrush(Color.FromArgb(0xEE, 252, 228, 236)));
}
