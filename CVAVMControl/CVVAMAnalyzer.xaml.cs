using ColorVision.FileIO;
using ConoscopeDemo;
using CVAVMControl;
using CVCommCore.CVImage;
using CVWaferProber.Core;
using CVWaferProber.Core.Events;
using CVWaferProber.Core.ViewModels;
using log4net;
using Microsoft.Win32;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WaferComm.Core;
using Path = System.IO.Path;
using Rect = System.Windows.Rect;

namespace CVAVMControl
{
    /// <summary>
    /// CVVAMAnalyzer.xaml 的交互逻辑
    /// </summary>
    public partial class CVVAMAnalyzer : UserControl
    {

        private static readonly ILog logger = LogManager.GetLogger(typeof(CVVAMAnalyzer));

        private Mat? XMat;
        private Mat? YMat;
        private Mat? ZMat;
        private Mat? XYZMat;
        private Mat? pseudoColorMat;

        private byte[] dataXyz;

        private System.Windows.Point center;
        private int imageRadius;
        private double MaxAngle = 60; // Default max angle
        private double ConoscopeCoefficient = 0.01935; // Pixels per degree   0.01935 0.02645

        private int displayAngle = 120; // Default display angle
        private ExportChannel displayChannel = ExportChannel.Y; // Default display channel
        private ExportDataType displayChannel1 = ExportDataType.Y;
        private int displayRadius = 40; // Default display radius angle
                                        // CVVAMAnalyzer.cs 中新增定时器
        private DispatcherTimer? _resourceCleanTimer;
        // 自定义悬浮面板（用于显示格式信息）
        private Border? _hoverInfoPanel;
        private TextBlock? _hoverInfoText;
        // 移除动态位置相关变量，新增固定面板配置
        private bool _isHovering = false; // 仅标记是否悬浮，不跟踪坐标
        private readonly object _lockObj = new object(); // 线程锁，避免并发更新

        // 记录当前选中的角度（用于区分普通线和选中线）
        private int _selectedAngle = -1;
        // 记录当前选中的半径（R圆面板用）
        private int _selectedRadius = -1;

        private IEventAggregator? EventAggregator;

        // 1. 定义DLL返回状态枚举（与DLL定义一致）
        private enum CV_AliResType
        {
            SUCCESS = 1,          // 完全成功
            FAILED = 0,           // 失败
            PART_SUCCESS = 2,     // 部分成功
            ERR_LENGTH = -1,      // 内存长度不够
            ERR_FILE = -2,        // 存文件失败
            ERR_JSON = -3         // JSON格式异常
        }

        // 2. 新增ImageData结构（匹配DLL定义）
        private class ImageData
        {
            public int _w;
            public int _h;
            public int _bpp;
            public int _channels;
            public byte[] data;
        }

        // 3. 重新导入DLL方法（适配StdCall+字节数组参数）
        private const string LIBRARY_CV_Ali = "CV_algorithm.dll";
        [DllImport(LIBRARY_CV_Ali, EntryPoint = "CV_Ali_calcVam",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern CV_AliResType CV_Ali_calcVam(
            IntPtr handle,                // 第1个参数：句柄（通常传IntPtr.Zero）
            int w,                        // 第2个参数：图像宽度
            int h,                        // 第3个参数：图像高度
            int bpp,                      // 第4个参数：每像素位数
            int channels,                 // 第5个参数：通道数
            byte[] bgrData,               // 第6个参数：BGR图像数据
            byte[] xyzData,               // 第7个参数：XYZ图像数据
            string paramJson,             // 第8个参数：JSON参数
            StringBuilder resultJson,     // 第9个参数：返回的JSON结果
            ref int resultJsonLength,     // 第10个参数：结果缓冲区长度
            ref int dstBpp,               // 第11个参数：输出图像BPP
            ref int dstChannels,          // 第12个参数：输出图像通道数
            byte[] dstData                // 第13个参数：输出图像数据
        );

        // 新增：裁切正方形的DLL导入（如果DLL有现成裁切接口，优先用DLL；无则用OpenCV实现）
        [DllImport(LIBRARY_CV_Ali, EntryPoint = "CV_Ali_cutVamImage",
          CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern CV_AliResType CV_Ali_cutVamImage(
            IntPtr handle,       // 第1个参数：句柄
            ref int w,           // 第2个参数：图像宽度（ref）
            ref int h,           // 第3个参数：图像高度（ref）
            int bpp,             // 第4个参数：每像素位数
            int channels,        // 第5个参数：通道数
            byte[] data,         // 第6个参数：图像数据
            string staticJson    // 第7个参数：JSON参数
        );
        // 2. 修正封装调用方法
        private CV_AliResType CallCV_Ali_calcVam(ImageData bgrImg, ImageData xyzImg, string paramJson, out string resultJson, out ImageData showImage)
        {
            // 初始化输出参数（关键：先赋默认值，由DLL覆盖）
            resultJson = string.Empty;
            showImage = new ImageData
            {
                _w = xyzImg._w,
                _h = xyzImg._h,
                _bpp = 0,  // 初始化为0，由DLL返回真实值
                _channels = 0, // 初始化为0，由DLL返回真实值
                data = new byte[xyzImg._w * xyzImg._h * 4 * 3] // 预分配足够内存（4字节/像素，3通道）
            };

            // 初始化结果缓冲区（扩大到4MB，避免长度不足）
            int resultBufLen = 4 * 1024 * 1024; // 4MB
            StringBuilder resultBuf = new StringBuilder(resultBufLen);
            int dstBpp = showImage._bpp;
            int dstChannels = showImage._channels;

            CV_AliResType res = CV_AliResType.FAILED;
            try
            {
                // 核心：参数顺序严格匹配导入签名
                res = CV_Ali_calcVam(
                    IntPtr.Zero,                // handle
                    xyzImg._w,                  // w
                    xyzImg._h,                  // h
                    bgrImg._bpp,                // bpp
                    xyzImg._channels,           // channels
                    bgrImg.data,                // bgrData（无数据传null）
                    xyzImg.data,                // xyzData
                    paramJson,                  // JSON参数
                    resultBuf,                  // 返回结果缓冲区
                    ref resultBufLen,           // 缓冲区长度
                    ref dstBpp,                 // 输出BPP
                    ref dstChannels,            // 输出通道数
                    showImage.data              // 输出图像数据
                );

                // 处理缓冲区长度不足的情况（重新调用）
                if (res == CV_AliResType.ERR_LENGTH)
                {
                    resultBuf = new StringBuilder(resultBufLen);
                    res = CV_Ali_calcVam(
                        IntPtr.Zero,
                        xyzImg._w, xyzImg._h, bgrImg._bpp, xyzImg._channels,
                        bgrImg.data, xyzImg.data, paramJson,
                        resultBuf, ref resultBufLen, ref dstBpp, ref dstChannels, showImage.data
                    );
                }

                // 更新输出图像的真实参数
                showImage._bpp = dstBpp;
                showImage._channels = dstChannels;
                // 获取返回的JSON（去除空字符）
                resultJson = resultBuf.ToString().Trim('\0');
            }
            catch (SEHException ex)
            {
                logger.Error($"DLL{(string)Application.Current.FindResource("callexception")}：{ex.Message}", ex);
                res = CV_AliResType.FAILED;
            }
            catch (Exception ex)
            {
                logger.Error($"{(string)Application.Current.FindResource("ErrorcallingDLL")}：{ex.Message}", ex);
                res = CV_AliResType.FAILED;
            }

            // 关键：打印返回结果，定位问题
            logger.Info($"DLL{(string)Application.Current.FindResource("Returncode")}：{res}");//，{(string)Application.Current.FindResource("Return")}JSON：{resultJson}"
            return res;
        }

        // 2. 定义接口返回结果的JSON序列化模型（匹配DLL输出格式）
        private class VamResultRoot
        {
            public VamResultData result { get; set; } = new VamResultData();
        }

        private class VamResultData
        {
            public VamCircle circle { get; set; } = new VamCircle(); // 圆环数据（R圆用）
            public VamLine line { get; set; } = new VamLine();       // 线条数据（直径线用）
        }

        private class VamCircle
        {
            public List<VamSamplePoint> Data { get; set; } = new List<VamSamplePoint>();
        }

        private class VamLine
        {
            public List<VamSamplePoint> Data { get; set; } = new List<VamSamplePoint>();
        }

        public class VamSamplePoint
        {
            public double X { get; set; }       // 三刺激值X
            public double Y { get; set; }       // 三刺激值Y（亮度值，图表用）
            public double Z { get; set; }       // 三刺激值Z
            public double cie_x { get; set; }   // CIE坐标x（可选）
            public double cie_y { get; set; }   // CIE坐标y（可选）
            public double position { get; set; } // 角度位置（对应图表X轴）
        }

        // 新增：全局缓存DLL返回的所有方位角数据（0°~180°）
        public Dictionary<int, List<VamSamplePoint>> _dllAllAzimuthData = new Dictionary<int, List<VamSamplePoint>>();
        public Dictionary<(int polar, double azimuth), RgbSample> DllAllCircleData { get; private set; }
        public CVVAMAnalyzer()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            InitializeComponent();

            // 替换原有采样点更新绑定，改为间隔角度更新
            UpdateLinePolarIntervalText();
            UpdateAzimuthIntervalText();
            txtLinePolarInterval.TextChanged += (s, e) => UpdateLinePolarIntervalText();
            txtAzimuthInterval.TextChanged += (s, e) => UpdateAzimuthIntervalText();

            // 初始化悬浮信息面板（样式匹配目标图）
            InitializeHoverInfoPanel();
            // 初始化定时器：5分钟未使用VAM则释放资源
            _resourceCleanTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(20)
            };
            _resourceCleanTimer.Tick += (s, e) =>
            {
                if (!IsVisible) // 面板隐藏且5分钟未使用
                {
                    ResetDataWithoutDispose();
                    _resourceCleanTimer.Stop();
                }
            };
            InitializeEvents();
            this.IsVisibleChanged += OnCVVAMAnalyzerVisibleChanged;
            //this.Unloaded += CVVAMAnalyzer_Unloaded;
            // 新增：初始化DllAllCircleData字典
            DllAllCircleData = new Dictionary<(int polar, double azimuth), RgbSample>();
            InitializeProgressBar();
        }
        /// <summary>
        /// 当控件可见性发生变化时触发
        /// </summary>
        private void OnCVVAMAnalyzerVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 当控件变为可见时，重新设置图表的Autoscale
            if (IsVisible)
            {
                // 延迟一小段时间确保UI已完全加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ResetChartScales();
                }), DispatcherPriority.Render);
            }
        }
        /// <summary>
        /// 重置图表缩放为AutoScale
        /// </summary>
        private void ResetChartScales()
        {
            try
            {
                // 重置直径线图表
                if (wpfPlotDiameterLine != null && wpfPlotDiameterLine.Plot != null)
                {
                    wpfPlotDiameterLine.Plot.Axes.AutoScale();
                    wpfPlotDiameterLine.Refresh();
                }

                // 重置R圆图表
                if (wpfPlotRCircle != null && wpfPlotRCircle.Plot != null)
                {
                    wpfPlotRCircle.Plot.Axes.AutoScale();
                    wpfPlotRCircle.Refresh();
                }

                //logger.Info("图表已重置为AutoScale");
            }
            catch (Exception ex)
            {
                logger.Error("Failed to reset the chart's AutoScale.", ex);
            }
        }
        private void InitializeEvents(IEventAggregator? eventAggregator = null)
        {
            this.EventAggregator = eventAggregator == null ? CVWPEventAggregatorInstance.Instance : eventAggregator;
            this.EventAggregator.Subscribe<VAMFlowCompletedEvent>(OnFlowCompleted);
            //this.EventAggregator.Subscribe<VAMFlowStartingEvent>(OnFlowStarting);
            this.EventAggregator.Subscribe<VAMResultGUIClearEvent>(OnResultGUIClear);
            //订阅自动导出CSV事件
            //this.EventAggregator.Subscribe<VAMAutoExportCsvEvent>(OnAutoExportCsv);
        }

        private void UnInitializeEvents()
        {
            this.EventAggregator?.Unsubscribe<VAMFlowCompletedEvent>(OnFlowCompleted);
            //this.EventAggregator?.Unsubscribe<VAMFlowStartingEvent>(OnFlowStarting);
            this.EventAggregator?.Unsubscribe<VAMResultGUIClearEvent>(OnResultGUIClear);
            //this.EventAggregator?.Unsubscribe<VAMAutoExportCsvEvent>(OnAutoExportCsv);
        }

        //private void Export_Click(object sender, RoutedEventArgs e)
        //{
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        try
        //        {
        //            // 1. 基础校验
        //            if (!_isDataValid || !IsMatSafe(YMat) || dataXyz == null)
        //            {
        //                logger.Warn("VAM数据未加载，自动导出失败");
        //                return;
        //            }

        //            // 2. 导出路径（桌面+时间戳）
        //            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        //            string fileName = $"VAM_MatrixExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        //            string exportPath = System.IO.Path.Combine(desktopPath, fileName);

        //            // 3. 采集图二格式的数据（角度行 + 多采样点列）
        //            List<VamMatrixExportModel> matrixData = GetVamMatrixData();
        //            if (matrixData.Count == 0)
        //            {
        //                logger.Warn("无有效数据可导出");
        //                return;
        //            }

        //            // 4. 生成图二格式的CSV
        //            using (var writer = new StreamWriter(exportPath, false, Encoding.UTF8))
        //            {
        //                // 4.1 写入表头（第1-2行）
        //                writer.WriteLine($"Measurement Date,,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,"); // 第1行
        //                writer.WriteLine($"Instrument,,VAM 60°,,,,,,,,,,,,"); // 第2行
        //                writer.WriteLine(); // 第3行（空行）

        //                // 4.2 写入采样点序号行（第4行：C列开始是0、1、2…）
        //                int maxSampleCount = matrixData.Max(m => m.AllSampleValues.Count);
        //                string sampleHeader = $",,{string.Join(",", Enumerable.Range(0, maxSampleCount))}";
        //                writer.WriteLine(sampleHeader);

        //                // 4.3 写入数据行（B列是角度，C~N列是该角度的所有采样点值）
        //                foreach (var data in matrixData)
        //                {
        //                    // 格式：空列 + 角度 + 该角度的所有采样点值（横向排列）
        //                    string valuesStr = string.Join(",", data.AllSampleValues.Select(v => v.ToString("F5")));
        //                    string line = $",{data.Angle},{valuesStr}";
        //                    writer.WriteLine(line);
        //                }
        //            }

        //            logger.Info($"VAM图二格式CSV导出成功！路径：{exportPath}");
        //            MessageBox.Show($"CSV已自动导出至：\n{exportPath}", "导出成功",
        //                MessageBoxButton.OK, MessageBoxImage.Information);
        //        }
        //        catch (Exception ex)
        //        {
        //            logger.Error("VAM图二格式导出失败", ex);
        //            MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //        }
        //    });
        //}
        /// <summary>
        /// 自动导出CSV事件处理（测试完成后触发）
        /// </summary>
        //private void OnAutoExportCsv(VAMAutoExportCsvEvent @event)
        //{
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        try
        //        {
        //            // 1. 基础校验
        //            if (!_isDataValid || !IsMatSafe(YMat) || dataXyz == null)
        //            {
        //                logger.Warn("VAM数据未加载，自动导出失败");
        //                return;
        //            }

        //            // 2. 导出路径（桌面+时间戳）
        //            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        //            string fileName = $"VAM_MatrixExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        //            string exportPath = System.IO.Path.Combine(desktopPath, fileName);

        //            // 3. 采集图二格式的数据（角度行 + 多采样点列）
        //            List<VamMatrixExportModel> matrixData = GetVamMatrixData();
        //            if (matrixData.Count == 0)
        //            {
        //                logger.Warn("无有效数据可导出");
        //                return;
        //            }

        //            // 4. 生成图二格式的CSV
        //            using (var writer = new StreamWriter(exportPath, false, Encoding.UTF8))
        //            {
        //                // 4.1 写入表头（第1-2行）
        //                writer.WriteLine($"Measurement Date,,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,"); // 第1行
        //                writer.WriteLine($"Instrument,,VAM 60°,,,,,,,,,,,,"); // 第2行
        //                writer.WriteLine(); // 第3行（空行）

        //                // 4.2 写入采样点序号行（第4行：C列开始是0、1、2…）
        //                int maxSampleCount = matrixData.Max(m => m.AllSampleValues.Count);
        //                string sampleHeader = $",,{string.Join(",", Enumerable.Range(0, maxSampleCount))}";
        //                writer.WriteLine(sampleHeader);

        //                // 4.3 写入数据行（B列是角度，C~N列是该角度的所有采样点值）
        //                foreach (var data in matrixData)
        //                {
        //                    // 格式：空列 + 角度 + 该角度的所有采样点值（横向排列）
        //                    string valuesStr = string.Join(",", data.AllSampleValues.Select(v => v.ToString("F5")));
        //                    string line = $",{data.Angle},{valuesStr}";
        //                    writer.WriteLine(line);
        //                }
        //            }

        //            logger.Info($"VAM图二格式CSV导出成功！路径：{exportPath}");
        //            MessageBox.Show($"CSV已自动导出至：\n{exportPath}", "导出成功",
        //                MessageBoxButton.OK, MessageBoxImage.Information);
        //        }
        //        catch (Exception ex)
        //        {
        //            logger.Error("VAM图二格式导出失败", ex);
        //            MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //        }
        //    });
        //}
        /// <summary>
        /// 同一角度的多个采样点值作为横向列
        /// </summary>
        private List<VamMatrixExportModel> GetVamMatrixData()
        {
            List<VamMatrixExportModel> matrixData = new List<VamMatrixExportModel>();
            string currentBtnText = btnSwitchChart.Content.ToString();
            string rCircleTitle = FindResource("Plot.Title.RCircle").ToString();

            // 模式：R圆（图二是R圆的“角度行+多采样点列”格式）
            if (currentBtnText != rCircleTitle)
            {
                int targetRadius = _selectedRadius != -1 ? _selectedRadius : 40;
                var circleLine = CreateRCircleLine(targetRadius);

                // 按角度分组：将同一角度的所有采样点值收集到一个列表
                var angleGroups = circleLine.RgbData
                    .GroupBy(sample => Math.Round(sample.Position, 0)) // 按角度（取整）分组
                    .OrderByDescending(g => g.Key); // 按角度从大到小排序（匹配图二的-60到60）

                // 遍历每个角度组，整理为“角度+多采样点列”
                foreach (var group in angleGroups)
                {
                    matrixData.Add(new VamMatrixExportModel
                    {
                        Angle = group.Key,
                        // 该角度对应的所有采样点值（横向列）
                        AllSampleValues = group.Select(sample => Math.Round(sample.Y, 5)).ToList()
                    });
                }
            }

            return matrixData;
        }
        // 表格的导出模型（角度+多采样点值）
        private class VamMatrixExportModel
        {
            public double Angle { get; set; } // B列：角度
            public List<double> AllSampleValues { get; set; } = new List<double>(); // C~N列：该角度的所有采样点值
        }

        private void OnFlowCompleted(VAMFlowCompletedEvent @event)
        {
            if (!string.IsNullOrEmpty(@event.ResultFileName) && System.IO.File.Exists(@event.ResultFileName))
            {
                this.Dispatcher.Invoke(() => { 
                    ProcessCVCIEFile(@event.ResultFileName);
                });
            }
            else
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("VAM result cvcie file not exist => {0}", @event.ResultFileName);
            }
        }
        private void OnFlowStarting(VAMFlowStartingEvent @event)
        {
            ResetDataWithoutDispose();
        }
        private void OnResultGUIClear(VAMResultGUIClearEvent @event)
        {
            ResetDataWithoutDispose();
        }


        //private void CVVAMAnalyzer_Unloaded(object sender, RoutedEventArgs e)
        //{
        //    XMat?.Dispose();
        //    YMat?.Dispose();
        //    ZMat?.Dispose();
        //    pseudoColorMat?.Dispose();
        //}

        private void CVVAMAnalyzer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                _resourceCleanTimer?.Start();
            }
        }
        private void WpfPlot_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScottPlot.WPF.WpfPlot plot)
            {
                // 从Tag读取动态资源（标题）
                string title = plot.Tag?.ToString() ?? string.Empty;
                // 从资源字典读取坐标轴标签
                string axisX = Application.Current.Resources["Plot.Axis.X"]?.ToString() ?? "Degrees";
                string axisY = Application.Current.Resources["Plot.Axis.Y"]?.ToString() ?? "Luminance (cd/m²)";

                // 初始化图表
                InitializePlot(plot, title, axisX, axisY);
            }
        }
        private void InitializePlot(ScottPlot.WPF.WpfPlot plot, string title, string axisX, string axisY)
        {
            plot.Plot.Title(title);
            plot.Plot.XLabel(axisX); // 动态X轴标签
            plot.Plot.YLabel(axisY); // 动态Y轴标签

            // 保留原字体、样式逻辑
            string fontSample = $"$中文 {axisY} {axisX}";
            plot.Plot.Axes.Title.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            plot.Plot.Axes.Left.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            plot.Plot.Axes.Bottom.Label.FontName = ScottPlot.Fonts.Detect(fontSample);

            plot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
            plot.Plot.Grid.MajorLineWidth = 1;
            plot.Plot.Axes.SetLimits(-80, 80, 0, 600);
            plot.Refresh();
        }
        //private void UserContrl_Initialized(object sender, EventArgs e)
        //{
        //    InitializePlot(wpfPlotDiameterLine, "直径线分布曲线 (Diameter Line Distribution)");
        //    InitializePlot(wpfPlotRCircle, "R圆分布曲线 (R Circle Distribution)");
        //}
        //private void InitializePlot(ScottPlot.WPF.WpfPlot plot, string title)
        //{
        //    plot.Plot.Title(title);
        //    plot.Plot.XLabel("Degrees");
        //    plot.Plot.YLabel("Luminance (cd/m²)");
        //    //plot.Plot.Legend.FontName = ScottPlot.Fonts.Detect("中文");
        //    string fontSample = $"中文 Luminance Voltage";
        //    plot.Plot.Axes.Title.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
        //    plot.Plot.Axes.Left.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
        //    plot.Plot.Axes.Bottom.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
        //    plot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
        //    plot.Plot.Grid.MajorLineWidth = 1;
        //    plot.Plot.Axes.SetLimits(-80, 80, 0, 600);
        //    plot.Refresh();
        //} 
        string select = (string)Application.Current.FindResource("VAM.SelectCVCIEFile");
        public void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CVCIE Files (*.cvcie)|*.cvcie|All Files (*.*)|*.*",
                Title = select
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ProcessCVCIEFile(openFileDialog.FileName);
            }
        }

        private void ProcessCVCIEFile(string filename)
        {
            try
            {
                XMat?.Dispose();
                YMat?.Dispose();
                ZMat?.Dispose();

                CVCIEFileInfo cvfileInfo = new CVCIEFileInfo();
                CVCIEFile fileInfo = new CVCIEFile();
                bool bR = CVCommCore.Core.CVFileUtils.ReadImageFile(filename, ref cvfileInfo);
                if (!bR)
                {
                    if (logger.IsErrorEnabled) logger.ErrorFormat("Read ImageFile error => {0}", filename);
                    return;
                }
                fileInfo.Bpp = cvfileInfo.FrameInfo.bppInt;
                fileInfo.Channels = cvfileInfo.FrameInfo.channelsInt;
                fileInfo.Cols = cvfileInfo.FrameInfo.widthInt;
                fileInfo.Rows = cvfileInfo.FrameInfo.heightInt;
                fileInfo.Data = cvfileInfo.data;
                // 保存原始数据
                byte[] originalData = fileInfo.Data; // 重要：保存原始数据引用

                string cropJson = JsonConvert.SerializeObject(new
                {
                    RHO = 60.0,
                    pixelToAngle = ConoscopeCoefficient,
                    center = new CropCenter
                    {
                        x = fileInfo.Cols / 2.0,
                        y = fileInfo.Rows / 2.0
                    }
                });

                // 调用DLL裁切
                int dstW = fileInfo.Cols;
                int dstH = fileInfo.Rows;
                CV_AliResType cropResult = CV_Ali_cutVamImage(
                    IntPtr.Zero,
                    ref dstW,
                    ref dstH,
                    fileInfo.Bpp,
                    fileInfo.Channels,
                    originalData,     // 传入原始数据
                    cropJson
                );

                OpenCvSharp.MatType singleChannelTypeFinal = fileInfo.Bpp switch
                {
                    8 => MatType.CV_8UC1,
                    16 => MatType.CV_16UC1,
                    32 => MatType.CV_32FC1,
                    64 => MatType.CV_64FC1,
                    _ => throw new NotSupportedException($"Bpp {fileInfo.Bpp} not supported")
                };
                // 分离XYZ通道
                int cropChannelSize = dstW * dstH * (fileInfo.Bpp / 8);
                byte[] croppedX = new byte[cropChannelSize];
                byte[] croppedY = new byte[cropChannelSize];
                byte[] croppedZ = new byte[cropChannelSize];

                if (cropResult == CV_AliResType.SUCCESS)
                {
                    fileInfo.Cols = dstW;
                    fileInfo.Rows = dstH;
                    logger.Info($"DLL裁切成功，裁切后尺寸：{fileInfo.Cols}x{fileInfo.Rows}");

                    // 重要：更新dataXyz为裁切后的数据
                    // 假设DLL裁切函数会在原数组上进行修改
                    // 如果DLL返回新数组，需要相应调整

                    int cropAllPixLen = cropChannelSize * fileInfo.Channels;

                    // 检查裁切后数据是否有效
                    if (originalData.Length >= cropAllPixLen)
                    {
                        dataXyz = new byte[cropAllPixLen];
                        Buffer.BlockCopy(originalData, 0, dataXyz, 0, cropAllPixLen);

                        Buffer.BlockCopy(dataXyz, 0, croppedX, 0, cropChannelSize);
                        Buffer.BlockCopy(dataXyz, cropChannelSize, croppedY, 0, cropChannelSize);
                        Buffer.BlockCopy(dataXyz, cropChannelSize * 2, croppedZ, 0, cropChannelSize);

                        // 创建Mat对象

                        XMat = Mat.FromPixelData(dstW, dstH, singleChannelTypeFinal, croppedX);
                        YMat = Mat.FromPixelData(dstW, dstH, singleChannelTypeFinal, croppedY);
                        ZMat = Mat.FromPixelData(dstW, dstH, singleChannelTypeFinal, croppedZ);
                    }
                    else
                    {
                        throw new InvalidOperationException("裁切后数据长度不足");
                    }
                }
                else
                {
                    logger.Warn("DLL裁切失败，使用原始数据");
                    // 使用原始数据创建Mat
                    // ... 原有创建Mat的代码 ...
                    Buffer.BlockCopy(originalData, 0, croppedX, 0, cropChannelSize);
                    Buffer.BlockCopy(originalData, cropChannelSize, croppedY, 0, cropChannelSize);
                    Buffer.BlockCopy(originalData, cropChannelSize*2, croppedZ, 0, cropChannelSize);
                    XMat = Mat.FromPixelData(dstW, dstH, singleChannelTypeFinal, croppedX);
                    YMat = Mat.FromPixelData(dstW, dstH, singleChannelTypeFinal, croppedY);
                    ZMat = Mat.FromPixelData(dstW, dstH, singleChannelTypeFinal, croppedZ);
                }

                // 后续代码保持不变...
                center = new System.Windows.Point(YMat.Width / 2.0, YMat.Height / 2.0);
                imageRadius = (int)(MaxAngle / ConoscopeCoefficient);

                if (cbDisplayAngle.Items.Count > 0 && cbDisplayAngle.Items[0] is ComboBoxItem firstItem)
                {
                    cbDisplayAngle.SelectedItem = firstItem;
                    if (int.TryParse(firstItem.Tag?.ToString(), out int firstAngle))
                    {
                        _selectedAngle = firstAngle;
                    }
                }

                UpdateDisplay();
                //fileInfo.Dispose();
                _isDataValid = true;
            }
            catch (Exception ex)
            {
                logger.Error("处理CVCIE文件失败", ex);
                //MessageBox.Show($"处理文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // 原有方法保持不变，修改ProcessCVCIEFile方法，添加裁切逻辑
        //private void ProcessCVCIEFile(string filename)
        //{
        //    try
        //    {
        //        XMat?.Dispose();
        //        YMat?.Dispose();
        //        ZMat?.Dispose();

        //        CVCIEFile fileInfo = new CVCIEFile();
        //        CVFileUtil.Read(filename, out fileInfo);

        //        // ========== 新增：裁切正方形核心逻辑 ==========
        //        //int originalCols = fileInfo.Cols;
        //        //int originalRows = fileInfo.Rows;
        //        //int bpp = fileInfo.Bpp;
        //        //int channelSize = originalCols * originalRows * (bpp / 8);
        //        //int allPixLen = originalCols * originalRows * (bpp / 8) * fileInfo.Channels;

        //        //// 1. 计算裁切后的正方形尺寸（取宽高最小值）
        //        //int squareSize = Math.Min(originalCols, originalRows);
        //        //int cropChannelSize = squareSize * squareSize * (bpp / 8);
        //        //int cropAllPixLen = cropChannelSize * fileInfo.Channels;

        //        //// 2. 初始化裁切后的数据缓冲区
        //        //byte[] croppedData = new byte[cropAllPixLen];
        //        //byte[] croppedX = new byte[cropChannelSize];
        //        //byte[] croppedY = new byte[cropChannelSize];
        //        //byte[] croppedZ = new byte[cropChannelSize];

        //        string cropJson = JsonConvert.SerializeObject(new
        //        {
        //            RHO = 60.0,//线条角度
        //            pixelToAngle = ConoscopeCoefficient,
        //            center = new CropCenter
        //            {
        //                x = fileInfo.Cols / 2.0,     //传进来
        //                y = fileInfo.Rows / 2.0
        //            }
        //        });
        //        // 3. 优先调用DLL裁切接口；若DLL无此接口，使用OpenCV裁切
        //        bool useDllCrop = true; // 可配置是否使用DLL裁切
        //        if (useDllCrop)
        //        {
        //            int dstW = fileInfo.Cols;
        //            int dstH = fileInfo.Rows;
        //            CV_AliResType cropResult = CV_Ali_cutVamImage(
        //                IntPtr.Zero,
        //                ref dstW,          // ref参数：输出裁切后宽度
        //                ref dstH,          // ref参数：输出裁切后高度
        //                fileInfo.Bpp,
        //                fileInfo.Channels,
        //                fileInfo.Data,     // 输入图像数据
        //                cropJson           // JSON参数
        //            );

        //            if (cropResult == CV_AliResType.SUCCESS)
        //            {
        //             //   squareSize = dstW; // 以DLL返回的尺寸为准
        //                fileInfo.Cols = dstW;
        //                fileInfo.Rows = dstH;

        //                // 同步更新裁切后的中心坐标（DLL裁切成功后，中心为裁切后图像的中心）
        //                logger.Info($"DLL裁切成功，裁切后尺寸：{fileInfo.Cols}x{fileInfo.Rows}");

        //            }
        //            else
        //            {
        //                logger.Warn("DLL裁切正方形失败");
        //                useDllCrop = false;
        //            }
        //        }


        //        // ========== 原有逻辑适配裁切后的数据 ==========
        //        OpenCvSharp.MatType singleChannelTypeFinal = fileInfo.Bpp switch
        //        {
        //            8 => MatType.CV_8UC1,
        //            16 => MatType.CV_16UC1,
        //            32 => MatType.CV_32FC1,
        //            64 => MatType.CV_64FC1,
        //            _ => throw new NotSupportedException($"Bpp {fileInfo.Bpp} not supported")
        //        };
        //        int squareSize = Math.Min(fileInfo.Cols, fileInfo.Rows);
        //        int cropChannelSize = squareSize * squareSize * (fileInfo.Bpp / 8);
        //        int cropAllPixLen = cropChannelSize * fileInfo.Channels;

        //        byte[] croppedData = new byte[cropAllPixLen];
        //        byte[] croppedX = new byte[cropChannelSize];
        //        byte[] croppedY = new byte[cropChannelSize];
        //        byte[] croppedZ = new byte[cropChannelSize];
        //        if (fileInfo.Channels == 3)
        //        {
        //            // 使用裁切后的数据
        //            if (dataXyz == null || dataXyz.Length != cropAllPixLen)
        //            {
        //                dataXyz = new byte[cropAllPixLen];
        //            }
        //            Buffer.BlockCopy(croppedData, 0, dataXyz, 0, cropAllPixLen);

        //            XMat = Mat.FromPixelData(squareSize, squareSize, singleChannelTypeFinal, croppedX);
        //            YMat = Mat.FromPixelData(squareSize, squareSize, singleChannelTypeFinal, croppedY);
        //            ZMat = Mat.FromPixelData(squareSize, squareSize, singleChannelTypeFinal, croppedZ);
        //        }

        //        // 更新中心坐标为裁切后正方形的中心
        //        center = new System.Windows.Point(YMat.Width / 2.0, YMat.Height / 2.0);
        //        imageRadius = (int)(MaxAngle / ConoscopeCoefficient);

        //        // 初始化默认选中角度
        //        if (cbDisplayAngle.Items.Count > 0 && cbDisplayAngle.Items[0] is ComboBoxItem firstItem)
        //        {
        //            cbDisplayAngle.SelectedItem = firstItem;
        //            if (int.TryParse(firstItem.Tag?.ToString(), out int firstAngle))
        //            {
        //                _selectedAngle = firstAngle;
        //            }
        //        }

        //        UpdateDisplay();
        //        fileInfo.Dispose();
        //        _isDataValid = true;

        //        logger.Info($"成功裁切为正方形，原始尺寸：{fileInfo.Cols}x{fileInfo.Rows}，裁切后尺寸：{squareSize}x{squareSize}");
        //    }
        //    catch (Exception ex)
        //    {
        //        string errorMsg = (string)Application.Current.FindResource("State.Error");
        //        MessageBox.Show($"{(string)Application.Current.FindResource("Anerroroccurredwhileprocessingthefile")}: {ex.Message}", $"{errorMsg}", MessageBoxButton.OK, MessageBoxImage.Error);
        //        logger.Error("处理CVCIE文件并裁切正方形失败", ex);
        //    }
        //}
        #region 角度备注绘制（通用方法）
        /// <summary>
        /// 绘制角度/半径备注（通用方法，支持不同位置和样式）
        /// </summary>
        /// <param name="mat">绘制画布</param>
        /// <param name="pos">标注位置</param>
        /// <param name="text">标注文本</param>
        /// <param name="textColor">文字颜色（默认白色）</param>
        /// <param name="bgColor">背景颜色（默认黑色半透明）</param>
        /// <param name="fontScale">字体缩放（默认0.8）</param>
        /// <param name="thickness">文字粗细（默认2）</param>
        private void DrawAngleLabel(Mat mat, OpenCvSharp.Point pos, string text, Scalar? textColor = null, Scalar? bgColor = null, double fontScale = 0.8, int thickness = 15)
        {
            Scalar txtColor = textColor ?? new Scalar(0, 0, 0); // 默认白色
            //Scalar backgroundColor = bgColor ?? new Scalar(0, 0, 0, 128); // 黑色半透明

            // 1. 计算文本尺寸，绘制背景框（避免文字与图像重叠）
            int baseline = 0;
            OpenCvSharp.Size textSize = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, fontScale, thickness, out baseline);
            OpenCvSharp.Rect bgRect = new OpenCvSharp.Rect(
                pos.X - textSize.Width / 2 - 4,
                pos.Y - textSize.Height / 2 - 4,
                textSize.Width + 8,
                textSize.Height + 8
            );

            // 确保背景框不超出图像边界
            bgRect.X = Math.Max(0, Math.Min(mat.Width - bgRect.Width, bgRect.X));
            bgRect.Y = Math.Max(0, Math.Min(mat.Height - bgRect.Height, bgRect.Y));

            // 绘制背景框（半透明）
            // Cv2.Rectangle(mat, bgRect, backgroundColor, -1);
            // 绘制背景框边框（增加辨识度）
            Cv2.Rectangle(mat, bgRect, txtColor, 1);

            // 2. 绘制文字（带描边，增强可读性）
            OpenCvSharp.Point textPos = new OpenCvSharp.Point(
                bgRect.X + 4,
                bgRect.Y + textSize.Height + 2
            );
            // 先绘制黑色描边（文字更清晰）
            Cv2.PutText(mat, text, textPos, HersheyFonts.HersheySimplex, fontScale, new Scalar(0, 0, 0), thickness + 2);
            // 再绘制主文字
            Cv2.PutText(mat, text, textPos, HersheyFonts.HersheySimplex, fontScale, txtColor, thickness);
        }


        #endregion
        #region 通用角度添加/删除逻辑（支持不同ComboBox）
        /// <summary>
        /// 判断当前是否为R圆模式
        /// </summary>
        private bool IsRCircleMode()
        {
            string currentBtnText = btnSwitchChart.Content.ToString();
            string rCircleTitle = FindResource("Plot.Title.RCircle").ToString();
            string diameterTitle = FindResource("Plot.Title.DiameterLine").ToString();
            // R圆模式：按钮显示直径线标题（与原有切换逻辑一致）
            return currentBtnText == diameterTitle;
        }
        /// <summary>
        /// 通用添加角度方法（支持任意ComboBox）
        /// </summary>
        /// <param name="targetComboBox">目标下拉框（如cbDisplayAngle/cbDisplayRadius）</param>
        /// <param name="inputAngleText">输入的角度文本</param>
        private void AddAngleToComboBox(ComboBox targetComboBox, string inputAngleText)
        {

            if (string.IsNullOrWhiteSpace(inputAngleText))
            {
                MessageBox.Show($"{FindResource("Cannotempty")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            // 1. 输入校验（兼容整数/负数）
            if (!int.TryParse(inputAngleText.Trim(), out int newAngle) && !string.IsNullOrWhiteSpace(inputAngleText))
            {
                MessageBox.Show($"{FindResource("Pleaseenteravalidintegerangle")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. 区分模式校验角度范围
            bool isRCircle = IsRCircleMode();
            if (isRCircle)
            {
                // R圆模式：-60° ~ 60°
                if (newAngle < -60 || newAngle > 60)
                {
                    MessageBox.Show($"{FindResource("Shouldbe")} -60°~60°", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            else
            {
                // 直径线模式：0° ~ 360°（保留原规则）
                if (newAngle < 0 || newAngle > 360)
                {
                    MessageBox.Show($"{FindResource("Shouldbe")} 0°~360°", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            // 3. 检查是否已存在
            if (!string.IsNullOrWhiteSpace(inputAngleText) && IsAngleExistsInComboBox(targetComboBox, newAngle))
            {
                MessageBox.Show($"{FindResource("Anglealreadyexists")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 4. 添加到目标ComboBox（R圆模式保留负号显示）
            ComboBoxItem newItem = new ComboBoxItem
            {
                Content = $"{newAngle}°", // 负角度会显示为 "-60°"，无需额外处理
                Tag = newAngle.ToString()
            };
            targetComboBox.Items.Add(newItem);

            // 5. 自动选中新项
            targetComboBox.SelectedItem = newItem;

            // 6. 刷新显示
            if (IsMatSafe(YMat)) UpdateDisplay();
        }

        /// <summary>
        /// 通用删除角度方法（支持任意ComboBox）
        /// </summary>
        /// <param name="targetComboBox">目标下拉框（如cbDisplayAngle/cbDisplayRadius）</param>
        /// <param name="inputAngleText">输入的角度文本</param>
        //private void DeleteAngleFromComboBox(ComboBox targetComboBox)
        //{
        //    // 1. 校验是否有选中项
        //    if (targetComboBox.SelectedItem == null)
        //    {
        //        MessageBox.Show("请选择对应角度", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        //        return;
        //    }

        //    // 2. 获取选中项的角度值
        //    if (!(targetComboBox.SelectedItem is ComboBoxItem selectedItem) ||
        //        !int.TryParse(selectedItem.Tag?.ToString(), out int delAngle))
        //    {
        //        MessageBox.Show("选中项无效", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        //        return;
        //    }

        //    // 3. 弹出确认提示
        //    MessageBoxResult result = MessageBox.Show(
        //        $"是否确认删除 {delAngle}°？",
        //        "确认删除",
        //        MessageBoxButton.YesNo,
        //        MessageBoxImage.Question
        //    );
        //    if (result != MessageBoxResult.Yes)
        //    {
        //        return; // 点击“否”，不操作
        //    }

        //    // 4. 删除选中项
        //    targetComboBox.Items.Remove(selectedItem);

        //    // 5. 自动选中第一个项（若还有项）
        //    if (targetComboBox.Items.Count > 0)
        //    {
        //        targetComboBox.SelectedIndex = 0;
        //    }
        //    else
        //    {
        //        // 若下拉框为空，重置选中状态
        //        _selectedAngle = -1;
        //        _selectedRadius = -1;
        //    }

        //    // 6. 刷新显示
        //    if (IsMatSafe(YMat)) UpdateDisplay();
        //}
        // 新增：标记是否正在执行删除操作（屏蔽DLL调用）


        private void DeleteAngleFromComboBox(ComboBox targetComboBox)
        {
            _isDeletingAngle = true;
            try
            {
                if (targetComboBox.SelectedItem == null)
                {
                    MessageBox.Show($"{FindResource("DA")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!(targetComboBox.SelectedItem is ComboBoxItem selectedItem) ||
                    !int.TryParse(selectedItem.Tag?.ToString(), out int delAngle))
                {
                    MessageBox.Show($"{FindResource("Re-select")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                bool isRCircle = IsRCircleMode();
                //string modeName = isRCircle ? "R圆" : "直径线";

                MessageBoxResult result = MessageBox.Show(
                    $"{FindResource("Confirmdeletion")}",
                    $"{FindResource("DeleteConfirmation")}",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                // ========== 关键修复1：删除项后强制重置选中状态 ==========
                targetComboBox.Items.Remove(selectedItem);
                targetComboBox.SelectedItem = null; // 清空选中项
                targetComboBox.UpdateLayout(); // 强制刷新UI

                // ========== 关键修复2：重置全局选中状态（避免绘制已删除的选中项） ==========
                if (isRCircle)
                {
                    _selectedRadius = -1; // 清空选中半径，避免高亮已删除的圆环
                    displayRadius = -1;
                }
                else
                {
                    _selectedAngle = -1;
                    displayAngle = -1;
                }

                // 自动选中第一个项（仅UI层面）
                if (targetComboBox.Items.Count > 0)
                {
                    targetComboBox.SelectedIndex = 0;
                    // 同步更新全局状态（仅选中有效项）
                    if (isRCircle && targetComboBox.SelectedItem is ComboBoxItem newSelItem &&
                        int.TryParse(newSelItem.Tag?.ToString(), out int newRadius))
                    {
                        _selectedRadius = newRadius;
                        displayRadius = newRadius;
                    }
                    else if (!isRCircle && targetComboBox.SelectedItem is ComboBoxItem newSelItem1 &&
                        int.TryParse(newSelItem1.Tag?.ToString(), out int newAngle))
                    {
                        _selectedAngle = newAngle;
                        displayAngle = newAngle;
                    }
                }
                else
                {
                    // 删空时添加默认项
                    int defaultAngle = isRCircle ? 40 : 120;
                    ComboBoxItem defaultItem = new ComboBoxItem
                    {
                        Content = $"{defaultAngle}°",
                        Tag = defaultAngle.ToString()
                    };
                    targetComboBox.Items.Add(defaultItem);
                    targetComboBox.SelectedIndex = 0;
                    if (isRCircle)
                    {
                        _selectedRadius = defaultAngle;
                        displayRadius = defaultAngle;
                    }
                    else
                    {
                        _selectedAngle = defaultAngle;
                        displayAngle = defaultAngle;
                    }

                }

                // ========== 关键修复3：强制刷新显示（立即重绘画布） ==========
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, () =>
                {
                    if (IsMatSafe(YMat))
                    {
                        UpdateDisplay();
                    }
                });

                logger.Info($" {FindResource("DeleteA")}：{delAngle}°");
            }
            finally
            {
                _isDeletingAngle = false;
            }
        }
        private bool _isDeletingAngle = false;
        private void DeleteAngleFromComboBox(ComboBox targetComboBox, string inputAngleText)
        {
            // 1. 输入校验（兼容负数）
            if (!int.TryParse(inputAngleText.Trim(), out int delAngle))
            {
                MessageBox.Show("请输入有效的整数角度（支持负数）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. 查找目标项（负角度已兼容，无需修改）
            ComboBoxItem targetItem = null;
            foreach (ComboBoxItem item in targetComboBox.Items)
            {
                if (int.TryParse(item.Tag?.ToString(), out int existingAngle) && existingAngle == delAngle)
                {
                    targetItem = item;
                    break;
                }
            }

            // 3. 处理删除
            if (targetItem == null)
            {
                bool isRCircle = IsRCircleMode();
                string tipText = isRCircle ? "找不到该角度（范围-60°~60°）" : "找不到该角度（范围0°~360°）";
                MessageBox.Show(tipText, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            targetComboBox.Items.Remove(targetItem);
            // 自动选中第一个项
            if (targetComboBox.SelectedItem == targetItem && targetComboBox.Items.Count > 0)
            {
                targetComboBox.SelectedIndex = 1;
            }

            // 4. 刷新显示
            if (IsMatSafe(YMat)) UpdateDisplay();
        }
        /// <summary>
        /// 检查目标ComboBox中是否存在指定角度
        /// </summary>
        private bool IsAngleExistsInComboBox(ComboBox targetComboBox, int angle)
        {
            foreach (ComboBoxItem item in targetComboBox.Items)
            {
                if (int.TryParse(item.Tag?.ToString(), out int existingAngle) && existingAngle == angle)
                {
                    return true;
                }
            }
            return false;
        }

        // 直径线面板 - 添加角度
        private void BtnAddAngle_Diameter_Click(object sender, RoutedEventArgs e)
        {
            // 直接传入目标下拉框，无需输入框
            AddAngleToComboBox(cbDisplayAngle, txtAddAngle.Text);
            UpdateDisplay();
        }

        // 直径线面板 - 删除角度
        private void BtnDeleteAngle_Diameter_Click(object sender, RoutedEventArgs e)
        {
            DeleteAngleFromComboBox(cbDisplayAngle);

            UpdateDisplay();
        }
        // R圆面板 - 添加角度
        private void BtnAddAngle_RCircle_Click(object sender, RoutedEventArgs e)
        {
            AddAngleToComboBox(cbDisplayRadius, txtAddAngle1.Text);
            txtAddAngle1.Text = string.Empty;  // 新增：删除后立即刷新显示
            UpdateDisplay();
        }

        // R圆面板 - 删除角度
        private void BtnDeleteAngle_RCircle_Click(object sender, RoutedEventArgs e)
        {
            //DeleteAngleFromComboBox(cbDisplayRadius,txtDeleteAngle1.Text);
            DeleteAngleFromComboBox(cbDisplayRadius);
            //txtDeleteAngle1.Text = string.Empty;
            UpdateDisplay();
        }
        #endregion
        private void UpdateDisplay()
        {
            Mat? selectedMat = GetSelectedChannelMat(displayChannel);
            if (selectedMat == null || selectedMat.Empty())
                return;

            pseudoColorMat?.Dispose();
            Mat normalizedMat = new Mat();
            Mat colorMat = new Mat();
            Cv2.Normalize(selectedMat, normalizedMat, 0, 255, NormTypes.MinMax);
            Mat mat8U = new Mat();
            normalizedMat.ConvertTo(mat8U, MatType.CV_8UC1);
            Cv2.ApplyColorMap(mat8U, colorMat, ColormapTypes.Jet);
            normalizedMat.Dispose();
            mat8U.Dispose();

            // ========== 基础参数（核心修改：动态计算图像实际有效半径） ==========
            OpenCvSharp.Point centerPoint = new OpenCvSharp.Point(
                colorMat.Width / 2,  // 图像中心X（动态取图像宽度的一半）
                colorMat.Height / 2  // 图像中心Y（动态取图像高度的一半）
            );
            // 动态计算“图像实际有效半径”：取图像宽/高的较小值的一半（确保圆环在图像内）
            float imageActualRadius = Math.Min(colorMat.Width, colorMat.Height) / 2f;
            // 动态计算“角度系数”：让最大圆环的半径刚好等于图像实际有效半径
            double dynamicConoscopeCoefficient = imageActualRadius / MaxAngle;


            // ========== 1. 绘制背景同心圆（均匀包裹图像） ==========
            Scalar bgCircleColor = new Scalar(0, 255, 255); // 黄色
            int bgCircleLineWidth = 10;
            int circleCount = 6; // 固定6条圆环
                                 // 均匀分布：最大半径 = 图像实际有效半径，按6条均分
            float bgCircleInterval = imageActualRadius / circleCount;

            for (int i = 1; i <= circleCount; i++)
            {
                // 直接用图像像素半径（不再依赖MaxAngle换算）
                float circleRadius = i * bgCircleInterval;
                Cv2.Circle(
                    colorMat,
                    centerPoint,
                    (int)circleRadius,
                    bgCircleColor,
                    bgCircleLineWidth,
                    LineTypes.AntiAlias // 抗锯齿
                );
            }


            // ========== 2. 直径线/R圆模式的绘制逻辑（同步修改半径计算） ==========
            Scalar yellowColor = new Scalar(0, 255, 255);
            Scalar purpleColor = new Scalar(255, 0, 255);
            int yellowLineWidth = 15;
            int purpleLineWidth = 30;
            int yellowCircleWidth = 15;
            int purpleCircleWidth = 30;

            string currentBtnText = btnSwitchChart.Content.ToString();
            string rCircleTitle = FindResource("Plot.Title.RCircle").ToString();
            string diameterTitle = FindResource("Plot.Title.DiameterLine").ToString();


            // 直径线模式：直线端点匹配图像边缘
            if (currentBtnText == rCircleTitle)
            {
                List<double> angleValues = GetAllComboBoxValues(cbDisplayAngle);
                foreach (double angle in angleValues)
                {
                    double radian = -angle * Math.PI / 180.0;
                    // 直线端点取图像实际有效半径（确保直线贯穿图像）
                    OpenCvSharp.Point startPoint = new OpenCvSharp.Point(
                        (int)(centerPoint.X - imageActualRadius * Math.Cos(radian)),
                        (int)(centerPoint.Y - imageActualRadius * Math.Sin(radian))
                    );
                    OpenCvSharp.Point endPoint = new OpenCvSharp.Point(
                        (int)(centerPoint.X + imageActualRadius * Math.Cos(radian)),
                        (int)(centerPoint.Y + imageActualRadius * Math.Sin(radian))
                    );
                    Cv2.Line(colorMat, startPoint, endPoint, yellowColor, yellowLineWidth, LineTypes.AntiAlias);

                    // 备注位置
                    OpenCvSharp.Point labelPos = new OpenCvSharp.Point(
                        (int)(endPoint.X + 15 * Math.Cos(radian)),
                        (int)(endPoint.Y + 15 * Math.Sin(radian))
                    );
                    DrawAngleLabel(colorMat, labelPos, $"{angle}(A)", yellowColor, fontScale: 4);
                }

                // 选中项高亮
                if (_selectedAngle != -1)
                {
                    double radian = -_selectedAngle * Math.PI / 180.0;
                    OpenCvSharp.Point startPoint = new OpenCvSharp.Point(
                        (int)(centerPoint.X - imageActualRadius * Math.Cos(radian)),
                        (int)(centerPoint.Y - imageActualRadius * Math.Sin(radian))
                    );
                    OpenCvSharp.Point endPoint = new OpenCvSharp.Point(
                        (int)(centerPoint.X + imageActualRadius * Math.Cos(radian)),
                        (int)(centerPoint.Y + imageActualRadius * Math.Sin(radian))
                    );
                    Cv2.Line(colorMat, startPoint, endPoint, purpleColor, purpleLineWidth, LineTypes.AntiAlias);

                    OpenCvSharp.Point labelPos = new OpenCvSharp.Point(
                        (int)(endPoint.X + 15 * Math.Cos(radian)),
                        (int)(endPoint.Y + 15 * Math.Sin(radian))
                    );
                    DrawAngleLabel(colorMat, labelPos, $"{_selectedAngle}(A)", purpleColor, fontScale: 4);
                }
            }


            // R圆模式：圆环半径匹配图像实际有效区域
            if (currentBtnText == diameterTitle)
            {
                // 强制刷新下拉框并读取最新值列表
                cbDisplayRadius.UpdateLayout();
                List<double> radiusValues = GetAllComboBoxValues(cbDisplayRadius);

                // 绘制所有当前有效的R圆角度（已删除的不会出现在列表中）
                foreach (double radius in radiusValues)
                {
                    float radiusPixel = (float)(Math.Abs(radius) / MaxAngle * imageActualRadius);
                    if (radiusPixel > imageActualRadius) continue;

                    Cv2.Circle(
                        colorMat,
                        centerPoint,
                        (int)radiusPixel,
                        yellowColor,
                        yellowCircleWidth,
                        LineTypes.AntiAlias
                    );

                    // ========== 正负角度区分标签位置 ==========
                    OpenCvSharp.Point labelPos;
                    if (radius >= 0)
                    {
                        // 正角度：显示在右边红框位置（中心右侧）
                        labelPos = new OpenCvSharp.Point((int)(centerPoint.X + radiusPixel + 20), (int)centerPoint.Y);
                        // 避免超出图像右边界
                        if (labelPos.X > colorMat.Width - 100)
                        {
                            labelPos.X = (int)(centerPoint.X + radiusPixel - 100);
                        }
                    }
                    else
                    {
                        // 负角度：显示在左边红框位置（中心左侧）
                        labelPos = new OpenCvSharp.Point((int)(centerPoint.X - radiusPixel - 100), (int)centerPoint.Y);
                        // 避免超出图像左边界
                        if (labelPos.X < 0)
                        {
                            labelPos.X = (int)(centerPoint.X - radiusPixel + 20);
                        }
                    }
                    DrawAngleLabel(colorMat, labelPos, $"{radius}(R)", yellowColor, fontScale: 4);
                }

                // 选中项高亮（仅绘制当前选中的有效半径）
                if (_selectedRadius != -1 && radiusValues.Contains(_selectedRadius))
                {
                    float radiusPixel = (float)(Math.Abs(_selectedRadius) / MaxAngle * imageActualRadius);
                    if (radiusPixel > imageActualRadius) return;

                    Cv2.Circle(
                        colorMat,
                        centerPoint,
                        (int)radiusPixel,
                        purpleColor,
                        purpleCircleWidth,
                        LineTypes.AntiAlias
                    );

                    // ========== 核心修改：选中项的正负角度标签位置 ==========
                    OpenCvSharp.Point labelPos;
                    if (_selectedRadius >= 0)
                    {
                        // 正角度：右边红框
                        labelPos = new OpenCvSharp.Point((int)(centerPoint.X + radiusPixel + 20), (int)centerPoint.Y);
                        if (labelPos.X > colorMat.Width - 100)
                        {
                            labelPos.X = (int)(centerPoint.X + radiusPixel - 100);
                        }
                    }
                    else
                    {
                        // 负角度：左边红框
                        labelPos = new OpenCvSharp.Point((int)(centerPoint.X - radiusPixel - 100), (int)centerPoint.Y);
                        if (labelPos.X < 0)
                        {
                            labelPos.X = (int)(centerPoint.X - radiusPixel + 20);
                        }
                    }
                    DrawAngleLabel(colorMat, labelPos, $"{_selectedRadius}(R)", purpleColor, fontScale: 4);
                }
            }



            // ========== 绘制XY轴（贯穿图像） ==========
            Scalar redColor = new Scalar(0, 0, 255);
            int redLineWidth = 20;
            OpenCvSharp.Point xAxisStart = new OpenCvSharp.Point(0, centerPoint.Y);
            OpenCvSharp.Point xAxisEnd = new OpenCvSharp.Point(colorMat.Width, centerPoint.Y);
            Cv2.Line(colorMat, xAxisStart, xAxisEnd, redColor, redLineWidth, LineTypes.AntiAlias);

            OpenCvSharp.Point yAxisStart = new OpenCvSharp.Point(centerPoint.X, 0);
            OpenCvSharp.Point yAxisEnd = new OpenCvSharp.Point(centerPoint.X, colorMat.Height);
            Cv2.Line(colorMat, yAxisStart, yAxisEnd, redColor, redLineWidth, LineTypes.AntiAlias);


            // ========== 更新显示 ==========
            pseudoColorMat = colorMat;
            WriteableBitmap writeableBitmap = pseudoColorMat.ToWriteableBitmap();
            imgDisplay.Source = writeableBitmap;

            _imgNaturalWidth = writeableBitmap.PixelWidth;
            _imgNaturalHeight = writeableBitmap.PixelHeight;
            ResetImageScale();
            PlotDiameterLineChart();
            PlotRCircleChart();
            // 额外确保AutoScale
            ResetChartScales();
        }
        /// <summary>
        /// 辅助方法：读取ComboBox中所有ComboBoxItem的Tag值（转为double）
        /// </summary>
        /// <param name="comboBox">目标下拉框（cbDisplayAngle/cbDisplayRadius）</param>
        /// <returns>所有有效的角度/半径值列表</returns>
        private List<double> GetAllComboBoxValues(ComboBox comboBox)
        {
            List<double> values = new List<double>();
            if (comboBox == null || comboBox.Items.Count == 0)
                return values;

            foreach (var item in comboBox.Items)
            {
                if (item is ComboBoxItem comboItem && !string.IsNullOrEmpty(comboItem.Tag?.ToString()))
                {
                    // 尝试转换为double，兼容整数/小数角度值
                    if (double.TryParse(comboItem.Tag.ToString(), out double value))
                    {
                        values.Add(value);
                    }
                }
            }
            return values;
        }

        private Mat? GetSelectedChannelMat(ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => XMat,
                ExportChannel.Y => YMat,
                ExportChannel.Z => ZMat,
                _ => YMat
            };
        }

        /// <summary>
        /// 使用ScottPlot绘制直径线图表
        /// </summary>
        private void PlotDiameterLineChart()
        {
            Mat? selectedMat = GetSelectedChannelMat(displayChannel);
            if (selectedMat == null || selectedMat.Empty())
                return;

            var diameterLine = CreateDiameterLine(displayAngle, selectedMat);

            wpfPlotDiameterLine.Plot.Clear();

            if (diameterLine.RgbData.Count == 0)
            {
                wpfPlotDiameterLine.Plot.Axes.SetLimits(-80, 80, 0, 600);
                wpfPlotDiameterLine.Refresh();
                return;
            }

            // Get values for the selected channel
            double[] positions = diameterLine.RgbData.Select(s => s.Position).ToArray();
            double[] values = diameterLine.RgbData.Select(s => GetChannelValue(s, displayChannel)).ToArray();

            // 绘制线图
            var scatter = wpfPlotDiameterLine.Plot.Add.Scatter(positions, values);
            scatter.LineWidth = 2;
            scatter.Color = ScottPlot.Color.FromHex("#1f77b4");

            wpfPlotDiameterLine.Plot.Axes.AutoScale();
            wpfPlotDiameterLine.Plot.Title(DC);
            wpfPlotDiameterLine.Refresh();
        }
        string DC = (string)Application.Current.FindResource("Plot.Title.DiameterLine");
        string RC = (string)Application.Current.FindResource("VAM.RCircle");
        string CDC = (string)Application.Current.FindResource("VAM.CircumferentialDistributionCurve");
        string CA = (string)Application.Current.FindResource("Plot.Axis.X");
        string Pixel = (string)Application.Current.FindResource("Plot.Axis.Y");
        private void PlotRCircleChart()
        {
            var circleLine = CreateRCircleLine(displayRadius);

            wpfPlotRCircle.Plot.Clear();

            if (circleLine.RgbData.Count == 0)
            {
                wpfPlotRCircle.Plot.Axes.AutoScale();
                wpfPlotRCircle.Refresh();
                return;
            }

            // Extract position (circumferential angle 0-359°)
            double[] positions = circleLine.RgbData.Select(s => s.Position).ToArray();


            // Y channel - Gray color
            double[] yValues = circleLine.RgbData.Select(s => s.Y).ToArray();
            var yScatter = wpfPlotRCircle.Plot.Add.Scatter(positions, yValues);
            yScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
            yScatter.LineWidth = 2;
            yScatter.LegendText = "Y";


            wpfPlotRCircle.Plot.Title($"{RC} {displayRadius}° {CDC}");
            wpfPlotRCircle.Plot.XLabel($"{CA}");
            wpfPlotRCircle.Plot.YLabel($"{Pixel}");
            wpfPlotRCircle.Plot.Legend.IsVisible = true;
            wpfPlotRCircle.Plot.Axes.AutoScale();

            wpfPlotRCircle.Refresh();
        }

        /// <summary>
        /// 创建指定半径角度的R圆数据（按原代码采样模式）
        /// </summary>
        public ConcentricCircleLine CreateRCircleLine(double radiusAngle)
        {
            ConcentricCircleLine circleLine = new ConcentricCircleLine
            {
                RadiusAngle = radiusAngle
            };

            if (radiusAngle == 0)
            {
                // 修复：用Math.Floor替代Round，避免越界
                int ix = (int)Math.Floor(center.X);
                int iy = (int)Math.Floor(center.Y);
                // 二次边界校验
                ix = Math.Clamp(ix, 0, YMat.Width - 1);
                iy = Math.Clamp(iy, 0, YMat.Height - 1);

                double X = 0, Y = 0, Z = 0;
                ExtractPixelValues(ix, iy, out X, out Y, out Z);

                // Fill all 360 samples with the center point value
                for (int anglePos = 0; anglePos <= 360; anglePos++)
                {
                    circleLine.RgbData.Add(new RgbSample
                    {
                        Position = anglePos,
                        X = X,
                        Y = Y,
                        Z = Z
                    });
                }
            }
            else
            {
                // Calculate radius in pixels for this degree angle
                double radiusPixels = radiusAngle / ConoscopeCoefficient;

                // Sample 360 points around the circle (same as original)
                int numSamples = 360;
                for (int i = 0; i <= numSamples; i++)
                {
                    double anglePos = i * 360.0 / numSamples; // 0.5 degree intervals
                    double radians = -anglePos * Math.PI / 180.0;
                    double x = center.X + radiusPixels * Math.Cos(radians);
                    double y = center.Y + radiusPixels * Math.Sin(radians);

                    // Ensure coordinates are within bounds
                    int ix = Math.Max(0, Math.Min(YMat.Width - 1, (int)Math.Round(x)));
                    int iy = Math.Max(0, Math.Min(YMat.Height - 1, (int)Math.Round(y)));

                    // Extract RGB values
                    double X = 0, Y = 0, Z = 0;

                    ExtractPixelValues(ix, iy, out X, out Y, out Z);

                    circleLine.RgbData.Add(new RgbSample
                    {
                        Position = anglePos, // 0 to 360 with 0.5 degree intervals
                        X = X,
                        Y = Y,
                        Z = Z
                    });
                }
            }

            return circleLine;
        }

        /// <summary>
        /// 创建指定角度的直径线数据（按原代码采样模式）
        /// </summary>
        private PolarAngleLine CreateDiameterLine(double angle, Mat mat)
        {

            PolarAngleLine polarLine = new PolarAngleLine
            {
                Angle = angle
            };


            double angleRadians = -angle * Math.PI / 180.0;
            System.Windows.Point start = new System.Windows.Point(
                center.X - imageRadius * Math.Cos(angleRadians),
                center.Y - imageRadius * Math.Sin(angleRadians)
            );
            System.Windows.Point end = new System.Windows.Point(
                center.X + imageRadius * Math.Cos(angleRadians),
                center.Y + imageRadius * Math.Sin(angleRadians)
            );

            // Calculate line length in pixels (same as original)
            double lineLength = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
            int numSamples = (int)lineLength;

            if (numSamples <= 1)
                return polarLine;

            // Sample points along the line (same as original)
            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)(numSamples - 1);
                double x = start.X + t * (end.X - start.X);
                double y = start.Y + t * (end.Y - start.Y);

                // Ensure coordinates are within bounds
                int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                // Map position from pixel index to -MaxAngle to MaxAngle range (same as original)
                // Linear mapping: position = -MaxAngle + (i / (numSamples - 1)) * (2 * MaxAngle)
                double position = -MaxAngle + (i / (double)(numSamples - 1)) * (2 * MaxAngle);

                double X = 0, Y = 0, Z = 0;
                ExtractPixelValues(ix, iy, out X, out Y, out Z);

                polarLine.RgbData.Add(new RgbSample
                {
                    Position = position,
                    X = X,
                    Y = Y,
                    Z = Z
                });
            }

            return polarLine;
        }

        // CVVAMAnalyzer.cs 中新增
        private bool _isDataValid = false; // 标记数据是否有效

        // 切换Flow时调用（替代ResetAllResources）
        public void ResetDataWithoutDispose()
        {
            // 仅清空数据、重置参数，不释放Mat
            _isDataValid = false;
            displayAngle = 120;
            displayRadius = 40;
            // 清空图表
            if (wpfPlotDiameterLine != null && wpfPlotDiameterLine.Plot != null)
            {
                wpfPlotDiameterLine.Plot.Clear();
                wpfPlotDiameterLine.Plot.Axes.AutoScale();
                wpfPlotDiameterLine.Refresh();
            }

            if (wpfPlotRCircle != null && wpfPlotRCircle.Plot != null)
            {
                wpfPlotRCircle.Plot.Clear();
                wpfPlotRCircle.Plot.Axes.AutoScale();
                wpfPlotRCircle.Refresh();
            }

            imgDisplay.Source = null;
        }
        /// <summary>
        /// 公开方法：强制刷新图表缩放
        /// </summary>
        public void RefreshChartsAutoScale()
        {
            if (!IsVisible) return;

            try
            {
                Dispatcher.Invoke(() =>
                {
                    ResetChartScales();
                });
            }
            catch (Exception ex)
            {
                logger.Error("Failed to refresh chart AutoScale", ex);
            }
        }
        public void UpdateVAMParams(double maxAngle, double conoscopeCoefficient)
        {
            // 更新VAM的核心参数（与坐标/角度映射逻辑强相关）
            this.MaxAngle = maxAngle;
            this.ConoscopeCoefficient = conoscopeCoefficient;

            // 参数更新后可同步刷新显示（若需要）
            this.displayAngle = 120; // 重置默认显示角度（根据业务需求调整）
            this.displayRadius = 40; // 重置默认显示半径
        }
        private void ExtractPixelValues(int ix, int iy, out double X, out double Y, out double Z)
        {
            X = Y = Z = 0;
            // 新增：空值保护
            if (XMat != null && !XMat.Empty() && ix < XMat.Width && iy < XMat.Height)
                X = XMat.At<float>(iy, ix);
            if (YMat != null && !YMat.Empty() && ix < YMat.Width && iy < YMat.Height)
                Y = YMat.At<float>(iy, ix);
            if (ZMat != null && !ZMat.Empty() && ix < ZMat.Width && iy < ZMat.Height)
                Z = ZMat.At<float>(iy, ix);

            //if (XMat != null)
            //    X = XMat.At<float>(iy, ix);
            //if (YMat != null)
            //    Y = YMat.At<float>(iy, ix);
            //if (ZMat != null)
            //    Z = ZMat.At<float>(iy, ix);
        }


        private double GetChannelValue(RgbSample sample, ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => sample.X,
                ExportChannel.Y => sample.Y,
                ExportChannel.Z => sample.Z,
                _ => 0
            };
        }


        private void BtnExportDiameter_Click1(object sender, RoutedEventArgs e)
        {
            try
            {
                

                // 1. 基础校验
                if (YMat == null || YMat.Empty())
                {
                    MessageBox.Show($"{FindResource("Nodata")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (cbDisplayAngle.SelectedItem is not ComboBoxItem selectedAngleItem ||
                    !int.TryParse(selectedAngleItem.Tag?.ToString(), out int currentAngle))
                {
                    MessageBox.Show($"{FindResource("VAM.Choosetheangle")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // ========== 关键修改：从 cbDisplayChannel 获取当前选中通道 ==========
                ExportChannel selectedExportChannel = ExportChannel.Y; // 兜底默认值
                if (cbDisplayChannel.SelectedItem is ComboBoxItem channelItem && !string.IsNullOrEmpty(channelItem.Tag?.ToString()))
                {
                    // 尝试将下拉框Tag值转换为 ExportChannel 枚举
                    if (!Enum.TryParse<ExportChannel>(channelItem.Tag.ToString(), out selectedExportChannel))
                    {
                        MessageBox.Show($"{FindResource("VAM.InvalidChannel")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Warning);
                        // 保留兜底值，继续执行（避免流程中断）
                        selectedExportChannel = ExportChannel.Y;
                    }
                    // 更新进度条
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _progressManager.UpdateProgress(10);
                    });
                }
                else
                {
                    MessageBox.Show($"{FindResource("VAM.PleaseSelectChannel")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. 调用DLL获取当前角度的直径线数据（传入选中的通道）
                bool dllSuccess = CallVamDllForDiameterLine(currentAngle);
                if (!dllSuccess || _dllAllAzimuthData == null || !_dllAllAzimuthData.ContainsKey(currentAngle))
                {
                    MessageBox.Show($"{FindResource("Interfacecallfailed")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _progressManager.UpdateProgress(40);
                });
                List<VamSamplePoint> currentAngleData = _dllAllAzimuthData[currentAngle];

                // 3. 选择导出路径（文件名中增加通道标识，提升可读性）
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"VAM_Azimuth_{currentAngle}°_{selectedExportChannel}_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Title = $"{FindResource("SaveAzimuth")}"
                };
                if (saveFileDialog.ShowDialog() != true) return;
                string exportPath = saveFileDialog.FileName;

                // 4. 按目标表格格式导出（传入选中的通道，不再使用默认 displayChannel）
                ExportSingleAngleToCsv(exportPath, currentAngle, currentAngleData, selectedExportChannel);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _progressManager.UpdateProgress(90);
                    MessageBox.Show($"{FindResource("Exportsuccessful")}！", $"{FindResource("Log.Success")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    _progressManager.Complete();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    
                    MessageBox.Show($"{FindResource("Exportfailed")}: {ex.Message}", $"{FindResource("Log.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
                    _progressManager.Fail();
                });
            }
        }

        /// <summary>
        /// 导出DLL返回的单个角度数据到CSV
        /// </summary>
        /// <summary>
        /// 导出单个角度的DLL数据（目标表格格式）
        /// </summary>
        private void ExportSingleAngleToCsv(string filePath, int currentAngle, List<VamSamplePoint> angleData, ExportChannel targetChannel)
        {

            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // 第1行：Measurement Date
                writer.WriteLine($"Measurement Date,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,");
                // 第2行：Instrument（增加通道标识）
                writer.WriteLine($"Instrument,VAM 60° (Channel: {targetChannel}),,,,,,,,,,,,");
                // 第3行：空行
                writer.WriteLine();
                // 第4行：列标题（B列为当前角度）
                writer.WriteLine($",,{currentAngle}°");

                // 遍历数据行（径向角从-60到60，使用传入的目标通道获取值）
                foreach (var sample in angleData.OrderBy(s => s.position))
                {
                    double radialAngle = sample.position; // 径向角（-60~60）
                    double value = GetChannelValueFromDll(sample, targetChannel); // 使用传入的通道，而非全局变量
                    writer.WriteLine($",{radialAngle:F0}°,{value:F5}");
                }
            }
        }
        //private void ExportAngleModeToCSV(string filePath, ExportChannel channel)
        //{
        //    Mat? selectedMat = GetSelectedChannelMat(channel);
        //    if (selectedMat == null || selectedMat.Empty())
        //        return;


        //    // Create angle lines from 0° to 180°
        //    var angleLines = CreateAngleLinesForExport(selectedMat);

        //    using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
        //    {
        //        if (angleLines.Count == 0)
        //            return;

        //        // Write CSV header: Phi \ Theta, followed by each Phi angle (0-180)
        //        StringBuilder headerLine = new StringBuilder();
        //        headerLine.Append("Phi \\ Theta");
        //        foreach (var line in angleLines)
        //        {
        //            headerLine.Append($",{line.Angle:F0}");
        //        }
        //        writer.WriteLine(headerLine.ToString());

        //        // Find the maximum number of samples across all lines
        //        int maxSamples = angleLines.Max(l => l.RgbData.Count);
        //        if (maxSamples == 0) return;

        //        // Export each row (Theta position from 0 to MaxAngle)
        //        for (int i = 0; i < maxSamples; i++)
        //        {
        //            StringBuilder dataLine = new StringBuilder();

        //            // Get Theta position from first line
        //            double theta = angleLines[0].RgbData.Count > i ? angleLines[0].RgbData[i].Position : 0;
        //            dataLine.Append($"{theta:F2}");

        //            // Add value for each Phi angle
        //            foreach (var line in angleLines)
        //            {
        //                if (line.RgbData.Count > i)
        //                {
        //                    double value = GetChannelValue(line.RgbData[i], channel);
        //                    dataLine.Append($",{value:F2}");
        //                }
        //                else
        //                {
        //                    dataLine.Append(",");
        //                }
        //            }
        //            writer.WriteLine(dataLine.ToString());
        //        }
        //    }
        //}

        /// <summary>
        /// 为导出创建从0°到180°的直径线数据
        /// </summary>
        private List<PolarAngleLine> CreateAngleLinesForExport(Mat mat)
        {
            var angleLines = new List<PolarAngleLine>();

            for (int phi = 0; phi <= 180; phi++)
            {
                angleLines.Add(ExportDiameterLine(phi, mat));
            }

            return angleLines;
        }

        private PolarAngleLine ExportDiameterLine(double angle, Mat mat)
        {

            PolarAngleLine polarLine = new PolarAngleLine
            {
                Angle = angle
            };

            double radians = angle * Math.PI / 180.0;
            // Sample points along the line (same as original)
            // 修复：采样范围从 -MaxAngle 到 MaxAngle
            for (int theta = (int)-MaxAngle; theta <= (int)MaxAngle; theta++)
            {
                double radiusPixels = Math.Abs(theta) / ConoscopeCoefficient;
                // 方向控制：负角度向反方向延伸
                double direction = theta >= 0 ? 1 : -1;

                double x = center.X + radiusPixels * Math.Cos(radians) * direction;
                double y = center.Y + radiusPixels * Math.Sin(radians) * direction;

                int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                double X = 0, Y = 0, Z = 0;
                ExtractPixelValues(ix, iy, out X, out Y, out Z);

                polarLine.RgbData.Add(new RgbSample
                {
                    Position = theta, // 保留负角度值
                    X = X,
                    Y = Y,
                    Z = Z
                });
            }

            return polarLine;
        }

        /// <summary>
        /// 导出R圆CSV
        /// </summary>
        private void BtnExportCircle_Click1(object sender, RoutedEventArgs e)
        {
            // 1. 前置校验：确保字典已初始化且有数据
            try
            {
                // 1. 基础数据有效性校验
                if (YMat == null || YMat.Empty())
                {
                    MessageBox.Show($"{FindResource("Nodata")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. 校验当前是否选中有效R圆半径角度
                if (cbDisplayRadius.SelectedItem is not ComboBoxItem selectedRadiusItem ||
                    !int.TryParse(selectedRadiusItem.Tag?.ToString(), out int currentRadius))
                {
                    MessageBox.Show($"{FindResource("VAM.Choosetheradiusangle")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
              
                // 3. 校验R圆数据缓存是否有效（仅当前半径）
                if (DllAllCircleData == null || !DllAllCircleData.Any(kv => kv.Key.polar == currentRadius))
                {
                    // 尝试调用DLL获取当前半径数据（兜底逻辑）
                    bool dllCallSuccess = CallVamDllForRCircle(currentRadius);
                    if (!dllCallSuccess || DllAllCircleData == null || !DllAllCircleData.Any(kv => kv.Key.polar == currentRadius))
                    {
                        MessageBox.Show($"{FindResource("Interfacecallfailed")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                // 更新进度条
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _progressManager.UpdateProgress(40);
                });
                // 4. 校验导出通道是否选中（复用圆环模式通道校验逻辑）
                var selectedChannels = GetCircleSelectedChannels();
                if (selectedChannels.Count == 0)
                {
                    MessageBox.Show($"{FindResource("VAM.Onechannel")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // 更新进度条
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _progressManager.UpdateProgress(50);
                });
                // 5. 选择保存路径（保留弹窗，文件名包含当前半径角度）
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"VAM_Polar_Angle_{currentRadius}°_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Title = $"{FindResource("Savepolarangle")}"
                };
                if (saveFileDialog.ShowDialog() != true) return;
                string basePath = Path.ChangeExtension(saveFileDialog.FileName, null);

                // 6. 提取当前半径的所有有效数据（按方位角排序）
                var currentRadiusData = DllAllCircleData
                    .Where(kv => kv.Key.polar == currentRadius)
                    .Select(kv => new RadiusDataItem // 显式模型封装数据
                    {
                        Azimuth = kv.Key.azimuth,
                        Value = GetChannelValueFromCircleSample(kv.Value, displayChannel1)
                    })
                    .OrderBy(d => d.Azimuth)
                    .ToList();
                // 更新进度条
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _progressManager.UpdateProgress(70);
                });
                // 7. 按1°步长补全数据（保证0°~360°完整覆盖，匹配业务需求）
                var fullRadiusData = ComplementRadiusDataBy1Step(currentRadius, currentRadiusData);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _progressManager.UpdateProgress(80);
                });
                // 8. 为每个选中通道导出独立CSV（仅当前半径）
                foreach (var channel in selectedChannels)
                {
                    // 构建最终文件路径（包含通道名和当前半径）
                    string csvFileName = $"{basePath}_{channel}_RCircle_{currentRadius}°.csv";
                    string fullCsvPath = Path.Combine(Path.GetDirectoryName(csvFileName) ?? "", Path.GetFileName(csvFileName));

                    // 调用导出方法（按目标表格格式写入）
                    ExportSingleRadiusToCsv_1Step(fullCsvPath, currentRadius, fullRadiusData, channel);
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 9. 导出成功提示
                    _progressManager.UpdateProgress(90);
                    MessageBox.Show($"{FindResource("Exportsuccessful")}！", $"{FindResource("Log.Success")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    _progressManager.Complete();
                });
               
               
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _progressManager.Fail();
                    logger.Error($"{FindResource("Exportfailed")}：{ex.Message}", ex);
                    MessageBox.Show($"{FindResource("Exportfailed")}: {ex.Message}", $"{FindResource("Log.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
                });
              
            }

        }
        /// <summary>
        /// 补全当前半径数据为1°步长（0°~360°完整覆盖）
        /// </summary>
        /// <param name="currentRadius">当前选中半径</param>
        /// <param name="sourceData">原始数据</param>
        /// <returns>1°步长补全后的数据</returns>
        private List<RadiusDataItem> ComplementRadiusDataBy1Step(int currentRadius, List<RadiusDataItem> sourceData)
        {
            // 构建1°步长的完整方位角集合
            var fullData = new List<RadiusDataItem>();
            var sourceDataDict = sourceData.ToDictionary(d => Math.Round(d.Azimuth, 0), d => d.Value);

            for (int azimuth = 0; azimuth < 360; azimuth++)
            {
                // 存在数据则取真实值，不存在则赋值0（可按需改为插值）
                if (sourceDataDict.TryGetValue(azimuth, out double value))
                {
                    fullData.Add(new RadiusDataItem
                    {
                        Azimuth = azimuth,
                        Value = value
                    });
                }
                else
                {
                    fullData.Add(new RadiusDataItem
                    {
                        Azimuth = azimuth,
                        Value = 0.0
                    });
                }
            }

            return fullData;
        }
        /// <summary>
        /// 导出单个半径+单个通道的CSV（适配目标表格格式）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="currentRadius">当前半径</param>
        /// <param name="radiusData">完整数据</param>
        /// <param name="channel">导出通道</param>
        private void ExportSingleRadiusToCsv_1Step(string filePath, int currentRadius, List<RadiusDataItem> radiusData, ExportDataType channel)
        {
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // 标准化表头（包含通道信息和当前半径）
                writer.WriteLine($"Measurement Date,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,");
                writer.WriteLine($"Instrument,VAM R-Circle {currentRadius}° (Channel: {channel}),,,,,,,,,,,,,");
                writer.WriteLine(); // 空行分隔
                writer.WriteLine($",,{currentRadius}°"); // 列标题（当前半径）

                // 写入1°步长完整数据
                foreach (var data in radiusData)
                {
                    double azimuthAngle = data.Azimuth;
                    double value = data.Value;
                    writer.WriteLine($",{azimuthAngle:F0}°,{value:F5}");
                }
            }
        }
        // 辅助方法：获取当前选中的半径（替换为你原有业务逻辑，仅作占位）
        //private int GetCurrentSelectedRadius()
        //{
        //    // 此处替换为你原有获取currentRadius的逻辑，示例返回字典中第一个有效半径
        //    return DllAllCircleData.Select(kv => kv.Key.polar).FirstOrDefault();
        //}
        private class RadiusDataItem
        {
            public double Azimuth { get; set; } // 方位角
            public double Value { get; set; }   // 通道值
        }

        /// <summary>
        /// 显示角度选择改变
        /// </summary>
        private bool _isFirstLoad = true;
        private void CbDisplayAngle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 步骤1：首次加载（启动时）直接标记为非首次，不执行后续逻辑
            if (_isDeletingAngle) return; // 删除过程中跳过
            if (_isFirstLoad)
            {
                _isFirstLoad = false;
                return;
            }

            if (cbDisplayAngle.SelectedItem is ComboBoxItem item && item.Tag is string angleStr)
            {
                if (int.TryParse(angleStr, out int angle))
                {
                    displayAngle = angle;
                    _selectedAngle = angle;
                    _selectedRadius = -1;

                    if (IsMatSafe(YMat))
                    {
                        bool dllCallSuccess = CallVamDllForDiameterLine(angle);
                        if (dllCallSuccess)
                        {
                            UpdateDisplay();
                        }
                        else
                        {
                            MessageBox.Show($"{FindResource("Interfacecallfailed")}", $"{FindResource("Prompt")}");
                            UpdateDisplay();
                        }
                    }
                    else
                    {
                        MessageBox.Show($"{FindResource("Reopen")}", $"{FindResource("Prompt")}");
                    }
                }
            }
        }


        /// <summary>
        /// 显示通道选择改变
        /// </summary>
        private void CbDisplayChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDisplayChannel.SelectedItem is ComboBoxItem item && item.Tag is string channelStr)
            {
                if (Enum.TryParse<ExportChannel>(channelStr, out var channel))
                {
                    displayChannel = channel;
                    if (YMat != null && !YMat.Empty())
                    {
                        UpdateDisplay();
                    }
                }
            }
        }

        /// <summary>
        /// 显示半径选择改变
        /// </summary>
        private void CbDisplayRadius_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            // 删除过程中跳过所有逻辑（避免触发DLL调用）
            if (_isDeletingAngle) return;

            if (cbDisplayRadius.SelectedItem is ComboBoxItem item && item.Tag is string radiusStr)
            {
                if (int.TryParse(radiusStr, out int radius))
                {
                    if (radius < -60 || radius > 60)
                    {
                        MessageBox.Show($"{FindResource("-60~60")}", $"{FindResource("Prompt")}");
                        return;
                    }
                    displayRadius = radius;
                    _selectedRadius = radius;
                    _selectedAngle = -1;

                    if (IsMatSafe(YMat))
                    {
                        bool dllCallSuccess = CallVamDllForRCircle(radius);
                        if (dllCallSuccess)
                        {
                            UpdateDisplay();
                        }
                        else
                        {
                            MessageBox.Show($"{FindResource("Interfacecallfailed")}", $"{FindResource("Prompt")}");
                            UpdateDisplay();
                        }
                    }
                }
            }
        }
        #region 实现 DLL 调用核心方法（直径线 + R 圆）
        /// <summary>
        /// 调用DLL接口获取直径线数据，更新直径线图表
        /// </summary>
        /// <param name="targetAngle">选中的直径线角度</param>
        /// <returns>是否调用成功</returns>
        private bool CallVamDllForDiameterLine(int targetAngle)
        {
            try
            {
                // 步骤1：基础校验
                if (!IsMatSafe(XMat) || !IsMatSafe(YMat) || !IsMatSafe(ZMat))
                {
                    logger.Error($"{FindResource("XYZnull")}");
                    return false;
                }
                if (center.X == 0 && center.Y == 0)
                {
                    logger.Error($"{FindResource("Uninitialized")}");
                    return false;
                }

                // 步骤2：获取图像基础参数（修复BPP计算）
                int imgWidth = YMat.Width;
                int imgHeight = YMat.Height;
                int bpp = YMat.Depth() switch
                {
                    MatType.CV_8U => 8,
                    MatType.CV_16U => 16,
                    MatType.CV_32F => 32,
                    _ => 16
                };
                int elementSize = bpp / 8; // 单个通道像素的字节数

                // 步骤3：修复XYZ数据拼接（核心）
                // byte[] xyzData = MergeXYZToInterleaved(XMat, YMat, ZMat); // 调用新的拼接方法

                if (dataXyz == null)
                {
                    return false;
                }

                // 步骤4：构建XYZ的ImageData（匹配DLL入参）
                ImageData xyzImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 3, // 关键：XYZ是3通道（交叉存储）
                    data = dataXyz
                };

                // 步骤5：构建空的BGR ImageData（无BGR数据时传null）
                ImageData bgrImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 3,
                    data = null
                };
                // 先获取极角范围
                int polarRHO = int.TryParse(txtLinePolarRHO.Text.Trim(), out int rho) ? rho : 60;
                // 由间隔角度计算采样点数
                int _pointNumLine = (int)(Math.Abs(2 * polarRHO) / _linePolarInterval) + 1;
                // 步骤6：修复JSON参数（修正角度符号+添加通道）
                string staticJson = JsonConvert.SerializeObject(new
                {
                    debugCfg = new
                    {
                        Debug = false,
                        debugPath = "Result\\",
                        debugImgResize = 2
                    },
                    azimuthalAngle = targetAngle, // 匹配DLL预期
                    polar_RHO = 60.0,//线条角度
                    polar_Angle = 60.0,//方位角
                    pixelToAngle = ConoscopeCoefficient,
                    pointNumLine = _pointNumLine,  // 采样点数量
                    pointNumCircle = 60, // 还原为60，避免DLL数组越界
                    center = new { x = center.X, y = center.Y },
                    displayChannel = displayChannel.ToString() // 新增：传递选中通道
                });

                // 步骤7：打印参数日志（调试用）
                logger.Info($"DLL{FindResource("Callparameters")}：targetAngle={targetAngle}, center=({center.X},{center.Y}), bpp={bpp}, imgSize=({imgWidth}x{imgHeight})");
                logger.Info($"JSON{FindResource("Parameters")}：{staticJson}");

                // 步骤8：调用DLL封装方法
                string resultJson;
                ImageData showImage;
                CV_AliResType callResult = CallCV_Ali_calcVam(
                    bgrImageData,
                    xyzImageData,
                    staticJson,
                    out resultJson,
                    out showImage
                );

                // 步骤9：处理DLL返回结果
                if (callResult != CV_AliResType.SUCCESS && callResult != CV_AliResType.PART_SUCCESS)
                {
                    logger.Error($"DLL{FindResource("Callfailed")}，{FindResource("Errorcode")}：{callResult}");
                    return false;
                }

                // 步骤10：清理showImage内存（避免泄漏）
                if (showImage.data != null)
                {
                    Array.Clear(showImage.data, 0, showImage.data.Length);
                    showImage.data = null;
                }

                // 步骤11：解析JSON（清理空字符）
                string cleanResultJson = resultJson.Trim('\0').Trim();
                if (string.IsNullOrEmpty(cleanResultJson))
                {
                    logger.Error($"{FindResource("nullJSON")}");
                    return false;
                }

                VamResultRoot vamResult = JsonConvert.DeserializeObject<VamResultRoot>(cleanResultJson);
                if (vamResult?.result?.line?.Data == null || vamResult.result.line.Data.Count == 0)
                {
                    logger.Error($"{FindResource("Azimuthnull")}");
                    return false;
                }

                // 步骤12：更新图表
                UpdateDiameterLineChartFromDll(vamResult.result.line.Data);
                _dllAllAzimuthData[targetAngle] = vamResult.result.line.Data; // 缓存当前角度数据
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"{FindResource("Azimuthdataex")}", ex);
                return false;
            }

        }
        //缓存所有方位角数据
        /// <summary>
        /// 重构：支持传入动态参数（通道、采样点数量、极径/极角）
        /// </summary>
        public bool CallVamDllForAllAzimutha(ExportChannel exportChannel, int pointNumLine = 360, double polarRHO = 60.0, double polarAngle = 60.0)
        {
            try
            {
                if (!IsMatSafe(XMat) || !IsMatSafe(YMat) || !IsMatSafe(ZMat) || center.X == 0 || center.Y == 0)
                {
                    logger.Error($"{FindResource("Datanotloadedorimagecenternotinitialized")}");
                    return false;
                }

                int imgWidth = YMat.Width;
                int imgHeight = YMat.Height;
                int bpp = YMat.Depth() switch
                {
                    MatType.CV_8U => 8,
                    MatType.CV_16U => 16,
                    MatType.CV_32F => 32,
                    _ => 16
                };

                if (dataXyz == null) return false;

                ImageData xyzImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 3,
                    data = dataXyz
                };
                ImageData bgrImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 3,
                    data = null
                };

                // 清空原有缓存
                _dllAllAzimuthData.Clear();

                // 遍历0°~180°所有方位角，调用DLL并缓存数据
                for (int azimuth = 0; azimuth <= 180; azimuth++)
                {
                    // 使用传入的动态参数构建JSON
                    string staticJson = JsonConvert.SerializeObject(new
                    {
                        debugCfg = new { Debug = false, debugPath = "Result\\", debugImgResize = 2 },
                        azimuthalAngle = azimuth,
                        polar_RHO = polarRHO, // 动态参数
                        polar_Angle = polarAngle, // 动态参数
                        pixelToAngle = ConoscopeCoefficient,
                        pointNumLine = pointNumLine, // 动态参数（采样点）
                        pointNumCircle = 60,
                        center = new { x = center.X, y = center.Y },
                        displayChannel = exportChannel.ToString() // 动态参数（通道）
                    });

                    string resultJson;
                    ImageData showImage;
                    CV_AliResType callResult = CallCV_Ali_calcVam(bgrImageData, xyzImageData, staticJson, out resultJson, out showImage);

                    if (callResult != CV_AliResType.SUCCESS && callResult != CV_AliResType.PART_SUCCESS)
                    {
                        logger.Error($"{FindResource("Azimuth")}{azimuth}° DLL{FindResource("Callfailed")}，{FindResource("Errorcode")}：{callResult}");
                        continue;
                    }

                    if (showImage.data != null)
                    {
                        Array.Clear(showImage.data, 0, showImage.data.Length);
                        showImage.data = null;
                    }

                    string cleanJson = resultJson.Trim('\0').Trim();
                    if (string.IsNullOrEmpty(cleanJson)) continue;

                    VamResultRoot result = JsonConvert.DeserializeObject<VamResultRoot>(cleanJson);
                    if (result?.result?.line?.Data != null && result.result.line.Data.Count > 0)
                    {
                        _dllAllAzimuthData[azimuth] = result.result.line.Data;
                    }
                }

                if (_dllAllAzimuthData.ContainsKey(0) && !_dllAllAzimuthData.ContainsKey(180))
                {
                    var zeroData = _dllAllAzimuthData[0];
                    var reversed180Data = new List<VamSamplePoint>();

                    // 核心修复：反转径向角度的数据顺序
                    // 0°的+60° → 180°的-60°，0°的-60° → 180°的+60°
                    for (int i = zeroData.Count - 1; i >= 0; i--)
                    {
                        var originalPoint = zeroData[i];
                        // 径向角度取反（保持数值，反转位置）
                        reversed180Data.Add(new VamSamplePoint
                        {
                            X = originalPoint.X,
                            Y = originalPoint.Y,
                            Z = originalPoint.Z,
                            cie_x = originalPoint.cie_x,
                            cie_y = originalPoint.cie_y,
                            position = -originalPoint.position // 关键：径向角度取反
                        });
                    }

                    _dllAllAzimuthData[180] = reversed180Data;
                }

                return _dllAllAzimuthData.Count > 0;
            }
            catch (Exception ex)
            {
                logger.Error($"{FindResource("Failedtoobtainfullazimuthdata")}", ex);
                return false;
            }
        }

        public async Task<bool> CallVamDllForAllAzimuthAsync( ExportChannel exportChannel,int pointNumLine = 360,double polarRHO = 60.0, double polarAngle = 60.0, Action<int> onProgressUpdate = null)
        {
            try
            {
                // 基础校验
                if (!IsMatSafe(XMat) || !IsMatSafe(YMat) || !IsMatSafe(ZMat) || center.X == 0 || center.Y == 0)
                {
                    logger.Error($"{FindResource("Datanotloadedorimagecenternotinitialized")}");
                    return false;
                }

                int imgWidth = YMat.Width;
                int imgHeight = YMat.Height;
                int bpp = YMat.Depth() switch
                {
                    MatType.CV_8U => 8,
                    MatType.CV_16U => 16,
                    MatType.CV_32F => 32,
                    _ => 16
                };

                if (dataXyz == null) return false;

                ImageData xyzImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 3,
                    data = dataXyz
                };
                ImageData bgrImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 3,
                    data = null
                };

                // 清空原有缓存
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _dllAllAzimuthData.Clear();
                });

                int totalAzimuths = 181; // 0°~180°
                int completedAzimuths = 0;

                // 遍历0°~180°所有方位角，异步调用DLL并缓存数据
                for (int azimuth = 0; azimuth <= 180; azimuth++)
                {
                    // 使用传入的动态参数构建JSON
                    string staticJson = JsonConvert.SerializeObject(new
                    {
                        debugCfg = new { Debug = false, debugPath = "Result\\", debugImgResize = 2 },
                        azimuthalAngle = azimuth,
                        polar_RHO = polarRHO,
                        polar_Angle = polarAngle,
                        pixelToAngle = ConoscopeCoefficient,
                        pointNumLine = pointNumLine,
                        pointNumCircle = 60,
                        center = new { x = center.X, y = center.Y },
                        displayChannel = exportChannel.ToString()
                    });

                    string resultJson = string.Empty;
                    ImageData showImage = null;
                    CV_AliResType callResult = CV_AliResType.FAILED;

                    // 异步调用DLL
                    await Task.Run(() =>
                    {
                        callResult = CallCV_Ali_calcVam(
                            bgrImageData,
                            xyzImageData,
                            staticJson,
                            out resultJson,
                            out showImage
                        );
                    });

                    // 清理内存
                    if (showImage?.data != null)
                    {
                        Array.Clear(showImage.data, 0, showImage.data.Length);
                        showImage.data = null;
                    }

                    if (callResult != CV_AliResType.SUCCESS && callResult != CV_AliResType.PART_SUCCESS)
                    {
                        logger.Warn($"{FindResource("Azimuth")}{azimuth}° DLL{FindResource("Callfailed")}，{FindResource("Errorcode")}：{callResult}");

                        // 即使失败也更新进度
                        completedAzimuths++;
                        if (onProgressUpdate != null)
                        {
                            int progress = (int)((double)completedAzimuths / totalAzimuths * 100);
                            onProgressUpdate(progress);
                        }

                        await Task.Delay(10);
                        continue;
                    }

                    string cleanJson = resultJson?.Trim('\0').Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(cleanJson))
                    {
                        logger.Warn($"{FindResource("Azimuth")}{azimuth}° {FindResource("nullJSON")}");

                        completedAzimuths++;
                        if (onProgressUpdate != null)
                        {
                            int progress = (int)((double)completedAzimuths / totalAzimuths * 100);
                            onProgressUpdate(progress);
                        }

                        await Task.Delay(10);
                        continue;
                    }

                    try
                    {
                        VamResultRoot result = JsonConvert.DeserializeObject<VamResultRoot>(cleanJson);
                        if (result?.result?.line?.Data != null && result.result.line.Data.Count > 0)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                _dllAllAzimuthData[azimuth] = result.result.line.Data;
                            });
                        }
                    }
                    catch (JsonException ex)
                    {
                        logger.Error($"{FindResource("Azimuth")}{azimuth}° {FindResource("JSONEX")}", ex);
                    }

                    // 更新进度
                    completedAzimuths++;
                    if (onProgressUpdate != null)
                    {
                        int progress = (int)((double)completedAzimuths / totalAzimuths * 100);
                        onProgressUpdate(progress);
                    }

                    // 短暂延迟，让UI有机会更新
                    await Task.Delay(10);
                }

                // 处理180°数据（从0°数据反转）
                if (_dllAllAzimuthData.ContainsKey(0) && !_dllAllAzimuthData.ContainsKey(180))
                {
                    var zeroData = await Application.Current.Dispatcher.InvokeAsync(() => _dllAllAzimuthData[0]);
                    var reversed180Data = new List<VamSamplePoint>();

                    // 反转径向角度的数据顺序
                    for (int i = zeroData.Count - 1; i >= 0; i--)
                    {
                        var originalPoint = zeroData[i];
                        reversed180Data.Add(new VamSamplePoint
                        {
                            X = originalPoint.X,
                            Y = originalPoint.Y,
                            Z = originalPoint.Z,
                            cie_x = originalPoint.cie_x,
                            cie_y = originalPoint.cie_y,
                            position = -originalPoint.position
                        });
                    }

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _dllAllAzimuthData[180] = reversed180Data;
                    });
                }

                int dataCount = await Application.Current.Dispatcher.InvokeAsync(() => _dllAllAzimuthData.Count);
                return dataCount > 0;
            }
            catch (Exception ex)
            {
                logger.Error($"{FindResource("Failedtoobtainfullazimuthdata")}", ex);
                return false;
            }
        }

        /// <summary>
        /// 批量调用DLL获取所有极角+方位角的圆环全量数据（对齐CallVamDllForAllAzimuth）
        /// </summary>
        /// <param name="polarStart">极角起始值</param>
        /// <param name="polarEnd">极角结束值</param>
        /// <param name="polarStep">极角间隔</param>
        /// <param name="azimuthSampleCount">方位角采样点数</param>
        /// <returns>是否调用成功</returns>
        //public bool CallVamDllForAllCircle(int polarStart, int polarEnd, int polarStep, int azimuthSampleCount)
        //{
        //    // 初始化返回状态
        //    bool isSuccess = false;

        //    try
        //    {
        //        // 步骤1：基础校验（同直径线批量调用逻辑）
        //        if (!IsMatSafe(XMat) || !IsMatSafe(YMat) || !IsMatSafe(ZMat) || center.X == 0 || center.Y == 0)
        //        {
        //            logger.Error($"{FindResource("Datanotloadedorimagecenternotinitialized")}");
        //            return false;
        //        }

        //        // 步骤2：极角参数合法性校验
        //        if (polarStep <= 0)
        //        {
        //            logger.Error($"{FindResource("Thepolarangleintervalmustbepositiveinteger")}");
        //            // 在UI线程显示消息框
        //            Application.Current.Dispatcher.Invoke(() =>
        //            {
        //                MessageBox.Show($"{FindResource("Thepolarangleintervalmustbepositiveinteger")}",
        //                    $"{FindResource("Parametererror")}", MessageBoxButton.OK, MessageBoxImage.Warning);
        //            });
        //            return false;
        //        }
        //        if (azimuthSampleCount <= 0)
        //        {
        //            logger.Error($"{FindResource("Azimuthsamplingpointsmustbepositiveinteger")}");
        //            // 在UI线程显示消息框
        //            Application.Current.Dispatcher.Invoke(() =>
        //            {
        //                MessageBox.Show($"{FindResource("Azimuthsamplingpointsmustbepositiveinteger")}",
        //                    $"{FindResource("Parametererror")}", MessageBoxButton.OK, MessageBoxImage.Warning);
        //            });
        //            return false;
        //        }
        //        // 极角范围限制（-60°~60°，符合VAM业务规则）
        //        polarStart = Math.Clamp(polarStart, -60, 60);
        //        polarEnd = Math.Clamp(polarEnd, -60, 60);

        //        // 步骤3：获取图像基础参数（复用已有逻辑）
        //        int imgWidth = YMat.Width;
        //        int imgHeight = YMat.Height;
        //        int bpp = YMat.Depth() switch
        //        {
        //            MatType.CV_8U => 8,
        //            MatType.CV_16U => 16,
        //            MatType.CV_32F => 32,
        //            _ => 16
        //        };

        //        // 步骤4：校验XYZ数据
        //        if (dataXyz == null)
        //        {
        //            logger.Error($"{FindResource("XYZImagedataisempty")}");
        //            return false;
        //        }

        //        // 步骤5：构建ImageData入参（复用已有逻辑）
        //        ImageData xyzImageData = new ImageData
        //        {
        //            _w = imgWidth,
        //            _h = imgHeight,
        //            _bpp = bpp,
        //            _channels = 3,
        //            data = dataXyz
        //        };
        //        ImageData bgrImageData = new ImageData
        //        {
        //            _w = imgWidth,
        //            _h = imgHeight,
        //            _bpp = bpp,
        //            _channels = 3,
        //            data = null // 无BGR数据时传null
        //        };

        //        // 步骤6：初始化/清空缓存字典
        //        if (DllAllCircleData == null)
        //        {
        //            DllAllCircleData = new Dictionary<(int polar, double azimuth), RgbSample>();
        //        }
        //        // 在UI线程清空数据
        //        Application.Current.Dispatcher.Invoke(() =>
        //        {
        //            DllAllCircleData.Clear();
        //        });

        //        // 步骤7：遍历极角范围（支持正负极角）
        //        int polarAngle = polarStart;
        //        while (polarAngle <= polarEnd)
        //        {
        //            // 步骤7.1：遍历方位角（按采样点数均分0~360°）
        //            double azimuthStep = 360.0 / azimuthSampleCount;
        //            for (int i = 0; i < azimuthSampleCount; i++)
        //            {
        //                double currentAzimuth = i * azimuthStep; // 0°, 1°, 2°...359°

        //                // 注意：需要在UI线程中获取txtLinePolarRHO的值
        //                int polarRHO = 60; // 默认值
        //                int _pointNumLine = 360; // 默认值

        //                // 在UI线程中获取界面控件的值
        //                Application.Current.Dispatcher.Invoke(() =>
        //                {
        //                    // 先获取极角范围
        //                    polarRHO = int.TryParse(txtLinePolarRHO.Text.Trim(), out int rho) ? rho : 60;
        //                    // 由间隔角度计算采样点数
        //                    _pointNumLine = (int)(Math.Abs(2 * polarRHO) / _linePolarInterval) + 1;
        //                });

        //                // 步骤7.2：构建R圆模式的JSON参数（适配DLL要求）
        //                string circleJson = JsonConvert.SerializeObject(new
        //                {
        //                    debugCfg = new
        //                    {
        //                        Debug = false,
        //                        debugPath = "Result\\",
        //                        debugImgResize = 2
        //                    },
        //                    azimuthalAngle = currentAzimuth, // 方位角（0~360°）
        //                    polar_RHO = polarAngle,          // 极角（当前遍历的半径角度）
        //                    polar_Angle = 60.0,              // 固定60°（VAM业务默认值）
        //                    pixelToAngle = ConoscopeCoefficient,
        //                    pointNumLine = _pointNumLine,    // 全局采样点配置
        //                    pointNumCircle = azimuthSampleCount, // 方位角采样点数
        //                    center = new { x = center.X, y = center.Y },
        //                    displayChannel = displayChannel.ToString() // 当前选中通道
        //                });

        //                // 步骤7.3：调用DLL封装方法
        //                string resultJson;
        //                ImageData showImage;
        //                CV_AliResType callResult = CallCV_Ali_calcVam(
        //                    bgrImageData,
        //                    xyzImageData,
        //                    circleJson,
        //                    out resultJson,
        //                    out showImage
        //                );

        //                // 步骤7.4：清理showImage内存（避免泄漏）
        //                if (showImage.data != null)
        //                {
        //                    Array.Clear(showImage.data, 0, showImage.data.Length);
        //                    showImage.data = null;
        //                }

        //                // 步骤7.5：处理DLL返回结果
        //                if (callResult != CV_AliResType.SUCCESS && callResult != CV_AliResType.PART_SUCCESS)
        //                {
        //                    logger.Warn($"{$"{FindResource("VAM.RCircle")}"}{polarAngle}° {$"{FindResource("Azimuth")}"}{currentAzimuth:F1}° DLL{$"{FindResource("Callfailed")}"}");
        //                    continue;
        //                }

        //                // 步骤7.6：解析JSON结果
        //                string cleanResultJson = resultJson.Trim('\0').Trim();
        //                if (string.IsNullOrEmpty(cleanResultJson))
        //                {
        //                    logger.Warn($"{$"{FindResource("VAM.RCircle")}"}{polarAngle}°  {$"{FindResource("Azimuth")}"}{currentAzimuth:F1}° {$"{FindResource("nullJSON")}"}");
        //                    continue;
        //                }

        //                // 步骤7.7：反序列化结果并存入缓存
        //                try
        //                {
        //                    VamResultRoot vamResult = JsonConvert.DeserializeObject<VamResultRoot>(cleanResultJson);
        //                    if (vamResult?.result?.circle?.Data != null && vamResult.result.circle.Data.Count > 0)
        //                    {
        //                        // 找到当前方位角对应的采样点
        //                        var targetSample = vamResult.result.circle.Data
        //                            .FirstOrDefault(p => Math.Abs(p.position - currentAzimuth) < 0.1); // 误差允许0.1°

        //                        if (targetSample != null)
        //                        {
        //                            // 转换为RgbSample格式存入缓存
        //                            var rgbSample = new RgbSample
        //                            {
        //                                Position = currentAzimuth,
        //                                X = targetSample.X,
        //                                Y = targetSample.Y,
        //                                Z = targetSample.Z
        //                            };

        //                            // 在UI线程更新字典
        //                            Application.Current.Dispatcher.Invoke(() =>
        //                            {
        //                                DllAllCircleData.Add((polarAngle, currentAzimuth), rgbSample);
        //                            });
        //                            isSuccess = true; // 有有效数据则标记成功
        //                        }
        //                    }
        //                }
        //                catch (JsonException ex)
        //                {
        //                    logger.Error($"{$"{FindResource("VAM.RCircle")}"}{polarAngle}° {$"{FindResource("Azimuth")}"}{currentAzimuth:F1}° {$"{FindResource("JSONEX")}"}", ex);
        //                    continue;
        //                }
        //            }

        //            // 步骤7.8：步进极角
        //            polarAngle += polarStep;
        //        }

        //        // 步骤8：日志输出统计信息
        //        logger.Info($"Execution completed - Angular range[{polarStart}~{polarEnd}]° interval{polarStep}° | Azimuth sampling {azimuthSampleCount}points | Valid data{DllAllCircleData.Count}items");

        //        // 步骤9：空数据兜底提示
        //        if (!isSuccess)
        //        {
        //            Application.Current.Dispatcher.Invoke(() =>
        //            {
        //                MessageBox.Show($"{FindResource("nullRdata")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //            });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error($"{FindResource("Executionexception")}", ex);
        //        Application.Current.Dispatcher.Invoke(() =>
        //        {
        //            MessageBox.Show($"{FindResource("Failedtobatchfetchringdata")}：{ex.Message}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Error);
        //        });
        //        isSuccess = false;
        //    }

        //    return isSuccess;
        //    //// 初始化返回状态
        //    //bool isSuccess = false;

        //    //try
        //    //{
        //    //    // 步骤1：基础校验（同直径线批量调用逻辑）
        //    //    if (!IsMatSafe(XMat) || !IsMatSafe(YMat) || !IsMatSafe(ZMat) || center.X == 0 || center.Y == 0)
        //    //    {
        //    //        logger.Error($"{FindResource("Datanotloadedorimagecenternotinitialized")}");
        //    //        return false;
        //    //    }

        //    //    // 步骤2：极角参数合法性校验
        //    //    if (polarStep <= 0)
        //    //    {
        //    //        logger.Error($"{FindResource("Thepolarangleintervalmustbepositiveinteger")}");
        //    //        MessageBox.Show($"{FindResource("Thepolarangleintervalmustbepositiveinteger")}", $"{FindResource("Parametererror")}", MessageBoxButton.OK, MessageBoxImage.Warning);
        //    //        return false;
        //    //    }
        //    //    if (azimuthSampleCount <= 0)
        //    //    {
        //    //        logger.Error($"{FindResource("Azimuthsamplingpointsmustbepositiveinteger")}");
        //    //        MessageBox.Show($"{FindResource("Azimuthsamplingpointsmustbepositiveinteger")}", $"{FindResource("Parametererror")}", MessageBoxButton.OK, MessageBoxImage.Warning);
        //    //        return false;
        //    //    }
        //    //    // 极角范围限制（-60°~60°，符合VAM业务规则）
        //    //    polarStart = Math.Clamp(polarStart, -60, 60);
        //    //    polarEnd = Math.Clamp(polarEnd, -60, 60);

        //    //    // 步骤3：获取图像基础参数（复用已有逻辑）
        //    //    int imgWidth = YMat.Width;
        //    //    int imgHeight = YMat.Height;
        //    //    int bpp = YMat.Depth() switch
        //    //    {
        //    //        MatType.CV_8U => 8,
        //    //        MatType.CV_16U => 16,
        //    //        MatType.CV_32F => 32,
        //    //        _ => 16
        //    //    };

        //    //    // 步骤4：校验XYZ数据
        //    //    if (dataXyz == null)
        //    //    {
        //    //        logger.Error($"{FindResource("XYZImagedataisempty")}");
        //    //        return false;
        //    //    }

        //    //    // 步骤5：构建ImageData入参（复用已有逻辑）
        //    //    ImageData xyzImageData = new ImageData
        //    //    {
        //    //        _w = imgWidth,
        //    //        _h = imgHeight,
        //    //        _bpp = bpp,
        //    //        _channels = 3,
        //    //        data = dataXyz
        //    //    };
        //    //    ImageData bgrImageData = new ImageData
        //    //    {
        //    //        _w = imgWidth,
        //    //        _h = imgHeight,
        //    //        _bpp = bpp,
        //    //        _channels = 3,
        //    //        data = null // 无BGR数据时传null
        //    //    };

        //    //    // 步骤6：初始化/清空缓存字典
        //    //    if (DllAllCircleData == null)
        //    //    {
        //    //        DllAllCircleData = new Dictionary<(int polar, double azimuth), RgbSample>();
        //    //    }
        //    //    DllAllCircleData.Clear();

        //    //    // 步骤7：遍历极角范围（支持正负极角）
        //    //    int polarAngle = polarStart;
        //    //    while (polarAngle <= polarEnd)
        //    //    {
        //    //        // 步骤7.1：遍历方位角（按采样点数均分0~360°）
        //    //        double azimuthStep = 360.0 / azimuthSampleCount;
        //    //        for (int i = 0; i < azimuthSampleCount; i++)
        //    //        {
        //    //            double currentAzimuth = i * azimuthStep; // 0°, 1°, 2°...359°
        //    //                                                     // 先获取极角范围
        //    //            int polarRHO = int.TryParse(txtLinePolarRHO.Text.Trim(), out int rho) ? rho : 60;
        //    //            // 由间隔角度计算采样点数
        //    //            int _pointNumLine = (int)(Math.Abs(2 * polarRHO) / _linePolarInterval) + 1;
        //    //            // 步骤7.2：构建R圆模式的JSON参数（适配DLL要求）
        //    //            string circleJson = JsonConvert.SerializeObject(new
        //    //            {
        //    //                debugCfg = new
        //    //                {
        //    //                    Debug = false,
        //    //                    debugPath = "Result\\",
        //    //                    debugImgResize = 2
        //    //                },
        //    //                azimuthalAngle = currentAzimuth, // 方位角（0~360°）
        //    //                polar_RHO = polarAngle,          // 极角（当前遍历的半径角度）
        //    //                polar_Angle = 60.0,              // 固定60°（VAM业务默认值）
        //    //                pixelToAngle = ConoscopeCoefficient,
        //    //                pointNumLine = _pointNumLine,    // 全局采样点配置
        //    //                pointNumCircle = azimuthSampleCount, // 方位角采样点数
        //    //                center = new { x = center.X, y = center.Y },
        //    //                displayChannel = displayChannel.ToString() // 当前选中通道
        //    //            });

        //    //            // 步骤7.3：调用DLL封装方法
        //    //            string resultJson;
        //    //            ImageData showImage;
        //    //            CV_AliResType callResult = CallCV_Ali_calcVam(
        //    //                bgrImageData,
        //    //                xyzImageData,
        //    //                circleJson,
        //    //                out resultJson,
        //    //                out showImage
        //    //            );

        //    //            // 步骤7.4：清理showImage内存（避免泄漏）
        //    //            if (showImage.data != null)
        //    //            {
        //    //                Array.Clear(showImage.data, 0, showImage.data.Length);
        //    //                showImage.data = null;
        //    //            }

        //    //            // 步骤7.5：处理DLL返回结果
        //    //            if (callResult != CV_AliResType.SUCCESS && callResult != CV_AliResType.PART_SUCCESS)
        //    //            {
        //    //                logger.Warn($"{$"{FindResource("VAM.RCircle")}"}{polarAngle}° {$"{FindResource("Azimuth")}"}{currentAzimuth:F1}° DLL{$"{FindResource("Callfailed")}"}");
        //    //                continue;
        //    //            } //，{ $"{FindResource("Errorcode")}"}：{ callResult}

        //    //            // 步骤7.6：解析JSON结果
        //    //            string cleanResultJson = resultJson.Trim('\0').Trim();
        //    //            if (string.IsNullOrEmpty(cleanResultJson))
        //    //            {
        //    //                logger.Warn($"{$"{FindResource("VAM.RCircle")}"}{polarAngle}°  {$"{FindResource("Azimuth")}"}{currentAzimuth:F1}° {$"{FindResource("nullJSON")}"}");
        //    //                continue;
        //    //            }

        //    //            // 步骤7.7：反序列化结果并存入缓存
        //    //            try
        //    //            {
        //    //                VamResultRoot vamResult = JsonConvert.DeserializeObject<VamResultRoot>(cleanResultJson);
        //    //                if (vamResult?.result?.circle?.Data != null && vamResult.result.circle.Data.Count > 0)
        //    //                {
        //    //                    // 找到当前方位角对应的采样点
        //    //                    var targetSample = vamResult.result.circle.Data
        //    //                        .FirstOrDefault(p => Math.Abs(p.position - currentAzimuth) < 0.1); // 误差允许0.1°

        //    //                    if (targetSample != null)
        //    //                    {
        //    //                        // 转换为RgbSample格式存入缓存
        //    //                        var rgbSample = new RgbSample
        //    //                        {
        //    //                            Position = currentAzimuth,
        //    //                            X = targetSample.X,
        //    //                            Y = targetSample.Y,
        //    //                            Z = targetSample.Z
        //    //                        };
        //    //                        DllAllCircleData.Add((polarAngle, currentAzimuth), rgbSample);
        //    //                        isSuccess = true; // 有有效数据则标记成功
        //    //                    }
        //    //                }
        //    //            }
        //    //            catch (JsonException ex)
        //    //            {
        //    //                logger.Error($"{$"{FindResource("VAM.RCircle")}"}{polarAngle}° {$"{FindResource("Azimuth")}"}{currentAzimuth:F1}° {$"{FindResource("JSONEX")}"}", ex);
        //    //                continue;
        //    //            }
        //    //        }

        //    //        // 步骤7.8：步进极角
        //    //        polarAngle += polarStep;
        //    //    }

        //    //    // 步骤8：日志输出统计信息
        //    //    logger.Info($"Execution completed - Angular range[{polarStart}~{polarEnd}]° interval{polarStep}° | Azimuth sampling {azimuthSampleCount}points | Valid data{DllAllCircleData.Count}items");


        //    //    // 步骤9：空数据兜底提示
        //    //    if (!isSuccess)
        //    //    {
        //    //        MessageBox.Show($"{FindResource("nullRdata")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //    //    }
        //    //}
        //    //catch (Exception ex)
        //    //{
        //    //    logger.Error($"{FindResource("Executionexception")}", ex);
        //    //    MessageBox.Show($"{FindResource("Failedtobatchfetchringdata")}：{ex.Message}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Error);
        //    //    isSuccess = false;
        //    //}

        //    //return isSuccess;
        //}

        public async Task<bool> CallVamDllForAllCircleAsync( int polarStart,int polarEnd,int polarStep,int azimuthSampleCount, Action<int> onProgressUpdate = null)
        {
            // 初始化返回状态
            bool isSuccess = false;
            int totalIterations = 0;
            int completedIterations = 0;

            try
            {
                // 步骤1：基础校验（同直径线批量调用逻辑）
                if (!IsMatSafe(XMat) || !IsMatSafe(YMat) || !IsMatSafe(ZMat) || center.X == 0 || center.Y == 0)
                {
                    logger.Error($"{FindResource("Datanotloadedorimagecenternotinitialized")}");
                    return false;
                }

                // 步骤2：极角参数合法性校验
                if (polarStep <= 0)
                {
                    logger.Error($"{FindResource("Thepolarangleintervalmustbepositiveinteger")}");
                    // 在UI线程显示消息框
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"{FindResource("Thepolarangleintervalmustbepositiveinteger")}",
                            $"{FindResource("Parametererror")}", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return false;
                }
                if (azimuthSampleCount <= 0)
                {
                    logger.Error($"{FindResource("Azimuthsamplingpointsmustbepositiveinteger")}");
                    // 在UI线程显示消息框
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"{FindResource("Azimuthsamplingpointsmustbepositiveinteger")}",
                            $"{FindResource("Parametererror")}", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return false;
                }

                // 极角范围限制（-60°~60°，符合VAM业务规则）
                polarStart = Math.Clamp(polarStart, -60, 60);
                polarEnd = Math.Clamp(polarEnd, -60, 60);

                // 计算总迭代次数
                int polarCount = 0;
                for (int p = polarStart; p <= polarEnd; p += polarStep) polarCount++;
                totalIterations = polarCount * azimuthSampleCount;

                if (totalIterations == 0)
                {
                    logger.Error($"{FindResource("Noiterationcountgenerated")}");
                    return false;
                }

                // 步骤3：获取图像基础参数（复用已有逻辑）
                int imgWidth = YMat.Width;
                int imgHeight = YMat.Height;
                int bpp = YMat.Depth() switch
                {
                    MatType.CV_8U => 8,
                    MatType.CV_16U => 16,
                    MatType.CV_32F => 32,
                    _ => 16
                };

                // 步骤4：校验XYZ数据
                if (dataXyz == null)
                {
                    logger.Error($"{FindResource("XYZImagedataisempty")}");
                    return false;
                }

                // 步骤5：构建ImageData入参（复用已有逻辑）
                ImageData xyzImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 3,
                    data = dataXyz
                };
                ImageData bgrImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 3,
                    data = null // 无BGR数据时传null
                };

                // 步骤6：初始化/清空缓存字典
                if (DllAllCircleData == null)
                {
                    DllAllCircleData = new Dictionary<(int polar, double azimuth), RgbSample>();
                }
                // 在UI线程清空数据
                Application.Current.Dispatcher.Invoke(() =>
                {
                    DllAllCircleData.Clear();
                });

                // 步骤7：遍历极角范围（支持正负极角）- 使用异步循环
                int polarAngle = polarStart;
                int currentPolarIndex = 0;

                while (polarAngle <= polarEnd)
                {
                    // 步骤7.1：遍历方位角（按采样点数均分0~360°）
                    double azimuthStep = 360.0 / azimuthSampleCount;

                    for (int i = 0; i < azimuthSampleCount; i++)
                    {
                        double currentAzimuth = i * azimuthStep; // 0°, 1°, 2°...359°

                        // 注意：需要在UI线程中获取txtLinePolarRHO的值
                        int polarRHO = 60; // 默认值
                        int _pointNumLine = 360; // 默认值

                        // 在UI线程中获取界面控件的值
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            // 先获取极角范围
                            polarRHO = int.TryParse(txtLinePolarRHO.Text.Trim(), out int rho) ? rho : 60;
                            // 由间隔角度计算采样点数
                            _pointNumLine = (int)(Math.Abs(2 * polarRHO) / _linePolarInterval) + 1;
                        });

                        // 步骤7.2：构建R圆模式的JSON参数（适配DLL要求）
                        string circleJson = JsonConvert.SerializeObject(new
                        {
                            debugCfg = new
                            {
                                Debug = false,
                                debugPath = "Result\\",
                                debugImgResize = 2
                            },
                            azimuthalAngle = currentAzimuth, // 方位角（0~360°）
                            polar_RHO = polarAngle,          // 极角（当前遍历的半径角度）
                            polar_Angle = 60.0,              // 固定60°（VAM业务默认值）
                            pixelToAngle = ConoscopeCoefficient,
                            pointNumLine = _pointNumLine,    // 全局采样点配置
                            pointNumCircle = azimuthSampleCount, // 方位角采样点数
                            center = new { x = center.X, y = center.Y },
                            displayChannel = displayChannel.ToString() // 当前选中通道
                        });

                        // 步骤7.3：调用DLL封装方法 - 使用Task.Run避免阻塞
                        string resultJson = string.Empty;
                        ImageData showImage = null;
                        CV_AliResType callResult = CV_AliResType.FAILED;

                        await Task.Run(() =>
                        {
                            callResult = CallCV_Ali_calcVam(
                                bgrImageData,
                                xyzImageData,
                                circleJson,
                                out resultJson,
                                out showImage
                            );
                        });

                        // 步骤7.4：清理showImage内存（避免泄漏）
                        if (showImage?.data != null)
                        {
                            Array.Clear(showImage.data, 0, showImage.data.Length);
                            showImage.data = null;
                        }

                        // 步骤7.5：处理DLL返回结果
                        if (callResult != CV_AliResType.SUCCESS && callResult != CV_AliResType.PART_SUCCESS)
                        {
                            logger.Warn($"{$"{FindResource("VAM.RCircle")}"}{polarAngle}° {$"{FindResource("Azimuth")}"}{currentAzimuth:F1}° DLL{$"{FindResource("Callfailed")}"}");

                            // 即使失败也更新进度
                            completedIterations++;
                            if (onProgressUpdate != null)
                            {
                                int progress = (int)((double)completedIterations / totalIterations * 100);
                                onProgressUpdate(progress);
                            }

                            // 短暂延迟，避免UI卡顿
                            await Task.Delay(10);
                            continue;
                        }

                        // 步骤7.6：解析JSON结果
                        string cleanResultJson = resultJson?.Trim('\0').Trim() ?? string.Empty;
                        if (string.IsNullOrEmpty(cleanResultJson))
                        {
                            logger.Warn($"{$"{FindResource("VAM.RCircle")}"}{polarAngle}°  {$"{FindResource("Azimuth")}"}{currentAzimuth:F1}° {$"{FindResource("nullJSON")}"}");

                            // 更新进度
                            completedIterations++;
                            if (onProgressUpdate != null)
                            {
                                int progress = (int)((double)completedIterations / totalIterations * 100);
                                onProgressUpdate(progress);
                            }

                            await Task.Delay(10);
                            continue;
                        }

                        // 步骤7.7：反序列化结果并存入缓存
                        try
                        {
                            VamResultRoot vamResult = JsonConvert.DeserializeObject<VamResultRoot>(cleanResultJson);
                            if (vamResult?.result?.circle?.Data != null && vamResult.result.circle.Data.Count > 0)
                            {
                                // 找到当前方位角对应的采样点
                                var targetSample = vamResult.result.circle.Data
                                    .FirstOrDefault(p => Math.Abs(p.position - currentAzimuth) < 0.1); // 误差允许0.1°

                                if (targetSample != null)
                                {
                                    // 转换为RgbSample格式存入缓存
                                    var rgbSample = new RgbSample
                                    {
                                        Position = currentAzimuth,
                                        X = targetSample.X,
                                        Y = targetSample.Y,
                                        Z = targetSample.Z
                                    };

                                    // 在UI线程更新字典
                                    await Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        DllAllCircleData.Add((polarAngle, currentAzimuth), rgbSample);
                                    });
                                    isSuccess = true; // 有有效数据则标记成功
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            logger.Error($"{$"{FindResource("VAM.RCircle")}"}{polarAngle}° {$"{FindResource("Azimuth")}"}{currentAzimuth:F1}° {$"{FindResource("JSONEX")}"}", ex);
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"{$"{FindResource("VAM.RCircle")}"}{polarAngle}° {$"{FindResource("Azimuth")}"}{currentAzimuth:F1}° {$"{FindResource("Exceptionoccurredwhileprocessingresults")}"}", ex);
                        }
                        finally
                        {
                            // 更新进度
                            completedIterations++;
                            if (onProgressUpdate != null)
                            {
                                int progress = (int)((double)completedIterations / totalIterations * 100);
                                onProgressUpdate(progress);
                            }

                            // 短暂延迟，让UI有机会更新
                            await Task.Delay(10);
                        }
                    }

                    // 步骤7.8：步进极角
                    polarAngle += polarStep;
                    currentPolarIndex++;
                }

                // 步骤8：日志输出统计信息
                int validDataCount = await Application.Current.Dispatcher.InvokeAsync(() => DllAllCircleData?.Count ?? 0);
                logger.Info($"Execution completed - Angular range[{polarStart}~{polarEnd}]° interval{polarStep}° | Azimuth sampling {azimuthSampleCount}points | Valid data{validDataCount}items");

                // 步骤9：空数据兜底提示
                if (!isSuccess)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"{FindResource("nullRdata")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Error($"{FindResource("Executionexception")}", ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"{FindResource("Failedtobatchfetchringdata")}：{ex.Message}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                isSuccess = false;
            }

            return isSuccess;
        }
        /// <summary>
        /// 调用DLL接口获取R圆数据，更新R圆图表
        /// </summary>
        /// <param name="targetRadius">选中的R圆半径角度</param>
        /// <returns>是否调用成功</returns>
        private bool CallVamDllForRCircle(int targetRadius)
        {
            // 新增：0°时直接返回false，使用本地计算逻辑
            //if (targetRadius == 0)
            //{
            //    logger.Warn("R圆0°跳过DLL调用，使用本地计算数据");
            //    return false;
            //}
            try
            {
                // 步骤1：基础校验（同直径线）
                if (!IsMatSafe(XMat) || !IsMatSafe(YMat) || !IsMatSafe(ZMat))
                {
                    logger.Error("XYZ Mat is null or has been released");//"XYZ Mat 为空或已释放"
                    return false;
                }
                if (center.X == 0 && center.Y == 0)
                {
                    logger.Error("Image center not initialized (CVCIE file not loaded)");// "图像中心未初始化（未加载CVCIE文件）"
                    return false;
                }

                // 步骤2：获取图像参数（同直径线）
                int imgWidth = YMat.Width;
                int imgHeight = YMat.Height;
                int bpp = YMat.Depth() switch
                {
                    MatType.CV_8U => 8,
                    MatType.CV_16U => 16,
                    MatType.CV_32F => 32,
                    _ => 16
                };
                int elementSize = bpp / 8;

                // 步骤3：调用修复后的XYZ拼接方法
                // byte[] xyzData = MergeXYZToInterleaved(XMat, YMat, ZMat);
                if (dataXyz == null)
                {
                    return false;
                }

                // 步骤4：构建ImageData（同直径线）
                ImageData xyzImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 3,
                    data = dataXyz
                };
                ImageData bgrImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 3,
                    data = null
                };
                // 先获取极角范围
                int polarRHO = int.TryParse(txtLinePolarRHO.Text.Trim(), out int rho) ? rho : 60;
                // 由间隔角度计算采样点数
                int _pointNumLine = (int)(Math.Abs(2 * polarRHO) / _linePolarInterval) + 1;
                int azimuthSampleCount = 360;
                // 步骤5：修复R圆的JSON参数
                string staticJson = JsonConvert.SerializeObject(new
                {
                    debugCfg = new
                    {
                        Debug = false,
                        debugPath = "Result\\",
                        debugImgResize = 2
                    },
                    azimuthalAngle = 0.0, // R圆无需方位角
                    polar_RHO = targetRadius, // R圆的极径为目标半径
                    polar_Angle = 0.0,
                    pixelToAngle = ConoscopeCoefficient,
                    pointNumLine = _pointNumLine,
                    pointNumCircle = azimuthSampleCount, // 还原为60
                    center = new { x = center.X, y = center.Y },
                    displayChannel = displayChannel.ToString()
                });

                // 步骤6：调用DLL（同直径线）
                string resultJson;
                ImageData showImage;
                CV_AliResType callResult = CallCV_Ali_calcVam(
                    bgrImageData,
                    xyzImageData,
                    staticJson,
                    out resultJson,
                    out showImage
                );

                // 步骤7：处理结果（同直径线）
                if (callResult != CV_AliResType.SUCCESS && callResult != CV_AliResType.PART_SUCCESS)
                {
                    logger.Error($"DLL{FindResource("Callfailed")}"); //，{FindResource("Errorcode")}：{callResult}
                    return false;
                }
                if (showImage.data != null)
                {
                    Array.Clear(showImage.data, 0, showImage.data.Length);
                    showImage.data = null;
                }

                string cleanResultJson = resultJson.Trim('\0').Trim();
                if (string.IsNullOrEmpty(cleanResultJson))
                {
                    logger.Error("DLL returns an empty JSON");//: "DLL返回空JSON"
                    return false;
                }

                VamResultRoot vamResult = JsonConvert.DeserializeObject<VamResultRoot>(cleanResultJson);
                if (vamResult?.result?.circle?.Data == null || vamResult.result.circle.Data.Count == 0)
                {
                    logger.Error("The polar section data returned by the DLL is empty");//"DLL返回的极角截面数据为空"
                    return false;
                }

                UpdateRCircleChartFromDll(vamResult.result.circle.Data);
                // 将DLL返回的当前半径数据存入缓存
                foreach (var sample in vamResult.result.circle.Data)
                {
                    DllAllCircleData[(targetRadius, sample.position)] = new RgbSample
                    {
                        Position = sample.position,
                        X = sample.X,
                        Y = sample.Y,
                        Z = sample.Z
                    };
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("Exception occurred while calling DLL to get polar section data", ex);//: "调用DLL获取极角截面数据异常"
                return false;
            }
        }
        #endregion

        #region

        // 辅助方法：读取单个像素的字节数组（适配不同Mat类型）
        private byte[] GetPixelBytes(Mat mat, int x, int y)
        {
            int elementSize = mat.ElemSize1();
            byte[] bytes = new byte[elementSize];
            IntPtr pixelPtr = mat.Ptr(y, x); // 获取单个像素的指针
            Marshal.Copy(pixelPtr, bytes, 0, elementSize);
            return bytes;
        }

        #endregion
        #region 实现图表更新方法（从 DLL 结果刷新）
        /// <summary>
        /// 使用DLL返回的直径线数据更新图表
        /// </summary>
        private void UpdateDiameterLineChartFromDll(List<VamSamplePoint> dllData)
        {
            wpfPlotDiameterLine.Plot.Clear();

            if (dllData.Count == 0)
            {
                wpfPlotDiameterLine.Plot.Axes.AutoScale();
                wpfPlotDiameterLine.Refresh();
                return;
            }

            // 提取DLL返回的角度（X轴）和亮度值（Y轴，取Y通道）
            double[] positions = dllData.Select(p => p.position).ToArray();
            double[] values = dllData.Select(p => GetChannelValueFromDll(p, displayChannel)).ToArray();

            // 绘制图表（保持原有样式）
            var scatter = wpfPlotDiameterLine.Plot.Add.Scatter(positions, values);
            scatter.LineWidth = 2;
            scatter.Color = ScottPlot.Color.FromHex("#1f77b4");

            wpfPlotDiameterLine.Plot.Axes.AutoScale();
            wpfPlotDiameterLine.Plot.Title(DC);
            wpfPlotDiameterLine.Refresh();
        }

        // 辅助方法：从DLL结果中获取指定通道的值
        private double GetChannelValueFromDll(VamSamplePoint dllPoint, ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => dllPoint.X,
                ExportChannel.Y => dllPoint.Y,
                ExportChannel.Z => dllPoint.Z,
                _ => dllPoint.Y
            };
        }
        /// <summary>
        /// 使用DLL返回的R圆数据更新图表
        /// </summary>
        private void UpdateRCircleChartFromDll(List<VamSamplePoint> dllData)
        {
            wpfPlotRCircle.Plot.Clear();

            if (dllData.Count == 0)
            {
                wpfPlotRCircle.Plot.Axes.AutoScale();
                wpfPlotRCircle.Refresh();
                return;
            }

            // 提取DLL返回的圆周角度（X轴）和Y通道值（Y轴）
            double[] positions = dllData.Select(p => p.position).ToArray();
            double[] yValues = dllData.Select(p => p.Y).ToArray();

            // 绘制图表（保持原有样式）
            var yScatter = wpfPlotRCircle.Plot.Add.Scatter(positions, yValues);
            yScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
            yScatter.LineWidth = 2;
            yScatter.LegendText = "Y";

            wpfPlotRCircle.Plot.Title($"{RC} {displayRadius}° {CDC}");
            wpfPlotRCircle.Plot.XLabel($"{CA}");
            wpfPlotRCircle.Plot.YLabel($"{Pixel}");
            wpfPlotRCircle.Plot.Legend.IsVisible = true;
            wpfPlotRCircle.Plot.Axes.AutoScale();
            wpfPlotRCircle.Refresh();
        }
        #endregion
        // 切换图表
        private void BtnSwitchChart_Click(object sender, RoutedEventArgs e)
        {
            // 获取当前按钮显示的文本（通过DynamicResource对应的Key）
            string currentBtnText = btnSwitchChart.Content.ToString();
            string rCircleTitle = FindResource("Plot.Title.RCircle").ToString();
            string diameterTitle = FindResource("Plot.Title.DiameterLine").ToString();

            if (currentBtnText == rCircleTitle)
            {
                // 切换为R圆分布曲线
                btnSwitchChart.Content = diameterTitle;
                chartPanelDiameter.Visibility = Visibility.Collapsed;
                chartPanelRCircle.Visibility = Visibility.Visible;
                paramPanelDiameter.Visibility = Visibility.Collapsed;
                paramPanelRCircle.Visibility = Visibility.Visible;

            }
            else
            {
                // 切换为直径线分布曲线
                btnSwitchChart.Content = rCircleTitle;
                chartPanelDiameter.Visibility = Visibility.Visible;
                chartPanelRCircle.Visibility = Visibility.Collapsed;
                paramPanelDiameter.Visibility = Visibility.Visible;
                paramPanelRCircle.Visibility = Visibility.Collapsed;
            }
            _selectedAngle = -1;
            _selectedRadius = -1;
            // 切换后刷新显示，重新绘制对应黄线
            if (YMat != null && !YMat.Empty())
            {
                UpdateDisplay();
            }
        }

        /// <summary>
        /// 安全校验Mat对象（未释放、非空、非空矩阵）
        /// </summary>
        private bool IsMatSafe(Mat? mat)
        {
            try
            {
                // 仅校验非空、非空矩阵，不校验是否Disposed
                return mat != null && !mat.Empty();
            }
            catch (ObjectDisposedException)
            {
                // 若仍触发Disposed异常，直接返回false并提示重新加载
                MessageBox.Show($"{FindResource("vamInvalid")}", $"{FindResource("Prompt")}");
                return false;
            }
        }

        #region  自定义悬浮面板
        /// <summary>
        /// 初始化悬浮信息面板（黑色背景、白色文字，匹配目标图样式）
        /// </summary>
        private void InitializeHoverInfoPanel()
        {
            if (_hoverInfoPanel != null) return;

            _hoverInfoPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Visibility = Visibility.Collapsed,
                CornerRadius = new CornerRadius(3),
                CacheMode = new BitmapCache(192), // 保留硬件加速
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };

            // 渲染优化（静态方法设置）
            RenderOptions.SetBitmapScalingMode(_hoverInfoPanel, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(_hoverInfoPanel, EdgeMode.Aliased);
            RenderOptions.SetClearTypeHint(_hoverInfoPanel, ClearTypeHint.Enabled);

            _hoverInfoText = new TextBlock
            {
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                LineHeight = 15,
                TextWrapping = TextWrapping.NoWrap,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,

            };
            // 禁用文本渲染优化，避免文字抖动
            // 移到外部，用静态方法设置RenderOptions属性
            RenderOptions.SetBitmapScalingMode(_hoverInfoPanel, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(_hoverInfoPanel, EdgeMode.Aliased);
            _hoverInfoPanel.Child = _hoverInfoText;

            // 固定面板位置：图像控件的右下角（绝对定位，不随鼠标移动）
            Canvas.SetZIndex(_hoverInfoPanel, 999); // 置顶显示
            Canvas.SetLeft(_hoverInfoPanel, 20); // 固定X坐标
            Canvas.SetTop(_hoverInfoPanel, 20);  // 固定Y坐标（也可设为imgDisplay.ActualHeight - 100）

            // 添加到Canvas容器（若当前布局不是Canvas，需先包裹）
            if (this.Parent is Canvas canvas)
            {
                canvas.Children.Add(_hoverInfoPanel);
            }
            else
            {
                // 若没有Canvas，创建一个覆盖层
                var overlayCanvas = new Canvas { Width = double.NaN, Height = double.NaN };
                this.LayoutRoot.Children.Add(overlayCanvas);
                overlayCanvas.Children.Add(_hoverInfoPanel);
            }
        }

        /// <summary>
        /// 鼠标在图像上移动时，显示悬浮信息
        /// </summary>
        private void ImgDisplay_MouseMove(object sender, MouseEventArgs e)
        {
            lock (_lockObj) // 加锁，避免并发更新
            {
                if (!_isHovering)
                {
                    _isHovering = true;
                    _hoverInfoPanel?.SetValue(VisibilityProperty, Visibility.Visible);
                }

                if (XMat == null || YMat == null || ZMat == null || pseudoColorMat == null || _hoverInfoText == null)
                    return;

                // 1. 获取坐标（仅计算一次）
                System.Windows.Point currentMousePos = e.GetPosition(imgDisplay);
                var writeableBmp = imgDisplay.Source as WriteableBitmap;
                if (writeableBmp == null) return;

                int pixelX = (int)(currentMousePos.X * writeableBmp.PixelWidth / imgDisplay.ActualWidth);
                int pixelY = (int)(currentMousePos.Y * writeableBmp.PixelHeight / imgDisplay.ActualHeight);

                // 2. 越界判断
                if (pixelX < 0 || pixelX >= pseudoColorMat.Width || pixelY < 0 || pixelY >= pseudoColorMat.Height)
                    return;

                // 3. 仅更新文本（无布局变化，彻底消除闪烁）
                try
                {
                    Vec3b bgr = pseudoColorMat.At<Vec3b>(pixelY, pixelX);
                    double xVal = XMat.At<float>(pixelY, pixelX);
                    double yVal = YMat.At<float>(pixelY, pixelX);
                    double zVal = ZMat.At<float>(pixelY, pixelX);
                    double dx = pixelX - center.X;
                    double dy = pixelY - center.Y;
                    double u = dx * ConoscopeCoefficient;
                    double v = dy * ConoscopeCoefficient;

                    // 文本格式化（仅更新内容，无布局操作）
                    string infoText = $"R:{bgr.Item2,-3} G:{bgr.Item1,-3} B:{bgr.Item0,-2}\n" +
                                      $"({pixelX,-4},{pixelY,-4})\n" +
                                      $"X:{xVal,-6:F1} Y:{yVal,-6:F1} Z:{zVal,-2:F1}\n" +
                                      $"x:{u,-4:F2} y:{v,-4:F2},u:{Math.Abs(u),-4:F2} v:{Math.Abs(v),-4:F2}";

                    // 仅当文本变化时才更新（避免无意义刷新）
                    if (_hoverInfoText.Text != infoText)
                    {
                        _hoverInfoText.Text = infoText;
                    }
                }
                catch { /* 忽略异常，避免UI卡顿 */ }
            }
        }

        /// <summary>
        /// 鼠标离开图像时，隐藏悬浮信息
        /// </summary>
        private void ImgDisplay_MouseLeave(object sender, MouseEventArgs e)
        {
            lock (_lockObj)
            {
                _isHovering = false;
                _hoverInfoPanel?.SetValue(VisibilityProperty, Visibility.Collapsed);
                // 清空文本（可选，避免残留）
                _hoverInfoText.Text = string.Empty;
            }
        }
        #endregion

        #region 鼠标事件处理
        // 保留原有变量，新增以下关键变量
        private double _imgRenderWidth; // 图片渲染宽度（Image控件显示宽度）
        private double _imgRenderHeight; // 图片渲染高度（Image控件显示高度）
        private double _imgNaturalWidth; // 图片原始宽度（像素）
        private double _imgNaturalHeight; // 图片原始高度（像素）
        // 缩放相关变量
        private double _currentScale = 1.0; // 当前缩放比例
        private const double _scaleStep = 0.1; // 每次滚轮缩放步长
        private const double _minScale = 0.5; // 最小缩放比例（避免缩太小）
        private const double _maxScale = 5.0; // 最大缩放比例（避免缩太大）
        private System.Windows.Point _lastMousePos; // 记录鼠标位置，用于中心缩放

        /// <summary>
        /// 鼠标滚轮缩放图片（以鼠标位置为中心）
        /// </summary>
        //private void ImgDisplay_MouseWheel(object sender, MouseWheelEventArgs e)
        //{
        //    if (imgDisplay.Source == null || imgGrid == null) return;

        //    // 1. 获取基础尺寸信息
        //    _imgRenderWidth = imgDisplay.ActualWidth;
        //    _imgRenderHeight = imgDisplay.ActualHeight;
        //    if (_imgRenderWidth == 0 || _imgRenderHeight == 0) return;

        //    // 2. 获取鼠标在imgGrid中的绝对位置（关键：基于Grid而非Image）
        //    System.Windows.Point mousePosInGrid = e.GetPosition(imgGrid);
        //    _lastMousePos = mousePosInGrid;

        //    // 3. 计算缩放前鼠标在图片上的绝对像素坐标
        //    // 3.1 计算Image控件在imgGrid中的偏移（处理居中对齐）
        //    double imgOffsetX = (imgGrid.ActualWidth - _imgRenderWidth) / 2;
        //    double imgOffsetY = (imgGrid.ActualHeight - _imgRenderHeight) / 2;

        //    // 3.2 计算鼠标在Image控件内的相对位置（去除偏移）
        //    double mouseXInImage = Math.Max(0, mousePosInGrid.X - imgOffsetX);
        //    double mouseYInImage = Math.Max(0, mousePosInGrid.Y - imgOffsetY);

        //    // 3.3 计算鼠标指向的图片原始像素坐标
        //    double pixelX = (mouseXInImage / _imgRenderWidth) * _imgNaturalWidth;
        //    double pixelY = (mouseYInImage / _imgRenderHeight) * _imgNaturalHeight;

        //    // 4. 计算新的缩放比例
        //    double delta = e.Delta > 0 ? _scaleStep : -_scaleStep;
        //    double newScale = _currentScale + delta;
        //    newScale = Math.Clamp(newScale, _minScale, _maxScale);
        //    if (newScale == _currentScale) return;

        //    // 5. 核心：计算平移补偿量（保证鼠标位置固定）
        //    // 5.1 缩放前鼠标位置的屏幕坐标（相对于Image左上角）
        //    double screenXBefore = (pixelX / _imgNaturalWidth) * _imgRenderWidth * _currentScale;
        //    double screenYBefore = (pixelY / _imgNaturalHeight) * _imgRenderHeight * _currentScale;

        //    // 5.2 缩放后鼠标位置的屏幕坐标
        //    double screenXAfter = (pixelX / _imgNaturalWidth) * _imgRenderWidth * newScale;
        //    double screenYAfter = (pixelY / _imgNaturalHeight) * _imgRenderHeight * newScale;

        //    // 5.3 计算需要补偿的平移量（抵消缩放带来的位置变化）
        //    double deltaX = screenXBefore - screenXAfter;
        //    double deltaY = screenYBefore - screenYAfter;

        //    // 6. 更新变换
        //    // 6.1 先更新缩放
        //    imgScaleTransform.ScaleX = newScale;
        //    imgScaleTransform.ScaleY = newScale;

        //    // 6.2 再更新平移（累加补偿量）
        //    imgTranslateTransform.X += deltaX;
        //    imgTranslateTransform.Y += deltaY;

        //    // 7. 限制平移范围（避免图片完全移出可视区域）
        //    LimitTranslation();

        //    // 8. 更新当前缩放比例
        //    _currentScale = newScale;
        //}
        // 3.限制平移范围
        private void LimitTranslation()
        {
            if (_imgRenderWidth == 0 || _imgRenderHeight == 0) return;

            // 计算图片缩放后的尺寸
            double scaledWidth = _imgRenderWidth * _currentScale;
            double scaledHeight = _imgRenderHeight * _currentScale;

            // 计算最大平移范围（保证图片至少有一部分在可视区域）
            double maxTranslateX = Math.Max(0, scaledWidth - imgGrid.ActualWidth);
            double maxTranslateY = Math.Max(0, scaledHeight - imgGrid.ActualHeight);

            // 限制平移X轴
            imgTranslateTransform.X = Math.Clamp(
                imgTranslateTransform.X,
                -maxTranslateX,
                0
            );

            // 限制平移Y轴
            imgTranslateTransform.Y = Math.Clamp(
                imgTranslateTransform.Y,
                -maxTranslateY,
                0
            );
        }
        // imgGrid大小变化时更新裁剪区域
        private void ImgGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (imgGrid != null)
            {
                // 设置裁剪区域为imgGrid的完整边界
                imgGridClip.Rect = new System.Windows.Rect(0, 0, imgGrid.ActualWidth, imgGrid.ActualHeight);
            }
        }
        /// <summary>
        /// 重置图片缩放到原始大小
        /// </summary>
        private void ResetImageScale()
        {
            _currentScale = 1.0;
            imgScaleTransform.ScaleX = 1.0;
            imgScaleTransform.ScaleY = 1.0;
            // 重置缩放中心为图片中心
            imgScaleTransform.CenterX = 0.5;
            imgScaleTransform.CenterY = 0.5;
            // 重置平移变换
            imgTranslateTransform.X = 0;
            imgTranslateTransform.Y = 0;

            // 重置平移变换
            imgTranslateTransform.X = 0;
            imgTranslateTransform.Y = 0;
            // 确保裁剪区域始终有效
            if (imgGrid != null)
            {
                imgGridClip.Rect = new System.Windows.Rect(0, 0, imgGrid.ActualWidth, imgGrid.ActualHeight);
            }
        }
        #endregion

        // 全局采样点数量（默认值100，可通过按钮修改）
        private int _pointNumLine = 360;
        private double _linePolarInterval = 1; // 极角间隔角度（默认1°）
        private double _azimuthInterval = 3; // 方位角间隔角度（默认3°）
                                             //private void Button_Click(object sender, RoutedEventArgs e)
                                             //{
                                             //    // 1. 先校验输入是否为空
                                             //    if (string.IsNullOrWhiteSpace(pointNumLineBox.Text))
                                             //    {
                                             //        MessageBox.Show($"{FindResource("Pleaseenteranumber")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                                             //        return;
                                             //    }

        //    // 2. 尝试转换为整数
        //    if (!int.TryParse(pointNumLineBox.Text, out int pointNumLine))
        //    {
        //        MessageBox.Show($"{FindResource("Pleaseenteraninteger")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //        return;
        //    }

        //    // 3. 校验是否为正整数
        //    if (pointNumLine <= 0)
        //    {
        //        MessageBox.Show($"{FindResource("Pleaseenterapositiveinteger")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //        return;
        //    }
        //    // 先获取极角范围
        //    int polarRHO = int.TryParse(txtLinePolarRHO.Text.Trim(), out int rho) ? rho : 60;
        //    // 由间隔角度计算采样点数
        //    int _pointNumLine = (int)(Math.Abs(2 * polarRHO) / _linePolarInterval) + 1;
        //    // 将合法值赋值给全局变量
        //    _pointNumLine = pointNumLine;


        //}

        //private void BtnExport_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        if (YMat == null || YMat.Empty())
        //        {
        //            MessageBox.Show($"{FindResource("Nodata")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //            return;
        //        }

        //        // 选择导出基础路径
        //        var saveFileDialog = new SaveFileDialog
        //        {
        //            Filter = "CSV Files (*.csv)|*.csv",
        //            FileName = $"VAM_Export_{DateTime.Now:yyyyMMdd_HHmmss}",
        //            Title = $"{FindResource("Basepath")}"
        //        };

        //        if (saveFileDialog.ShowDialog() != true) return;
        //        string basePath = System.IO.Path.ChangeExtension(saveFileDialog.FileName, null); // 去除.csv后缀

        //        // 打开导出配置弹窗
        //        var exportDialog = new VamExportDialog(this, basePath)
        //        {
        //            Owner = System.Windows.Window.GetWindow(this) // 设置父窗口，保证居中
        //        };

        //        if (exportDialog.ShowDialog() == true)
        //        {
        //            MessageBox.Show($"{FindResource("Exportcompleted")}", $"{FindResource("Log.Success")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error("Export initialization failed", ex);//: "导出初始化失败"
        //        MessageBox.Show($"Export initialization failed：{ex.Message}", $"{FindResource("Log.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }

        //}
        public void BtnExportClick()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (YMat == null || YMat.Empty())
                    {
                        MessageBox.Show($"{FindResource("Nodata")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 调用 DLL 获取并填充 _dllAllAzimuthData
                    bool dllSuccess = CallVamDllForAllAzimutha(
                        exportChannel: displayChannel,
                        pointNumLine: _pointNumLine,
                        polarRHO: 60.0,
                        polarAngle: 60.0
                    );

                    if (!dllSuccess)
                    {
                        MessageBox.Show($"{FindResource("VAM.Noexport")}", $"{FindResource("Log.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 1. 获取导出目录（GetVamGlobalExportPath 返回目录）
                    string exportDir = GetVamGlobalExportPath();
                    if (string.IsNullOrWhiteSpace(exportDir))
                    {
                        MessageBox.Show($"{FindResource("VAM.Exportfailed")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 2. 确保目录存在
                    if (!Directory.Exists(exportDir))
                    {
                        Directory.CreateDirectory(exportDir);
                    }

                    // 3. 生成带时间戳的文件名并合并成完整文件路径
                    string fileName = $"VAM_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    string exportFilePath = Path.Combine(exportDir, fileName);

                    // 4. 按原有格式写入文件（确保使用文件路径）
                    using (var writer = new StreamWriter(exportFilePath, false, Encoding.UTF8))
                    {
                        // 第1行：Measurement Date
                        writer.WriteLine($"Measurement Date,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,");

                        // 第2行：Instrument
                        writer.WriteLine($"Instrument,VAM 60°,,,,,,,,,,,,");

                        // 第3行：空行
                        writer.WriteLine();

                        // 第4行：列标题（径向角行标题 + 方位角0°~180°）
                        StringBuilder headerLine = new StringBuilder();
                        headerLine.Append(","); // A列空
                        headerLine.Append(" "); // B列：径向角
                        foreach (int azimuth in Enumerable.Range(0, 180)) // 0°~179°
                        {
                            headerLine.Append($",{azimuth}°");
                        }
                        writer.WriteLine(headerLine.ToString());

                        // 遍历径向角-60°~60°，逐行写入数据
                        for (int radial = -60; radial <= 60; radial++)
                        {
                            StringBuilder dataLine = new StringBuilder();
                            dataLine.Append(","); // A列空
                            dataLine.Append($"{radial}°"); // B列：当前径向角

                            foreach (int azimuth in Enumerable.Range(0, 180))
                            {
                                if (_dllAllAzimuthData.TryGetValue(azimuth, out var sampleList))
                                {
                                    var sample = sampleList.FirstOrDefault(p => Math.Round(p.position, 0) == radial);
                                    double value = sample != null ? GetChannelValueFromDll(sample, displayChannel) : 0;
                                    dataLine.Append($",{value:F5}");
                                }
                                else
                                {
                                    dataLine.Append(",");
                                }
                            }
                            writer.WriteLine(dataLine.ToString());
                        }
                    }

                    logger.Info($"VAM data has been exported to: {exportFilePath}");
                    // MessageBox.Show($"{FindResource("Exportcompleted")}\n{exportFilePath}", $"{FindResource("Log.Success")}", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    logger.Error($"{FindResource("Exportfailed")}", ex);
                    //MessageBox.Show($"{FindResource("Exportfailed")}: {ex.Message}", $"{FindResource("Log.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        #region VAM导出入口方法
        // 直径线模式：获取选中的导出通道
        private List<ExportDataType> GetDiameterSelectedChannels()
        {
            var channels = new List<ExportDataType>();
            if (cbExportX.IsChecked == true) channels.Add(ExportDataType.X);
            if (cbExportY.IsChecked == true) channels.Add(ExportDataType.Y);
            if (cbExportZ.IsChecked == true) channels.Add(ExportDataType.Z);
            if (cbExportCieX.IsChecked == true) channels.Add(ExportDataType.CieX);
            if (cbExportCieY.IsChecked == true) channels.Add(ExportDataType.CieY);
            return channels;
        }

        // 圆环模式：获取选中的导出通道
        private List<ExportDataType> GetCircleSelectedChannels()
        {
            var channels = new List<ExportDataType>();
            if (cbCircleExportX.IsChecked == true) channels.Add(ExportDataType.X);
            if (cbCircleExportY.IsChecked == true) channels.Add(ExportDataType.Y);
            if (cbCircleExportZ.IsChecked == true) channels.Add(ExportDataType.Z);
            if (cbCircleExportCieX.IsChecked == true) channels.Add(ExportDataType.CieX);
            if (cbCircleExportCieY.IsChecked == true) channels.Add(ExportDataType.CieY);
            return channels;
        }
        /// <summary>
        /// 导出数据类型枚举
        /// </summary>
        public enum ExportDataType
        {
            X,
            Y,
            Z,
            CieX,
            CieY
        }
        /// <summary>
        /// 获取线条采样点的通道值
        /// </summary>
        private double GetChannelValue(VamSamplePoint sample, ExportDataType channel)
        {
            if (sample == null) return 0;
            return channel switch
            {
                ExportDataType.X => sample.X,
                ExportDataType.Y => sample.Y,
                ExportDataType.Z => sample.Z,
                ExportDataType.CieX => sample.cie_x,
                ExportDataType.CieY => sample.cie_y,
                _ => 0
            };
        }
        // 单独封装采样点更新逻辑（原TextChanged事件中的代码）
        /// <summary>
        /// 更新极角间隔角度备注文本
        /// </summary>
        private void UpdateLinePolarIntervalText()
        {
            // 1. 获取极角范围（从原有txtLinePolarRHO控件读取）
            string polarRhoInput = txtLinePolarRHO.Text.Trim();
            if (!int.TryParse(polarRhoInput, out int polarRHO) || polarRHO <= 0)
            {
                LinePolarIntervalBook.Text = (string)Application.Current.FindResource("VAM.60Point");
                return;
            }

            // 2. 获取极角间隔角度
            string intervalInput = txtLinePolarInterval.Text.Trim();
            if (!double.TryParse(intervalInput, out double polarInterval) || polarInterval <= 0)
            {
                LinePolarIntervalBook.Text = $"（[-{polarRHO},{polarRHO}]{(string)Application.Current.FindResource("VAM.InvalidInterval")}，0{(string)Application.Current.FindResource("VAM.Points")}）";
                return;
            }

            // 3. 计算总采样点数（总范围=2*polarRHO，点数=总范围/间隔 + 1）
            int sampleCount = (int)(Math.Abs(2 * polarRHO) / polarInterval) + 1;

            // 4. 更新备注文本
            LinePolarIntervalBook.Text = $"（[-{polarRHO},{polarRHO}]{(string)Application.Current.FindResource("VAM.Interval")}{polarInterval}°，{sampleCount}{(string)Application.Current.FindResource("VAM.Points")}）";

            // 5. 同步更新全局变量
            _linePolarInterval = polarInterval;
        }
        private void UpdateAzimuthIntervalText()
        {
            // 1. 获取方位角间隔角度
            string intervalInput = txtAzimuthInterval.Text.Trim();
            if (!double.TryParse(intervalInput, out double azimuthInterval) || azimuthInterval <= 0)
            {
                AzimuthIntervalBook.Text = (string)Application.Current.FindResource("VAM.360Point");
                return;
            }

            // 2. 计算总采样点数（总范围360°，点数=360/间隔）
            int sampleCount = (int)(360 / azimuthInterval);

            // 3. 更新备注文本
            AzimuthIntervalBook.Text = $"（[0,360){(string)Application.Current.FindResource("VAM.Interval")}{azimuthInterval}°，{sampleCount}{(string)Application.Current.FindResource("VAM.Points")}）";

            // 4. 同步更新全局变量
            _azimuthInterval = azimuthInterval;
        }
      
        private async void BtnExportDiameter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 启动进度条（必须在UI线程）
                _progressManager.Start();

                // 异步执行导出操作
                await Task.Run(async () =>
                {
                    try
                    {
                        // 第一阶段：参数准备
                        string basePath = string.Empty;
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var saveFileDialog = new SaveFileDialog
                            {
                                Filter = "CSV Files (*.csv)|*.csv",
                                FileName = $"VAM_Azimuth_{DateTime.Now:yyyyMMdd_HHmmss}",
                                Title = $"{FindResource("VAM.SaveAzimuth")}"
                            };

                            if (saveFileDialog.ShowDialog() == true)
                            {
                                basePath = Path.ChangeExtension(saveFileDialog.FileName, null);
                            }
                        });

                        if (string.IsNullOrEmpty(basePath)) return;
                        _progressManager.UpdateProgress(10);

                        // 第二阶段：获取界面参数
                        int polarRHO = 60;
                        double polarInterval = 1;
                        List<ExportDataType> selectedChannels = new List<ExportDataType>();

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            // 获取极角范围
                            polarRHO = int.TryParse(txtLinePolarRHO.Text.Trim(), out int rho) ? rho : 60;
                            // 获取极角间隔
                            polarInterval = _linePolarInterval;
                            selectedChannels = GetDiameterSelectedChannels();
                        });

                        if (selectedChannels.Count == 0)
                        {
                            throw new Exception($"{FindResource("VAM.Onechannel")}");
                        }

                        // 第三阶段：调用DLL获取所有方位角数据 - 使用异步版本
                        _progressManager.UpdateProgress(20);

                        // 计算采样点数
                        int totalPolarSamples = (int)((2 * polarRHO) / polarInterval) + 1;
                        if (totalPolarSamples <= 0) totalPolarSamples = 121; // 默认值

                        bool dllSuccess = false;

                        // 使用异步版本的DLL调用方法
                        dllSuccess = await CallVamDllForAllAzimuthAsync(
                            exportChannel: displayChannel,
                            pointNumLine: totalPolarSamples,
                            polarRHO: polarRHO,
                            polarAngle: polarRHO,
                            onProgressUpdate: (progress) =>
                            {
                                // 在DLL调用过程中更新进度（从20%到40%）
                                int totalProgress = 20 + (int)(progress * 0.2); // 20% + (progress * 0.2)
                                _progressManager.UpdateProgress(totalProgress);
                            }
                        );

                        if (!dllSuccess || _dllAllAzimuthData == null || _dllAllAzimuthData.Count == 0)
                        {
                            throw new Exception($"{FindResource("VAM.DLLcallfailed")}");
                        }

                        _progressManager.UpdateProgress(40);

                        // 第四阶段：导出文件
                        await ExportLineModeAsync(selectedChannels, basePath, polarRHO, polarInterval);

                        // 第五阶段：完成
                        _progressManager.UpdateProgress(100);

                        // 显示成功消息
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show($"{FindResource("Exportsuccessful")}",
                                $"{FindResource("Log.Success")}", MessageBoxButton.OK, MessageBoxImage.Information);
                            _progressManager.Complete();
                        });
                    }
                    catch (Exception ex)
                    {
                        // 错误处理
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _progressManager.Fail();
                            MessageBox.Show($"{FindResource("Exportfailed")}：{ex.Message}",
                                $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                _progressManager.Fail();
                MessageBox.Show($"{FindResource("Exportfailed")}：{ex.Message}",
                    $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            //try
            //{
            //    // 启动进度条（必须在UI线程）
            //    _progressManager.Start();
            //    string polarRhoInput = txtLinePolarRHO.Text.Trim();
            //    // 异步执行导出操作
            //    await Task.Run(async () =>
            //    {
            //        try
            //        {
            //            // 第一阶段：参数准备
            //            // 选择保存路径（必须在UI线程）
            //            string basePath = string.Empty;
            //            await Application.Current.Dispatcher.InvokeAsync(() =>
            //            {
            //                var saveFileDialog = new SaveFileDialog
            //                {
            //                    Filter = "CSV Files (*.csv)|*.csv",
            //                    FileName = $"VAM_Azimuth_{DateTime.Now:yyyyMMdd_HHmmss}",
            //                    Title = $"{FindResource("VAM.SaveAzimuth")}"
            //                };

            //                if (saveFileDialog.ShowDialog() == true)
            //                {
            //                    basePath = Path.ChangeExtension(saveFileDialog.FileName, null);
            //                }
            //            });

            //            if (string.IsNullOrEmpty(basePath)) return;


            //            // 参数校验...

            //            if (!int.TryParse(polarRhoInput, out int polarRHO))
            //            {
            //                throw new Exception($"{FindResource("VAM.InvalidPolarRHO")}");
            //            }

            //            // 注意：所有UI访问必须在UI线程
            //            var selectedChannels = new List<ExportDataType>();
            //            await Application.Current.Dispatcher.InvokeAsync(() =>
            //            {
            //                selectedChannels = GetDiameterSelectedChannels();
            //            });

            //            if (selectedChannels.Count == 0)
            //            {
            //                throw new Exception($"{FindResource("VAM.Onechannel")}");
            //            }

            //            // 第二阶段：数据准备
            //            //_progressManager.UpdateProgress(10);




            //            // 第三阶段：调用DLL
            //            _progressManager.UpdateProgress(20);

            //            // 注意：CallVamDllForAllAzimuth可能包含UI访问，需要检查
            //            bool dllSuccess = false;
            //            await Task.Run(() =>
            //            {
            //                dllSuccess = this.CallVamDllForAllAzimuth(
            //                    exportChannel: displayChannel,
            //                    pointNumLine: _pointNumLine,
            //                    polarRHO: polarRHO,
            //                    polarAngle: polarRHO
            //                );
            //            });

            //            if (!dllSuccess)
            //            {
            //                throw new Exception($"{FindResource("VAM.DLLcallfailed")}");
            //            }

            //            // 第四阶段：导出文件
            //            _progressManager.UpdateProgress(40);

            //            // 导出逻辑...
            //            await ExportLineModeAsync(selectedChannels, basePath);

            //            // 第五阶段：完成
            //            _progressManager.UpdateProgress(100);

            //            // 显示成功消息（必须在UI线程）
            //            await Application.Current.Dispatcher.InvokeAsync(() =>
            //            {
            //                MessageBox.Show($"{FindResource("Exportsuccessful")}",
            //                    $"{FindResource("Log.Success")}", MessageBoxButton.OK, MessageBoxImage.Information);
            //                _progressManager.Complete();
            //            });
            //        }
            //        catch (Exception ex)
            //        {
            //            // 错误处理（必须在UI线程）
            //            await Application.Current.Dispatcher.InvokeAsync(() =>
            //            {
            //                _progressManager.Fail();
            //                MessageBox.Show($"{FindResource("Exportfailed")}：{ex.Message}",
            //                    $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
            //            });
            //        }
            //    });
            //}
            //catch (Exception ex)
            //{
            //    _progressManager.Fail();
            //    MessageBox.Show($"{FindResource("Exportfailed")}：{ex.Message}",
            //        $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
            //}

        }
        //private async Task ExportLineModeAsync(List<ExportDataType> selectedChannels, string basePath)
        //{
        //    try
        //    {
        //        // ========== 第1阶段：参数准备 ==========
        //       // _progressManager.UpdateProgress(10);

        //        int polarRHO = await Application.Current.Dispatcher.InvokeAsync(() => LinePolarRHO);
        //        double polarInterval = _linePolarInterval;

        //        // ========== 第2阶段：采样点计算 ==========
        //        _progressManager.UpdateProgress(45);

        //        // 生成极角数组
        //        int totalPolarSamples = (int)((2 * polarRHO) / polarInterval) + 1;
        //        double[] polarAngles = new double[totalPolarSamples];

        //        for (int i = 0; i < totalPolarSamples; i++)
        //        {
        //            polarAngles[i] = Math.Round(-polarRHO + i * polarInterval, 2);
        //        }

        //        // 方位角数组（0-180度，1度间隔）
        //        int totalAzimuthSamples = 181;
        //        double[] azimuthAngles = new double[totalAzimuthSamples];
        //        for (int i = 0; i < totalAzimuthSamples; i++)
        //        {
        //            azimuthAngles[i] = i;
        //        }

        //        logger.Info($"极角采样点: {totalPolarSamples}, 方位角采样点: {totalAzimuthSamples}");

        //        // ========== 第3阶段：调用DLL ==========
        //        _progressManager.UpdateProgress(50);

        //        bool dllSuccess = false;
        //        await Task.Run(() =>
        //        {
        //            dllSuccess = this.CallVamDllForAllAzimuth(
        //                exportChannel: displayChannel,
        //                pointNumLine: totalPolarSamples,
        //                polarRHO: polarRHO,
        //                polarAngle: polarRHO
        //            );
        //        });

        //        if (!dllSuccess || _dllAllAzimuthData == null || _dllAllAzimuthData.Count == 0)
        //        {
        //            throw new Exception($"{FindResource("VAM.DLLcallfailed")}");
        //        }

        //        _progressManager.UpdateProgress(60);

        //        // ========== 第4阶段：预准备数据矩阵 ==========
        //        // 为每个通道创建数据矩阵，提高写入效率
        //        Dictionary<ExportDataType, double[,]> channelDataMatrices = new Dictionary<ExportDataType, double[,]>();

        //        foreach (var channel in selectedChannels)
        //        {
        //            channelDataMatrices[channel] = new double[totalPolarSamples, totalAzimuthSamples];
        //        }

        //        // 填充数据矩阵
        //        for (int azimuthIndex = 0; azimuthIndex < totalAzimuthSamples; azimuthIndex++)
        //        {
        //            int azimuth = (int)azimuthAngles[azimuthIndex];

        //            if (_dllAllAzimuthData.TryGetValue(azimuth, out var sampleList) && sampleList.Count > 0)
        //            {
        //                // 对每个极角采样点
        //                for (int polarIndex = 0; polarIndex < totalPolarSamples; polarIndex++)
        //                {
        //                    double polar = polarAngles[polarIndex];

        //                    // 找到最接近的采样点
        //                    var targetSample = sampleList
        //                        .OrderBy(s => Math.Abs(Math.Round(s.position, 2) - polar))
        //                        .FirstOrDefault();

        //                    if (targetSample != null)
        //                    {
        //                        foreach (var channel in selectedChannels)
        //                        {
        //                            channelDataMatrices[channel][polarIndex, azimuthIndex] = GetChannelValue(targetSample, channel);
        //                        }
        //                    }
        //                }
        //            }

        //            // 每处理10个方位角更新一次进度
        //            if (azimuthIndex % 10 == 0)
        //            {
        //                int progress = 60 + (int)(azimuthIndex * 10.0 / totalAzimuthSamples);
        //                _progressManager.UpdateProgress(progress);
        //            }
        //        }

        //        // ========== 第5阶段：导出文件 ==========
        //        int channelCount = selectedChannels.Count;
        //        for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
        //        {
        //            var channel = selectedChannels[channelIndex];
        //            double[,] dataMatrix = channelDataMatrices[channel];

        //            int channelStartProgress = 70 + (int)(channelIndex * 35.0 / channelCount);
        //            _progressManager.UpdateProgress(channelStartProgress);

        //            string csvFileName = $"{basePath}_{channel}.csv";
        //            string fullCsvPath = Path.Combine(Path.GetDirectoryName(csvFileName) ?? "", Path.GetFileName(csvFileName));

        //            await Task.Run(() =>
        //            {
        //                using (var writer = new StreamWriter(fullCsvPath, false, Encoding.UTF8))
        //                {
        //                    // 写入表头
        //                    writer.WriteLine($"Measurement Date,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,");
        //                    writer.WriteLine($"Instrument,VAM {polarRHO}°,,,,,,,,,,,,");
        //                    writer.WriteLine($"Channel,{channel},,,,,,,,,,,,");
        //                    writer.WriteLine($"PolarInterval,{polarInterval}°,,,,,,,,,,,,,");
        //                    writer.WriteLine();

        //                    // 写入列标题
        //                    writer.Write("Polar Angle(°)");
        //                    for (int i = 0; i < totalAzimuthSamples; i++)
        //                    {
        //                        writer.Write($",{azimuthAngles[i]:F0}°");
        //                    }
        //                    writer.WriteLine();

        //                    // 写入数据行
        //                    for (int polarIndex = 0; polarIndex < totalPolarSamples; polarIndex++)
        //                    {
        //                        writer.Write($"{polarAngles[polarIndex]:F2}");

        //                        for (int azimuthIndex = 0; azimuthIndex < totalAzimuthSamples; azimuthIndex++)
        //                        {
        //                            writer.Write($",{dataMatrix[polarIndex, azimuthIndex]:F5}");
        //                        }

        //                        writer.WriteLine();

        //                        // 每处理20行更新一次进度
        //                        if (polarIndex % 20 == 0)
        //                        {
        //                            int progress = channelStartProgress + (int)((polarIndex + 1) * 35.0 / totalPolarSamples / channelCount);
        //                            _progressManager.UpdateProgress(Math.Min(progress, 95));
        //                        }
        //                    }
        //                }

        //                logger.Info($"通道 {channel} 导出完成: {fullCsvPath}");
        //            });
        //        }

        //        _progressManager.UpdateProgress(100);
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error("ExportLineModeAsync 执行失败", ex);
        //        throw;
        //    }
        //}
        private async Task ExportLineModeAsync(List<ExportDataType> selectedChannels, string basePath, int polarRHO, double polarInterval)
        {
            try
            {
                // ========== 第1阶段：参数准备 ==========
                _progressManager.UpdateProgress(45);

                // 生成极角数组
                int totalPolarSamples = (int)((2 * polarRHO) / polarInterval) + 1;
                double[] polarAngles = new double[totalPolarSamples];

                for (int i = 0; i < totalPolarSamples; i++)
                {
                    polarAngles[i] = Math.Round(-polarRHO + i * polarInterval, 2);
                }

                // 方位角数组（0-180度，1度间隔）
                int totalAzimuthSamples = 181;
                double[] azimuthAngles = new double[totalAzimuthSamples];
                for (int i = 0; i < totalAzimuthSamples; i++)
                {
                    azimuthAngles[i] = i;
                }

                logger.Info($"polar angle sampling points: {totalPolarSamples}, azimuth angle sampling points: {totalAzimuthSamples}");

                // ========== 第2阶段：预准备数据矩阵 ==========
                _progressManager.UpdateProgress(50);

                // 为每个通道创建数据矩阵，提高写入效率
                Dictionary<ExportDataType, double[,]> channelDataMatrices = new Dictionary<ExportDataType, double[,]>();

                foreach (var channel in selectedChannels)
                {
                    channelDataMatrices[channel] = new double[totalPolarSamples, totalAzimuthSamples];
                }

                // 填充数据矩阵
                for (int azimuthIndex = 0; azimuthIndex < totalAzimuthSamples; azimuthIndex++)
                {
                    int azimuth = (int)azimuthAngles[azimuthIndex];

                    // 从缓存中获取数据
                    var sampleList = await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        return _dllAllAzimuthData.TryGetValue(azimuth, out var list) ? list : null;
                    });

                    if (sampleList != null && sampleList.Count > 0)
                    {
                        // 对每个极角采样点
                        for (int polarIndex = 0; polarIndex < totalPolarSamples; polarIndex++)
                        {
                            double polar = polarAngles[polarIndex];

                            // 找到最接近的采样点
                            var targetSample = sampleList
                                .OrderBy(s => Math.Abs(Math.Round(s.position, 2) - polar))
                                .FirstOrDefault();

                            if (targetSample != null)
                            {
                                foreach (var channel in selectedChannels)
                                {
                                    channelDataMatrices[channel][polarIndex, azimuthIndex] = GetChannelValue(targetSample, channel);
                                }
                            }
                        }
                    }

                    // 更新进度
                    if (azimuthIndex % 10 == 0)
                    {
                        int progress = 50 + (int)(azimuthIndex * 20.0 / totalAzimuthSamples);
                        _progressManager.UpdateProgress(progress);
                    }
                }

                // ========== 第3阶段：导出文件 ==========
                int channelCount = selectedChannels.Count;
                for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
                {
                    var channel = selectedChannels[channelIndex];
                    double[,] dataMatrix = channelDataMatrices[channel];

                    int channelStartProgress = 70 + (int)(channelIndex * 25.0 / channelCount);
                    _progressManager.UpdateProgress(channelStartProgress);

                    string csvFileName = $"{basePath}_{channel}.csv";
                    string fullCsvPath = Path.Combine(Path.GetDirectoryName(csvFileName) ?? "", Path.GetFileName(csvFileName));

                    await Task.Run(() =>
                    {
                        using (var writer = new StreamWriter(fullCsvPath, false, Encoding.UTF8))
                        {
                            // 写入表头
                            writer.WriteLine($"Measurement Date,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,");
                            writer.WriteLine($"Instrument,VAM {polarRHO}°,,,,,,,,,,,,");
                            writer.WriteLine($"Channel,{channel},,,,,,,,,,,,");
                            writer.WriteLine($"PolarInterval,{polarInterval}°,,,,,,,,,,,,,");
                            writer.WriteLine();

                            // 写入列标题
                            writer.Write("Polar Angle(°)");
                            for (int i = 0; i < totalAzimuthSamples; i++)
                            {
                                writer.Write($",{azimuthAngles[i]:F0}°");
                            }
                            writer.WriteLine();

                            // 写入数据行
                            for (int polarIndex = 0; polarIndex < totalPolarSamples; polarIndex++)
                            {
                                writer.Write($"{polarAngles[polarIndex]:F2}");

                                for (int azimuthIndex = 0; azimuthIndex < totalAzimuthSamples; azimuthIndex++)
                                {
                                    writer.Write($",{dataMatrix[polarIndex, azimuthIndex]:F5}");
                                }

                                writer.WriteLine();

                                // 每处理20行更新一次进度
                                if (polarIndex % 20 == 0)
                                {
                                    int progress = channelStartProgress +
                                        (int)((polarIndex + 1) * 25.0 / totalPolarSamples / channelCount);

                                    // 在主线程更新进度
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        _progressManager.UpdateProgress(Math.Min(progress, 95));
                                    });
                                }
                            }
                        }

                        logger.Info($"Channel {channel} {FindResource("Exportcompleted")}: {fullCsvPath}");
                    });
                }

                _progressManager.UpdateProgress(100);
            }
            catch (Exception ex)
            {
                logger.Error("ExportLineModeAsync execution failed", ex);
                throw;
            }
        }
        private async void BtnExportCircle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 启动进度条（必须在UI线程）
                _progressManager.Start();

                // 异步执行导出操作
                await Task.Run(async () =>
                {
                    try
                    {
                        // 第一阶段：参数准备
                        string basePath = string.Empty;
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            // 3. 校验数据
                            if (YMat == null || YMat.Empty())
                            {
                                throw new Exception($"{FindResource("Nodata")}");
                            }

                            // 4. 选择保存路径
                            var saveFileDialog = new SaveFileDialog
                            {
                                Filter = "CSV Files (*.csv)|*.csv",
                                FileName = $"VAM_Polar_Angle_{DateTime.Now:yyyyMMdd_HHmmss}",
                                Title = $"{FindResource("VAM.SavePolar")}"
                            };

                            if (saveFileDialog.ShowDialog() == true)
                            {
                                basePath = Path.ChangeExtension(saveFileDialog.FileName, null);
                            }
                        });

                        if (string.IsNullOrEmpty(basePath)) return;

                        _progressManager.UpdateProgress(10);

                        // 第二阶段：获取界面参数（再次获取，确保最新值）
                        int polarStart = 0;
                        int polarEnd = 0;
                        int polarStep = 0;
                        double azimuthInterval = 0;
                        List<ExportDataType> selectedChannels = new List<ExportDataType>();

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            polarStart = CirclePolarStart;
                            polarEnd = CirclePolarEnd;
                            polarStep = CirclePolarStep;
                            azimuthInterval = _azimuthInterval;
                            selectedChannels = GetCircleSelectedChannels();
                        });

                        // 第三阶段：调用DLL获取圆环数据 - 使用异步版本
                        _progressManager.UpdateProgress(20);

                        int azimuthSampleCount = (int)Math.Round(360 / azimuthInterval);
                        if (azimuthSampleCount < 1) azimuthSampleCount = 360;

                        bool dllSuccess = false;

                        // 使用异步版本的DLL调用方法
                        dllSuccess = await CallVamDllForAllCircleAsync(
                            polarStart: polarStart,
                            polarEnd: polarEnd,
                            polarStep: polarStep,
                            azimuthSampleCount: azimuthSampleCount,
                            onProgressUpdate: (progress) =>
                            {
                                // 在DLL调用过程中更新进度（从20%到40%）
                                int totalProgress = 20 + (int)(progress * 0.2); // 20% + (progress * 0.2)
                                _progressManager.UpdateProgress(totalProgress);
                            }
                        );

                        if (!dllSuccess)
                        {
                            throw new Exception($"{FindResource("VAM.DLLcallfailed")}");
                        }

                        _progressManager.UpdateProgress(40);

                        // 第四阶段：导出文件
                        await ExportCircleModeAsync(selectedChannels, basePath, polarStart, polarEnd, polarStep, azimuthInterval);

                        // 第五阶段：完成
                        _progressManager.UpdateProgress(100);

                        // 显示成功消息
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show($"{FindResource("Exportsuccessful")}",
                                $"{FindResource("Log.Success")}", MessageBoxButton.OK, MessageBoxImage.Information);
                            _progressManager.Complete();
                        });
                    }
                    catch (Exception ex)
                    {
                        // 错误处理
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _progressManager.Fail();
                            MessageBox.Show($"{FindResource("Exportfailed")}：{ex.Message}",
                                $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                _progressManager.Fail();
                MessageBox.Show($"{FindResource("Exportfailed")}：{ex.Message}",
                    $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            //try
            //{
            //    // 启动进度条（必须在UI线程）
            //    _progressManager.Start();

            //    // 异步执行导出操作
            //    await Task.Run(async () =>
            //    {
            //        try
            //        {
            //            // 第一阶段：参数准备
            //            string basePath = string.Empty;
            //            await Application.Current.Dispatcher.InvokeAsync(() =>
            //            {
            //                // 3. 校验数据
            //                if (YMat == null || YMat.Empty())
            //                {
            //                    throw new Exception($"{FindResource("Nodata")}");
            //                }

            //                // 4. 选择保存路径
            //                var saveFileDialog = new SaveFileDialog
            //                {
            //                    Filter = "CSV Files (*.csv)|*.csv",
            //                    FileName = $"VAM_Polar_Angle_{DateTime.Now:yyyyMMdd_HHmmss}",
            //                    Title = $"{FindResource("VAM.SavePolar")}"
            //                };

            //                if (saveFileDialog.ShowDialog() == true)
            //                {
            //                    basePath = Path.ChangeExtension(saveFileDialog.FileName, null);
            //                }
            //            });

            //            if (string.IsNullOrEmpty(basePath)) return;

            //            _progressManager.UpdateProgress(10);

            //            // 第二阶段：获取界面参数（再次获取，确保最新值）
            //            int polarStart = 0;
            //            int polarEnd = 0;
            //            int polarStep = 0;
            //            double azimuthInterval = 0;
            //            List<ExportDataType> selectedChannels = new List<ExportDataType>();

            //            await Application.Current.Dispatcher.InvokeAsync(() =>
            //            {
            //                polarStart = CirclePolarStart;
            //                polarEnd = CirclePolarEnd;
            //                polarStep = CirclePolarStep;
            //                azimuthInterval = _azimuthInterval;
            //                selectedChannels = GetCircleSelectedChannels();
            //            });

            //            // 第三阶段：调用DLL获取圆环数据
            //            _progressManager.UpdateProgress(20);

            //            int azimuthSampleCount = (int)Math.Round(360 / azimuthInterval);
            //            if (azimuthSampleCount < 1) azimuthSampleCount = 360;

            //            bool dllSuccess = false;
            //            // 直接调用修复后的方法，现在它内部已经处理了线程安全
            //            dllSuccess = this.CallVamDllForAllCircle(
            //                polarStart: polarStart,
            //                polarEnd: polarEnd,
            //                polarStep: polarStep,
            //                azimuthSampleCount: azimuthSampleCount
            //            );

            //            if (!dllSuccess)
            //            {
            //                throw new Exception($"{FindResource("VAM.DLLcallfailed")}");
            //            }

            //            _progressManager.UpdateProgress(40);

            //            // 第四阶段：导出文件
            //            await ExportCircleModeAsync(selectedChannels, basePath, polarStart, polarEnd, polarStep, azimuthInterval);

            //            // 第五阶段：完成
            //            _progressManager.UpdateProgress(100);

            //            // 显示成功消息
            //            await Application.Current.Dispatcher.InvokeAsync(() =>
            //            {
            //                MessageBox.Show($"{FindResource("Exportsuccessful")}",
            //                    $"{FindResource("Log.Success")}", MessageBoxButton.OK, MessageBoxImage.Information);
            //                _progressManager.Complete();
            //            });
            //        }
            //        catch (Exception ex)
            //        {
            //            // 错误处理
            //            await Application.Current.Dispatcher.InvokeAsync(() =>
            //            {
            //                _progressManager.Fail();
            //                MessageBox.Show($"{FindResource("Exportfailed")}：{ex.Message}",
            //                    $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
            //            });
            //        }
            //    });
            //}
            //catch (Exception ex)
            //{
            //    _progressManager.Fail();
            //    MessageBox.Show($"{FindResource("Exportfailed")}：{ex.Message}",
            //        $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
        }
        private async Task ExportCircleModeAsync(List<ExportDataType> selectedChannels,string basePath,int polarStart, int polarEnd,int polarStep,double azimuthInterval)
        {
            try
            {
                // ========== 第1阶段：参数计算 ==========
                _progressManager.UpdateProgress(45);

                // 生成极角采样点
                List<int> polarAngles = new List<int>();
                for (int p = polarStart; p <= polarEnd; p += polarStep)
                {
                    polarAngles.Add(p);
                }
                if (polarAngles.Count == 0) polarAngles.Add(polarStart);
                logger.Info($"The polar angle range is [{polarStart}, {polarEnd}] degrees, with a step size of {polarStep} degrees, resulting in a total of {polarAngles.Count} rings being generated.");
                // 生成方位角采样点
                List<double> azimuthAngles = new List<double>();
                double currentAzimuth = 0;
                while (currentAzimuth < 360 - 1e-6)
                {
                    azimuthAngles.Add(Math.Round(currentAzimuth, 2));
                    currentAzimuth += azimuthInterval;
                }
                azimuthAngles = azimuthAngles.Distinct().OrderBy(a => a).ToList();
                int azimuthSampleCount = azimuthAngles.Count;

                // 兜底：确保至少有36个方位角采样点
                if (azimuthSampleCount < 36)
                {
                    azimuthAngles = Enumerable.Range(0, 36).Select(x => (double)(x * 10)).ToList();
                    azimuthSampleCount = 36;
                }

                logger.Info($"Azimuth Angle Range [0, 360)°, Interval {azimuthInterval:F2}°, a total of {azimuthSampleCount} sampling points generated.");

                // ========== 第2阶段：准备数据矩阵 ==========
                _progressManager.UpdateProgress(50);

                // 为每个通道创建数据矩阵
                Dictionary<ExportDataType, double[,]> channelDataMatrices = new Dictionary<ExportDataType, double[,]>();
                foreach (var channel in selectedChannels)
                {
                    channelDataMatrices[channel] = new double[polarAngles.Count, azimuthSampleCount];
                }

                // 填充数据矩阵
                for (int polarIndex = 0; polarIndex < polarAngles.Count; polarIndex++)
                {
                    int polar = polarAngles[polarIndex];

                    for (int azimuthIndex = 0; azimuthIndex < azimuthSampleCount; azimuthIndex++)
                    {
                        double azimuth = azimuthAngles[azimuthIndex];

                        // 从DLL缓存中获取数据
                        if (DllAllCircleData.TryGetValue((polar, azimuth), out var rgbSample))
                        {
                            foreach (var channel in selectedChannels)
                            {
                                channelDataMatrices[channel][polarIndex, azimuthIndex] =
                                    GetChannelValueFromCircleSample(rgbSample, channel);
                            }
                        }
                        else
                        {
                            // 容错：匹配误差范围内的方位角
                            var matchingKey = DllAllCircleData.Keys
                                .Where(k => k.polar == polar && Math.Abs(k.azimuth - azimuth) < 0.01)
                                .FirstOrDefault();

                            if (!matchingKey.Equals(default) &&
                                DllAllCircleData.TryGetValue(matchingKey, out var matchingSample))
                            {
                                foreach (var channel in selectedChannels)
                                {
                                    channelDataMatrices[channel][polarIndex, azimuthIndex] =
                                        GetChannelValueFromCircleSample(matchingSample, channel);
                                }
                            }
                        }
                    }

                    // 更新进度
                    int progress = 50 + (int)(polarIndex * 30.0 / polarAngles.Count);
                    _progressManager.UpdateProgress(progress);
                }

                // ========== 第3阶段：导出文件 ==========
                int channelCount = selectedChannels.Count;
                for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
                {
                    var channel = selectedChannels[channelIndex];
                    double[,] dataMatrix = channelDataMatrices[channel];

                    int channelStartProgress = 80 + (int)(channelIndex * 15.0 / channelCount);
                    _progressManager.UpdateProgress(channelStartProgress);

                    string csvFileName = $"{basePath}_{channel}.csv";
                    string fullCsvPath = Path.Combine(Path.GetDirectoryName(csvFileName) ?? "", Path.GetFileName(csvFileName));

                    await Task.Run(() =>
                    {
                        using (var writer = new StreamWriter(fullCsvPath, false, Encoding.UTF8))
                        {
                            // 写入表头
                            writer.WriteLine($"Measurement Date,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,");
                            writer.WriteLine($"Instrument,VAM R-Circle（[{polarStart}°~{polarEnd}°],,,,,,,,,,,,");
                            writer.WriteLine($"AngleStep,{polarStep}°,,,,,,,,,,,,");
                            writer.WriteLine($"AzimuthInterval, {azimuthInterval:F2}°,,,,,,,,,,,,");
                            writer.WriteLine();

                            // 写入列标题
                            writer.Write(" ");
                            foreach (double azimuth in azimuthAngles)
                            {
                                writer.Write($",{azimuth:F2}°");
                            }
                            writer.WriteLine();

                            // 写入数据行
                            for (int polarIndex = 0; polarIndex < polarAngles.Count; polarIndex++)
                            {
                                int polar = polarAngles[polarIndex];
                                writer.Write($"{polar}°");

                                for (int azimuthIndex = 0; azimuthIndex < azimuthSampleCount; azimuthIndex++)
                                {
                                    writer.Write($",{dataMatrix[polarIndex, azimuthIndex]:F5}");
                                }

                                writer.WriteLine();

                                // 每处理5个圆环更新一次进度
                                if (polarIndex % 2 == 0)
                                {
                                    int progress = channelStartProgress +
                                        (int)((polarIndex + 1) * 15.0 / polarAngles.Count / channelCount);
                                    _progressManager.UpdateProgress(Math.Min(progress, 95));
                                }
                            }
                        }

                        logger.Info($"Channel {channel} {FindResource("Exportcompleted")}: {fullCsvPath}");
                    });
                }

                _progressManager.UpdateProgress(100);
            }
            catch (Exception ex)
            {
                logger.Error("ExportCircleModeAsync e xecution failed", ex);
                throw;
            }
        }

        //private void BtnExportCircle_Click(object sender, RoutedEventArgs e)
        //{
        //    // 1. 从界面获取核心参数（极角范围/步长、方位角间隔）
        //    int polarStart = CirclePolarStart;
        //    int polarEnd = CirclePolarEnd;
        //    int polarStep = CirclePolarStep;
        //    double azimuthInterval = _azimuthInterval; // 方位角间隔（从txtAzimuthInterval获取）
        //                                               // 容错处理：确保方位角间隔有效
        //    if (azimuthInterval <= 0 || azimuthInterval > 360)
        //    {
        //        azimuthInterval = 3; // 默认3°间隔
        //        logger.Warn($"{FindResource("VAM.Switchedtoazimuthvalue")}：{azimuthInterval}°");
        //        MessageBox.Show($"{FindResource("VAM.Switchedtoazimuthvalue")} {azimuthInterval}°", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //        return;
        //    }
        //    if (!int.TryParse(txtCirclePolarEnd.Text.Trim(), out int end) || end < -60 || end > 60)
        //    {
        //        MessageBox.Show($"{FindResource("VAM.Effectiveendvalue")}", $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
        //        logger.Error($"{FindResource("VAM.Effectiveendvalue")}");
        //        return;
        //    }
        //    if (polarStart > polarEnd)
        //    {
        //        MessageBox.Show("The end value of the polar angle cannot be less than the starting value ", $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //        logger.Error("The end value of the polar angle cannot be less than the starting value");
        //        return;
        //    }
        //    try
        //    {
        //        if (YMat == null || YMat.Empty())
        //        {
        //            MessageBox.Show($"{FindResource("Nodata")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //            return;
        //        }
        //        // 1. 校验通道
        //        var selectedChannels = GetCircleSelectedChannels();
        //        if (selectedChannels.Count == 0)
        //        {
        //            MessageBox.Show($"{FindResource("VAM.Onechannel")}", $"{FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Warning);
        //            return;
        //        }

        //        // 2. 选择保存路径（保留弹窗）
        //        var saveFileDialog = new SaveFileDialog
        //        {
        //            Filter = "CSV Files (*.csv)|*.csv",
        //            FileName = $"VAM_Polar_Angle_{DateTime.Now:yyyyMMdd_HHmmss}",
        //            Title = $"{FindResource("VAM.SavePolar")}"
        //        };
        //        if (saveFileDialog.ShowDialog() != true) return;
        //        string basePath = Path.ChangeExtension(saveFileDialog.FileName, null);

        //        // 3. 执行原VamExportDialog的圆环模式导出逻辑
        //        ExportCircleMode(selectedChannels, basePath);

        //        MessageBox.Show($"{FindResource("Exportsuccessful")}", $"{FindResource("Log.Success")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"{FindResource("Exportfailed")}：{ex.Message}", $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}

        //private void ExportCircleMode(List<ExportDataType> selectedChannels, string basePath)
        //{
        //    // 1. 从界面获取核心参数（极角范围/步长、方位角间隔）
        //    int polarStart = CirclePolarStart;
        //    int polarEnd = CirclePolarEnd;
        //    int polarStep = CirclePolarStep;
        //    double azimuthInterval = _azimuthInterval; // 方位角间隔（从txtAzimuthInterval获取）

        //    //// 容错处理：确保方位角间隔有效
        //    //if (azimuthInterval <= 0 || azimuthInterval > 360)
        //    //{
        //    //    azimuthInterval = 3; // 默认3°间隔
        //    //    logger.Warn($"{FindResource("VAM.Switchedtoazimuthvalue")}：{azimuthInterval}°");
        //    //    MessageBox.Show($"{FindResource("VAM.Switchedtoazimuthvalue")} {azimuthInterval}°", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        //    //    return;
        //    //}
        //    //if (!int.TryParse(txtCirclePolarEnd.Text.Trim(), out int end) || end < -60 || end > 60)
        //    //{
        //    //    MessageBox.Show($"{FindResource("VAM.Effectiveendvalue")}", $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
        //    //    logger.Error($"{FindResource("VAM.Effectiveendvalue")}");
        //    //    return;
        //    //}
        //    //if (polarStart < polarEnd)
        //    //{
        //    //    MessageBox.Show($"{polarStart < polarEnd} ", $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Information);
        //    //    logger.Error($"{polarStart < polarEnd}");
        //    //    return;
        //    //}

        //    // 2. 动态生成极角采样点（圆环半径角度）
        //    List<int> polarAngles = new List<int>();
        //    for (int p = polarStart; p <= polarEnd; p += polarStep)
        //    {
        //        polarAngles.Add(p);
        //    }
        //    // 确保至少有一个极角采样点
        //    if (polarAngles.Count == 0) polarAngles.Add(polarStart);
        //    logger.Info($"The polar angle range is [{polarStart}, {polarEnd}] degrees, with a step size of {polarStep} degrees, resulting in a total of {polarAngles.Count} rings being generated.");

        //    // 3. 动态生成方位角采样点（按界面间隔角度，覆盖 [0, 360°)）
        //    List<double> azimuthAngles = new List<double>();
        //    double currentAzimuth = 0;
        //    while (currentAzimuth < 360 - 1e-6) // 加微小偏移，避免因浮点精度生成360°
        //    {
        //        azimuthAngles.Add(Math.Round(currentAzimuth, 2));
        //        currentAzimuth += azimuthInterval;
        //    }
        //    // 去重+排序：确保采样点无重复、有序排列
        //    azimuthAngles = azimuthAngles.Distinct().OrderBy(a => a).ToList();
        //    int azimuthSampleCount = azimuthAngles.Count;
        //    // 兜底：确保至少有36个方位角采样点（10°间隔）
        //    if (azimuthSampleCount < 36)
        //    {
        //        azimuthAngles = Enumerable.Range(0, 36).Select(x => (double)(x * 10)).ToList();
        //        azimuthSampleCount = 36;
        //    }
        //    logger.Info($"Azimuth Angle Range [0, 360)°, Interval {azimuthInterval:F2}°, a total of {azimuthSampleCount} sampling points generated.");

        //    // 4. 调用DLL批量获取全量圆环数据（传入动态计算的采样点数量）
        //    bool dllSuccess = this.CallVamDllForAllCircle(
        //        polarStart: polarStart,
        //        polarEnd: polarEnd,
        //        polarStep: polarStep,
        //        azimuthSampleCount: azimuthSampleCount
        //    );

        //    if (!dllSuccess || DllAllCircleData == null || DllAllCircleData.Count == 0)
        //    {

        //        throw new Exception($"{FindResource("VAM.DLLcallfailed")}");
        //    }

        //    // 5. 为每个选中通道生成独立CSV文件
        //    foreach (var channel in selectedChannels)
        //    {
        //        // 构建最终文件路径
        //        string csvFileName = $"{basePath}_{channel}.csv";
        //        string fullCsvPath = Path.Combine(Path.GetDirectoryName(csvFileName) ?? "", Path.GetFileName(csvFileName));

        //        using (var writer = new StreamWriter(fullCsvPath, false, Encoding.UTF8))
        //        {
        //            // 5.1 写入标准化表头（包含间隔角度信息）
        //            writer.WriteLine($"Measurement Date,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,");
        //            writer.WriteLine($"Instrument,VAM R-Circle（[{polarStart}°~{polarEnd}°],,,,,,,,,,,,");
        //            writer.WriteLine($"AngleStep,{polarStep}°,,,,,,,,,,,,");///*{FindResource("VAM.AngleStep")}*/ 
        //            writer.WriteLine($"AzimuthInterval, {azimuthInterval:F2}°,,,,,,,,,,,,");//{FindResource("VAM.Azimuthinterval")}
        //            writer.WriteLine(); // 空行分隔

        //            StringBuilder headerBuilder = new StringBuilder();
        //            headerBuilder.Append(" "); // 第一列：半径角度（极角）
        //            foreach (double azimuth in azimuthAngles)
        //            {
        //                headerBuilder.Append($",{azimuth:F2}°"); // 方位角列（保留2位小数）
        //            }
        //            writer.WriteLine(headerBuilder.ToString());

        //            // 5.2 写入数据行（按动态生成的极角/方位角采样点填充）
        //            foreach (int polar in polarAngles)
        //            {
        //                StringBuilder dataBuilder = new StringBuilder();
        //                dataBuilder.Append($"{polar}°"); // 极角（整数，简洁展示）

        //                // 遍历每个方位角，填充对应数据
        //                foreach (double azimuth in azimuthAngles)
        //                {
        //                    double channelValue = 0.0;
        //                    // 从DLL缓存中获取对应（极角+方位角）的采样点
        //                    if (DllAllCircleData.TryGetValue((polar, azimuth), out var rgbSample))
        //                    {
        //                        channelValue = GetChannelValueFromCircleSample(rgbSample, channel);
        //                    }
        //                    else
        //                    {
        //                        // 容错：匹配误差范围内的方位角（适配浮点精度）
        //                        var matchingKey = DllAllCircleData.Keys
        //                            .Where(k => k.polar == polar && Math.Abs(k.azimuth - azimuth) < 0.01)
        //                            .FirstOrDefault();

        //                        if (DllAllCircleData.TryGetValue(matchingKey, out var matchingSample))
        //                        {
        //                            channelValue = GetChannelValueFromCircleSample(matchingSample, channel);
        //                        }
        //                    }

        //                    // 写入通道值（保留5位小数，满足高精度测量需求）
        //                    dataBuilder.Append($",{channelValue:F5}");
        //                }

        //                writer.WriteLine(dataBuilder.ToString());
        //            }
        //        }

        //        logger.Info($"Ring Channel {channel} has been exported successfully. Export path: {fullCsvPath}");
        //    }
        //}
        // 从圆环采样点中获取指定通道的值（需确保RgbSample类已定义）
        private double GetChannelValueFromCircleSample(RgbSample sample, ExportDataType channel)
        {
            if (sample == null)
                return 0;

            // 根据通道类型返回对应值
            return channel switch
            {
                ExportDataType.X => sample.X,
                ExportDataType.Y => sample.Y,
                ExportDataType.Z => sample.Z,
                // 若圆环数据不包含cieX/cieY，返回0
                ExportDataType.CieX => 0,
                ExportDataType.CieY => 0,
                _ => 0
            };
        }
        /// <summary>
        /// 圆环模式采样点数量（从界面输入推导）
        /// </summary>
        private int circleSampleCount
        {
            get
            {
                // 从方位角间隔计算采样点数量（360° / 间隔角度）
                if (!double.TryParse(txtAzimuthInterval.Text.Trim(), out double azimuthInterval) || azimuthInterval <= 0)
                {
                    return 360; // 默认360个采样点（1°间隔）
                }
                int count = (int)Math.Round(360 / azimuthInterval);
                return count < 1 ? 360 : count;
            }
        }

        /// <summary>
        /// 直径线模式：极角范围（从界面txtLinePolarRHO获取）
        /// </summary>
        private int LinePolarRHO
        {
            get
            {
                // 检查是否在UI线程
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => LinePolarRHO);
                }

                if (int.TryParse(txtLinePolarRHO.Text.Trim(), out int rho))
                {
                    return rho;
                }
                return 60;
            }
        }

        /// <summary>
        /// 圆环模式：极角起始值（从界面txtCirclePolarStart获取）
        /// </summary>
        private int CirclePolarStart
        {
            get
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => CirclePolarStart);
                }

                if (!int.TryParse(txtCirclePolarStart.Text.Trim(), out int start) || start < -60 || start > 60)
                {
                    return 0;
                }
                return start;
            }
        }

        /// <summary>
        /// 圆环模式：极角结束值（从界面txtCirclePolarEnd获取）
        /// </summary>
        private int CirclePolarEnd
        {
            get
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => CirclePolarEnd);
                }

                if (!int.TryParse(txtCirclePolarEnd.Text.Trim(), out int End) || End < CirclePolarStart || End > 60)
                {
                    return 0;
                }
                return End;
            }
        }

        /// <summary>
        /// 圆环模式：极角步长（从界面txtCirclePolarStep获取）
        /// </summary>
        private int CirclePolarStep
        {
            get
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => CirclePolarStep);
                }

                if (!int.TryParse(txtCirclePolarStep.Text.Trim(), out int step) || step <= 0)
                {
                    return 10;
                }
                return step;
            }
        }
        #endregion
        /// <summary>
        /// 读取全局配置中的VAM导出路径
        /// </summary>
        private string GetVamGlobalExportPath()
        {
            try
            {
                // 配置文件路径（与GlobalConfigWindow保持一致）
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string configDir = Path.Combine(appDir, "Config");
                string configPath = Path.Combine(configDir, "GlobalConfig.json");

                if (File.Exists(configPath))
                {
                    string configContent = File.ReadAllText(configPath);
                    var globalConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<GlobalConfigModel>(configContent);
                    if (globalConfig != null && !string.IsNullOrWhiteSpace(globalConfig.VamExportPath))
                    {
                        // 确保文件夹存在
                        if (!Directory.Exists(globalConfig.VamExportPath))
                        {
                            Directory.CreateDirectory(globalConfig.VamExportPath);
                        }
                        return globalConfig.VamExportPath;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Failed to read the VAM global export path", ex);
            }

            // 兜底使用默认路径
            string defaultPath = @"D:\Project\VAM";
            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }
            return defaultPath;
        }
        private ExportProgressManager _progressManager;
        private void InitializeProgressBar()
        {
            // 确保在UI线程初始化
            if (progressBarContainer == null)
            {
                progressBarContainer = this.FindName("progressBarContainer") as Border;
            }

            if (exportProgressBar == null)
            {
                exportProgressBar = this.FindName("exportProgressBar") as ProgressBar;
            }

            if (exportProgressText == null)
            {
                exportProgressText = this.FindName("exportProgressText") as TextBlock;
            }

            // 初始化进度管理器
            if (exportProgressBar != null && exportProgressText != null && progressBarContainer != null)
            {
                _progressManager = new ExportProgressManager(exportProgressBar, exportProgressText, progressBarContainer);
            }
        }
        
    }

    internal class CropCenter
    {
        public double x { get; set; }
        public double y { get; set; }
    }

    class ExportProgressManager
    {
        private readonly ProgressBar _progressBar;
        private readonly TextBlock _progressText;
        private readonly Border _container;
        private readonly Dispatcher _dispatcher;

        public ExportProgressManager(ProgressBar progressBar, TextBlock progressText, Border container)
        {
            _progressBar = progressBar;
            _progressText = progressText;
            _container = container;
            _dispatcher = Application.Current.Dispatcher; // 获取主线程Dispatcher
        }

        public void Start()
        {
            // 确保在UI线程执行
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(() => Start());
                return;
            }

            _progressBar.Value = 0;
            _progressText.Text = "0%";
            _progressBar.Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // #FF007ACC
            _progressText.Foreground = Brushes.White;
            _container.Visibility = Visibility.Visible;
        }

        public void UpdateProgress(int progress)
        {
            // 确保在UI线程执行
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(() => UpdateProgress(progress));
                return;
            }

            progress = Math.Clamp(progress, 0, 100);
            _progressBar.Value = progress;
            _progressText.Text = $"{progress}%";
        }

        public void Complete()
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(() => Complete());
                return;
            }

            _progressBar.Value = 100;
            _progressText.Text = (string)Application.Current.FindResource("Exportcompleted");
            _progressText.Foreground = Brushes.LightGreen;

            // 延迟隐藏进度条
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                _container.Visibility = Visibility.Collapsed;
                Reset();
            };
            timer.Start();
        }

        public void Fail()
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(() => Fail());
                return;
            }

            _progressBar.Foreground = Brushes.Red;
            _progressBar.Value = 100;
            _progressText.Text = (string)Application.Current.FindResource("Exportfailed");
            _progressText.Foreground = Brushes.Red;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                _container.Visibility = Visibility.Collapsed;
                Reset();
            };
            timer.Start();
        }

        private void Reset()
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(() => Reset());
                return;
            }

            _progressBar.Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // #FF007ACC
            _progressBar.Value = 0;
            _progressText.Text = "0%";
            _progressText.Foreground = Brushes.White;
        }
    }
}
