using CVWaferProber.Log;
using CVWaferProber.ViewModels;
using log4net;
using log4net.Config;
using System.Windows;
using System.Windows.Controls;

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
        // 窗口关闭时保存面板状态
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SavePanelStates();
            }
        }
        private void MenuMappingPanel_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            menuItem.IsChecked = !menuItem.IsChecked;
            PanelMapping.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

            // 同步更新ViewModel属性（保持数据一致性）
            if (DataContext is MainViewModel vm)
            {
                vm.IsMappingPanelVisible = menuItem.IsChecked;
            }
        }
        private void MenuCameraPanel_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            menuItem.IsChecked = !menuItem.IsChecked;
            PanelCamera.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

            // 同步更新ViewModel属性（保持数据一致性）
            if (DataContext is MainViewModel vm)
            {
                vm.IsCameraPanelVisible = menuItem.IsChecked;
            }
        }
        private void MenuSPPanel_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            menuItem.IsChecked = !menuItem.IsChecked;
            PanelSP.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

            // 同步更新ViewModel属性（保持数据一致性）
            if (DataContext is MainViewModel vm)
            {
                vm.IsSPPanelVisible = menuItem.IsChecked;
            }
        }
    }
}
