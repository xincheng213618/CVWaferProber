using CVWaferProber.Log;
using CVWaferProber.ViewModels;
using log4net;
using log4net.Config;
using System.Windows;

namespace CVWaferProber.Views
{
    /// <summary>
    /// DockMainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DockMainWindow : Window
    {
        public DockMainWindow()
        {
            InitializeComponent();
            InitializeLogging();
            // 创建并初始化消息处理器
            this.Loaded += DockMainWindow_Loaded;
        }
        private void InitializeLogging()
        {
            // 配置log4net
            XmlConfigurator.Configure();

            // 获取TextBoxAppender并设置目标TextBox
            var appender = LogManager.GetRepository()
                .GetAppenders()
                .OfType<TextBoxAppender>()
                .FirstOrDefault();

            if (appender != null)
            {
                appender.TargetTextBox = LogTextBox;
            }
        }
        private void DockMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null && this.DataContext is MainViewModel mainModel)
            {
                mainModel.WinLoadInit(this);
            }
        }

        private void ClearLogMenuItem_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }
    }
}
