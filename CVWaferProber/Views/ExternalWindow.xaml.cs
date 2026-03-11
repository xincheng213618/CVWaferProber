using CVWaferProber.External;
using System;
using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace CVWaferProber.Views
{
    /// <summary>
    /// ExternalWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ExternalWindow : Window
    {
        private ExternalAppHost? _vEysHost;
        private ExternalAppHost? _ExAppHost;
        private DispatcherTimer? _simAutoTestTimer;

        public ExternalWindow()
        {
            InitializeComponent();
            _vEysHost = null;
            _ExAppHost = null;
            InitVEyeHost();
        }

        private void InitializeSimAutoTestTimer()
        {
            _simAutoTestTimer = new DispatcherTimer();
            _simAutoTestTimer.Interval = TimeSpan.FromMilliseconds(10000); // 500ms闪烁一次
            //_simAutoTestTimer.Tick += SimAutoTestTimer_Tick;
            _simAutoTestTimer.Tick += FindWindow_Tick;
            _simAutoTestTimer.Start();
        }

        private void FindWindow_Tick(object? sender, EventArgs e)
        {
            if (_vEysHost!=null && _vEysHost.FindAndSetWindow())
            {
                _simAutoTestTimer?.Stop();
                _simAutoTestTimer = null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            //if (_vEysHost != null)
            //{
            //    _vEysHost.HostedProcess.Close();
            //    _vEysHost.HostedProcess.Dispose();
            //    HostContainer_VEye.Content = null;
            //    _vEysHost = null;
            //}
            //if (_ExAppHost != null)
            //{
            //    _ExAppHost = null;
            //}
            base.OnClosed(e);
        }
        private void StartVEyeButton_Click(object sender, RoutedEventArgs e)
        {
            InitVEyeHost();
        }

        private void InitVEyeHost()
        {
            if (_vEysHost != null)
            {
                HostContainer_VEye.Content = null;
                _vEysHost = null;
            }

            try
            {
                _vEysHost = new ExternalAppHost(AppPathTextBox.Text);
                // 设置Stretch属性确保填充容器
                //_vEysHost.HorizontalAlignment = HorizontalAlignment.Stretch;
                //_vEysHost.VerticalAlignment = VerticalAlignment.Stretch;

                HostContainer_VEye.Content = _vEysHost;

                //InitializeSimAutoTestTimer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法启动应用程序: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StoptVEyeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vEysHost != null)
            {
                _vEysHost.HostedProcess.Close();
                _vEysHost.HostedProcess.Dispose();
                HostContainer_VEye.Content = null;
                _vEysHost = null;
            }
        }

        private void SetVEyeButton_Click(object sender, RoutedEventArgs e)
        {
            _vEysHost?.FindAndSetWindow();
        }
    }
}
