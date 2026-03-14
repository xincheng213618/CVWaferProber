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
                values[2] is ChipStatus status)
            {
                // 如果被选中，返回白色
                if (isSelected)
                {
                    return new SolidColorBrush(Colors.DeepSkyBlue);
                }

                // 否则根据状态返回颜色
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