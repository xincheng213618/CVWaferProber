using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChipMapping.Converters
{
    public class SelectedToStrokeBrushConverter : IValueConverter
    {
        public static SolidColorBrush solidColorBrush = Brushes.Blue;

        public static SolidColorBrush solidColorBrush1 = new SolidColorBrush(Color.FromArgb(50,125, 125, 125));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isSelected && isSelected) ? solidColorBrush : solidColorBrush1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}