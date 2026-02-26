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
                return GetStatusBrush(status);
            }

            return new SolidColorBrush(Colors.Gray);
        }

        private SolidColorBrush GetStatusBrush(ChipStatus status)
        {
            return status switch
            {
                ChipStatus.WAITING => new SolidColorBrush(Colors.Blue),      // 等待中 - 蓝色
                ChipStatus.TESTING => new SolidColorBrush(Colors.Yellow),    // 测试中 - 黄色
                ChipStatus.OK => new SolidColorBrush(Colors.Green),          // 通过 - 绿色
                ChipStatus.AOI_NG => new SolidColorBrush(Colors.Red),        // AOI不良 - 红色
                ChipStatus.DW_NG => new SolidColorBrush(Colors.Orange),      // 波长不良 - 橙色
                ChipStatus.BLIND => new SolidColorBrush(Colors.Gray),        // 不亮 - 灰色
                ChipStatus.CAL_NG => new SolidColorBrush(Colors.Purple),     // 校准不良 - 紫色
                ChipStatus.I2C_NG => new SolidColorBrush(Colors.White),      // I2C异常 - 白色
                ChipStatus.AOI_LINE_NG => new SolidColorBrush(Colors.Olive), // 线缺陷 - 橄榄色
                ChipStatus.IVL_TESTING => new SolidColorBrush(Colors.LightYellow), // IVL测试中 - 浅黄
                ChipStatus.IVL_COMPLETED => new SolidColorBrush(Colors.LightGreen), // IVL完成 - 浅绿
                ChipStatus.SKIP => new SolidColorBrush(Colors.Orange),    // 跳过 - 橙色
                _ => new SolidColorBrush(Colors.Gray)                        // 默认灰色
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}