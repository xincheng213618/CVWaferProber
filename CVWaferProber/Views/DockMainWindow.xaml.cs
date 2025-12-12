using AvalonDock.Layout;
using CVWaferProber.Components;
using CVWaferProber.Log;
using CVWaferProber.ViewModels;
using CVWPFCamImageCtrl;
using CVWPFSpectrometerCtrl;
using log4net;
using log4net.Config;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace CVWaferProber.Views
{
    /// <summary>
    /// DockMainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DockMainWindow : Window
    {
        //// 导入Win32 API（用于窗口托管）
        //[DllImport("user32.dll")]
        //private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        //[DllImport("user32.dll")]
        //private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        //private Process _demoProcess; // 保存Demo进程引用
        // 1. 导入Win32 API（放在类内部，方法外部）
       

        public DockMainWindow()
        {
            InitializeComponent();
            InitializeLogging();
            
            //LoadConoscopeDemo();
            // 监听Mapping面板可见性变化
            AnchorableMapping.IsVisibleChanged += (s, e) =>
            {
                if (AnchorableMapping.IsVisible)
                {
                    LeftPaneGroup.DockWidth = new GridLength(300); // 显示时宽度300
                }
                else
                {
                    LeftPaneGroup.DockWidth = new GridLength(0); // 隐藏时宽度0
                }
                // 强制布局更新
                DockingManager.UpdateLayout();
            };
            // 创建并初始化消息处理器
            //this.Loaded += DockMainWindow_Loaded;
            //if (DataContext is MainViewModel vm)
            //{
            //    vm.ResetLayoutRequested += (s, e) => ResetToDefaultLayout();
            //}
        }
        private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 获取选中的语言标签
            var menuItem = sender as MenuItem;
            if (menuItem?.Tag is string language)
            {
                // 调用AppSettingsManager切换语言
                AppSettingsManager.ChangeLanguage(language);

                // （可选）刷新界面布局（部分控件可能需要强制更新）
                Application.Current.MainWindow?.InvalidateVisual();
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
