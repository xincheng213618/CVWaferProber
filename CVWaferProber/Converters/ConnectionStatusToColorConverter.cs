using CVWaferProber.Models;
using System.Globalization;
using System.Windows.Data;
using Brushes = System.Windows.Media.Brushes;

namespace CVWaferProber.Converters
{
    public class ConnectionStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConnectionStatus status)
            {
                return status switch
                {
                    ConnectionStatus.Connected => Brushes.LimeGreen,
                    ConnectionStatus.Connecting => Brushes.Orange,
                    ConnectionStatus.Error => Brushes.Red,
                    _ => Brushes.Red // Disconnected
                };
            }

            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}