using System.Globalization;
using System.Windows.Data;

namespace WhereWindsMeetMidiPlayer.Converters;

public sealed class EqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;
        return Equals(values[0]?.ToString(), values[1]?.ToString());
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
