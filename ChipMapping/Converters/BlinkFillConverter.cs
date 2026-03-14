using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChipMapping.Converters
{
    public class BlinkFillConverter : IMultiValueConverter
    {
        private static readonly SolidColorBrush _focusedBrush = new SolidColorBrush(Color.FromRgb(0, 200, 255));
        private static readonly SolidColorBrush _selectedBrush = new SolidColorBrush(Colors.White);

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 && values[0] is bool isFocused && values[1] is bool isSelected && values[2] is ChipStatus status)
            {
                if (isFocused)
                    return _focusedBrush;
                if (isSelected)
                    return _selectedBrush;
                return ChipStatusTool.GetStatusBrush(status);
            }

            if (values.Length >= 2 && values[0] is bool isSelected2 && values[1] is ChipStatus status2)
            {
                return ChipStatusTool.GetStatusBrush(status2);
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}