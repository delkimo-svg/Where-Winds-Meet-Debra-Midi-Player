using System.Globalization;
using System.Windows.Data;
using WhereWindsMeetMidiPlayer.Helpers;

namespace WhereWindsMeetMidiPlayer.Converters;

public sealed class DurationMsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long ms)
            return TimeFormat.FromMilliseconds(ms);
        if (value is int ims)
            return TimeFormat.FromMilliseconds(ims);
        return "0:00";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
