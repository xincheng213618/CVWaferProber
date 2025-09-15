using System;
using System.Globalization;
using System.Windows.Data;

namespace ChipMapping.Converters
{
    public class EnumBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            bool bR= value.Equals(parameter);
            return bR;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue && parameter != null)
            {
                return parameter;
            }
            return Binding.DoNothing;
        }
    }
}