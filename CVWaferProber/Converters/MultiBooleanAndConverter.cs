using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace CVWaferProber.Converters
{
    /// <summary>
    /// 多值转换器：所有布尔值都为true时返回true，否则返回false
    /// </summary>
    public class MultiBooleanAndConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // 遍历所有传入的布尔值，只要有一个为false/null，就返回false
            foreach (var value in values)
            {
                if (value is bool boolValue && !boolValue)
                {
                    return false;
                }
                // 处理null值（未初始化的绑定）
                if (value == null || value == DependencyProperty.UnsetValue)
                {
                    return false;
                }
            }
            return true;
        }

        // 反向转换无需实现（IsEnabled是单向绑定）
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
