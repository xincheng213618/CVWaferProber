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
    public class BooleanToGridLengthConverter: IValueConverter
    {
        // 显示时的宽度（默认300）
        public double TrueValue { get; set; } = 300;
        // 隐藏时的宽度（默认0）
        public double FalseValue { get; set; } = 0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible && isVisible)
            {
                return new GridLength(TrueValue);
            }
            return new GridLength(FalseValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("反向转换未实现");
        }
    }
}
