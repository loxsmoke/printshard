using System.Globalization;
using System.Windows.Data;

namespace PrintShard.Converters;

/// <summary>
/// Allows radio buttons to bind to an enum property.
/// ConverterParameter must be the enum value the radio button represents.
/// </summary>
[ValueConversion(typeof(Enum), typeof(bool))]
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.Equals(parameter) ?? false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? parameter : Binding.DoNothing;
}
