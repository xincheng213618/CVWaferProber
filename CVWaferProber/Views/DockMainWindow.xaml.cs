using AvalonDock.Layout;
using CVWaferProber.Components;
using CVWaferProber.Log;
using CVWaferProber.ViewModels;
using CVWPFCamImageCtrl;
using CVWPFSpectrometerCtrl;
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
            //this.Loaded += DockMainWindow_Loaded;
            if (DataContext is MainViewModel vm)
            {
                vm.ResetLayoutRequested += (s, e) => ResetToDefaultLayout();
            }
        }

        private void ResetToDefaultLayout()
        {
            // 1. 新建布局根
            var newLayoutRoot = new LayoutRoot();

            // --------------------------
            // 左侧区域：映射面板 + 测试表格（垂直排列）
            // --------------------------
            // 映射面板（LayoutAnchorable → 放入LayoutAnchorablePane）
            var mappingAnchorable = new LayoutAnchorable
            {
                Title = "晶圆映射",
                CanClose = false,
                CanHide = true,
                Content = new MappingDataControl()
            };
            var mappingPane = new LayoutAnchorablePane { Children = { mappingAnchorable } };

            // 左侧垂直容器（用LayoutPanel控制方向）
            var leftVerticalContainer = new LayoutPanel
            {
               
                Children = { mappingPane }
            };


            // --------------------------
            // 右侧区域：相机图像 + 测试图表（垂直排列）
            // --------------------------
            // 相机面板（LayoutAnchorable → 放入LayoutAnchorablePane）
            var cameraAnchorable = new LayoutAnchorable
            {
                Title = "相机图像",
                CanClose = false,
                CanHide = true,
                Content = new CVCamImagerCtrl()
            };
            var cameraPane = new LayoutAnchorablePane { Children = { cameraAnchorable } };

            // 图表面板（LayoutAnchorable → 放入LayoutAnchorablePane）

            var chartAnchorable = new LayoutAnchorable
            {
                Title = (string)Application.Current.FindResource("Dock.Layout.Title.SP"),
                CanClose = false,
                CanHide = true,
                Content = new CVSpectrumAnalyzer()
            };
            var chartPane = new LayoutAnchorablePane { Children = { chartAnchorable } };

            // 2.3 日志面板（新增）
            var logAnchorable = new LayoutAnchorable
            {
                Title = "日志",
                CanClose = false,
                CanHide = true,
                Content = new TextBox // 日志文本框（或自定义日志控件）
                {
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 12,
                    HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    IsReadOnly = true,
                    TextWrapping = System.Windows.TextWrapping.NoWrap,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
                }
            };
            var logPane = new LayoutAnchorablePane { Children = { logAnchorable } };
            var rightHorizentolContainer = new LayoutPanel
            {
                Orientation = Orientation.Vertical,
                Children = {chartPane, logPane }
            };
            // 右侧垂直容器（用LayoutPanel控制方向）
            var rightVerticalContainer = new LayoutPanel
            {
                Orientation = Orientation.Vertical,
                Children = { cameraPane, rightHorizentolContainer }
            };

           
            // --------------------------
            // 主容器：左右区域水平排列
            // --------------------------
            var mainHorizontalContainer = new LayoutPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { leftVerticalContainer, rightVerticalContainer}
            };


            // 2. 应用布局：通过RootPanel添加主容器
            newLayoutRoot.RootPanel = mainHorizontalContainer;
            DockingManager.Layout = newLayoutRoot;
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
