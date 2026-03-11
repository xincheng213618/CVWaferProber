using CVWaferProber.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Linq;

namespace CVWaferProber.Converters
{
    public class SysDeviceStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    "Open" => new SolidColorBrush(Colors.Green),
                    "Closed" => new SolidColorBrush(Colors.Red),
                    "Error" => new SolidColorBrush(Colors.Orange),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E3F2FD"));
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public static class BoolToVisibilityConverter
    {
        public static readonly IValueConverter Default = new BooleanToVisibilityConverter();
        public static readonly IValueConverter Inverse = new InverseBooleanToVisibilityConverter();
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class OnlineCountConverter : IValueConverter
    {
        public static readonly OnlineCountConverter Default = new OnlineCountConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int total && parameter is ObservableCollection<DeviceItemViewModel> devices)
            {
                return devices.Count(d => d.IsLive);
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLive)
            {
                return isLive ?
                    System.Windows.Application.Current.FindResource("SysDev_Online") :
                    System.Windows.Application.Current.FindResource("SysDev_Offline");
            }
            return System.Windows.Application.Current.FindResource("SysDev_Offline");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DeviceStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    "运行中" or "Running" => System.Windows.Application.Current.FindResource("SysDev_Running"),
                    "待机" or "Standby" => System.Windows.Application.Current.FindResource("SysDev_Standby"),
                    "维护中" or "Maintenance" => System.Windows.Application.Current.FindResource("SysDev_Maintenance"),
                    _ => status
                };
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
