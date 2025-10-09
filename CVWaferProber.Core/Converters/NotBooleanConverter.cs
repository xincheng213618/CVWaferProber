using System.Globalization;
using System.Windows.Data;

namespace CVWaferProber.Core.Converters
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class NotBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handles null or non-boolean values by returning false
            if (value is bool boolValue)
            {
                // Inverts the boolean value
                return !boolValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack also needs to invert the value for two-way binding
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}
