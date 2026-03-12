using ColorVision.UI;
using CVWaferProber.Services;
using CVWaferProber.ViewModels;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WaferComm.Client;
using static OpenTK.Graphics.OpenGL.GL;

namespace CVWaferProber.Views
{
    /// <summary>
    /// WindowCheck.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCheck : Window
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(WindowCheck));

        IWaferProberClient _client { get; set; }

        public WindowCheck()
        {
            InitializeComponent();
        }
        private bool CheckAndWarnIfMoving()
        {
            if (_client.IsMoving)
            {
                MessageBox.Show(
                    (string)Application.Current.FindResource("Axismoving"),
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return true;
            }
            return false;
        }

        private async void AOI_Check(object sender, RoutedEventArgs e)
        {
            if (CheckAndWarnIfMoving()) return;

            logger.Info("Raise the equipment");

            //await _client.ZAllUpAsync();
            bool arrived = await _client.SendMoveCommandAndWaitAsync("gmc", 120);
            if (!arrived)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "LiftAll移动超时，未收到到位确认！", "超时警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            ConfigService.Instance.GetRequiredService<ToolsBarConfig>().CameraPosition = "AOICheck";
        }

        private async void VAM_Check(object sender, RoutedEventArgs e)
        {
            if (CheckAndWarnIfMoving()) return;

            logger.Info("Raise the equipment");

            //await _client.ZAllUpAsync();
            bool arrived = await _client.SendMoveCommandAndWaitAsync("gac", 120);
            if (!arrived)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "LiftAll移动超时，未收到到位确认！", "超时警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            ConfigService.Instance.GetRequiredService<ToolsBarConfig>().CameraPosition = "VAMCheck";

        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _client = ProberClientService.Instance.ProberClient;
        }
    }
}
