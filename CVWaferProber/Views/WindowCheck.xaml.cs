using CVWaferProber.Services;
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

namespace CVWaferProber.Views
{
    /// <summary>
    /// WindowCheck.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCheck : Window
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(WindowCheck));

        IWaferProberClient Client { get; set; }

        public WindowCheck()
        {
            InitializeComponent();
        }

        private void AOI_Check(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                await Client.ZToMainCameraCheckAsync();
                logger.Info("ZToMainCameraCheckAsync");
            });


        }

        private void VAM_Check(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                await Client.ZToAuxCameraCheckAsync();
                logger.Info("ZToAuxCameraCheckAsync");
            });
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            Client = ProberClientService.Instance.ProberClient;
        }
    }
}
