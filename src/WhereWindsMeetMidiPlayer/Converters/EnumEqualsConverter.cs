using System.Globalization;
using System.Windows.Data;

namespace WhereWindsMeetMidiPlayer.Converters;

public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is null)
            return false;
        return value?.ToString()?.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase) == true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null)
            return Enum.Parse(targetType, parameter.ToString()!, true);
        return Binding.DoNothing;
    }
}
