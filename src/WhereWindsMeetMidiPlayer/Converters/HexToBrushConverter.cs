using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WhereWindsMeetMidiPlayer.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Brushes.Transparent;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex.Trim());
            if (parameter is string param && double.TryParse(param, NumberStyles.Float, culture, out var alpha))
                color = Color.FromArgb(
                    (byte)Math.Clamp(alpha * 255, 0, 255),
                    color.R,
                    color.G,
                    color.B);

            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(74, 158, 255));
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
