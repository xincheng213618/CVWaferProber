using ChipMapping.Models;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChipMapping.Converters
{
    public class BlinkFillConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 &&
                values[0] is bool isSelected &&
                values[1] is bool isBlinking &&
                values[2] is ChipStatus status)
            {
                if (isSelected && isBlinking)
                {
                    return new SolidColorBrush(Colors.White);
                }

                // 返回状态对应的颜色
                return ChipStatusTool.GetStatusBrush(status);
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}