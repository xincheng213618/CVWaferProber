using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Services;
using System.Globalization;
using System.Windows.Data;
using Brushes = System.Windows.Media.Brushes;

namespace CVWaferProber.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Brushes.Transparent;

            var mainVM = MainService.Instance.MainVM;
            if (mainVM != null && mainVM.DataMappingVM != null)
            {
                bool isColorEnabled = mainVM.DataMappingVM.IsColorEnabled; // 或者通过其他方式获取
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
