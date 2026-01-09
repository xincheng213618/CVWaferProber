using CVAVMControl;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using static CVAVMControl.CVVAMAnalyzer;
//using CVVAMAnalyzer.xaml.cs;
namespace CVVAMControl
{
    /// <summary>
    /// VamExportDialog.xaml 的交互逻辑
    /// </summary>
    public partial class VamExportDialog : Window
    {
        // 导出数据上下文（由主控件传入）
        private readonly CVVAMAnalyzer _analyzer;
        private readonly string _baseExportPath; // 导出基础路径

       
        // 构造函数
        public VamExportDialog(CVVAMAnalyzer analyzer, string basePath)
        {
            InitializeComponent();
            _analyzer = analyzer;
            _baseExportPath = basePath;

            // 绑定导出模式切换事件
            rbLine.Checked += (s, e) =>
            {
                pnlLineParams.Visibility = Visibility.Visible;
                pnlCircleParams.Visibility = Visibility.Collapsed;
            };
            rbCircle.Checked += (s, e) =>
            {
                pnlLineParams.Visibility = Visibility.Collapsed;
                pnlCircleParams.Visibility = Visibility.Visible;
            };
            // ========== 新增：绑定采样点数TextBox的TextChanged事件 ==========
            txtLineSampleCount.TextChanged += TxtLineSampleCount_TextChanged;
            txtCircleSampleCount.TextChanged += TxtCircleSampleCount_TextChanged;

            // 初始加载时手动触发一次，确保显示正确
            TxtLineSampleCount_TextChanged(this, null);
            TxtCircleSampleCount_TextChanged(this, null);
        }
        // ========== 新增：线条模式-极角采样点数实时更新逻辑 ==========
        private void TxtLineSampleCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = txtLineSampleCount.Text.Trim();
            // 校验输入是否为正整数
            if (!int.TryParse(input, out int sampleCount) || sampleCount <= 0)
            {
                LineSampleCountBook.Text = "（[-60,60]共无效点数）";
                return;
            }

            // 同步极角范围（若txtLinePolarRHO变化，范围也会动态更新）
            int polarRHO = int.TryParse(txtLinePolarRHO.Text.Trim(), out int rho) ? rho : 60;
            // 更新TextBlock内容
            LineSampleCountBook.Text = $"（[-{polarRHO},{polarRHO}]共{sampleCount}点）";
        }

        // ========== 新增：圆环模式-方位角采样点数实时更新逻辑 ==========
        private void TxtCircleSampleCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = txtCircleSampleCount.Text.Trim();
            // 校验输入是否为正整数
            if (!int.TryParse(input, out int sampleCount) || sampleCount <= 0)
            {
                CircleSampleCountBook.Text = "（[0,360)间隔无效）";
                return;
            }
            // 计算方位角间隔（360° ÷ 采样点数）
            double interval = 360.0 / sampleCount;
            // 更新TextBlock内容
            CircleSampleCountBook.Text = $"（[0,360)间隔{interval:F1}°）";
        }
        // 取消按钮
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // 导出按钮核心逻辑
        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 校验至少选择一个通道
                var selectedChannels = GetSelectedChannels();
                if (selectedChannels.Count == 0)
                {
                    MessageBox.Show("请至少选择一个导出通道！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. 区分导出模式
                if (rbLine.IsChecked == true)
                {
                    ExportLineMode(selectedChannels);
                }
                else
                {
                    ExportCircleMode(selectedChannels);
                }

                MessageBox.Show("数据导出成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 核心导出逻辑
        /// <summary>
        /// 线条（直径线）模式导出
        /// </summary>
      
        private void ExportLineMode(List<ExportDataType> selectedChannels)
        {
            // 1. 解析参数
            if (!int.TryParse(txtLinePolarRHO.Text, out int polarRHO) || polarRHO <= 0)
                throw new ArgumentException("极角范围（polar_RHO）必须为正整数！");
            if (!int.TryParse(txtLineSampleCount.Text, out int lineSampleCount) || lineSampleCount <= 0)
                throw new ArgumentException("极角采样点数必须为正整数！");
            LineSampleCountBook.Text = $"([-60,60]共{txtLineSampleCount.Text}点）)";
            // 2. 计算极角间隔和范围（[-polarRHO, polarRHO]）
            double polarStep = (2 * polarRHO) / (lineSampleCount - 1);
            var polarAngles = Enumerable.Range(0, lineSampleCount)
                                        .Select(i => -polarRHO + i * polarStep)
                                        .ToList();

            // 3. 方位角范围：[0, 180)，间隔1°
            var azimuthAngles = Enumerable.Range(0, 180).Select(x => (double)x).ToList();

            // 4. 获取所有方位角的DLL数据
            bool dllSuccess = _analyzer.CallVamDllForAllAzimuth(
                exportChannel: ExportChannel.Y, // 临时通道，仅用于获取全量数据
                pointNumLine: lineSampleCount,
                polarRHO: polarRHO,
                polarAngle: 60.0
            );
            if (!dllSuccess)
                throw new Exception("获取线条数据失败，请检查DLL调用！");

            // 5. 为每个选中通道生成CSV
            foreach (var channel in selectedChannels)
            {
                string csvPath = $"{_baseExportPath}_{channel}_Line.csv";
                using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
                {
                    // 写入表头：空列 + 方位角（0°~179°）
                    writer.Write(",");
                    writer.WriteLine(string.Join(",", azimuthAngles.Select(a => $"{a:F1}°")));

                    // 写入数据行：极角 + 对应方位角的数值
                    for (int i = 0; i < polarAngles.Count; i++)
                    {
                        double polar = polarAngles[i];
                        var line = new StringBuilder();
                        line.Append($"{polar:F1}°"); // 极角（Y轴）

                        foreach (double azimuth in azimuthAngles)
                        {
                            // 获取对应方位角的采样数据
                            if (_analyzer._dllAllAzimuthData.TryGetValue((int)azimuth, out var sampleList))
                            {
                                // 找到最接近当前极角的采样点
                                var sample = sampleList.OrderBy(s => Math.Abs(s.position - polar))
                                                      .FirstOrDefault();
                                double value = GetChannelValue(sample, channel);
                                line.Append($",{value:F5}");
                            }
                            else
                            {
                                line.Append(",");
                            }
                        }
                        writer.WriteLine(line.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// 圆环（R圆）模式导出
        /// </summary>
        private void ExportCircleMode(List<ExportDataType> selectedChannels)
        {
            // 1. 解析并校验用户输入参数
            if (!int.TryParse(txtCirclePolarStart.Text, out int polarStart) || polarStart < -60 || polarStart > 60)
                throw new ArgumentException("极角起始值必须为-60~60之间的整数！");
            if (!int.TryParse(txtCirclePolarEnd.Text, out int polarEnd) || polarEnd < -60 || polarEnd > 60 || polarEnd <= polarStart)
                throw new ArgumentException("极角结束值必须大于起始值，且在-60~60之间！");
            if (!int.TryParse(txtCirclePolarStep.Text, out int polarStep) || polarStep <= 0)
                throw new ArgumentException("极角间隔必须为正整数！");
            if (!int.TryParse(txtCircleSampleCount.Text, out int circleSampleCount) || circleSampleCount <= 0)
                throw new ArgumentException("方位角采样点数必须为正整数！");

            // 2. 计算极角范围：[polarStart, polarEnd]，间隔polarStep（支持正负极角）
            var polarAngles = new List<int>();
            for (int p = polarStart; p <= polarEnd; p += polarStep)
                polarAngles.Add(p);

            // 3. 计算方位角范围和间隔：[0, 360)，按采样点数均分
            double azimuthStep = 360.0 / circleSampleCount;
            var azimuthAngles = Enumerable.Range(0, circleSampleCount)
                                          .Select(i => i * azimuthStep)
                                          .ToList();

            // 4. 调用DLL批量获取所有极角+方位角的圆环数据
            bool dllSuccess = _analyzer.CallVamDllForAllCircle(
                polarStart: polarStart,
                polarEnd: polarEnd,
                polarStep: polarStep,
                azimuthSampleCount: circleSampleCount
            );
            if (!dllSuccess || _analyzer.DllAllCircleData == null || _analyzer.DllAllCircleData.Count == 0)
                throw new Exception("获取圆环数据失败，请检查DLL调用或参数设置！");

            // 5. 为每个选中通道生成CSV文件
            foreach (var channel in selectedChannels)
            {
                string csvPath = $"{_baseExportPath}_{channel}_Circle.csv";
                using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
                {
                    // 写入表头：空列 + 方位角（0°~359°等，保留1位小数）
                    writer.Write(",");
                    writer.WriteLine(string.Join(",", azimuthAngles.Select(a => $"{a:F1}°")));

                    // 写入数据行：极角 + 对应方位角的数值
                    foreach (int polar in polarAngles)
                    {
                        var line = new StringBuilder();
                        line.Append($"{polar}°"); // 极角（Y轴）

                        foreach (double azimuth in azimuthAngles)
                        {
                            // 从DLL缓存中获取对应极角+方位角的采样点
                            if (_analyzer.DllAllCircleData.TryGetValue((polar, azimuth), out var sample))
                            {
                                double value = GetChannelValueFromCircleSample(sample, channel);
                                line.Append($",{value:F5}");
                            }
                            else
                            {
                                // 无数据时填充空值
                                line.Append(",");
                            }
                        }
                        writer.WriteLine(line.ToString());
                    }
                }
            }
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取选中的导出通道
        /// </summary>
        private List<ExportDataType> GetSelectedChannels()
        {
            var channels = new List<ExportDataType>();
            if (cbX.IsChecked == true) channels.Add(ExportDataType.X);
            if (cbY.IsChecked == true) channels.Add(ExportDataType.Y);
            if (cbZ.IsChecked == true) channels.Add(ExportDataType.Z);
            if (cbCieX.IsChecked == true) channels.Add(ExportDataType.CieX);
            if (cbCieY.IsChecked == true) channels.Add(ExportDataType.CieY);
            return channels;
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

        /// <summary>
        /// 获取圆环采样点的通道值
        /// </summary>
        private double GetChannelValueFromCircleSample(RgbSample sample, ExportDataType channel)
        {
            if (sample == null) return 0;
            return channel switch
            {
                ExportDataType.X => sample.X,
                ExportDataType.Y => sample.Y,
                ExportDataType.Z => sample.Z,
                // 圆环数据暂无cieX/cieY，返回0
                ExportDataType.CieX => 0,
                ExportDataType.CieY => 0,
                _ => 0
            };
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
        #endregion
    }
}
