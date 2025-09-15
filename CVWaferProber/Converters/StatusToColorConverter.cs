using ChipMapping.Models;
using ChipMapping.Models.Enums;
using CVWaferProber.ViewModels;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CVWaferProber.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Brushes.Transparent;

            if (MainViewModel.Instance != null)
            {
                bool isColorEnabled = MainViewModel.Instance.IsColorEnabled; // 或者通过其他方式获取
                if (!isColorEnabled) return Brushes.White;
            }

            ChipStatus status = (ChipStatus)value;

            var result = ChipStatusTool.GetStatusBrush(status);
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
