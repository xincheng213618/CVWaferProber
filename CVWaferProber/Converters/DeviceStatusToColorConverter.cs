using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WaferComm.StateMachine;
using Brushes = System.Windows.Media.Brushes;

namespace CVWaferProber.Converters
{
    public class DeviceStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProberState status)
            {
                return status switch
                {
                    ProberState.Disconnected => Brushes.Red,
                    ProberState.Connected => Brushes.LightGreen,
                    ProberState.Ready => Brushes.Green,
                    ProberState.WaitingForWafer => Brushes.Orange,
                    ProberState.WaferLoaded => Brushes.Blue,
                    ProberState.Aligning => Brushes.Yellow,
                    ProberState.Testing => Brushes.Cyan,
                    ProberState.Paused => Brushes.Yellow,
                    ProberState.Stopping => Brushes.OrangeRed,
                    ProberState.Error => Brushes.Red,
                    ProberState.Maintenance => Brushes.Purple,
                    _ => Colors.Red
                    //ConnectionStatus.Error => Brushes.Red,
                    //_ => Brushes.Red // Disconnected
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
