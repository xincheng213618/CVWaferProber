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
    public class BooleanToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 若IsChecked为True，返回100；否则返回0
            return (value is bool isChecked && isChecked) ? 200.0 : 0.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 无需反向转换，返回UnsetValue
            return DependencyProperty.UnsetValue;
        }
    }
}
