using CVAVMControl;
using CVWaferProber.Log;
using CVWaferProber.Services;
using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl;
using log4net;
using log4net.Config;
using System.Diagnostics;
using System.Reflection;
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
                                                 // 语言切换相关常量（和AppSettingsManager保持一致）
        private const string ChineseTag = "Chinese";
        private const string EnglishTag = "English";
        public DockMainWindow()
        {
            InitializeComponent();
            InitializeLogging();
            InitializeSPControls();
            this.Loaded += DockMainWindow_Loaded;
            this.Closed += DockMainWindow_Closed;
            // 注册窗口按键监听（关键：捕获所有按键）
            this.KeyDown += DockMainWindow_KeyDown;

            // 初始化语言菜单选中状态（适配Properties.Settings实现）
            InitializeLanguageMenuSelection();

            if (DataContext is MainViewModel mainVm)
            {
                // 传递DockingManager和面板实例
                mainVm.DockingManager = DockingManager;
                mainVm.AnchorableCamera = AnchorableCamera; // 绑定XAML中的AOI面板
                mainVm.AnchorableSP = AnchorableSP;         // 绑定XAML中的SP面板
                mainVm.AnchorableVAM = AnchorableVAM;       // 绑定XAML中的VAM面板

                mainVm.InitializeServiveVM(MyVAM, spaly);

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
        #region 语言切换核心逻辑
        private void InitializeLanguageMenuSelection()
        {
            try
            {
                // 从Properties.Settings获取当前语言（适配你的AppSettingsManager）
                string currentLanguage = AppSettingsManager.CurrentLanguage ?? ChineseTag;

                // 严格匹配，设置菜单选中状态
                MenuLanguageChinese.IsChecked = string.Equals(currentLanguage, ChineseTag, StringComparison.OrdinalIgnoreCase);
                MenuLanguageEnglish.IsChecked = string.Equals(currentLanguage, EnglishTag, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                //logger.Error("初始化语言菜单选中状态失败", ex);
                // 兜底：默认选中中文，确保菜单状态不混乱
                MenuLanguageChinese.IsChecked = true;
                MenuLanguageEnglish.IsChecked = false;
            }
        }

        private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 1. 基础校验：确保触发控件是MenuItem
            if (sender is not MenuItem menuItem)
            {
                //logger.Warn("语言切换：触发事件的控件不是MenuItem");
                ShowLocalizedMessageBox(
                    "切换语言失败：无效的操作对象",
                    "Failed to switch language: Invalid operation object",
                    "错误",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // 2. 校验Tag值：确保是支持的语言类型
            if (menuItem.Tag is not string targetLanguage ||(targetLanguage != ChineseTag && targetLanguage != EnglishTag))
            {
                logger.Warn($"Language Switching: Invalid Language Tag Value：{menuItem.Tag}");
                ShowLocalizedMessageBox(
                    $"切换语言失败：不支持的语言类型「{menuItem.Tag}」",
                    $"Failed to switch language: Unsupported language type「{menuItem.Tag}」",
                    "错误",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // 3. 校验是否重复切换：避免无意义操作
            string currentLanguage = AppSettingsManager.CurrentLanguage ?? ChineseTag;
            if (string.Equals(currentLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
            {
                string langName = targetLanguage == ChineseTag ? "简体中文" : "English";
                
                ShowLocalizedMessageBox(
                    $"当前已使用{langName}，无需重复切换",
                    $"Currently using {langName}, no need to switch again",
                    "提示",
                    "Tips",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                // 确保菜单选中状态正确（防止手动修改导致的状态不一致）
                menuItem.IsChecked = true;
                return;
            }

            // 4. 确认用户是否要切换：防止误操作（多语言确认框）
            string targetLangName = targetLanguage == ChineseTag ? "简体中文" : "English";
            var confirmResult = ShowLocalizedMessageBox(
                $"确认切换为{targetLangName}吗？\n切换后程序将自动重启以生效",
                $"Confirm switch to {targetLangName}?\nThe program will restart automatically to take effect",
                "语言切换确认",
                "Language Switch Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
            {
                logger.Info($"Language switching: The user cancels the switch to「{targetLanguage}」");
                // 恢复原选中状态
                InitializeLanguageMenuSelection();
                return;
            }

            // 5. 执行语言切换逻辑
            try
            {
                // 5.1 调用你的AppSettingsManager保存语言设置
                AppSettingsManager.ChangeLanguage(targetLanguage);

                // 5.2 记录日志
                logger.Info($"Language switching: Successfully saved the language to 「{targetLanguage}」");

                RestartApplication();
            }
            catch (Exception ex)
            {
                logger.Error($"Language switching: Failed to save settings", ex);
                ShowLocalizedMessageBox(
                    $"切换语言失败：{ex.Message}\n请检查程序权限!",
                    $"Failed to switch language: {ex.Message}\nPlease check program permissions!",
                    "错误",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                // 恢复原选中状态，避免菜单状态异常
                InitializeLanguageMenuSelection();
            }
            //var menuItem = sender as MenuItem;

            //if (menuItem?.Tag is string language)
            //{
            //    // 1. 保存语言设置
            //    AppSettingsManager.ChangeLanguage(language);

            //    // 2. 提示用户重启程序
            //    var result = MessageBox.Show("语言已切换，需要重启程序生效！\n即将重启！", "提示",
            //                                 MessageBoxButton.OK, MessageBoxImage.Information);
            //    if (result == MessageBoxResult.OK)
            //    {
            //        // 3. 重启程序
            //        RestartApplication();
            //    }
            //}

        }
    
        #region 多语言 MessageBox 封装
        /// 获取多语言提示文本
        /// </summary>
        /// <param name="chineseText">中文文本</param>
        /// <param name="englishText">英文文本</param>
        /// <returns>对应语言的文本</returns>
        private string GetLocalizedText(string chineseText, string englishText)
        {
            return AppSettingsManager.CurrentLanguage == EnglishTag ? englishText : chineseText;
        }

        /// <summary>
        /// 显示多语言 MessageBox
        /// </summary>
        /// <param name="chineseMessage">中文消息</param>
        /// <param name="englishMessage">英文消息</param>
        /// <param name="chineseTitle">中文标题</param>
        /// <param name="englishTitle">英文标题</param>
        /// <param name="button">按钮类型</param>
        /// <param name="icon">图标类型</param>
        /// <returns>MessageBox 结果</returns>
        private MessageBoxResult ShowLocalizedMessageBox(string chineseMessage, string englishMessage,
                                                         string chineseTitle, string englishTitle,
                                                         MessageBoxButton button = MessageBoxButton.OK,
                                                         MessageBoxImage icon = MessageBoxImage.Information)
        {
            string message = GetLocalizedText(chineseMessage, englishMessage);
            string title = GetLocalizedText(chineseTitle, englishTitle);
            return System.Windows.MessageBox.Show(message, title, button, icon);
        }
        #endregion
        #endregion


        private void DockMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                MainService.Instance.Startup();
            });
        }

        private void DockMainWindow_Closed(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
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
                    ShowLocalizedMessageBox(
                        "未找到x:Name=spaly的CVSpectrumAnalyzer控件！",
                        "Cannot find CVSpectrumAnalyzer control with x:Name=spaly!",
                        "错误",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // 2. 获取内层TabControl（innerTabControl）
                _spInnerTabControl = _spAnalyzer.FindName("innerTabControl") as TabControl;
                if (_spInnerTabControl == null)
                {
                    ShowLocalizedMessageBox(
                        "spaly内未找到x:Name=innerTabControl的TabControl！",
                        "Cannot find TabControl with x:Name=innerTabControl in spaly!",
                        "错误",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // 3. 绑定MainViewModel的切换方法
                if (DataContext is MainViewModel mainVm)
                {
                    //mainVm.SpPanelView = _spAnalyzer;
                    mainVm.ActivateSpectralInnerTabAction = ActivateSpectralInnerTab;
                    mainVm.ActivateIVLCameraInnerTabAction = ActivateIVLCameraInnerTab;
                    mainVm.ActivateEQEOuterTabAction = ActivateEQEOuterTab;
                    //mainVm.SpPanelViewModel = _spAnalyzer.DataContext as CVWPFSpectrometerCtrl.ViewModels.CVSpectrumViewModel;
                }
            }), DispatcherPriority.Loaded);
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

        private void RestartApplication()
        {
            try
            {
                // 1. 获取当前程序路径和参数（简化获取逻辑，减少耗时）
                string exePath = Process.GetCurrentProcess().MainModule!.FileName;

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
                ShowLocalizedMessageBox(
                    $"重启程序失败：{ex.Message}\n请手动关闭并重新启动程序",
                    $"Failed to restart program: {ex.Message} Please close and restart the program manually",
                    "错误",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            //try
            //{
            //    // 1. 获取当前进程信息
            //    Process currentProcess = Process.GetCurrentProcess();
            //    string exePath = currentProcess.MainModule?.FileName ??
            //        Assembly.GetEntryAssembly()?.Location ??
            //        throw new InvalidOperationException(GetLocalizedText("无法获取程序路径", "Failed to get program path"));

            //    // 2. 构建启动参数（保留原命令行参数，适配带参数启动场景）
            //    ProcessStartInfo startInfo = new ProcessStartInfo(exePath)
            //    {
            //        CreateNoWindow = false,
            //        UseShellExecute = true,
            //        WindowStyle = ProcessWindowStyle.Normal,
            //        Arguments = Environment.CommandLine.Replace(exePath, "").Trim()
            //    };

            //    // 3. 启动新实例
            //    Process newProcess = Process.Start(startInfo);
            //    if (newProcess == null)
            //    {
            //        throw new InvalidOperationException(GetLocalizedText("启动新程序实例失败", "Failed to start new program instance"));
            //    }

            //    //logger.Info($"程序重启：新实例PID={newProcess.Id}，原实例PID={currentProcess.Id}");

            //    // 4. 优雅退出当前实例（先关闭窗口，再退出应用）
            //    this.Dispatcher.Invoke(() =>
            //    {
            //        this.Close(); // 触发Closed事件，执行Application.Shutdown
            //    });

            //    // 兜底：如果Close后仍未退出，延迟强制终止（给WPF清理资源的时间）
            //    Task.Delay(2000).ContinueWith(_ =>
            //    {
            //        if (!currentProcess.HasExited)
            //        {
            //            currentProcess.Kill();
            //            logger.Warn("程序重启：原实例未正常退出，已强制终止");
            //        }
            //    });
            //}
            //catch (Exception ex)
            //{
            //    logger.Error("程序重启失败", ex);
            //    ShowLocalizedMessageBox(
            //        $"重启程序失败：{ex.Message}\n请手动关闭并重新启动程序",
            //        $"Failed to restart program: {ex.Message}\nPlease close and restart the program manually",
            //        "错误",
            //        "Error",
            //        MessageBoxButton.OK,
            //        MessageBoxImage.Error);
            //}
        }
        private void InitializeLogging()
        {
            // 配置log4net
            // 配置log4net
            XmlConfigurator.Configure();

            // 获取TextBoxAppender并设置目标RichTextBox
            var appender = LogManager.GetRepository()
                .GetAppenders()
                .OfType<TextBoxAppender>()
                .FirstOrDefault();

            if (appender != null && LogTextBox != null)
            {
                // 关键修改：绑定TargetRichTextBox（替换原TargetTextBox）
                appender.TargetRichTextBox = LogTextBox;
                LogTextBox.Document.PageWidth = 100000; // 足够大的宽度，确保一行显示所有日志内容
            }

        }
       

        private void ClearLogMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (LogTextBox != null)
            {
                // 关键修改：清空FlowDocument的段落集合（替换原Clear()方法）
                LogTextBox.Document.Blocks.Clear();
            }
        }

        private void Button_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ReconnectDevCommand?.Execute(null);
            }
        }

        private void Button_Rc_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ReconnectRcCommand?.Execute(null);
            }
        }
    }
}
