using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WhereWindsMeetMidiPlayer.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Strings bind here for search placeholders: non-empty text counts as true.
        var visible = value is true or string { Length: > 0 };
        if (Invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
