using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace CVWaferProber.Converters
{
    public class ProgressBarClipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] is double width && values[1] is double value && values[2] is double maximum)
            {
                if (maximum > 0)
                {
                    double progressWidth = (value / maximum) * width;
                    return new Rect(0, 0, progressWidth, double.PositiveInfinity);
                }
            }
            return new Rect(0, 0, 0, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
