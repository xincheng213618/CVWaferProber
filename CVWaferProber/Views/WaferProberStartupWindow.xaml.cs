using CVWaferProber.Language;
using CVWaferProber.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TaskStatus = CVWaferProber.Models.TaskStatus;

namespace CVWaferProber.Views
{
    /// <summary>
    /// WaferProberStartupWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WaferProberStartupWindow : Window
    {
        // 定义启动完成事件
        public event EventHandler StartupCompleted;
        // 启动任务列表
        private List<StartupTask> startupTasks = new List<StartupTask>();
        private int currentTaskIndex = 0;
        private BackgroundWorker startupWorker;
        public WaferProberStartupWindow()
        {
            InitializeComponent();
            InitializeSystemInfo();
            //InitializeStartupTasks();
            InitializeStartupWorker();
            // 设置动态版权信息
            InitializeCopyrightInfo();
            // 添加窗口加载完成后的启动
            this.Loaded += MainWindow_Loaded;
        }

        // 在初始化方法中
        private void InitializeCopyrightInfo()
        {
            var languageManager = System.Windows.Application.Current.TryFindResource("LanguageManager") as LanguageManager;
            CopyrightTextBlock.Text = languageManager?.GetCopyrightNotice() ?? "Copyright";
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartStartupSequence();
        }

        private void InitializeSystemInfo()
        {
            // 获取当前执行的程序集文件路径
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            // 读取该文件（EXE）的版本信息
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assemblyPath);

            // 现在你可以使用这些信息了
            string productName = fileVersionInfo.ProductName; // 产品名称
            string fileVersion = fileVersionInfo.FileVersion;   // 文件版本号，通常格式为 1.0.0.0
            string productVersion = fileVersionInfo.ProductVersion; // 产品版本（可能包含其他标记）

            versionTextBlock.Text = $"{LanguageManager.Instance.GetString("Version")}: {fileVersion}"; // 动态获取的产品版本

            // 获取构建日期（使用方案2更准确）
            //DateTime buildDate = BuildDateHelper.GetLinkerTimestamp();
            DateTime buildDate = File.GetLastWriteTime(assemblyPath);

            string formattedDate = buildDate.ToString("yyyy-MM-dd");
            buildDateTextBlock.Text = $"{LanguageManager.Instance.GetString("BuildDate")}: {formattedDate}";
        }
        public void AddStartupTasks(StartupTask task)
        {
            startupTasks.Add(task);
        }
        public void InitializeStartupTasks()
        {
            StatusList.ItemsSource = startupTasks;
        }
        //private void InitializeStartupTasks()
        //{
        //    startupTasks = new List<StartupTask>
        //    {
        //        new StartupTask("初始化硬件接口", "#6B7280"),
        //        new StartupTask("加载运动控制系统", "#6B7280"),
        //        //new StartupTask("连接探针卡控制器", "#6B7280"),
        //        new StartupTask("校准晶圆平台", "#6B7280"),
        //        new StartupTask("初始化视觉系统", "#6B7280"),
        //        //new StartupTask("加载测试程序", "#6B7280"),
        //        //new StartupTask("启动安全监控", "#6B7280"),
        //        new StartupTask("准备就绪", "#6B7280")
        //    };

        //    StatusList.ItemsSource = startupTasks;
        //}

        private void InitializeStartupWorker()
        {
            startupWorker = new BackgroundWorker();
            startupWorker.WorkerReportsProgress = true;
            startupWorker.DoWork += StartupWorker_DoWork;
            startupWorker.ProgressChanged += StartupWorker_ProgressChanged;
            startupWorker.RunWorkerCompleted += StartupWorker_RunWorkerCompleted;
        }

        private void StartStartupSequence()
        {
            // 开始启动动画
            startupWorker.RunWorkerAsync();
        }

        private void StartupWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            for (int i = 0; i < startupTasks.Count; i++)
            {
                // 模拟每个任务的执行时间
                //System.Threading.Thread.Sleep(800);
                var task = startupTasks[i];
                task.Exec();
                // 更新进度
                int progress = (int)((i + 1) * 100.0 / startupTasks.Count);
                startupWorker.ReportProgress(progress, i);
            }
        }

        private void StartupWorker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            // 在主线程更新UI
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 更新进度条
                UpdateProgressBar(e.ProgressPercentage);

                // 更新当前任务状态
                if (e.UserState is int taskIndex)
                {
                    UpdateTaskStatus(taskIndex);
                }
            }), DispatcherPriority.Background);
        }

        private void StartupWorker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            // 在主线程执行完成操作
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 启动完成
                StatusMessage.Text = LanguageManager.Instance.GetString("StartupCompleted");

                // 延迟后关闭启动窗口
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1.5);
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    this.Close();
                    StartupCompleted?.Invoke(this, EventArgs.Empty);
                };
                timer.Start();
            }), DispatcherPriority.Background);
        }

        private void UpdateProgressBar(int percentage)
        {
            // 更新进度文本
            ProgressPercentage.Text = $"{percentage}%";

            // 计算进度条宽度
            var progressBarContainer = ProgressBarFill.Parent as Border;
            if (progressBarContainer != null)
            {
                double totalWidth = progressBarContainer.ActualWidth;
                double newWidth = totalWidth * percentage / 100.0;

                // 动画更新进度条
                var animation = new DoubleAnimation(newWidth, TimeSpan.FromSeconds(0.5));
                ProgressBarFill.BeginAnimation(Border.WidthProperty, animation);
            }
        }

        private void UpdateTaskStatus(int taskIndex)
        {
            if (taskIndex >= 0 && taskIndex < startupTasks.Count)
            {
                //// 更新之前任务的状态
                //if (currentTaskIndex >= 0 && currentTaskIndex < startupTasks.Count)
                //{
                //    startupTasks[currentTaskIndex].IsActive = false;
                //    startupTasks[currentTaskIndex].IsCompleted = true;
                //    startupTasks[currentTaskIndex].StatusColor = "#10B981";
                //    startupTasks[currentTaskIndex].StatusIcon = "✓";
                //}

                //// 更新当前任务
                //currentTaskIndex = taskIndex;
                //startupTasks[taskIndex].IsActive = true;
                //startupTasks[taskIndex].StatusColor = "#60A5FA";
                //startupTasks[taskIndex].StatusIcon = "⟳";

                //// 更新状态消息
                //StatusMessage.Text = startupTasks[taskIndex].Description + "...";

                //// 刷新UI
                //StatusList.Items.Refresh();
                // 更新之前任务的状态（如果存在）
                if (currentTaskIndex >= 0 && currentTaskIndex < startupTasks.Count)
                {
                    // 之前任务的状态已经在上一次调用中设置，这里不需要再设置
                }

                // 更新当前任务
                currentTaskIndex = taskIndex;

                if (startupTasks[taskIndex].Status == TaskStatus.Completed)
                {
                    // 任务成功
                    //startupTasks[taskIndex].Status = TaskStatus.Completed;
                    startupTasks[taskIndex].StatusColor = "#10B981"; // 绿色
                    startupTasks[taskIndex].StatusIcon = "✓";
                    startupTasks[taskIndex].IsActive = false;
                    startupTasks[taskIndex].IsCompleted = true;

                    // 更新状态消息
                    StatusMessage.Text = startupTasks[taskIndex].Description + " 成功";
                    StatusMessage.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    // 任务失败
                    //startupTasks[taskIndex].Status = TaskStatus.Failed;
                    startupTasks[taskIndex].StatusColor = "#EF4444"; // 红色
                    startupTasks[taskIndex].StatusIcon = "✗";
                    startupTasks[taskIndex].IsActive = false;
                    startupTasks[taskIndex].IsCompleted = false;

                    // 更新状态消息
                    StatusMessage.Text = startupTasks[taskIndex].Description + " 失败";
                    StatusMessage.Foreground = System.Windows.Media.Brushes.Red;
                }

                // 刷新UI
                StatusList.Items.Refresh();
            }
        }

        // 窗口拖动事件
        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // 关闭按钮事件
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    public static class BuildDateHelper
    {
        public static DateTime GetLinkerTimestamp()
        {
            string filePath = Assembly.GetExecutingAssembly().Location;
            const int PE_HEADER_OFFSET = 60;
            const int LINKER_TIMESTAMP_OFFSET = 8;

            byte[] buffer = new byte[2048];

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                stream.Read(buffer, 0, 2048);
            }

            int headerOffset = BitConverter.ToInt32(buffer, PE_HEADER_OFFSET);
            int secondsSince1970 = BitConverter.ToInt32(buffer, headerOffset + LINKER_TIMESTAMP_OFFSET);

            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(secondsSince1970);
            return dt.ToLocalTime();
        }

        public static string GetFormattedBuildDate()
        {
            DateTime buildDate = GetLinkerTimestamp();
            return buildDate.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
