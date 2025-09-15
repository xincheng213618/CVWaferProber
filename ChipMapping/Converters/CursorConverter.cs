using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;


namespace ChipMapping.Converters
{
    public class BoolToCursorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                return isEnabled ? Cursors.Arrow : Cursors.No;
            }
            return Cursors.Arrow;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 更通用的转换器
    public class CursorTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string cursorType)
            {
                return cursorType switch
                {
                    "None" => Cursors.None,
                    "No" => Cursors.No,
                    "Arrow" => Cursors.Arrow,
                    "Wait" => Cursors.Wait,
                    "Hand" => Cursors.Hand,
                    "IBeam" => Cursors.IBeam,
                    "Cross" => Cursors.Cross,
                    "SizeAll" => Cursors.SizeAll,
                    _ => Cursors.Arrow
                };
            }
            return Cursors.Arrow;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
