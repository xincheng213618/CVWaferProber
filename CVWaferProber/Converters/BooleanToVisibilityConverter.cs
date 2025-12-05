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
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }

        // 先注释反向转换，测试正向绑定是否生效
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // return (value is Visibility v && v == Visibility.Visible);
            throw new NotImplementedException();
        }

    }
}
