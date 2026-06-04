using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhereWindsMeetMidiPlayer.Helpers;

/// <summary>
/// Fills the title bar: uniform scale (no vertical squeeze), horizontal crop/extend as needed.
/// </summary>
public static class HeaderDecorBrush
{
    public static ImageBrush? CreateFill(ImageSource? source, double opacity = 1)
    {
        if (source is null)
            return null;

        var brush = new ImageBrush(source)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center,
            Opacity = opacity
        };
        brush.Freeze();
        return brush;
    }
}
