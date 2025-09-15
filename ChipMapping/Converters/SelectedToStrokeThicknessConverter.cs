using System;
using System.Globalization;
using System.Windows.Data;

namespace ChipMapping.Converters
{
    public class SelectedToStrokeThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 显著增大边框粗细
            return (value is bool isSelected && isSelected) ? 2.0 : 0.5;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}