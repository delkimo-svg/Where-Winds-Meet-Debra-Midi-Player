using System.Globalization;
using System.Windows.Data;
using WhereWindsMeetMidiPlayer.Helpers;

namespace WhereWindsMeetMidiPlayer.Converters;

public sealed class CatalogueDurationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ms = value switch
        {
            long l => l,
            int i => i,
            _ => 0L
        };

        return ms > 0 ? TimeFormat.FromMilliseconds(ms) : "—";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
