using log4net;
using log4net.Config;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace CVWaferProber
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private const string LIBRARY_CV_Ali = "CV_algorithm.dll";

        [DllImport(LIBRARY_CV_Ali, EntryPoint = "CV_Ali_initial",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern void CV_Ali_initial();

        [DllImport(LIBRARY_CV_Ali, EntryPoint = "CV_Ali_release",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern void CV_Ali_release();

        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        protected override void OnStartup(StartupEventArgs e)
        {
            // 设置兼容模式
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            // 初始化 log4net
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());

            // 使用 App.config 配置
            //XmlConfigurator.Configure(logRepository);

            // 或者使用单独的配置文件
            var configFile = new System.IO.FileInfo("log4net.config");
            XmlConfigurator.Configure(logRepository, configFile);

           

            log.Info("Application starting...");

            base.OnStartup(e);

            // 初始化语言（读取Settings中的默认语言）
            AppSettingsManager.InitializeLanguage();
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
                Console.WriteLine("CV_algorithm.dll 初始化成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DLL初始化失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(); // 初始化失败则关闭应用
            }
        }
        // 应用关闭时调用释放
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // 调用DLL释放方法
                CV_Ali_release();
                Console.WriteLine("CV_algorithm.dll 资源释放成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DLL释放失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            base.OnExit(e);
        }

    }

}
