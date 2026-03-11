using System;
using System.Globalization;
using System.Windows.Data;

namespace CVWaferProber.Converters
{
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
