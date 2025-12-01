using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CVWaferProber.Core.Converters
{
    internal class BooleanToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isShow)
            {
                // 勾选：返回Auto（自动宽度），未勾选：返回0
                return isShow ? double.NaN : 0.0;
            }
            return double.NaN; // 默认返回Auto
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
