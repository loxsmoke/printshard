using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PrintShard.Converters;

/// <summary>
/// Converts an #AARRGGBB / #RRGGBB hex string (as stored in AppSettings) to a WPF Color.
/// </summary>
[ValueConversion(typeof(string), typeof(Color))]
public sealed class ColorStringToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string s)
                return (Color)ColorConverter.ConvertFromString(s);
        }
        catch { }
        return Colors.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Color c)
            return c.ToString(); // returns #AARRGGBB
        return "#00000000";
    }
}
