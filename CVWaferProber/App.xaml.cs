using CVWaferProber.Config;
using CVWaferProber.Language;
using CVWaferProber.Models;
using CVWaferProber.Services;
using CVWaferProber.Views;
using log4net;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace CVWaferProber
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string LIBRARY_CV_Ali = "CV_algorithm.dll";

        [DllImport(LIBRARY_CV_Ali, EntryPoint = "CV_Ali_initial",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern void CV_Ali_initial();

        [DllImport(LIBRARY_CV_Ali, EntryPoint = "CV_Ali_release",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern void CV_Ali_release();

        // 导入 Win32 API：设置当前线程的区域设置
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int SetThreadLocale(int localeId);

        // 语言对应的 Locale ID（常用值，可直接使用）
        private const int LOCALE_EN_US = 0x0409; // 英文（美国）
        private const int LOCALE_ZH_CN = 0x0804; // 中文（中国）

        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        //private CVWaferProber.Views.SplashScreen _splash;
        private CVWaferProber.Views.WaferProberStartupWindow _splash;

        private const string AppMutexName = "CVWaferProber_79E62ABA-AEAB-4AD5-A973-F281F69E8271"; // 替换为唯一标识符
        private static Mutex _mutex;
        private static bool _isFirstInstance;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 尝试创建命名的 Mutex
            _mutex = new Mutex(true, AppMutexName, out _isFirstInstance);

            if (!_isFirstInstance)
            {
                // 如果已经存在实例，激活它并退出
                ActivateExistingInstance();
                Shutdown();
                return;
            }

            // 设置兼容模式
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            LogManagerService.ConfigureLog4Net();
            // 初始化 log4net
            //var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());

            // 使用 App.config 配置
            //XmlConfigurator.Configure(logRepository);

            // 或者使用单独的配置文件
            //var configFile = new System.IO.FileInfo("log4net.config");
            //XmlConfigurator.ConfigureAndWatch(logRepository, configFile);
            //XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net.config"));
            //int RegisterAddress = 0x08;
            //byte iAddr = Convert.ToByte(RegisterAddress);
            //int indexFrame = 0x40;
            //byte[] bytes = BitConverter.GetBytes(indexFrame);
            //byte iAddr = bytes[1];
            //byte nValue = bytes[0];

            // 加载配置
            ConfigManager.LoadConfig();

            log.Info("Application starting...");

            base.OnStartup(e);

            // 初始化语言（读取Settings中的默认语言）
            AppSettingsManager.InitializeLanguage();

            // 1. 创建并显示启动窗口
            _splash = new CVWaferProber.Views.WaferProberStartupWindow();
            _splash.AddStartupTasks(new MainStartupTask());
            _splash.AddStartupTasks(new MotionStartupTask());
            _splash.AddStartupTasks(new CommStartupTask(LanguageManager.Instance.GetString("Task_Vision"), "#6B7280"));
            _splash.InitializeStartupTasks();
            _splash.StartupCompleted += OnStartupCompleted;
            _splash.Show();

            _mainWindow = new DockMainWindow();

            // 1. 定义DataGrid行的样式（覆盖选中状态）
            var rowStyle = new Style(typeof(DataGridRow))
            {
                Setters =
                {
                    // 默认行背景
                    new Setter(DataGridRow.BackgroundProperty, Brushes.White),
                    // 默认行文字色
                    new Setter(DataGridRow.ForegroundProperty, Brushes.Black),
                },
                Triggers =
                {
                    // 覆盖选中状态的背景/文字色
                    new Trigger
                    {
                        Property = DataGridRow.IsSelectedProperty,
                        Value = true,
                        Setters =
                        {
                            // 选中行背景（自定义蓝色，可替换为任意色）
                            new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 30, 144, 255))),
                            // 选中行文字色
                            new Setter(DataGridRow.ForegroundProperty, Brushes.White),
                            // 选中行边框（可选，强化视觉）
                            new Setter(DataGridRow.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(255, 0, 80, 160))),
                            new Setter(DataGridRow.BorderThicknessProperty, new Thickness(1)),
                        }
                    },
                    // 可选：鼠标悬浮样式
                    new Trigger
                    {
                        Property = DataGridRow.IsMouseOverProperty,
                        Value = true,
                        Setters =
                        {
                            new Setter(DataGridRow.BackgroundProperty, Brushes.LightBlue),
                        }
                    }
                }
            };

            // 2. 定义单元格样式（避免单元格遮挡行背景）
            var cellStyle = new Style(typeof(DataGridCell))
            {
                Triggers =
                {
                    new Trigger
                    {
                        Property = DataGridCell.IsSelectedProperty,
                        Value = true,
                        Setters =
                        {
                            // 单元格选中背景（和行背景一致）
                            new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 0, 102, 204))),
                            // 单元格选中文字色
                            new Setter(DataGridCell.ForegroundProperty, Brushes.White),
                            // 取消单元格选中边框
                            new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent),
                        }
                    }
                }
            };

            // 3. 注册全局样式
            Application.Current.Resources.Add(typeof(DataGridRow), rowStyle);
            Application.Current.Resources.Add(typeof(DataGridCell), cellStyle);
            try
            {
                // 调用DLL初始化方法
                CV_Ali_initial();
                Console.WriteLine("CV_algorithm.dll Initialization successful");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DLL Initialization failed：{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(); // 初始化失败则关闭应用
            }
            // 新增：应用启动后尝试调用 ResetStatusCommand（通过 Dispatcher 延迟，确保 MainViewModel 已构造）
            //try
            //{
            //    Dispatcher.BeginInvoke(new Action(() =>
            //    {
            //        try
            //        {
            //            var vm = MainViewModel.Instance;
            //            if (vm != null && vm.ResetStatusCommand != null)
            //            {
            //                if (vm.ResetStatusCommand.CanExecute(null))
            //                    vm.ResetStatusCommand.Execute(null);
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            log.Warn("Invoke ResetStatusCommand failed.", ex);
            //        }
            //    }), DispatcherPriority.ApplicationIdle);
            //}
            //catch (Exception dex)
            //{
            //    log.Warn("Failed to schedule ResetStatusCommand invocation.", dex);
            //}
        }
        private DockMainWindow _mainWindow;

        private void OnStartupCompleted(object sender, EventArgs e)
        {
            // 关闭启动窗口
            _splash.Close();

            // 创建并显示主窗口
            //if (_mainWindow == null) _mainWindow = new DockMainWindow();
            //Application.Current.MainWindow = _mainWindow;
            //_mainWindow.Show();
            // 创建主窗口（确保在UI线程执行）
            // 使用 Dispatcher.BeginInvoke 确保在 UI 线程执行
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_mainWindow == null)
                {
                    _mainWindow = new DockMainWindow();
                }

                Application.Current.MainWindow = _mainWindow;
                _mainWindow.Show();
            }));
        }

        // 应用关闭时调用释放
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // 确保在程序退出时释放 Mutex
                if (_isFirstInstance)
                {
                    _mutex?.ReleaseMutex();
                    // 调用DLL释放方法
                    CV_Ali_release();
                    log.Info("CV_algorithm.dll Resource released successfully");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DLL Release failed：{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            base.OnExit(e);
        }

        private void ActivateExistingInstance()
        {
            // 查找已有的窗口并激活
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);

            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id)
                {
                    // 激活主窗口
                    IntPtr mainWindowHandle = process.MainWindowHandle;
                    if (mainWindowHandle != IntPtr.Zero)
                    {
                        BringWindowToForeground(mainWindowHandle);
                        break;
                    }
                }
            }
        }

        // 将窗口带到前台
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        private void BringWindowToForeground(IntPtr hWnd)
        {
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }
            SetForegroundWindow(hWnd);
        }
    }

}
