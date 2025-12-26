using ColorVision.FileIO;
using ConoscopeDemo;
using log4net;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

namespace CVAVMControl
{
    /// <summary>
    /// CVVAMAnalyzer.xaml 的交互逻辑
    /// </summary>
    public partial class CVVAMAnalyzer : UserControl
    {
        private Mat? XMat;
        private Mat? YMat;
        private Mat? ZMat;
        private Mat? pseudoColorMat;

        private System.Windows.Point center;
        private int imageRadius;
        private double MaxAngle = 80; // Default max angle
        private double ConoscopeCoefficient = 0.02645; // Pixels per degree

        private int displayAngle = 120; // Default display angle
        private ExportChannel displayChannel = ExportChannel.Y; // Default display channel
        private int displayRadius = 40; // Default display radius angle
                                        // CVVAMAnalyzer.cs 中新增定时器
        private DispatcherTimer? _resourceCleanTimer;
        private static readonly ILog log = LogManager.GetLogger(typeof(CVVAMAnalyzer));
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
            //this.Unloaded += CVVAMAnalyzer_Unloaded;
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
        private void DrawAngleLabel(Mat mat, OpenCvSharp.Point pos, string text, Scalar? textColor = null, Scalar? bgColor = null, double fontScale = 0.8, int thickness = 10)
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
        /// 通用添加角度方法（支持任意ComboBox）
        /// </summary>
        /// <param name="targetComboBox">目标下拉框（如cbDisplayAngle/cbDisplayRadius）</param>
        /// <param name="inputAngleText">输入的角度文本</param>
        private void AddAngleToComboBox(ComboBox targetComboBox, string inputAngleText)
        {
            // 1. 输入校验
            if (!int.TryParse(inputAngleText.Trim(), out int newAngle))
            {
                MessageBox.Show("请输入有效的整数角度", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (newAngle < 0 || newAngle > 360)
            {
                MessageBox.Show("角度范围应为0-360°", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. 检查是否已存在
            if (IsAngleExistsInComboBox(targetComboBox, newAngle))
            {
                MessageBox.Show("角度已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 3. 添加到目标ComboBox
            ComboBoxItem newItem = new ComboBoxItem
            {
                Content = $"{newAngle}°",
                Tag = newAngle.ToString()
            };
            targetComboBox.Items.Add(newItem);

            // 4. 自动选中新项
            targetComboBox.SelectedItem = newItem;

            // 5. 刷新显示
            if (IsMatSafe(YMat)) UpdateDisplay();
        }

        /// <summary>
        /// 通用删除角度方法（支持任意ComboBox）
        /// </summary>
        /// <param name="targetComboBox">目标下拉框（如cbDisplayAngle/cbDisplayRadius）</param>
        /// <param name="inputAngleText">输入的角度文本</param>
        private void DeleteAngleFromComboBox(ComboBox targetComboBox, string inputAngleText)
        {
            // 1. 输入校验
            if (!int.TryParse(inputAngleText.Trim(), out int delAngle))
            {
                MessageBox.Show("请输入有效的整数角度", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. 查找目标项
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
                MessageBox.Show("找不到相应数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            targetComboBox.Items.Remove(targetItem);
            // 自动选中第一个项（可选）
            if (targetComboBox.SelectedItem == targetItem && targetComboBox.Items.Count > 0)
            {
                targetComboBox.SelectedIndex = 0;
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
            AddAngleToComboBox(cbDisplayAngle, txtAddAngle.Text);
            txtAddAngle.Text = string.Empty;
        }

        // 直径线面板 - 删除角度
        private void BtnDeleteAngle_Diameter_Click(object sender, RoutedEventArgs e)
        {
            DeleteAngleFromComboBox(cbDisplayAngle, txtDeleteAngle.Text);
            txtDeleteAngle.Text = string.Empty;
        }
        // R圆面板 - 添加角度
        private void BtnAddAngle_RCircle_Click(object sender, RoutedEventArgs e)
        {
            AddAngleToComboBox(cbDisplayRadius, txtAddAngle1.Text);
            txtAddAngle1.Text = string.Empty;
        }

        // R圆面板 - 删除角度
        private void BtnDeleteAngle_RCircle_Click(object sender, RoutedEventArgs e)
        {
            DeleteAngleFromComboBox(cbDisplayRadius, txtDeleteAngle1.Text);
            txtDeleteAngle1.Text = string.Empty;
        }
        #endregion
        private void UpdateDisplay()
        {
            // Get the selected channel
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
            // ========== 画角度显示线 ==========
            // 基础参数
            OpenCvSharp.Point centerPoint = new OpenCvSharp.Point((int)center.X, (int)center.Y);
            float maxRadius = (float)(MaxAngle / ConoscopeCoefficient); // 图像半径

            // 1. 绘制同心圆（黄色细环线）
            Scalar circleColor = new Scalar(0, 255, 255);
            int circleLineWidth = 10;
            int circleIntervalAngle = 10;
            for (int angle = circleIntervalAngle; angle <= MaxAngle; angle += circleIntervalAngle)
            {
                float circleRadius = (float)(angle / ConoscopeCoefficient);
                Cv2.Circle(colorMat, centerPoint, (int)circleRadius, circleColor, circleLineWidth);
            }

           
            // 2. 核心逻辑：根据按钮文本切换绘制的下拉框黄线
            Scalar yellowColor = new Scalar(0, 255, 255); // 基础黄色
            Scalar greenColor = new Scalar(255, 0, 255);   // 选中→紫色
            int yellowLineWidth = 10;
            int selectedLineWidth = 20; // 选中绿线宽（新增15）

            // 获取按钮当前显示的文本（匹配动态资源）
            string currentBtnText = btnSwitchChart.Content.ToString();
            string rCircleTitle = FindResource("Plot.Title.RCircle").ToString();
            string diameterTitle = FindResource("Plot.Title.DiameterLine").ToString();
             if (currentBtnText == rCircleTitle)
            {
                // 按钮显示R圆标题：绘制cbDisplayAngle的所有黄线
                List<double> angleValues = GetAllComboBoxValues(cbDisplayAngle);
                foreach (double angle in angleValues)
                {
                    double radian = angle * Math.PI / 180.0;
                    OpenCvSharp.Point startPoint = new OpenCvSharp.Point(
                        (int)(centerPoint.X - maxRadius * Math.Cos(radian)),
                        (int)(centerPoint.Y - maxRadius * Math.Sin(radian))
                    );
                    OpenCvSharp.Point endPoint = new OpenCvSharp.Point(
                        (int)(centerPoint.X + maxRadius * Math.Cos(radian)),
                        (int)(centerPoint.Y + maxRadius * Math.Sin(radian))
                    );
                    // 关键：判断当前角度是否为选中状态，设置颜色
                    // 关键：同时判断颜色和线宽
                    bool isSelected = angle == _selectedAngle;
                    Scalar lineColor = isSelected ? greenColor : yellowColor;
                    int lineWidth = isSelected ? selectedLineWidth : yellowLineWidth;
                    Cv2.Line(colorMat, startPoint, endPoint, lineColor, lineWidth);
                    // 添加半径角度备注（标注在终点外侧）
                    OpenCvSharp.Point labelPos = new OpenCvSharp.Point(
                        (int)(endPoint.X + 15 * Math.Cos(radian)),
                        (int)(endPoint.Y + 15 * Math.Sin(radian))
                    );
                   
                    DrawAngleLabel(colorMat, labelPos, $"{angle}(R)", new Scalar(0, 255, 255), fontScale: 7);
                }
            }
            else if (currentBtnText == diameterTitle)
            {
                // 按钮显示直径线标题：绘制cbDisplayRadius的所有黄线
                List<double> radiusValues = GetAllComboBoxValues(cbDisplayRadius);
                foreach (double angle in radiusValues)
                {
                    double radian = angle * Math.PI / 180.0;
                    OpenCvSharp.Point startPoint = new OpenCvSharp.Point(
                        (int)(centerPoint.X - maxRadius * Math.Cos(radian)),
                        (int)(centerPoint.Y - maxRadius * Math.Sin(radian))
                    );
                    OpenCvSharp.Point endPoint = new OpenCvSharp.Point(
                        (int)(centerPoint.X + maxRadius * Math.Cos(radian)),
                        (int)(centerPoint.Y + maxRadius * Math.Sin(radian))
                    );
                    // 关键：同时判断颜色和线宽
                    bool isSelected = angle == _selectedRadius;
                    Scalar lineColor = isSelected ? greenColor : yellowColor;
                    int lineWidth = isSelected ? selectedLineWidth : yellowLineWidth;

                    Cv2.Line(colorMat, startPoint, endPoint, lineColor, lineWidth);
                    // 添加角度备注（标注在终点外侧）
                    OpenCvSharp.Point labelPos = new OpenCvSharp.Point(
                        (int)(endPoint.X + 15 * Math.Cos(radian)),
                        (int)(endPoint.Y + 15 * Math.Sin(radian))
                    );
                    DrawAngleLabel(colorMat, labelPos, $"{angle}(A)", new Scalar(0, 255, 255), fontScale: 7);
                }

            
           
            }

            // 3. 红色主角度线（X/Y轴，贯穿整张图）
            Scalar redColor = new Scalar(0, 0, 255);
            int redLineWidth = 12;
            // X轴（水平贯穿：左边缘→右边缘，经过中心点）
            OpenCvSharp.Point xAxisStart = new OpenCvSharp.Point(0, (int)centerPoint.Y);
            OpenCvSharp.Point xAxisEnd = new OpenCvSharp.Point(colorMat.Width, (int)centerPoint.Y);
            Cv2.Line(colorMat, xAxisStart, xAxisEnd, redColor, redLineWidth);
            // Y轴（垂直贯穿：上边缘→下边缘，经过中心点）
            OpenCvSharp.Point yAxisStart = new OpenCvSharp.Point((int)centerPoint.X, 0);
            OpenCvSharp.Point yAxisEnd = new OpenCvSharp.Point((int)centerPoint.X, colorMat.Height);
            Cv2.Line(colorMat, yAxisStart, yAxisEnd, redColor, redLineWidth);
            // 4. 保留参数框
            // ========== 参数框 ==========
            // 原始参数框：宽200，高120 → 放大3倍：宽600，高360
            //int boxWidth = 700;  // 原1400 * 0.5
            //int boxHeight = 360; // 原720 * 0.5
            //                     // 调整位置：内边距同步缩小，避免超出图像（根据图像宽度自适应）
            //int boxX = Math.Max(20, colorMat.Width - boxWidth - 20); // 原40 → 20
            //int boxY = 20;                                          // 原40 → 20

            //// 绘制白色半透明背景框
            //Mat roi = colorMat[new OpenCvSharp.Rect(boxX, boxY, boxWidth, boxHeight)];
            //roi.SetTo(new Scalar(255, 255, 255, 0.8));
            //// 边框粗细缩小0.5倍（原12 → 6）
            //Cv2.Rectangle(colorMat, new OpenCvSharp.Rect(boxX, boxY, boxWidth, boxHeight), new Scalar(0, 0, 0), 6);

            //// 字体/行间距同步缩小0.5倍（回到原3倍放大效果）
            //int textY = boxY + 80;   // 文字起始位置（原160 → 80）
            //int textStep = 75;       // 行间距（原150 → 75）
            //double fontScale = 2.1;  // 字体大小（原4.2 → 2.1）
            //int fontThickness = 6;   // 文字粗细（原12 → 6）
            //int textPadding = 40;    // 文字内边距（原80 → 40）

            //// 绘制参数文字（缩小0.5倍后比例协调）
            //Cv2.PutText(colorMat, $"MaxAngle: {MaxAngle}°", new OpenCvSharp.Point(boxX + textPadding, textY),
            //            (int)HersheyFonts.HersheySimplex, fontScale, new Scalar(0, 0, 0), fontThickness);
            //Cv2.PutText(colorMat, $"Coeff: {ConoscopeCoefficient:F4}", new OpenCvSharp.Point(boxX + textPadding, textY + textStep),
            //            (int)HersheyFonts.HersheySimplex, fontScale, new Scalar(0, 0, 0), fontThickness);
            //Cv2.PutText(colorMat, $"DisplayAngle: {displayAngle}°", new OpenCvSharp.Point(boxX + textPadding, textY + 2 * textStep),
            //            (int)HersheyFonts.HersheySimplex, fontScale, new Scalar(0, 0, 0), fontThickness);
            //Cv2.PutText(colorMat, $"DisplayRadius: {displayRadius}°", new OpenCvSharp.Point(boxX + textPadding, textY + 3 * textStep),
            //            (int)HersheyFonts.HersheySimplex, fontScale, new Scalar(0, 0, 0), fontThickness);
            // ========== 绘制结束 ==========

            pseudoColorMat = colorMat;
            WriteableBitmap writeableBitmap = pseudoColorMat.ToWriteableBitmap();
            imgDisplay.Source = writeableBitmap;

            // ========== 新增：记录图片自然尺寸 ==========
            _imgNaturalWidth = writeableBitmap.PixelWidth;
            _imgNaturalHeight = writeableBitmap.PixelHeight;
            // ========== 新增：重置缩放 ==========
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
        string DC=(string)Application.Current.FindResource("Plot.Title.DiameterLine");
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
                    double radians = anglePos * Math.PI / 180.0;
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


            double angleRadians = angle * Math.PI / 180.0;
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
            if (_isFirstLoad)
            {
                _isFirstLoad = false;
                return;
            }

            // 步骤2：用户主动切换时才检查数据
            if (cbDisplayAngle.SelectedItem is ComboBoxItem item && item.Tag is string angleStr)
            {
                if (int.TryParse(angleStr, out int angle))
                {
                    displayAngle = angle;
                    _selectedAngle = angle; // 更新“选中角度”
                    _selectedRadius = -1; // 切换面板时重置另一面板的选中状态
                    if (IsMatSafe(YMat)) UpdateDisplay();
                    else MessageBox.Show("数据未加载或已释放，请重新打开CVCIE文件", "提示");
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
            if (cbDisplayRadius.SelectedItem is ComboBoxItem item && item.Tag is string radiusStr)
            {
                if (int.TryParse(radiusStr, out int radius))
                {
                    displayRadius = radius;
                    _selectedRadius = radius; // 更新“选中半径”
                    _selectedAngle = -1; // 切换面板时重置另一面板的选中状态
                    if (IsMatSafe(YMat)) UpdateDisplay();
                }
            }
        }

        // 切换图表
        private void BtnSwitchChart_Click(object sender, RoutedEventArgs e)
        {
            //if (btnSwitchChart.Content.ToString() == RCircle)
            //{
            //    // 切换到R圆面板
            //    btnSwitchChart.Content = Diameter;
            //    panelDiameter.Visibility = Visibility.Collapsed;
            //    panelRCircle.Visibility = Visibility.Visible;
            //}
            //else
            //{
            //    // 切换回直径线面板
            //    btnSwitchChart.Content = RCircle;
            //    panelDiameter.Visibility = Visibility.Visible;
            //    panelRCircle.Visibility = Visibility.Collapsed;
            //}

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
    }
}
