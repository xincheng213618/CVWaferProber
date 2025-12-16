using AvalonDock.Layout;
using CVWaferProber.Components;
using CVWaferProber.Log;
using CVWaferProber.ViewModels;
using CVWPFCamImageCtrl;
using CVWPFSpectrometerCtrl;
using CVWPFSpectrometerCtrl.Models;
using CVWPFSpectrometerCtrl.ViewModels;
using log4net;
using log4net.Config;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

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
        
        public DockMainWindow()
        {
            InitializeComponent();
            InitializeLogging();
            InitializeSPControls();
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

            //// 2. 确保外层Tab索引0的面板已加载
            //var outerTab = _spAnalyzer?.FindName("outerTabControl") as TabControl;
            //if (outerTab != null && outerTab.SelectedIndex != 0)
            //{
            //    outerTab.SelectedIndex = 0;
            //    outerTab.UpdateLayout();
            //    this.Dispatcher.DoEvents();
            //}

            //// 3. 强制刷新内层TabControl的父容器
            //var innerTab = _spAnalyzer?.FindName("innerTabControl") as TabControl;
            //if (innerTab != null && innerTab.Parent is Panel parent)
            //{
            //    parent.UpdateLayout();
            //    this.Dispatcher.DoEvents();
            //}
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

        // 辅助：查找TabItem的标题栏控件
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

        private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem?.Tag is string language)
            {
                // 1. 保存语言设置
                AppSettingsManager.ChangeLanguage(language);

                // 2. 提示用户重启程序
                var result = MessageBox.Show("语言已切换，需要重启程序生效！\n是否立即重启？", "提示",
                                             MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
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

        // 4. 窗口加载完成后执行Demo嵌入

        // 在界面加载时调用（如ViewModel的初始化方法、窗口的Loaded事件）
        //public void LoadConoscopeDemo()
        //{
        //    try
        //    {
        //        // 1. 获取Demo.exe路径（编译后会复制到输出目录）
        //        string demoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "External/Demo/ConoscopeDemo.exe");
        //        if (!System.IO.File.Exists(demoPath))
        //        {
        //            MessageBox.Show("Demo文件不存在，请检查复制配置");
        //            return;
        //        }

        //        // 2. 启动Demo（隐藏初始窗口）
        //        _demoProcess = new Process
        //        {
        //            StartInfo = new ProcessStartInfo
        //            {
        //                FileName = demoPath,
        //                WindowStyle = ProcessWindowStyle.Minimized,
        //                CreateNoWindow = false
        //            }
        //        };
        //        _demoProcess.Start();
        //        _demoProcess.WaitForInputIdle(); // 等待Demo窗口初始化

        //        // 3. 获取Demo窗口句柄和容器句柄
        //        IntPtr demoHwnd = _demoProcess.MainWindowHandle;
        //        IntPtr hostHwnd = DemoHost.Handle; // DemoHost是XAML中的WindowsFormsHost

        //        // 4. 将Demo窗口嵌入到WPF界面的容器中
        //        SetParent(demoHwnd, hostHwnd);

        //        // 5. 调整Demo窗口大小以适配容器
        //        MoveWindow(demoHwnd, 0, 0, (int)DemoHost.ActualWidth, (int)DemoHost.ActualHeight, true);

        //        // 6. 监听容器大小变化，同步调整Demo窗口
        //        DemoHost.SizeChanged += (s, e) =>
        //        {
        //            if (demoHwnd != IntPtr.Zero)
        //            {
        //                MoveWindow(demoHwnd, 0, 0, (int)DemoHost.ActualWidth, (int)DemoHost.ActualHeight, true);
        //            }
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Demo嵌入失败：{ex.Message}");
        //    }
        //}

        //// 界面关闭时关闭Demo进程，避免残留
        //protected override void OnClosing(CancelEventArgs e)
        //{
        //    _demoProcess?.Kill();
        //    _demoProcess?.Dispose();
        //    base.OnClosing(e);
        //}
        //private void ResetToDefaultLayout()
        //{
        //    // 1. 新建布局根
        //    var newLayoutRoot = new LayoutRoot();

        //    // --------------------------
        //    // 左侧区域：映射面板 + 测试表格（垂直排列）
        //    // --------------------------
        //    // 映射面板（LayoutAnchorable → 放入LayoutAnchorablePane）
        //    var mappingAnchorable = new LayoutAnchorable
        //    {
        //        Title = "晶圆映射",
        //        CanClose = false,
        //        CanHide = true,
        //        Content = new MappingDataControl()
        //    };
        //    var mappingPane = new LayoutAnchorablePane { Children = { mappingAnchorable } };

        //    // 左侧垂直容器（用LayoutPanel控制方向）
        //    var leftVerticalContainer = new LayoutPanel
        //    {

        //        Children = { mappingPane }
        //    };


        //    // --------------------------
        //    // 右侧区域：相机图像 + 测试图表（垂直排列）
        //    // --------------------------
        //    // 相机面板（LayoutAnchorable → 放入LayoutAnchorablePane）
        //    var cameraAnchorable = new LayoutAnchorable
        //    {
        //        Title = "相机图像",
        //        CanClose = false,
        //        CanHide = true,
        //        Content = new CVCamImagerCtrl()
        //    };
        //    var cameraPane = new LayoutAnchorablePane { Children = { cameraAnchorable } };

        //    // 图表面板（LayoutAnchorable → 放入LayoutAnchorablePane）

        //    var chartAnchorable = new LayoutAnchorable
        //    {
        //        Title = (string)Application.Current.FindResource("Dock.Layout.Title.SP"),
        //        CanClose = false,
        //        CanHide = true,
        //        Content = new CVSpectrumAnalyzer()
        //    };
        //    var chartPane = new LayoutAnchorablePane { Children = { chartAnchorable } };

        //    // 2.3 日志面板（新增）
        //    var logAnchorable = new LayoutAnchorable
        //    {
        //        Title = "日志",
        //        CanClose = false,
        //        CanHide = true,
        //        Content = new TextBox // 日志文本框（或自定义日志控件）
        //        {
        //            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        //            FontSize = 12,
        //            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
        //            IsReadOnly = true,
        //            TextWrapping = System.Windows.TextWrapping.NoWrap,
        //            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
        //        }
        //    };
        //    var logPane = new LayoutAnchorablePane { Children = { logAnchorable } };
        //    var rightHorizentolContainer = new LayoutPanel
        //    {
        //        Orientation = Orientation.Vertical,
        //        Children = {chartPane, logPane }
        //    };
        //    // 右侧垂直容器（用LayoutPanel控制方向）
        //    var rightVerticalContainer = new LayoutPanel
        //    {
        //        Orientation = Orientation.Vertical,
        //        Children = { cameraPane, rightHorizentolContainer }
        //    };


        //    // --------------------------
        //    // 主容器：左右区域水平排列
        //    // --------------------------
        //    var mainHorizontalContainer = new LayoutPanel
        //    {
        //        Orientation = Orientation.Horizontal,
        //        Children = { leftVerticalContainer, rightVerticalContainer}
        //    };


        //    // 2. 应用布局：通过RootPanel添加主容器
        //    newLayoutRoot.RootPanel = mainHorizontalContainer;
        //    DockingManager.Layout = newLayoutRoot;
        //}



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
        //private void DockMainWindow_Loaded(object sender, RoutedEventArgs e)
        //{
        //    if (this.DataContext != null && this.DataContext is MainViewModel mainModel)
        //    {
        //        mainModel.WinLoadInit(this);
        //    }
        //}

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
