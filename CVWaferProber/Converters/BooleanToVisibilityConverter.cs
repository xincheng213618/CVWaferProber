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
        // 布尔值转Visibility转换器（true→Visible，false→Collapsed）
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isVisible && isVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
       
    }
}
