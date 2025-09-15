using System;
using System.Globalization;
using System.Windows.Data;

namespace ChipMapping.Converters
{
    public class CenterOffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double size)
            {
                // 返回负的一半尺寸，使长方形居中
                return -size / 2;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}