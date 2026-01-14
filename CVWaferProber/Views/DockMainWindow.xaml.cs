using CVAVMControl;
using CVWaferProber.Log;
using CVWaferProber.Services;
using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl;
using log4net;
using log4net.Config;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using TabControl = System.Windows.Controls.TabControl;

namespace CVWaferProber.Views
{
    public static class DispatcherExtensions
    {
        public static void DoEvents(this System.Windows.Threading.Dispatcher dispatcher)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
    }
    /// <summary>
    /// DockMainWindow.xaml 的交互逻辑
    /// </summary> 
    public partial class DockMainWindow : Window
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(DockMainWindow));
        // 缓存SP面板核心控件
        private CVSpectrumAnalyzer? _spAnalyzer; // 对应XAML中的spaly
        private TabControl? _spInnerTabControl;  // spaly内的innerTabControl
                                                 // 1. 红框第一项：切换到SP内层面板索引0（光谱）
                                                 // 暴露MappingDataControl
       
        public DockMainWindow()
        {
            InitializeComponent();
            InitializeLogging();
            InitializeSPControls();
            // 注册窗口按键监听（关键：捕获所有按键）
            this.KeyDown += DockMainWindow_KeyDown;
            if (DataContext is MainViewModel mainVm)
            {
                // 传递DockingManager和面板实例
                mainVm.DockingManager = DockingManager;
                mainVm.AnchorableCamera = AnchorableCamera; // 绑定XAML中的AOI面板
                mainVm.AnchorableSP = AnchorableSP;         // 绑定XAML中的SP面板
                mainVm.AnchorableVAM = AnchorableVAM;       // 绑定XAML中的VAM面板

                // 监听SP面板显示/隐藏事件，重新获取控件引用
                AnchorableSP.IsVisibleChanged += (s, e) =>
                {
                    if (AnchorableSP.IsVisible && _spAnalyzer != null)
                    {
                        _spInnerTabControl = _spAnalyzer.FindName("innerTabControl") as TabControl;
                    }
                };
            }
        }

        
        /// <summary>
        /// 初始化SP面板控件引用
        /// </summary>
        private void InitializeSPControls()
        {
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                // 1. 直接获取XAML中命名为spaly的CVSpectrumAnalyzer控件
                _spAnalyzer = spaly;
                if (_spAnalyzer == null)
                {
                    MessageBox.Show("未找到x:Name=spaly的CVSpectrumAnalyzer控件！");
                    return;
                }

                // 2. 获取内层TabControl（innerTabControl）
                _spInnerTabControl = _spAnalyzer.FindName("innerTabControl") as TabControl;
                if (_spInnerTabControl == null)
                {
                    MessageBox.Show("spaly内未找到x:Name=innerTabControl的TabControl！");
                    return;
                }

                // 3. 绑定MainViewModel的切换方法
                if (DataContext is ViewModels.MainViewModel mainVm)
                {
                    mainVm.SpPanelView = _spAnalyzer;
                    mainVm.ActivateSpectralInnerTabAction = ActivateSpectralInnerTab;
                    mainVm.ActivateIVLCameraInnerTabAction = ActivateIVLCameraInnerTab;
                    mainVm.ActivateEQEOuterTabAction = ActivateEQEOuterTab;
                    mainVm.SpPanelViewModel = _spAnalyzer.DataContext as CVWPFSpectrometerCtrl.ViewModels.CVSpectrumViewModel;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        #region 核心切换方法（适配红框3个选项）
        /// <summary>
        /// 红框第一项：切换到SP内层面板索引0（光谱）
        /// </summary>
        private void ActivateSpectralInnerTab()
        {
            if (_spAnalyzer == null) return;

            // 步骤1：强制切回外层Tab索引0（内层Tab所在的面板）
            SwitchOuterTab(0);

            // 步骤2：重置SP面板+激活内层Tab
            ResetSPPanelState();
            SwitchInnerTab(0);
        }

        /// <summary>
        /// 红框第二项：切换到SP内层面板索引5（IVLCamera）
        /// </summary>
        private void ActivateIVLCameraInnerTab()
        {
            if (_spAnalyzer == null) return;

            // 步骤1：强制切回外层Tab索引0
            SwitchOuterTab(0);

            // 步骤2：重置SP面板+激活内层Tab
            ResetSPPanelState();
            SwitchInnerTab(5);
        }
        // 切换外层TabControl的通用方法
        private void SwitchOuterTab(int index)
        {
            var outerTab = _spAnalyzer?.FindName("outerTabControl") as TabControl;
            if (outerTab == null) return;

            // 强制重置外层索引（避免缓存）
            outerTab.SelectedIndex = -1;
            outerTab.UpdateLayout();
            this.Dispatcher.DoEvents();
            outerTab.SelectedIndex = index;
            outerTab.Focus();
            outerTab.UpdateLayout();
            this.Dispatcher.DoEvents();
        }
        /// <summary>
        /// 红框第三项：切换到SP外层面板索引1（EQE）
        /// </summary>
        private void ActivateEQEOuterTab()
        {
            if (_spAnalyzer == null) return;

            // 重置SP面板状态
            ResetSPPanelState();

            // 找到外层TabControl并切换到索引1
            if (_spAnalyzer.FindName("outerTabControl") is TabControl outerTab)
            {
                outerTab.SelectedIndex = -1;
                outerTab.UpdateLayout();
                Dispatcher.DoEvents();
                outerTab.SelectedIndex = 1;
                outerTab.Focus();
            }
        }
        #endregion
        #region 辅助方法
        /// <summary>
        /// 重置SP面板状态（解决仅第一次有效）
        /// </summary>
        private void ResetSPPanelState()
        {
            // 1. 重置AvalonDock面板
            AnchorableSP.Hide();
            AnchorableSP.Show();
            AnchorableSP.IsSelected = true;
            AnchorableSP.IsActive = true;
            DockingManager.UpdateLayout();
            this.Dispatcher.DoEvents();

        }

        /// <summary>
        /// 切换内层TabControl索引（通用方法）
        /// </summary>
        /// <param name="index">目标索引</param>
        private void SwitchInnerTab(int index)
        {
            var innerTab = _spAnalyzer?.FindName("innerTabControl") as TabControl;
            if (innerTab == null) return;

            // 强制重置内层索引+刷新
            innerTab.SelectedIndex = -1;
            innerTab.UpdateLayout();
            this.Dispatcher.DoEvents();
            innerTab.SelectedIndex = index;
            innerTab.UpdateLayout();
            this.Dispatcher.DoEvents();

            // 强制聚焦TabItem（避免子控件抢占焦点）
            innerTab.Focus();
            if (innerTab.ItemContainerGenerator.ContainerFromIndex(index) is TabItem tabItem)
            {
                tabItem.Focus();
                tabItem.IsSelected = true;
                // 手动触发选中事件（确保UI响应）
                tabItem.RaiseEvent(new RoutedEventArgs(Selector.SelectedEvent));
            }

            _spAnalyzer?.UpdateLayout();
            DockingManager.UpdateLayout();
        }

        // 查找TabItem的标题栏控件
        private T? GetVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild) return tChild;
                var result = GetVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
        #endregion

        private void DockMainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 优先用 e.SystemKey（Alt 组合键的正确键值），无则用 e.Key
            Key key = e.SystemKey != Key.None ? e.SystemKey : e.Key;
            switch (key)
            {
                case Key.M: // Alt+M 切换映射面板
                    e.Handled = true;
                    AnchorableMapping.IsVisible = !AnchorableMapping.IsVisible;
                    break;
                //case Key.C: // Alt+C 切换中文
                //    e.Handled = true;
                //    LanguageMenuItem_Click(MenuLanguageChinese, new RoutedEventArgs());
                //    break;
                //case Key.E: // Alt+E 切换英文
                //    e.Handled = true;
                //    LanguageMenuItem_Click(MenuLanguageEnglish, new RoutedEventArgs());
                //    break;
                case Key.F4: // Alt+F4 退出
                    e.Handled = true;
                    (DataContext as ViewModels.MainViewModel)?.ExitCommand?.Execute(null);
                    break;
                // 视图-AOI面板（Alt+A）
                case Key.A:
                    e.Handled = true;
                    AnchorableCamera.IsVisible = !AnchorableCamera.IsVisible;
                    break;
                // 视图-SP面板（Alt+S）
                case Key.S:
                    e.Handled = true;
                    AnchorableSP.IsVisible = !AnchorableSP.IsVisible;
                    break;

                // VAM （Alt+V）
                case Key.V:
                    e.Handled = true;
                    AnchorableVAM.IsVisible = !AnchorableVAM.IsVisible;
                    break;
                case Key.L:
                    e.Handled = true;
                    AnchorableLog.IsVisible = !AnchorableLog.IsVisible;
                    break;
            }

        }
        private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            
            if (menuItem?.Tag is string language)
            {
                // 1. 保存语言设置
                AppSettingsManager.ChangeLanguage(language);

                // 2. 提示用户重启程序
                var result = MessageBox.Show("语言已切换，需要重启程序生效！\n即将重启！", "提示",
                                             MessageBoxButton.OK, MessageBoxImage.Information);
                if (result == MessageBoxResult.OK)
                {
                    // 3. 重启程序
                    RestartApplication();
                }
            }
           
        }

        private void RestartApplication()
        {
            try
            {
                // 1. 获取当前程序路径和参数（简化获取逻辑，减少耗时）
                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                // 2. 快速启动新实例（不等待、无窗口隐藏，加速启动）
                Process.Start(new ProcessStartInfo(exePath)
                {
                    CreateNoWindow = false,
                    UseShellExecute = true, // 用系统外壳启动，比直接启动更快
                    WindowStyle = ProcessWindowStyle.Normal
                });

                // 3. 强制退出当前进程（跳过WPF的Shutdown流程，大幅缩短退出耗时）
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重启失败：{ex.Message}", "错误");
            }
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
       

        private void ClearLogMenuItem_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }
        // 窗口关闭时保存面板状态
        //private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        //{
        //    if (DataContext is MainViewModel vm)
        //    {
        //        vm.SavePanelStates();
        //    }
        //}
        //private void MenuMappingPanel_Click(object sender, RoutedEventArgs e)
        //{
        //    var menuItem = sender as MenuItem;
        //    menuItem.IsChecked = !menuItem.IsChecked;
        //    PanelMapping.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

        //    // 同步更新ViewModel属性（保持数据一致性）
        //    if (DataContext is MainViewModel vm)
        //    {
        //        vm.IsMappingPanelVisible = menuItem.IsChecked;
        //    }
        //}
        //private void MenuCameraPanel_Click(object sender, RoutedEventArgs e)
        //{
        //    var menuItem = sender as MenuItem;
        //    menuItem.IsChecked = !menuItem.IsChecked;
        //    PanelCamera.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

        //    // 同步更新ViewModel属性（保持数据一致性）
        //    if (DataContext is MainViewModel vm)
        //    {
        //        vm.IsCameraPanelVisible = menuItem.IsChecked;
        //    }
        //}
        //private void MenuSPPanel_Click(object sender, RoutedEventArgs e)
        //{
        //    var menuItem = sender as MenuItem;
        //    menuItem.IsChecked = !menuItem.IsChecked;
        //    PanelSP.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

        //    // 同步更新ViewModel属性（保持数据一致性）
        //    if (DataContext is MainViewModel vm)
        //    {
        //        vm.IsSPPanelVisible = menuItem.IsChecked;
        //    }
        //}
    }
}
