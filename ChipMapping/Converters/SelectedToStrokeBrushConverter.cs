using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChipMapping.Converters
{
    public class SelectedToStrokeBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isSelected && isSelected) ?
                new SolidColorBrush(Color.FromRgb(0, 100, 255)) : // 更亮的蓝色
                new SolidColorBrush(Color.FromRgb(51, 51, 51));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}