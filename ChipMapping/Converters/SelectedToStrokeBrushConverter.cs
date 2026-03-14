using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChipMapping.Converters
{
    public class SelectedToStrokeBrushConverter : IValueConverter
    {
        public static SolidColorBrush focusedBrush = new SolidColorBrush(Colors.OrangeRed);

        public static SolidColorBrush normalBrush = new SolidColorBrush(Color.FromArgb(50,125, 125, 125));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isFocused && isFocused) ? focusedBrush : normalBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}