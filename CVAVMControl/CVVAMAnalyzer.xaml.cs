using ColorVision.FileIO;
using ConoscopeDemo;
using CVWaferProber.Core.Events;
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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WaferComm.Core;


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
        private double ConoscopeCoefficient = 0.02645; // Pixels per degree

        private int displayAngle = 120; // Default display angle
        private ExportChannel displayChannel = ExportChannel.Y; // Default display channel
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
            IntPtr handle,
            int w,
            int h,
            int bpp,
            int channels,
            byte[] data,          // 替换IntPtr为byte[]（BGR图片数据）
            byte[] xyz,           // 替换IntPtr为byte[]（XYZ数据）
            string staticJson,    // 移除MarshalAs，StdCall默认适配
            StringBuilder result, // 移除MarshalAs
            ref int resultLength,
            ref int dstBpp,
            ref int dstChannel,
            byte[] dstData        // 替换IntPtr为byte[]（输出图像数据）
        );

        // 4. 新增封装调用方法（适配ImageData入参）
        private CV_AliResType CallCV_Ali_calcVam(ImageData i, ImageData xyz, string paramJson, out string result, out ImageData showImage)
        {
            // 初始化输出图像
            showImage = new ImageData
            {
                _w = i._w,
                _h = i._h,
                _bpp = 16,
                _channels = 3,
                data = new byte[i._w * i._h * 3 * (16 / 8)] // 16位3通道初始化
            };

            // 初始化结果缓冲区
            int resultLength = 2048000; // 2MB缓冲区
            StringBuilder bf = new StringBuilder(resultLength);
            CV_AliResType res = CV_Ali_calcVam(
                IntPtr.Zero,
                xyz._w,
                xyz._h,
                i._bpp,
                xyz._channels,
                i.data,       // BGR图片数据（无则传null）
                xyz.data,     // XYZ数据
                paramJson,    // 静态参数JSON
                bf,
                ref resultLength,
                ref showImage._bpp,
                ref showImage._channels,
                showImage.data
            );

            // 处理缓冲区长度不足的情况
            if (res == CV_AliResType.ERR_LENGTH)
            {
                bf = new StringBuilder(resultLength);
                res = CV_Ali_calcVam(
                    IntPtr.Zero,
                    xyz._w,
                    xyz._h,
                    i._bpp,
                    xyz._channels,
                    i.data,
                    xyz.data,
                    paramJson,
                    bf,
                    ref resultLength,
                    ref showImage._bpp,
                    ref showImage._channels,
                    showImage.data
                );
            }

            result = bf.ToString();
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

        private class VamSamplePoint
        {
            public double X { get; set; }       // 三刺激值X
            public double Y { get; set; }       // 三刺激值Y（亮度值，图表用）
            public double Z { get; set; }       // 三刺激值Z
            public double cie_x { get; set; }   // CIE坐标x（可选）
            public double cie_y { get; set; }   // CIE坐标y（可选）
            public double position { get; set; } // 角度位置（对应图表X轴）
        }
        public CVVAMAnalyzer()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            InitializeComponent();
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

            //this.Unloaded += CVVAMAnalyzer_Unloaded;
        }

        private void InitializeEvents(IEventAggregator? eventAggregator = null)
        {
            this.EventAggregator = eventAggregator == null ? CVWPEventAggregatorInstance.Instance : eventAggregator;
            this.EventAggregator.Subscribe<VAMFlowCompletedEvent>(OnFlowCompleted);
            //this.EventAggregator.Subscribe<VAMFlowStartingEvent>(OnFlowStarting);
            this.EventAggregator.Subscribe<VAMResultGUIClearEvent>(OnResultGUIClear);
            //订阅自动导出CSV事件
            this.EventAggregator.Subscribe<VAMAutoExportCsvEvent>(OnAutoExportCsv);
        }

        private void UnInitializeEvents()
        {
            this.EventAggregator?.Unsubscribe<VAMFlowCompletedEvent>(OnFlowCompleted);
            //this.EventAggregator?.Unsubscribe<VAMFlowStartingEvent>(OnFlowStarting);
            this.EventAggregator?.Unsubscribe<VAMResultGUIClearEvent>(OnResultGUIClear);
            this.EventAggregator?.Unsubscribe<VAMAutoExportCsvEvent>(OnAutoExportCsv);
        }
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 1. 基础校验
                    if (!_isDataValid || !IsMatSafe(YMat) || dataXyz == null)
                    {
                        logger.Warn("VAM数据未加载，自动导出失败");
                        return;
                    }

                    // 2. 导出路径（桌面+时间戳）
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fileName = $"VAM_MatrixExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    string exportPath = System.IO.Path.Combine(desktopPath, fileName);

                    // 3. 采集图二格式的数据（角度行 + 多采样点列）
                    List<VamMatrixExportModel> matrixData = GetVamMatrixData();
                    if (matrixData.Count == 0)
                    {
                        logger.Warn("无有效数据可导出");
                        return;
                    }

                    // 4. 生成图二格式的CSV
                    using (var writer = new StreamWriter(exportPath, false, Encoding.UTF8))
                    {
                        // 4.1 写入表头（第1-2行）
                        writer.WriteLine($"Measurement Date,,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,"); // 第1行
                        writer.WriteLine($"Instrument,,VAM 60°,,,,,,,,,,,,"); // 第2行
                        writer.WriteLine(); // 第3行（空行）

                        // 4.2 写入采样点序号行（第4行：C列开始是0、1、2…）
                        int maxSampleCount = matrixData.Max(m => m.AllSampleValues.Count);
                        string sampleHeader = $",,{string.Join(",", Enumerable.Range(0, maxSampleCount))}";
                        writer.WriteLine(sampleHeader);

                        // 4.3 写入数据行（B列是角度，C~N列是该角度的所有采样点值）
                        foreach (var data in matrixData)
                        {
                            // 格式：空列 + 角度 + 该角度的所有采样点值（横向排列）
                            string valuesStr = string.Join(",", data.AllSampleValues.Select(v => v.ToString("F5")));
                            string line = $",{data.Angle},{valuesStr}";
                            writer.WriteLine(line);
                        }
                    }

                    logger.Info($"VAM图二格式CSV导出成功！路径：{exportPath}");
                    MessageBox.Show($"CSV已自动导出至：\n{exportPath}", "导出成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    logger.Error("VAM图二格式导出失败", ex);
                    MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
        /// <summary>
        /// 自动导出CSV事件处理（测试完成后触发）
        /// </summary>
        private void OnAutoExportCsv(VAMAutoExportCsvEvent @event)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 1. 基础校验
                    if (!_isDataValid || !IsMatSafe(YMat) || dataXyz == null)
                    {
                        logger.Warn("VAM数据未加载，自动导出失败");
                        return;
                    }

                    // 2. 导出路径（桌面+时间戳）
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fileName = $"VAM_MatrixExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    string exportPath = System.IO.Path.Combine(desktopPath, fileName);

                    // 3. 采集图二格式的数据（角度行 + 多采样点列）
                    List<VamMatrixExportModel> matrixData = GetVamMatrixData();
                    if (matrixData.Count == 0)
                    {
                        logger.Warn("无有效数据可导出");
                        return;
                    }

                    // 4. 生成图二格式的CSV
                    using (var writer = new StreamWriter(exportPath, false, Encoding.UTF8))
                    {
                        // 4.1 写入表头（第1-2行）
                        writer.WriteLine($"Measurement Date,,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,"); // 第1行
                        writer.WriteLine($"Instrument,,VAM 60°,,,,,,,,,,,,"); // 第2行
                        writer.WriteLine(); // 第3行（空行）

                        // 4.2 写入采样点序号行（第4行：C列开始是0、1、2…）
                        int maxSampleCount = matrixData.Max(m => m.AllSampleValues.Count);
                        string sampleHeader = $",,{string.Join(",", Enumerable.Range(0, maxSampleCount))}";
                        writer.WriteLine(sampleHeader);

                        // 4.3 写入数据行（B列是角度，C~N列是该角度的所有采样点值）
                        foreach (var data in matrixData)
                        {
                            // 格式：空列 + 角度 + 该角度的所有采样点值（横向排列）
                            string valuesStr = string.Join(",", data.AllSampleValues.Select(v => v.ToString("F5")));
                            string line = $",{data.Angle},{valuesStr}";
                            writer.WriteLine(line);
                        }
                    }

                    logger.Info($"VAM图二格式CSV导出成功！路径：{exportPath}");
                    MessageBox.Show($"CSV已自动导出至：\n{exportPath}", "导出成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    logger.Error("VAM图二格式导出失败", ex);
                    MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
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
                ProcessCVCIEFile(@event.ResultFileName);
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

        /// <summary>
        /// 处理CVCIE文件
        /// </summary>
        private void ProcessCVCIEFile(string filename)
        {
            try
            {
                XMat?.Dispose();
                YMat?.Dispose();
                ZMat?.Dispose();

                CVCIEFile fileInfo = new CVCIEFile();
                CVFileUtil.Read(filename, out fileInfo);

                int channelSize = fileInfo.Cols * fileInfo.Rows * (fileInfo.Bpp / 8);
                int allPixLen = fileInfo.Cols * fileInfo.Rows * (fileInfo.Bpp / 8) * fileInfo.Channels;
                OpenCvSharp.MatType singleChannelType;
                switch (fileInfo.Bpp)
                {
                    case 8: singleChannelType = OpenCvSharp.MatType.CV_8UC1; break;
                    case 16: singleChannelType = OpenCvSharp.MatType.CV_16UC1; break;
                    case 32: singleChannelType = OpenCvSharp.MatType.CV_32FC1; break; // Most likely for XYZ
                    case 64: singleChannelType = OpenCvSharp.MatType.CV_64FC1; break;
                    default: throw new NotSupportedException($"Bpp {fileInfo.Bpp} not supported");
                }
                if (fileInfo.Channels == 3)
                {
                    byte[] dataX = new byte[channelSize];
                    byte[] dataY = new byte[channelSize];
                    byte[] dataZ = new byte[channelSize];

                    Buffer.BlockCopy(fileInfo.Data, 0, dataX, 0, channelSize);
                    Buffer.BlockCopy(fileInfo.Data, channelSize, dataY, 0, channelSize);
                    Buffer.BlockCopy(fileInfo.Data, channelSize * 2, dataZ, 0, channelSize);
                    //dataXyz
                    if (dataXyz == null || dataXyz.Length != allPixLen)
                    {
                        dataXyz = new byte[allPixLen];
                    }
                    Buffer.BlockCopy(fileInfo.Data, 0, dataXyz, 0, allPixLen);
                    XMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, singleChannelType, dataX);
                    YMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, singleChannelType, dataY);
                    ZMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, singleChannelType, dataZ);
                }
                center = new System.Windows.Point(YMat.Width / 2.0, YMat.Height / 2.0);
                imageRadius = (int)(MaxAngle / ConoscopeCoefficient);

                // 初始化：默认选中第一个角度
                if (cbDisplayAngle.Items.Count > 0 && cbDisplayAngle.Items[0] is ComboBoxItem firstItem)
                {
                    cbDisplayAngle.SelectedItem = firstItem;
                    if (int.TryParse(firstItem.Tag?.ToString(), out int firstAngle))
                    {
                        _selectedAngle = firstAngle;
                    }
                }
                UpdateDisplay();

                fileInfo.Dispose();
                _isDataValid = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"处理文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                MessageBox.Show("输入的角度不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

            }

            // 1. 输入校验（兼容整数/负数）
            if (!int.TryParse(inputAngleText.Trim(), out int newAngle))
            {
                MessageBox.Show("请输入有效的整数角度", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. 区分模式校验角度范围
            bool isRCircle = IsRCircleMode();
            if (isRCircle)
            {
                // R圆模式：-60° ~ 60°
                if (newAngle < -60 || newAngle > 60)
                {
                    MessageBox.Show("角度范围应为-60°~60°", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            else
            {
                // 直径线模式：0° ~ 360°（保留原规则）
                if (newAngle < 0 || newAngle > 360)
                {
                    MessageBox.Show("角度范围应为0°~360°", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            // 3. 检查是否已存在
            if (IsAngleExistsInComboBox(targetComboBox, newAngle))
            {
                MessageBox.Show("角度已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show("请先从下拉框中选择要删除的角度", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!(targetComboBox.SelectedItem is ComboBoxItem selectedItem) ||
                    !int.TryParse(selectedItem.Tag?.ToString(), out int delAngle))
                {
                    MessageBox.Show("选中的角度项无效，请重新选择", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                bool isRCircle = IsRCircleMode();
                string modeName = isRCircle ? "R圆" : "直径线";

                MessageBoxResult result = MessageBox.Show(
                    $"确认删除{modeName}模式下 {delAngle}° 的角度吗？",
                    "删除确认",
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
                    MessageBox.Show($"{modeName}下拉框已空，自动填充默认角度：{defaultAngle}°", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // ========== 关键修复3：强制刷新显示（立即重绘画布） ==========
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, () =>
                {
                    if (IsMatSafe(YMat))
                    {
                        UpdateDisplay();
                    }
                });

                logger.Info($"[{modeName}] 成功删除角度：{delAngle}°，当前剩余角度数：{targetComboBox.Items.Count}");
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
                    DrawAngleLabel(colorMat, labelPos, $"{angle}(A)", yellowColor, fontScale: 7);
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
                    DrawAngleLabel(colorMat, labelPos, $"{_selectedAngle}(A)", purpleColor, fontScale: 7);
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

                    // 绘制角度标签（保留原有逻辑）
                    OpenCvSharp.Point labelPos = new OpenCvSharp.Point((int)(centerPoint.X + radiusPixel + 20), (int)centerPoint.Y);
                    if (labelPos.X > colorMat.Width - 100)
                    {
                        labelPos.X = (int)(centerPoint.X - radiusPixel - 100);
                    }
                    DrawAngleLabel(colorMat, labelPos, $"{radius}(R)", yellowColor, fontScale: 7);
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

                    OpenCvSharp.Point labelPos = new OpenCvSharp.Point((int)(centerPoint.X + radiusPixel + 20), (int)centerPoint.Y);
                    if (labelPos.X > colorMat.Width - 100)
                    {
                        labelPos.X = (int)(centerPoint.X - radiusPixel - 100);
                    }
                    DrawAngleLabel(colorMat, labelPos, $"{_selectedRadius}(R)", purpleColor, fontScale: 7);
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
                return;

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
        string CA = (string)Application.Current.FindResource("VAM.CircumferentialAngle");
        string Pixel = (string)Application.Current.FindResource("VAM.PixelValue");
        private void PlotRCircleChart()
        {
            var circleLine = CreateRCircleLine(displayRadius);

            wpfPlotRCircle.Plot.Clear();

            if (circleLine.RgbData.Count == 0)
            {
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
        private ConcentricCircleLine CreateRCircleLine(double radiusAngle)
        {
            ConcentricCircleLine circleLine = new ConcentricCircleLine
            {
                RadiusAngle = radiusAngle
            };

            if (radiusAngle == 0)
            {
                // Center point: Use the center pixel value for all 360 samples
                int ix = Math.Max(0, Math.Min(YMat.Width - 1, (int)Math.Round(center.X)));
                int iy = Math.Max(0, Math.Min(YMat.Height - 1, (int)Math.Round(center.Y)));

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
            wpfPlotDiameterLine.Plot.Clear();
            wpfPlotRCircle.Plot.Clear();
            wpfPlotDiameterLine.Refresh();
            wpfPlotRCircle.Refresh();
            imgDisplay.Source = null;
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

            if (XMat != null)
                X = XMat.At<float>(iy, ix);
            if (YMat != null)
                Y = YMat.At<float>(iy, ix);
            if (ZMat != null)
                Z = ZMat.At<float>(iy, ix);
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


        private void BtnExportDiameter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (YMat == null || YMat.Empty())
                {
                    MessageBox.Show("没有可导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"DiameterLine_Export_{displayChannel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "保存直径线数据"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportAngleModeToCSV(saveFileDialog.FileName, displayChannel);
                    MessageBox.Show($"直径线数据导出成功！\n已导出0°-180°所有角度数据", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void ExportAngleModeToCSV(string filePath, ExportChannel channel)
        {
            Mat? selectedMat = GetSelectedChannelMat(channel);
            if (selectedMat == null || selectedMat.Empty())
                return;

            // Create angle lines from 0° to 180°
            var angleLines = CreateAngleLinesForExport(selectedMat);

            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                if (angleLines.Count == 0)
                    return;

                // Write CSV header: Phi \ Theta, followed by each Phi angle (0-180)
                StringBuilder headerLine = new StringBuilder();
                headerLine.Append("Phi \\ Theta");
                foreach (var line in angleLines)
                {
                    headerLine.Append($",{line.Angle:F0}");
                }
                writer.WriteLine(headerLine.ToString());

                // Find the maximum number of samples across all lines
                int maxSamples = angleLines.Max(l => l.RgbData.Count);
                if (maxSamples == 0) return;

                // Export each row (Theta position from 0 to MaxAngle)
                for (int i = 0; i < maxSamples; i++)
                {
                    StringBuilder dataLine = new StringBuilder();

                    // Get Theta position from first line
                    double theta = angleLines[0].RgbData.Count > i ? angleLines[0].RgbData[i].Position : 0;
                    dataLine.Append($"{theta:F2}");

                    // Add value for each Phi angle
                    foreach (var line in angleLines)
                    {
                        if (line.RgbData.Count > i)
                        {
                            double value = GetChannelValue(line.RgbData[i], channel);
                            dataLine.Append($",{value:F2}");
                        }
                        else
                        {
                            dataLine.Append(",");
                        }
                    }
                    writer.WriteLine(dataLine.ToString());
                }
            }
        }

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
            for (int theta = 0; theta <= (int)MaxAngle; theta++)
            {
                double radiusPixels = theta / ConoscopeCoefficient;
                double x = center.X + radiusPixels * Math.Cos(radians);
                double y = center.Y + radiusPixels * Math.Sin(radians);

                int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                double X = 0, Y = 0, Z = 0;
                ExtractPixelValues(ix, iy, out X, out Y, out Z);

                polarLine.RgbData.Add(new RgbSample
                {
                    Position = theta,
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
        private void BtnExportCircle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (YMat == null || YMat.Empty())
                {
                    MessageBox.Show("没有可导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"RCircle_Export_{displayChannel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "保存R圆数据"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportCircleModeToCSV(saveFileDialog.FileName, displayChannel);
                    MessageBox.Show("R圆数据导出成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void ExportCircleModeToCSV(string filePath, ExportChannel channel)
        {
            Mat? selectedMat = GetSelectedChannelMat(channel);
            if (selectedMat == null || selectedMat.Empty())
                return;

            // Create concentric circles data
            var concentricCircles = CreateConcentricCirclesData(selectedMat);

            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                if (concentricCircles.Count == 0)
                    return;

                // Write CSV header
                StringBuilder headerLine = new StringBuilder();
                headerLine.Append("Phi \\ Theta");
                foreach (var circle in concentricCircles)
                {
                    headerLine.Append($",{circle.RadiusAngle:F0}");
                }
                writer.WriteLine(headerLine.ToString());

                // Export each row (360 positions)
                for (int anglePos = 0; anglePos <= 360; anglePos++)
                {
                    StringBuilder dataLine = new StringBuilder();
                    dataLine.Append($"{anglePos}");

                    // Add value for each radius
                    foreach (var circle in concentricCircles)
                    {
                        if (circle.RgbData.Count > anglePos)
                        {
                            double value = GetChannelValue(circle.RgbData[anglePos], channel);
                            dataLine.Append($",{value:F2}");
                        }
                        else
                        {
                            dataLine.Append(",");
                        }
                    }
                    writer.WriteLine(dataLine.ToString());
                }
            }
        }

        /// <summary>
        /// 创建同心圆数据（参考原始代码）
        /// </summary>
        private List<ConcentricCircleLine> CreateConcentricCirclesData(Mat mat)
        {
            var concentricCircles = new List<ConcentricCircleLine>();

            // Create circles from 0 to MaxAngle using the same method as plotting
            for (int degree = 0; degree <= (int)MaxAngle; degree++)
            {
                concentricCircles.Add(CreateRCircleLine(degree));
            }

            return concentricCircles;
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
                            MessageBox.Show("VAM接口调用失败，使用本地计算数据", "提示");
                            UpdateDisplay();
                        }
                    }
                    else
                    {
                        MessageBox.Show("数据未加载或已释放，请重新打开CVCIE文件", "提示");
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
                        MessageBox.Show("R圆半径角度需在-60~60之间（支持负数）", "提示");
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
                            MessageBox.Show("VAM接口调用失败，使用本地计算数据", "提示");
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
                    logger.Error("XYZ Mat 为空或已释放");
                    return false;
                }
                if (center.X == 0 && center.Y == 0)
                {
                    logger.Error("图像中心未初始化（未加载CVCIE文件）");
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
                logger.Info($"DLL调用参数：targetAngle={targetAngle}, center=({center.X},{center.Y}), bpp={bpp}, imgSize=({imgWidth}x{imgHeight})");
                logger.Info($"JSON参数：{staticJson}");

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
                    logger.Error($"DLL调用失败，错误码：{callResult}");
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
                    logger.Error("DLL返回空JSON");
                    return false;
                }

                VamResultRoot vamResult = JsonConvert.DeserializeObject<VamResultRoot>(cleanResultJson);
                if (vamResult?.result?.line?.Data == null || vamResult.result.line.Data.Count == 0)
                {
                    logger.Error("DLL返回的直径线数据为空");
                    return false;
                }

                // 步骤12：更新图表
                UpdateDiameterLineChartFromDll(vamResult.result.line.Data);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("调用DLL获取直径线数据异常", ex);
                return false;
            }
        }
        /// <summary>
        /// 调用DLL接口获取R圆数据，更新R圆图表
        /// </summary>
        /// <param name="targetRadius">选中的R圆半径角度</param>
        /// <returns>是否调用成功</returns>
        private bool CallVamDllForRCircle(int targetRadius)
        {
            try
            {
                // 步骤1：基础校验（同直径线）
                if (!IsMatSafe(XMat) || !IsMatSafe(YMat) || !IsMatSafe(ZMat))
                {
                    logger.Error("XYZ Mat 为空或已释放");
                    return false;
                }
                if (center.X == 0 && center.Y == 0)
                {
                    logger.Error("图像中心未初始化（未加载CVCIE文件）");
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
                    _channels = 1,
                    data = dataXyz
                };
                ImageData bgrImageData = new ImageData
                {
                    _w = imgWidth,
                    _h = imgHeight,
                    _bpp = bpp,
                    _channels = 1,
                    data = null
                };

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
                    pointNumCircle = 60, // 还原为60
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
                    logger.Error($"DLL调用失败，错误码：{callResult}");
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
                    logger.Error("DLL返回空JSON");
                    return false;
                }

                VamResultRoot vamResult = JsonConvert.DeserializeObject<VamResultRoot>(cleanResultJson);
                if (vamResult?.result?.circle?.Data == null || vamResult.result.circle.Data.Count == 0)
                {
                    logger.Error("DLL返回的R圆数据为空");
                    return false;
                }

                UpdateRCircleChartFromDll(vamResult.result.circle.Data);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("调用DLL获取R圆数据异常", ex);
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
                MessageBox.Show("VAM数据已失效，请重新加载CVCIE文件", "提示");
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
        private void ImgDisplay_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (imgDisplay.Source == null || imgGrid == null) return;

            // 1. 获取基础尺寸信息
            _imgRenderWidth = imgDisplay.ActualWidth;
            _imgRenderHeight = imgDisplay.ActualHeight;
            if (_imgRenderWidth == 0 || _imgRenderHeight == 0) return;

            // 2. 获取鼠标在imgGrid中的绝对位置（关键：基于Grid而非Image）
            System.Windows.Point mousePosInGrid = e.GetPosition(imgGrid);
            _lastMousePos = mousePosInGrid;

            // 3. 计算缩放前鼠标在图片上的绝对像素坐标
            // 3.1 计算Image控件在imgGrid中的偏移（处理居中对齐）
            double imgOffsetX = (imgGrid.ActualWidth - _imgRenderWidth) / 2;
            double imgOffsetY = (imgGrid.ActualHeight - _imgRenderHeight) / 2;

            // 3.2 计算鼠标在Image控件内的相对位置（去除偏移）
            double mouseXInImage = Math.Max(0, mousePosInGrid.X - imgOffsetX);
            double mouseYInImage = Math.Max(0, mousePosInGrid.Y - imgOffsetY);

            // 3.3 计算鼠标指向的图片原始像素坐标
            double pixelX = (mouseXInImage / _imgRenderWidth) * _imgNaturalWidth;
            double pixelY = (mouseYInImage / _imgRenderHeight) * _imgNaturalHeight;

            // 4. 计算新的缩放比例
            double delta = e.Delta > 0 ? _scaleStep : -_scaleStep;
            double newScale = _currentScale + delta;
            newScale = Math.Clamp(newScale, _minScale, _maxScale);
            if (newScale == _currentScale) return;

            // 5. 核心：计算平移补偿量（保证鼠标位置固定）
            // 5.1 缩放前鼠标位置的屏幕坐标（相对于Image左上角）
            double screenXBefore = (pixelX / _imgNaturalWidth) * _imgRenderWidth * _currentScale;
            double screenYBefore = (pixelY / _imgNaturalHeight) * _imgRenderHeight * _currentScale;

            // 5.2 缩放后鼠标位置的屏幕坐标
            double screenXAfter = (pixelX / _imgNaturalWidth) * _imgRenderWidth * newScale;
            double screenYAfter = (pixelY / _imgNaturalHeight) * _imgRenderHeight * newScale;

            // 5.3 计算需要补偿的平移量（抵消缩放带来的位置变化）
            double deltaX = screenXBefore - screenXAfter;
            double deltaY = screenYBefore - screenYAfter;

            // 6. 更新变换
            // 6.1 先更新缩放
            imgScaleTransform.ScaleX = newScale;
            imgScaleTransform.ScaleY = newScale;

            // 6.2 再更新平移（累加补偿量）
            imgTranslateTransform.X += deltaX;
            imgTranslateTransform.Y += deltaY;

            // 7. 限制平移范围（避免图片完全移出可视区域）
            LimitTranslation();

            // 8. 更新当前缩放比例
            _currentScale = newScale;
        }
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
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // 1. 先校验输入是否为空
            if (string.IsNullOrWhiteSpace(pointNumLineBox.Text))
            {
                MessageBox.Show("请输入数字", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. 尝试转换为整数
            if (!int.TryParse(pointNumLineBox.Text, out int pointNumLine))
            {
                MessageBox.Show("请输入数字", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 3. 校验是否为正整数
            if (pointNumLine <= 0)
            {
                MessageBox.Show("请输入正整数", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // 将合法值赋值给全局变量
            _pointNumLine = pointNumLine;
            
            MessageBox.Show($"采样点数量已设置为：{_pointNumLine}", "设置成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 基础校验逻辑
                if (YMat == null || YMat.Empty())
                {
                    MessageBox.Show("没有可导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. 复用文件保存对话框逻辑
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"VAM_Export_{displayChannel}_{DateTime.Now:yyyyMMdd_HHmmss}_-60to60.csv",
                    Title = "保存直径线数据(-60°~60°)"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // 3. 导出方法（核心：-60°~60°范围）
                    ExportAngleModeToCSV_60To60(saveFileDialog.FileName, displayChannel);
                    MessageBox.Show($"直径线数据导出成功！\n已导出0°-180°方位角，-60°~60°径向角度数据", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <summary>
        /// 导出-60°~60°径向角度范围的CSV（核心修改方法）
        /// </summary>
        private void ExportAngleModeToCSV_60To60(string filePath, ExportChannel channel)
        {
            Mat? selectedMat = GetSelectedChannelMat(channel);
            if (selectedMat == null || selectedMat.Empty())
                return;

            var angleLines = CreateAngleLinesForExport_60To60(selectedMat);

            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                if (angleLines.Count == 0)
                    return;

                // ========== 1. 保留图一的前两行固定表头 ==========
                writer.WriteLine($"Measurement Date,,{DateTime.Now:yyyy/MM/dd HH:mm},,,,,,,,,,,,"); // 第1行
                writer.WriteLine($"Instrument,,VAM 60°,,,,,,,,,,,,"); // 第2行
                writer.WriteLine(); // 第3行（空行）

                // ========== 2. 写入核心表头（补充方位角度标注） ==========
                StringBuilder headerLine = new StringBuilder();
                headerLine.Append(""); // 第一列标题（径向角度）
                foreach (var line in angleLines)
                {
                    // 列标题添加°符号（方位角度标注：0°、1°…180°）
                    headerLine.Append($",{line.Angle:F0}°");
                }
                writer.WriteLine(headerLine.ToString());

                // ========== 3. 写入数据行（径向角度-60°~60°） ==========
                int maxSamples = angleLines.Max(l => l.RgbData.Count);
                if (maxSamples == 0) return;

                for (int i = 0; i < maxSamples; i++)
                {
                    StringBuilder dataLine = new StringBuilder();
                    // 径向角度（-60.00 ~ 60.00）
                    double theta = angleLines[0].RgbData.Count > i ? angleLines[0].RgbData[i].Position : 0;
                    dataLine.Append($"{theta:F2}");

                    // 写入每个方位角度对应的数值
                    foreach (var line in angleLines)
                    {
                        if (line.RgbData.Count > i)
                        {
                            double value = GetChannelValue(line.RgbData[i], channel);
                            dataLine.Append($",{value:F2}");
                        }
                        else
                        {
                            dataLine.Append(",");
                        }
                    }
                    writer.WriteLine(dataLine.ToString());
                }
            }
        }

        /// <summary>
        /// 创建0°~180°方位角数据（径向角度改为-60°~60°）
        /// </summary>
        private List<PolarAngleLine> CreateAngleLinesForExport_60To60(Mat mat)
        {
            var angleLines = new List<PolarAngleLine>();

            // 保持方位角范围0°~180°不变
            for (int phi = 0; phi <= 180; phi++)
            {
                angleLines.Add(ExportVAM_60To60(phi, mat));
            }

            return angleLines;
        }

        /// <summary>
        /// 生成-60°~60°径向角度的直径线数据（核心修改）
        /// </summary>
        private PolarAngleLine ExportVAM_60To60(double angle, Mat mat)
        {
            PolarAngleLine polarLine = new PolarAngleLine
            {
                Angle = angle
            };

            double radians = angle * Math.PI / 180.0;

            // 核心修改：径向角度从-60°循环到60°
            for (int theta = -60; theta <= 60; theta++)
            {
                // 半径像素数取绝对值（距离中心的像素数）
                double radiusPixels = Math.Abs(theta) / ConoscopeCoefficient;
                // 方向控制：theta为负时向角度反方向延伸
                double direction = theta >= 0 ? 1 : -1;

                // 计算像素坐标（适配正负角度）
                double x = center.X + radiusPixels * Math.Cos(radians) * direction;
                double y = center.Y + radiusPixels * Math.Sin(radians) * direction;

                // 边界校验
                int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                // 提取像素值
                double X = 0, Y = 0, Z = 0;
                ExtractPixelValues(ix, iy, out X, out Y, out Z);

                // 保存采样点（Position为-60~60的角度值）
                polarLine.RgbData.Add(new RgbSample
                {
                    Position = theta,
                    X = X,
                    Y = Y,
                    Z = Z
                });
            }

            return polarLine;
        }
    }
}
