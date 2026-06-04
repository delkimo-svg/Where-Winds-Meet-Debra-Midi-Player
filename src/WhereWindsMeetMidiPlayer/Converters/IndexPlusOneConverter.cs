using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace WhereWindsMeetMidiPlayer.Converters;

public sealed class IndexPlusOneConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index)
            return (index + 1).ToString();
        return "1";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
