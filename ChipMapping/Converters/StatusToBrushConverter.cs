using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChipMapping.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChipStatus status)
            {
                return ChipStatusTool.GetStatusBrush(status);
            }
            return new SolidColorBrush(Color.FromRgb(33, 150, 243));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}