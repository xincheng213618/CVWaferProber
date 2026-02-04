using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace CVWPFSpectrometerCtrl.Converters
{
    /// <summary>
    /// 字符串匹配可见性转换器：值与参数一致则显示（Visible），否则隐藏（Collapsed）
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string valStr && parameter is string paramStr)
            {
                return valStr.Equals(paramStr, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        // 反向转换无需实现（TwoWay绑定用不到）
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
