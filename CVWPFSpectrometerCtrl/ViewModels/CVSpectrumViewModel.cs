using ColorVision.Core.Entities;
using CVCommCore;
using CVDB.Services.Algorithm;
using CVDB.Services.SMU;
using CVDB.Services.Spectrum;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using CVWPFSpectrumControl;
using CVWPFSpectrumControl.Models;
using log4net;
using Microsoft.Win32;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using ScottPlot.Colormaps;
using ScottPlot.Panels;
using ScottPlot.WPF;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using static FreeSql.Internal.GlobalFilter;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class CVSpectrumViewModel : ViewModelBase
    {
        private PlotModel _plotModel;
        private PlotModel _IVPlotModel;
        private PlotModel _ILPlotModel;
        private PlotModel _VLPlotModel;
        public void NotifyPropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }
        // ViewModel中新增：总览图的4个独立PlotModel
        public PlotModel OverviewSpectralPlotModel { get; private set; } = new PlotModel();
        public PlotModel OverviewIVPlotModel { get; private set; } = new PlotModel();
        public PlotModel OverviewILPlotModel { get; private set; } = new PlotModel();
        public PlotModel OverviewVLPlotModel { get; private set; } = new PlotModel();

        private SpectrumMeasurement _selectedMeasurement;
        private ObservableCollection<SpectrumMeasurement> _measurements;
        private ObservableCollection<ILMeasurement> _ILMeasurements;
        private ObservableCollection<IVMeasurement> _IVMeasurements;
        private ObservableCollection<VLMeasurement> _VLMeasurements;
        //private ObservableCollection<IVLMeasurement> _IVLMeasurements;
        private ObservableCollection<IVLCameraMeasurement> _IVLCameraMeasurements;
        private ObservableCollection<SpectralData> _SpectralData;

        private float[] Wavelengths;
        //private double[] Intensities;
        public ICommand ExportCommand { get;}
        private ILViewModel IL_viewModel;
        private IVViewModel IV_viewModel;
        private VLViewModel VL_viewModel;
        private IVLCameraViewModel IVLCamera_viewModel;
        
        private SpectrumControl _spectralCtrl;

        private WpfPlot _plotControl;
        //存储所有光谱曲线（Key=测量No，Value=曲线系列），用于快速切换高亮
        private Dictionary<int, LineSeries> _spectralSeriesCache = new Dictionary<int, LineSeries>();
        public WpfPlot PlotControl
        {
            get => _plotControl;
            set => SetProperty(ref _plotControl, value);
        }
        public PlotModel PlotModel
        {
            get => _plotModel;
            set => SetProperty(ref _plotModel, value);
        }

        private bool _isShowAllData;
        public bool IsShowAllData
        {
            get => _isShowAllData;
            set
            {
                _isShowAllData = value;
                OnPropertyChanged();
                // 勾选状态变化时，更新图表
                UpdateChartByShowAllState();
            }
        }
        public SpectrumMeasurement SelectedMeasurement
        {
            get => _selectedMeasurement;
            set
            {
                if (SetProperty(ref _selectedMeasurement, value))
                {
                    OnPropertyChanged(nameof(SelectedMeasurement));
                    if (IsShowAllData)
                    {
                        // 显示所有数据时，切换选中曲线高亮（不重新绘制，性能更优）
                        UpdateSelectedCurveHighlight();
                    }
                    else
                    {
                        // 未勾选时，显示单条选中数据（原有逻辑）
                        ResetAndUpdateChart();
                    }
                    // 新增：同步右侧DataGrid数据
                    if (IsShowSpectralDetail && value != null)
                    {
                        UpdateSpectralGridData(value);
                    }
                    
                }
            }
        }

        public ObservableCollection<SpectrumMeasurement> Measurements
        {
            get => _measurements;
            set => SetProperty(ref _measurements, value);
        }
        
        public ObservableCollection<IVMeasurement> IVMeasurements
        {
            get => _IVMeasurements;
            set => SetProperty(ref _IVMeasurements, value);
        }
        public ObservableCollection<ILMeasurement> ILMeasurements
        {
            get => _ILMeasurements;
            set => SetProperty(ref _ILMeasurements, value);
        }
        public ObservableCollection<VLMeasurement> VLMeasurements
        {
            get => _VLMeasurements;
            set => SetProperty(ref _VLMeasurements, value);
        }
        
        

        private void UpdateChartByShowAllState()
        {
            ResetPlotView(); // 重置图表

            if (IsShowAllData)
            {
                // 绘制所有数据
                DrawAllMeasurementsInChart();
            }
            else
            {
                // 绘制选中的单条数据（原逻辑）
                if (SelectedMeasurement != null)
                {
                    UpdateChartFromSelectedMeasurement();
                }
                else
                {
                    ShowEmptyChartMessage();
                }
            }
        }

        private void DrawAllMeasurementsInChart()
        {
            if (!Measurements.Any())
            {
                ShowEmptyChartMessage();
                return;
            }

            // 清空缓存和原有曲线
            _spectralSeriesCache.Clear();
            PlotModel.Series.Clear();

            // 带透明度的未选中颜色（Alpha=115≈45%透明度，适配低版本OxyPlot）
            var unselectedColors = new[]
            {
                OxyColor.FromAColor(115, OxyColors.Blue),
                OxyColor.FromAColor(115, OxyColors.Green),
                OxyColor.FromAColor(115, OxyColors.Purple),
                OxyColor.FromAColor(115, OxyColors.Orange),
                OxyColor.FromAColor(115, OxyColors.Teal),
                OxyColor.FromAColor(115, OxyColors.Magenta),
                OxyColor.FromAColor(115, OxyColors.Gold),
                OxyColor.FromAColor(115, OxyColors.Cyan),
                OxyColor.FromAColor(115, OxyColors.Lime),
                OxyColor.FromAColor(115, OxyColors.Indigo),
                OxyColor.FromAColor(115, OxyColors.Pink),
                OxyColor.FromAColor(115, OxyColors.Olive),
                OxyColor.FromAColor(115, OxyColors.SkyBlue)
            };
            int colorIndex = 0;

            foreach (var measurement in Measurements)
            {
                // 用测量No作为缓存Key（唯一标识）
                int measNo = measurement.No;
                // 图例标注：显示「测量No + 时间戳」
                string seriesTitle = $"No:{measNo} | {measurement.Timestamp:yyyy-MM-dd HH:mm}";
                // 判断是否为当前选中项
                bool isSelected = SelectedMeasurement != null && measNo == SelectedMeasurement.No;

                var lineSeries = new LineSeries
                {
                    Title = seriesTitle,
                    // 选中：红色；未选中：循环半透明颜色
                    Color = isSelected ? OxyColors.Red : unselectedColors[colorIndex % unselectedColors.Length],
                    // 选中：加粗（2.0px）；未选中：细线条（1.2px）
                    StrokeThickness = isSelected ? 2.0 : 1.2,
                    // 选中：显示圆形标记点；未选中：无标记点
                    //MarkerType = isSelected ? MarkerType.Circle : MarkerType.None,
                   // MarkerSize = 3,
                    //MarkerFill = OxyColors.Red,
                   // MarkerStroke = OxyColors.White, // 标记点白色边框，更醒目
                   // MarkerStrokeThickness = 0.5,
                    IsVisible = true,
                    TrackerFormatString = "波长: {X:.0}nm | 强度: {Y:.4f}" // 鼠标悬浮提示
                };

                // 填充数据点（过滤异常值）
                for (int i = 0; i < measurement.Wavelengths.Length; i++)
                {
                    if (!float.IsNaN(measurement.Intensities[i]) && !float.IsInfinity(measurement.Intensities[i]))
                    {
                        lineSeries.Points.Add(new DataPoint(
                            measurement.Wavelengths[i],
                            measurement.Intensities[i]));
                    }
                }

                // 缓存曲线
                _spectralSeriesCache.Add(measNo, lineSeries);
                // 添加到图表（未选中曲线先添加，选中曲线后续移到顶层）
                PlotModel.Series.Add(lineSeries);

                // 未选中曲线才递增颜色索引（避免颜色重复）
                if (!isSelected)
                    colorIndex++;
            }

            // 选中曲线移到顶层（确保不被遮挡）
            if (SelectedMeasurement != null && _spectralSeriesCache.ContainsKey(SelectedMeasurement.No))
            {
                BringSeriesToFront(SelectedMeasurement.No);
            }

            // 自动调整轴范围（适配所有数据）
            //AutoAdjustAxisRange();
            PlotModel.InvalidatePlot(true); // 刷新图表
        }

        public ObservableCollection<SpectralData> SpectralData
        {
            get => _SpectralData;
            set => SetProperty(ref _SpectralData, value);
        }
        public PlotModel IVPlotModel
        {
            get => _IVPlotModel;
            set => SetProperty(ref _IVPlotModel, value);
        }
        public PlotModel ILPlotModel
        {
            get => _ILPlotModel;
            set => SetProperty(ref _ILPlotModel, value);
        }
        public PlotModel VLPlotModel
        {
            get => _VLPlotModel;
            set => SetProperty(ref _VLPlotModel, value);
        }
        #region IVLCamera
        public IVLCameraMeasurement SelectedCameraMeasurement
        {
            get => IVLCamera_viewModel.SelectedMeasurement;
            set
            {
                IVLCamera_viewModel.SelectedMeasurement = value;
                OnPropertyChanged(nameof(SelectedCameraMeasurement));
            }
        }

        public BitmapSource IVLCameraImageSrc
        {
            get => IVLCamera_viewModel.ImageSrc;
            set
            {
                IVLCamera_viewModel.ImageSrc = value;
                OnPropertyChanged(nameof(IVLCameraImageSrc));
            }
        }
        public ObservableCollection<IVLCameraMeasurement> IVLCameraMeasurements
        {
            get => _IVLCameraMeasurements;
            set => SetProperty(ref _IVLCameraMeasurements, value);
        }
        #endregion IVLCamera

        #region Tab
        private TabType _selectedTab;
        public TabType SelectedTab
        {
            get => _selectedTab;
            set
            {
                _selectedTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedTabIndex));
            }
        }

        public int SelectedTabIndex
        {
            get => (int)SelectedTab;
            set => SelectedTab = (TabType)value;
        }
        #endregion Tab
        // 默认轴范围
        private PlotAxesCfg AxisX = new PlotAxesCfg() { DefaultMin = 350, DefaultMax = 800, DefaultMaxRange = 500 };
        private PlotAxesCfg AxisY = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 1.0f, DefaultMaxRange = 1.1f };

        public string DeviceCode { get;set; }

        private SpectraDataViewModel CurrentSpectrum;
        public ScottPlot.IColormap VisibleSpectrumColormap { get; }
        //public object DataCollection { get; private set; }

        public CVSpectrumViewModel()
        {
            // 新增：提前初始化波长数组
            Wavelengths = new float[10000];
            for (int i = 0; i < 10000; i++)
            {
                Wavelengths[i] = 380 + i / 10.0f;
            }
            Measurements = new ObservableCollection<SpectrumMeasurement>();
            SpectralGridItems = new ObservableCollection<SpectralGridItem>(); // 初始化右侧DataGrid数据源
            InitializePlotModel();
            InitializeIVPlotModel();
            InitializeILPlotModel();
            InitializeVLPlotModel();
            InitializeIVLCameraModel();
           
            DeviceCode = "DEV.Spectrum.Default";
            #region 输出CSV文件
            ExportCommand = new RelayCommand((s) =>
            {
                try
                {
                    string currentTab = GetCurrentTabName();
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "CSV Files|*.csv",
                        Title = $"Save {currentTab} Data to CSV",
                        FileName = $"{currentTab}_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {

                        try
                        {
                            //var csv = new StringBuilder();

                            // 根据不同的Tab索引生成不同的数据格式
                            switch (SelectedTabIndex)
                            {
                                case 2: // IV Tab
                                    ExportToCsv(IVMeasurements, saveFileDialog.FileName, 1);

                                    break;
                                case 3: // IL Tab
                                    ExportToCsv(ILMeasurements, saveFileDialog.FileName, 2);
                                    break;
                                case 4: // VL Tab 
                                    ExportToCsv(VLMeasurements, saveFileDialog.FileName, 0);
                                    break;
                                default:
                                    // 调用导出方法（传入路径、测量数据、波长数组）
                                    ExportToCsv(saveFileDialog.FileName, Measurements, Wavelengths);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error("Failed to save CSV file", ex);
                            MessageBox.Show($"Failed to save CSV file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        // 直接把导出逻辑写在这里
                        MessageBox.Show("导出执行成功");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"错误: {ex.Message}");
                }
            });
            #endregion

            // 初始化颜色映射（可见光谱：紫->蓝->绿->黄->红）
            ScottPlot.Color[] visibleSpectrumColors = {
            ScottPlot.Color.FromHex("#750085"), // 紫色
            ScottPlot.Color.FromHex("#0000FF"), // 蓝色
            ScottPlot.Color.FromHex("#00FF00"), // 绿色
            ScottPlot.Color.FromHex("#FFFF00"), // 黄色
            ScottPlot.Color.FromHex("#FF0000"),// 红色
            };

            VisibleSpectrumColormap = new ScottPlot.Colormaps.Custom(visibleSpectrumColors);

            // 初始化数据
            InitializeSampleData();

            InitializePlot();
            
            // 初始化总览图（关键步骤）
            InitializeOverviewPlotModels();
            
        }
        // 初始化总览图的PlotModel（克隆子Tab配置并绑定数据）
        private void InitializeOverviewPlotModels()
        {
            string Title = (string)Application.Current.FindResource("Sp.Spectral");
            // 1. 克隆子Tab的图表配置（轴、样式）
            OverviewSpectralPlotModel = ClonePlotModel(PlotModel, Title); // 克隆光谱子Tab配置
            OverviewIVPlotModel = ClonePlotModel(IVPlotModel, "IV");       // 克隆IV子Tab配置
            OverviewILPlotModel = ClonePlotModel(ILPlotModel, "IL");       // 克隆IL子Tab配置
            OverviewVLPlotModel = ClonePlotModel(VLPlotModel, "VL");       // 克隆VL子Tab配置

            // 2. 为总览图添加数据系列（绑定子Tab数据源）
            InitializeOverviewSeries();

            // 3. 通知UI更新
            OnPropertyChanged(nameof(OverviewSpectralPlotModel));
            OnPropertyChanged(nameof(OverviewIVPlotModel));
            OnPropertyChanged(nameof(OverviewILPlotModel));
            OnPropertyChanged(nameof(OverviewVLPlotModel));
        }
        private PlotModel ClonePlotModel(PlotModel sourceModel, string title)
        {
            var targetModel = new PlotModel
            {
                Title = title,
                Padding = new OxyThickness(5), // 手动控制边距
                PlotAreaBorderThickness = new OxyThickness(1)
            };

            // 克隆轴配置（适配低版本OxyPlot）
            foreach (var axis in sourceModel.Axes)
            {
                if (axis is LinearAxis linearAxis)
                {
                    var clonedAxis = new LinearAxis
                    {
                        Position = linearAxis.Position,
                        Title = linearAxis.Title,
                        // 不手动设置Min/Max，让轴自动计算范围（低版本默认行为）
                        Minimum = double.NaN,
                        Maximum = double.NaN,
                        FontSize = 10,
                        MajorGridlineStyle = linearAxis.MajorGridlineStyle,
                        MinorGridlineStyle = linearAxis.MinorGridlineStyle,
                        IsZoomEnabled = false, // 总览图禁用手动缩放
                        IsPanEnabled = false
                    };
                    targetModel.Axes.Add(clonedAxis);
                }
            }
            return targetModel;
        }
        // 辅助方法：强制刷新轴范围，让数据占满图表
        private void RefreshAxisRange(PlotModel plotModel)
        {
            foreach (var axis in plotModel.Axes)
            {
                if (axis is LinearAxis linearAxis)
                {
                    // 重置为自动范围
                    linearAxis.Minimum = double.NaN;
                    linearAxis.Maximum = double.NaN;
                }
            }

            // 先让PlotModel计算一次自动范围
            plotModel.InvalidatePlot(true);

            // 添加5%边距，避免数据贴轴
            foreach (var axis in plotModel.Axes)
            {
                if (axis is LinearAxis linearAxis)
                {
                    if (!double.IsNaN(linearAxis.ActualMinimum) && !double.IsNaN(linearAxis.ActualMaximum))
                    {
                        double range = linearAxis.ActualMaximum - linearAxis.ActualMinimum;
                        double margin = range * 0.05; // 5%边距
                        linearAxis.Minimum = linearAxis.ActualMinimum - margin;
                        linearAxis.Maximum = linearAxis.ActualMaximum + margin;
                    }
                }
            }

            // 再次刷新，应用边距
            plotModel.InvalidatePlot(true);
        }
        // 初始化总览图的数据系列（绑定子Tab数据源）
        private void InitializeOverviewSeries()
        {
            // 1. 清空总览图所有Series
            OverviewSpectralPlotModel.Series.Clear();
            OverviewIVPlotModel.Series.Clear();
            OverviewILPlotModel.Series.Clear();
            OverviewVLPlotModel.Series.Clear();

            // 2. 绑定光谱数据
            
            if (Measurements.Any())
            {
                // 预设多组不透明颜色（无Alpha通道，颜色鲜明且不重复）
                var curveColors = new[]
                {
                    OxyColors.Red, OxyColors.Green, OxyColors.Purple, OxyColors.Orange,
                    OxyColors.Teal, OxyColors.Magenta, OxyColors.Gold, OxyColors.Cyan,
                    OxyColors.Lime, OxyColors.Indigo, OxyColors.Olive, OxyColors.Pink,
                    OxyColors.Brown, OxyColors.DarkBlue, OxyColors.Blue
                };
                const double strokeThickness = 1; // 所有曲线统一粗细（无加粗）
                int colorIndex = 0;

                foreach (var measurement in Measurements)
                {
                    int measNo = measurement.No;
                    string seriesTitle = $"No:{measNo} | {measurement.Timestamp:yyyy-MM-dd HH:mm}";

                    var lineSeries = new LineSeries
                    {
                        Title = seriesTitle,
                        Color = curveColors[colorIndex % curveColors.Length], // 循环使用颜色
                        StrokeThickness = strokeThickness, // 统一粗细
                        IsVisible = true,
                        TrackerFormatString = "波长: {X:.0}nm | 强度: {Y:.4f}"
                    };

                    // 填充数据点（过滤异常值）
                    for (int i = 0; i < measurement.Wavelengths.Length; i++)
                    {
                        if (!float.IsNaN(measurement.Intensities[i]) && !float.IsInfinity(measurement.Intensities[i]))
                        {
                            lineSeries.Points.Add(new DataPoint(
                                measurement.Wavelengths[i],
                                measurement.Intensities[i]));
                        }
                    }

                    OverviewSpectralPlotModel.Series.Add(lineSeries);
                    colorIndex++; // 每条曲线递增颜色索引（确保颜色不同）
                }

                RefreshAxisRange(OverviewSpectralPlotModel);

                // 选中曲线移到顶层
                if (SelectedMeasurement != null)
                {
                    var selectedSeries = OverviewSpectralPlotModel.Series.OfType<LineSeries>()
                        .FirstOrDefault(s => s.Title.Contains($"No:{SelectedMeasurement.No}"));
                    if (selectedSeries != null)
                    {
                        OverviewSpectralPlotModel.Series.Remove(selectedSeries);
                        OverviewSpectralPlotModel.Series.Add(selectedSeries);
                    }
                }

                RefreshAxisRange(OverviewSpectralPlotModel);
            }


            // 3. 绑定IV数据（修正轴顺序：电流X，电压Y）
            if (IVMeasurements.Any())
            {
                var ivSeries = new LineSeries
                {
                    ItemsSource = IVMeasurements.Select(m => new DataPoint(m.Current, m.Voltage)),
                    Color = OxyColors.Red,
                    StrokeThickness = 1.5,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 2,
                    MarkerFill = OxyColors.Red,
                };
                OverviewIVPlotModel.Series.Add(ivSeries);
                RefreshAxisRange(OverviewIVPlotModel);
            }

            // 4. 绑定IL数据
            if (ILMeasurements.Any())
            {
                var ilSeries = new LineSeries
                {
                    ItemsSource = ILMeasurements.Select(m => new DataPoint(m.Current, m.Luminance)),
                    Color = OxyColors.Green,
                    StrokeThickness = 1.5,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 2,
                    MarkerFill = OxyColors.Green
                };
                OverviewILPlotModel.Series.Add(ilSeries);
                RefreshAxisRange(OverviewILPlotModel);
            }

            // 5. 绑定VL数据
            if (VLMeasurements.Any())
            {
                var vlSeries = new LineSeries
                {
                    ItemsSource = VLMeasurements.Select(m => new DataPoint(m.Voltage, m.Luminance)),
                    Color = OxyColors.Purple,
                    StrokeThickness = 1.5,
                    MarkerType = MarkerType.Circle,
                    //MarkerSize = 2,
                    MarkerFill = OxyColors.Purple
                };
                OverviewVLPlotModel.Series.Add(vlSeries);
                RefreshAxisRange(OverviewVLPlotModel);
            }

            // 6. 强制刷新图表
            OverviewSpectralPlotModel.InvalidatePlot(true);
            OverviewIVPlotModel.InvalidatePlot(true);
            OverviewILPlotModel.InvalidatePlot(true);
            OverviewVLPlotModel.InvalidatePlot(true);
        }

        // 辅助方法：将光谱Measurements转换为图表需要的DataPoint（波长-强度）
        private IEnumerable<DataPoint> GetSpectralDataPoints()
        {
            if (Measurements.Any() && Measurements.First().Wavelengths != null && Measurements.First().Intensities != null)
            {
                var firstMeas = Measurements.First();
                for (int i = 0; i < firstMeas.Wavelengths.Length; i++)
                {
                    yield return new DataPoint(firstMeas.Wavelengths[i], firstMeas.Intensities[i]);
                }
            }
        }
        #region IV/IL/VL
        private void ExportToCsv<T>(IEnumerable<T> data, string filePath, int startIndex)
        {
            try
            {
                if (data == null || !data.Any())
                {
                    MessageBox.Show("没有数据可导出");
                    return;
                }
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("保存路径不能为空", nameof(filePath));

                var csv = new StringBuilder();
                var properties = typeof(T).GetProperties();
               
                var headerMap = new Dictionary<string, string>();

                //IV
                var headerMap1 = new Dictionary<string, string>
                {
                    { "No", "Index" },
                    { "Timestamp", "Time" },
                    { "Current", "Current(mA)" },
                    { "Voltage", "Voltage/V" }

                };
                //IL
                var headerMap2 = new Dictionary<string, string>
                {

                    { "No", "Index" },
                    { "Timestamp", "Time" },
                    { "Current", "Current(mA)" },
                    { "Luminance", "Lv(cd/m²)" },
                };
                //VL
                var headerMap3 = new Dictionary<string, string>
                {

                    { "No", "Index" },
                    { "Timestamp", "Time" },
                    { "Voltage", "Voltage/V" },
                    { "Luminance", "Lv(cd/m²)" }
                };
                switch (startIndex)
                {
                    case 1:
                        headerMap = headerMap1;
                        break;
                    case 2:
                        headerMap = headerMap2;
                        break;
                    default:
                        headerMap = headerMap3;
                        break;
                }

                var headers = properties.Select(p =>
                {
                    // 字典中存在则用映射值，否则用原字段名
                    return headerMap.TryGetValue(p.Name, out var chineseHeader)
                        ? chineseHeader
                        : p.Name;
                    // 可自定义表头（如需要中文表头，这里可扩展）
                });
                csv.AppendLine(string.Join(",", headers));
                // 数据
                foreach (var item in data)
                {
                    var values = properties.Select(p =>
                    {
                        var value = p.GetValue(item)?.ToString() ?? "";
                        string formattedValue = FormatValue(value);
                        if (NeedsEscaping(value))
                        {
                            value = $"\"{value.Replace("\"", "\"\"")}\"";
                        }
                        return value;
                    });
                    csv.AppendLine(string.Join(",", values));
                }

                File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
               // MessageBox.Show($"成功导出 {data.Count()} 行数据");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}");
            }
        }
        #endregion

        #region 光谱
        // 导出CSV的方法（参数：保存路径、Measurements数据列表、波长数组）
        private void ExportToCsv(string fileName, ObservableCollection<SpectrumMeasurement> measurements, float[]? wavelengths, float fPlambda = 1.0f)
        {
            if (measurements == null || !measurements.Any() || wavelengths == null || wavelengths.Length == 0)
            {
                MessageBox.Show("无有效数据可导出！", "提示");
                return;
            }

            const int Step = 10;
            const int MinWave = 380;
            const int MaxWave = 780;
            //const int OriginalTotalPoints = (MaxWave - MinWave) * 10 + 1; // 4001个原始点（380.0~780.0nm，步长0.1）

            //// 校验wavelengths数组长度是否符合目标代码要求（避免数据来源不一致）
            //if (wavelengths.Length != OriginalTotalPoints)
            //{
            //    MessageBox.Show($"wavelengths数组长度需为{OriginalTotalPoints}（380.0~780.0nm，步长0.1）！", "错误");
            //    return;
            //}

            try
            {
                // 1. 固定表头（与目标代码数据项对齐）
                var fixedHeaders = new List<string>
                {
                    "Time","Meas_Id", "Voltage/V", "Current/mA", "Lv(cd/m²)", "IP",
                    "BlueLight", "cx", "cy", "u'", "v'", "CCT(K)",
                    "Dominant Wavelength(nm)", "Saturation(%)", "Peak Wavelength(nm)", "FWHM"
                };

                // 2. 波长表头：完全对齐目标代码（380~780nm，隔10取1整数波长）
                var waveHeaders = new List<string>();
                var selectedIndexes = new List<int>(); // 存储目标代码对应的原始索引

                // 用目标代码的循环逻辑生成表头和索引（确保完全一致）
                for (int i = 0; i <= (MaxWave - MinWave) * 10; i += Step)
                {
                    // 目标代码的波长计算逻辑：i/10 + 380（整数波长）
                    double targetWave = i / 10.0 + MinWave;
                    // 从wavelengths数组中匹配对应波长（避免数组索引错位）
                    int originalIndex = Array.FindIndex(wavelengths, w => Math.Abs(w - targetWave) < 0.001);

                    if (originalIndex != -1)
                    {
                        waveHeaders.Add($"{targetWave:F0}"); // 整数波长格式（380nm）
                        selectedIndexes.Add(originalIndex);
                    }
                }

                if (waveHeaders.Count == 0)
                {
                    MessageBox.Show("未找到≤780nm的有效波长点！", "错误");
                    return;
                }

                // 3. 完整表头 = 固定表头 + 波长表头（可选择相对强度或绝对强度，这里默认相对强度）
                var allHeaders = fixedHeaders.Concat(waveHeaders);

                // 4. 构建CSV内容
                var csv = new System.Text.StringBuilder();
                csv.AppendLine(string.Join(",", allHeaders));

                // 3. 遍历测量数据：用索引+1作为Meas_Id（核心修改）
                for (int rowIndex = 0; rowIndex < measurements.Count; rowIndex++)
                {
                    var item = measurements[rowIndex];
                    // 动态生成Meas_Id：从1开始递增（rowIndex是0-based，+1后为1-based）
                    int measId = rowIndex + 1;

                    // 4. 固定字段值：第一个值为动态生成的Meas_Id
                    var fixedValues = new List<string>
                    {
                        item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        measId.ToString(), // 替换原固定值36，改为1、2、3...
                        item.Voltage.ToString("F6"),
                        item.Current.ToString(), // A→mA
                        item.Luminance.ToString(),
                        EscapeCsvValue(item.IP ?? ""),
                        item.Blue.ToString(),
                        item.CIE_x.ToString(),
                        item.CIE_y.ToString(),
                        item.CIE_u.ToString(),
                        item.CIE_v.ToString(),
                        item.CCT.ToString(),
                        item.PeakWavelength.ToString(),
                        //$"{Math.Round(item.fPur * 100, 2)}", // 饱和度转百分比（对齐目标代码显示）
                        item.fPur.ToString(),
                        item.PeakIntensity.ToString("F2"),
                        item.FHW.ToString("F2")
                     };

                    // 5. 强度值处理（与之前逻辑一致）
                    var waveValues = new List<string>();
                    if (item.Intensities != null && item.Intensities.Length == wavelengths.Length)
                    {
                        // 遍历目标代码筛选后的索引，取对应强度值
                        foreach (int idx in selectedIndexes)
                        {
                            float intensity = item.Intensities[idx];
                            // 目标代码逻辑：负强度转为0
                            intensity = intensity > 0 ? intensity : 0;
                            // 可选：导出相对强度（SpectralData.RelativeSpectrum）或绝对强度（SpectralData.AbsoluteSpectrum）
                            // 相对强度：直接用处理后的intensity；绝对强度：intensity * fPlambda
                            double targetIntensity = intensity * item.fPlambda; // 相对强度（要绝对强度则改为 intensity * fPlambda）
                                                                                // 格式化（与目标代码数据精度一致）
                            string value = targetIntensity < 0.0001f ? targetIntensity.ToString() : targetIntensity.ToString();
                            waveValues.Add(EscapeCsvValue(value));
                        }
                    }
                    else
                    {
                        waveValues = Enumerable.Repeat("0.000000", waveHeaders.Count)
                                              .Select(v => EscapeCsvValue(v))
                                              .ToList();
                    }

                    // 6. 拼接并写入行
                    var allValues = fixedValues.Concat(waveValues);
                    csv.AppendLine(string.Join(",", allValues));
                }

                File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
               // MessageBox.Show($"CSV导出成功！\n路径：{fileName}", "成功");
            
                // 6. 写入文件
                //File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
               // MessageBox.Show($"CSV导出成功！\n路径：{fileName}\n筛选后波长点：{waveHeaders.Count}（与目标代码一致）", "成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误");
            }
        }
        #endregion

        #region CSV值转义辅助方法
        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
        private string FormatValue(object  value)
        {
            if (value == null) return "";

            switch (value)
            {
                case double d:
                    return d.ToString("F2");
                case float f:
                    return f.ToString("F2");
                case decimal m:
                    return m.ToString("F2");
                case int i:
                    return i.ToString();
                case string s:
                    return s;
                case DateTime dt:
                    return dt.ToString("yyyy-MM-dd HH:mm:ss");
                default:
                    return value.ToString();
            }
        }
        private string GetCurrentTabName()
        {
            return SelectedTabIndex switch
            {
                1 => "光谱",
                2 => "IV",
                3 => "IL",
                4 => "VL",
                _ => "数据"
            };
        }
        private bool NeedsEscaping(string value)
        {
            return value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
        }
        #endregion
        private void InitializeSampleData()
        {
            // 创建初始光谱数据（380nm - 780nm）
            //double[] Wavelengths = Enumerable.Range(380, 401).Select(w => (double)w).ToArray();
            double[] Wavelengths = new double[4010];
            for (int i = 0; i < Wavelengths.Length; i++)
            {
                Wavelengths[i] = 380 + i / 10.0;
            }
          double[]  Intensities = GenerateSampleSpectrum(Wavelengths);
            CurrentSpectrum = new SpectraDataViewModel(Wavelengths, Intensities);
        }
        private Random _random = new Random();
        // 生成样本光谱数据（可替换为真实设备数据）
        private double[] GenerateSampleSpectrum(double[] wavelengths)
        {
            double[] intensities = new double[wavelengths.Length];

            // 模拟多个高斯峰
            for (int i = 0; i < wavelengths.Length; i++)
            {
                double wavelength = wavelengths[i];
                double intensity = 0;

                // 峰1：450nm附近
                intensity += Math.Exp(-Math.Pow((wavelength - 450) / 20, 2)) * 0.8;
                // 峰2：550nm附近
                intensity += Math.Exp(-Math.Pow((wavelength - 550) / 25, 2)) * 1.0;
                // 峰3：650nm附近
                intensity += Math.Exp(-Math.Pow((wavelength - 650) / 30, 2)) * 0.6;

                // 添加一些随机噪声
                intensity += (_random.NextDouble() - 0.5) * 0.1;

                intensities[i] = Math.Max(0, intensity);
            }

            return intensities;
        }
        private void InitializePlot()
        {
            PlotControl = new WpfPlot();
            //UpdatePlot();
            //UpdatePlotWithGradient();
            //CreateRadialWaveHeatmap();
            UpdatePlotWithCustomRendering();
            //UpdatePlotWithSimpleSegments();
            //CreateWaveformHeatmap();
            //CreateWaveformSurface();
        }
        private void CreateRadialWaveHeatmap()
        {
            if (PlotControl == null) return;

            PlotControl.Plot.Clear();

            var radialData = GenerateRadialWaveData();
            var heatmap = PlotControl.Plot.Add.Heatmap(radialData);
            heatmap.Colormap = CreateVisibleSpectrumColormap();
            heatmap.Smooth = true;

            PlotControl.Plot.Title("径向波浪Heatmap");
            PlotControl.Plot.Axes.AutoScale();
            PlotControl.Refresh();
        }

        private double[,] GenerateRadialWaveData()
        {
            int size = 200;
            double[,] data = new double[size, size];
            double center = size / 2.0;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx = x - center;
                    double dy = y - center;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    double angle = Math.Atan2(dy, dx);

                    // 限制在圆形范围内
                    if (distance > center)
                    {
                        data[y, x] = double.NaN; // 透明区域
                        continue;
                    }

                    // 创建径向波浪
                    double radiusNormalized = distance / center;
                    double radialWave = Math.Sin(radiusNormalized * Math.PI * 8 + angle * 4) * 0.5;
                    double concentricWave = Math.Sin(radiusNormalized * Math.PI * 16) * 0.3;

                    // 波长颜色从中心向外渐变
                    double wavelengthEffect = radiusNormalized;
                    data[y, x] = (radialWave + concentricWave) * (1 - radiusNormalized) + wavelengthEffect;
                }
            }

            return data;
        }
        private void CreateSpectrumWaveHeatmap()
        {
            if (CurrentSpectrum == null || PlotControl == null) return;

            PlotControl.Plot.Clear();

            // 基于真实光谱数据生成波浪Heatmap
            var waveData = GenerateSpectrumBasedWaveData(CurrentSpectrum);

            var heatmap = PlotControl.Plot.Add.Heatmap(waveData);
            heatmap.Colormap = CreateVisibleSpectrumColormap();
            heatmap.Smooth = true;

            PlotControl.Plot.Title("光谱数据波浪图");
            PlotControl.Plot.Axes.AutoScale();
            PlotControl.Refresh();
        }

        private double[,] GenerateSpectrumBasedWaveData(SpectraDataViewModel spectrum)
        {
            int width = spectrum.Wavelengths.Length;
            int height = 80;
            double[,] data = new double[height, width];

            // 找到最大强度用于归一化
            double maxIntensity = spectrum.Intensities.Max();

            for (int y = 0; y < height; y++)
            {
                double phase = (double)y / height * Math.PI * 2; // 相位变化

                for (int x = 0; x < width; x++)
                {
                    double intensity = spectrum.Intensities[x] / maxIntensity;
                    double wavelengthRatio = (double)x / width;

                    // 创建波浪形状：强度 × 波浪函数
                    double baseWave = Math.Sin(wavelengthRatio * Math.PI * 6 + phase) * 0.7;
                    double detailedWave = Math.Sin(wavelengthRatio * Math.PI * 12 + phase * 2) * 0.3;

                    // 应用强度包络
                    double yEnvelope = 1.0 - Math.Abs((double)y / height - 0.5) * 2; // 中间强，两边弱
                    double waveValue = (baseWave + detailedWave) * intensity * yEnvelope;

                    // 加入波长颜色效果
                    data[y, x] = waveValue + wavelengthRatio * 0.3;
                }
            }

            return data;
        }
        private void CreateWaveformSurface()
        {
            if (CurrentSpectrum == null || PlotControl == null) return;

            PlotControl.Plot.Clear();

            // 生成波形曲面数据
            var (x, y, z) = GenerateWaveformSurfaceData(CurrentSpectrum);

            // 创建3D曲面
            //var surface = PlotControl.Plot.Add.Surface(x, y, z);

            //// 设置可见光谱颜色映射
            //surface.Colormap = CreateVisibleSpectrumColormap();

            //// 设置光照效果（可选）
            //surface.Lighting = true;
            //surface.Shading = ScottPlot.Plottables.SurfaceShading.HighQuality;

            PlotControl.Plot.Title("波长渐变波浪曲面");
            PlotControl.Plot.Axes.AutoScale();
            PlotControl.Refresh();
        }

        private (double[] x, double[] y, double[,] z) GenerateWaveformSurfaceData(SpectraDataViewModel spectrum)
        {
            int xPoints = spectrum.Wavelengths.Length;
            int yPoints = 50; // Y方向的分辨率

            double[] x = spectrum.Wavelengths;
            double[] y = Enumerable.Range(0, yPoints).Select(i => (double)i / (yPoints - 1) * 2 - 1).ToArray();
            double[,] z = new double[yPoints, xPoints];

            // 创建波浪曲面
            for (int i = 0; i < yPoints; i++)
            {
                for (int j = 0; j < xPoints; j++)
                {
                    // 基础波形 + 随Y变化的衰减
                    double baseIntensity = spectrum.Intensities[j];
                    double yFactor = Math.Exp(-Math.Pow(y[i] * 2, 2)); // 高斯衰减
                    double waveModulation = Math.Sin(y[i] * 8) * 0.1; // 波浪效果

                    z[i, j] = baseIntensity * yFactor + waveModulation;
                }
            }

            return (x, y, z);
        }
        private void CreateWaveformHeatmap()
        {
            if (CurrentSpectrum == null || PlotControl == null) return;

            PlotControl.Plot.Clear();

            // 生成波浪形状的2D数据
            var intensity2D = GenerateWaveformHeatmapData(CurrentSpectrum);

            // 创建热图
            var heatmap = PlotControl.Plot.Add.Heatmap(intensity2D);
            heatmap.Colormap = CreateVisibleSpectrumColormap();

            // 设置平滑插值
            //heatmap.Interpolation = ScottPlot.Interpolation.Bicubic;
            heatmap.Smooth = true;

            PlotControl.Plot.Title("2.5D波长渐变波浪图");
            PlotControl.Plot.Axes.AutoScale();
            PlotControl.Refresh();
        }
        private ScottPlot.IColormap CreateVisibleSpectrumColormap()
        {
            // 创建可见光谱颜色映射
            ScottPlot.Color[] colors = {
            new(0x75, 0x00, 0x85), // 紫色
            new(0x00, 0x00, 0xFF), // 蓝色
            new(0x00, 0xFF, 0x00), // 绿色
            new(0xFF, 0xFF, 0x00), // 黄色
            new(0xFF, 0x00, 0x00)  // 红色
        };

            return new ScottPlot.Colormaps.Custom(colors);
        }
        private double[,] GenerateWaveformHeatmapData(SpectraDataViewModel spectrum)
        {
            int width = spectrum.Wavelengths.Length;
            int height = 100;
            double[,] data = new double[height, width];

            for (int y = 0; y < height; y++)
            {
                double yNormalized = (double)y / height;

                for (int x = 0; x < width; x++)
                {
                    // 创建波浪效果：基础强度 + 正弦波调制
                    double baseValue = spectrum.Intensities[x];
                    double wave = Math.Sin(yNormalized * Math.PI * 4) * 0.3; // 波浪形状
                    double envelope = Math.Sin(yNormalized * Math.PI) * 0.5 + 0.5; // 包络线

                    data[y, x] = baseValue * envelope + wave;
                }
            }

            return data;
        }
        private void UpdatePlotWithSimpleSegments()
        {
            PlotControl.Plot.Clear();

            for (int i = 0; i < CurrentSpectrum.Wavelengths.Length - 1; i++)
            {
                double x1 = CurrentSpectrum.Wavelengths[i];
                double x2 = CurrentSpectrum.Wavelengths[i + 1];
                double y1 = CurrentSpectrum.Intensities[i];
                double y2 = CurrentSpectrum.Intensities[i + 1];

                var line = PlotControl.Plot.Add.Line(x1, y1, x2, y2);

                double normalizedWavelength = (x1 - 380) / (780 - 380);
                var color = VisibleSpectrumColormap.GetColor(normalizedWavelength);
                line.Color = color;
                line.LineWidth = 2;
            }

            PlotControl.Refresh();
        }
        private void UpdatePlotWithCustomRendering()
        {
            if (CurrentSpectrum == null || PlotControl == null) return;

            PlotControl.Plot.Clear();

            // 创建自定义绘图对象
            var coloredLine = new WaveformSurfacePlot(
                CurrentSpectrum.Wavelengths,
                CurrentSpectrum.Intensities,
                VisibleSpectrumColormap
            );

            PlotControl.Plot.Add.Plottable(coloredLine);
            PlotControl.Plot.Axes.AutoScale();
            PlotControl.Refresh();
        }
        private void UpdatePlotWithGradient()
        {
            if (CurrentSpectrum == null) return;

            PlotControl.Plot.Clear();

            // 创建渐变背景 - 上边界为光谱曲线形状
            int rows = 100; // 渐变行数
            double[,] intensity2D = new double[rows, CurrentSpectrum.Wavelengths.Length];

            double maxIntensity = CurrentSpectrum.Intensities.Max();
            double minIntensity = CurrentSpectrum.Intensities.Min();

            for (int x = 0; x < CurrentSpectrum.Wavelengths.Length; x++)
            {
                double normalizedY = (CurrentSpectrum.Intensities[x] - minIntensity) / (maxIntensity - minIntensity);
                int curveHeight = (int)(normalizedY * rows);

                for (int y = 0; y < rows; y++)
                {
                    if (y <= curveHeight)
                    {
                        // 在曲线下方填充渐变
                        double normalizedWavelength = (CurrentSpectrum.Wavelengths[x] - 380) / (780 - 380);
                        double intensityFactor = (double)y / curveHeight; // 越靠近曲线强度越高
                        intensity2D[rows - 1 - y, x] = normalizedWavelength * intensityFactor;
                    }
                    else
                    {
                        intensity2D[rows - 1 - y, x] = double.NaN;
                    }
                }
            }

            var heatmap = PlotControl.Plot.Add.Heatmap(intensity2D);
            heatmap.Colormap = VisibleSpectrumColormap;

            // 绘制光谱曲线
            var scatter = PlotControl.Plot.Add.Scatter(CurrentSpectrum.Wavelengths, CurrentSpectrum.Intensities);
            scatter.LineWidth = 2;
            scatter.Color = ScottPlot.Colors.White;

            PlotControl.Plot.Axes.AutoScale();
            PlotControl.Refresh();
        }
        private void UpdatePlotWithGradient1()
        {
            if (CurrentSpectrum == null) return;

            PlotControl.Plot.Clear();

            // 创建渐变背景 - 上边界为光谱曲线形状
            int rows = 50; // 渐变行数
            double[,] intensity2D = new double[rows, CurrentSpectrum.Wavelengths.Length];

            double maxIntensity = CurrentSpectrum.Intensities.Max();
            double minIntensity = CurrentSpectrum.Intensities.Min();

            for (int x = 0; x < CurrentSpectrum.Wavelengths.Length; x++)
            {
                double normalizedY = (CurrentSpectrum.Intensities[x] - minIntensity) / (maxIntensity - minIntensity);
                int curveHeight = (int)(normalizedY * rows);

                for (int y = 0; y < rows; y++)
                {
                    if (y <= curveHeight)
                    {
                        // 在曲线下方填充渐变
                        double normalizedWavelength = (CurrentSpectrum.Wavelengths[x] - 380) / (780 - 380);
                        double intensityFactor = (double)y / curveHeight; // 越靠近曲线强度越高
                        intensity2D[rows - 1 - y, x] = normalizedWavelength * intensityFactor;
                    }
                    else
                    {
                        intensity2D[rows - 1 - y, x] = double.NaN;
                    }
                }
            }

            var heatmap = PlotControl.Plot.Add.Heatmap(intensity2D);
            heatmap.Colormap = VisibleSpectrumColormap;

            // 绘制光谱曲线
            var scatter = PlotControl.Plot.Add.Scatter(CurrentSpectrum.Wavelengths, CurrentSpectrum.Intensities);
            scatter.LineWidth = 2;
            scatter.Color = ScottPlot.Colors.White;

            PlotControl.Plot.Axes.AutoScale();
            PlotControl.Refresh();
        }
        // 在MainViewModel中添加渐变显示方法
        private void UpdatePlotWithGradient3()
        {
            if (CurrentSpectrum == null) return;

            PlotControl.Plot.Clear();

            // 创建渐变背景（可选）
            double[,] intensity2D = new double[100, CurrentSpectrum.Wavelengths.Length];
            for (int i = 0; i < intensity2D.GetLength(0); i++)
            {
                for (int j = 0; j < intensity2D.GetLength(1); j++)
                {
                    // 根据波长映射颜色强度
                    double normalizedWavelength = (CurrentSpectrum.Wavelengths[j] - 380) / (780 - 380);
                    intensity2D[i, j] = normalizedWavelength;
                }
            }

            var heatmap = PlotControl.Plot.Add.Heatmap(intensity2D);
            heatmap.Colormap = VisibleSpectrumColormap;
            heatmap.Opacity = 0.7; // 设置透明度

            //// 绘制主要光谱曲线
            //var scatter = PlotControl.Plot.Add.Scatter(CurrentSpectrum.Wavelengths, CurrentSpectrum.Intensities);
            //scatter.LineWidth = 3;
            //scatter.Color = ScottPlot.Colors.Black;

            PlotControl.Plot.Axes.AutoScale();
            PlotControl.Refresh();
        }
        // 在MainViewModel中更新绘图方法
        private void UpdatePlot()
        {
            if (CurrentSpectrum == null || PlotControl == null) return;

            // 清除之前的绘图
            PlotControl.Plot.Clear();

            // 绘制光谱曲线
            var scatter = PlotControl.Plot.Add.Scatter(
                CurrentSpectrum.Wavelengths,
                CurrentSpectrum.Intensities
            );

            scatter.LineWidth = 3;
            scatter.Color = ScottPlot.Colors.SteelBlue;

            // 设置图表样式
            PlotControl.Plot.Title("实时光谱图");
            PlotControl.Plot.XLabel("波长 (nm)");
            PlotControl.Plot.YLabel("强度");
            PlotControl.Plot.Axes.AutoScale();

            // 刷新显示
            PlotControl.Refresh();
        }
        
        private static readonly ILog log = LogManager.GetLogger(nameof(CVSpectrumAnalyzer));
       
        private void InitializeIVLCameraModel()
        {
            IVLCameraViewModel viewModel = new IVLCameraViewModel();
            IVLCamera_viewModel = viewModel;
            IVLCameraMeasurements = viewModel.Measurements;
           
            IVLCamera_viewModel.PropertyChanged += OnIVLCameraPropertyChanged;
        }
     
        

        
        private void OnIVLCameraPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IVLCameraViewModel.ImageSrc))
            {
                OnPropertyChanged(nameof(IVLCameraImageSrc));
            }
        }
        
       
        //电压/电流
        private void InitializeIVPlotModel()
        {
            IVViewModel viewModel = new IVViewModel();
            IV_viewModel = viewModel;
            IVPlotModel = viewModel.PlotModel;
            IVMeasurements = viewModel.Measurements;

        }

        //电流/亮度
        private void InitializeILPlotModel()
        {
            ILViewModel viewModel = new ILViewModel();
            IL_viewModel = viewModel;
            ILPlotModel = viewModel.PlotModel;
            ILMeasurements = viewModel.Measurements;
        }
        //电压/亮度
        private void InitializeVLPlotModel()
        {
            VLViewModel viewModel = new VLViewModel();
            VL_viewModel = viewModel;
            VLMeasurements = viewModel.Measurements;
            VLPlotModel = viewModel.PlotModel;
        }
        //光谱
        private void InitializePlotModel()
        {
            PlotModel = new PlotModel
            {
                Title = (string)Application.Current.FindResource("Sp.SpectralCurve"),
                TitleFontSize = 14
            };

            // 设置X轴（波长）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = (string)Application.Current.FindResource("Sp.Wavelength"),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
            };
            AxisCfg(xAxis, AxisX);

            // 设置Y轴（强度）
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = (string)Application.Current.FindResource("Sp.Spectral"),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
            };
            AxisCfg(yAxis, AxisY);

            PlotModel.Axes.Add(xAxis);
            PlotModel.Axes.Add(yAxis);

            Wavelengths = new float[10000];
            for (int i = 0; i < 10000; i++)
            {
                Wavelengths[i] = 380 + i/10.0f; 
            }
        }
        private void ResetPlotView()
        {
            // 清除所有系列
            PlotModel.Series.Clear();

            // 清除所有标注
            PlotModel.Annotations.Clear();

            // 重置轴范围到默认值
            ResetAxisToDefault();

            // 强制重绘
            PlotModel.InvalidatePlot(true);
        }
        private void AxisCfg(LinearAxis axis, PlotAxesCfg axisCfg)
        {
            axis.Minimum = axisCfg.DefaultMin;
            axis.Maximum = axisCfg.DefaultMax;
            axis.MaximumRange = axisCfg.DefaultMaxRange;
            axis.ExtraGridlines = null; // 清除特殊网格线

        }
        private void ResetAxisToDefault()
        {
            var xAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as LinearAxis;
            var yAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) as LinearAxis;

            if (xAxis != null) AxisCfg(xAxis, AxisX);

            if (yAxis != null) AxisCfg(yAxis, AxisY);
        }
        public void UpdateSpectrumData(double[] wavelengths, double[] intensities)
        {
            var lineSeries = new LineSeries
            {
                Title = (string)Application.Current.FindResource("Sp.Spectral"),
                Color = OxyColors.Blue,
                StrokeThickness = 1.5
            };

            for (int i = 0; i < wavelengths.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(wavelengths[i], intensities[i]));
            }

            PlotModel.Series.Clear();
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true);
        }
        private void ResetAndUpdateChart()
        {
            // 完全重置图表
            ResetPlotView();

            // 如果有选中的测量数据，则更新图表
            if (SelectedMeasurement != null)
            {
                UpdateChartFromSelectedMeasurement();
            }
            else
            {
                // 如果没有选中数据，显示空图表提示
                ShowEmptyChartMessage();
            }
        }
        private void ShowEmptyChartMessage()
        {
            // 添加提示文本标注
            var textAnnotation = new TextAnnotation
            {
                Text = "请选择测量数据以显示光谱曲线",
                TextPosition = new DataPoint((AxisX.DefaultMin + AxisX.DefaultMax) / 2, (AxisY.DefaultMin + AxisY.DefaultMax) / 2),
                TextColor = OxyColors.Gray,
                FontSize = 16,
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle
            };

            PlotModel.Annotations.Add(textAnnotation);
            PlotModel.InvalidatePlot(true);
        }
        private void UpdateChartFromSelectedMeasurement()
        {
            if (SelectedMeasurement == null) return;

            var lineSeries = new LineSeries
            {
                Title = $"测量 {SelectedMeasurement.Meas_Id}",
                Color = OxyColors.Blue,
                StrokeThickness = 1.5
            };

            for (int i = 0; i < SelectedMeasurement.Wavelengths.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(
                    SelectedMeasurement.Wavelengths[i],
                    SelectedMeasurement.Intensities[i]));
            }

            PlotModel.Series.Clear();
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true);
            //
            if (_spectralCtrl != null)
            {
                _spectralCtrl.SpectralData.SetData(SelectedMeasurement.Wavelengths, SelectedMeasurement.Intensities);
                _spectralCtrl.InvalidateVisual();
            }
        }
        private void Clear()
        {
            PlotModel.Series.Clear();
            Measurements.Clear();
            IL_viewModel.Clear();
            IV_viewModel.Clear();
            VL_viewModel.Clear();
            IVLCamera_viewModel.Clear();
            IVLCameraImageSrc = null;
        }
        public void LoadData(string serialNumber, bool isIVLCameraEnabled)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                ClearAllDisplays(); // 清空所有图像和数据
                return;
            }
            Clear();
            if (string.IsNullOrEmpty(serialNumber)) return;
            if (isIVLCameraEnabled) LoadCameraData(serialNumber); 
            else LoadSpectrumData(serialNumber);
        }

        private void ClearAllDisplays()
        {
            // 清空图表
            PlotModel.Series.Clear();
            PlotModel.Annotations.Clear();
            ResetAxisToDefault();
            PlotModel.InvalidatePlot(true);

            // 清空总览图
            OverviewSpectralPlotModel.Series.Clear();
            OverviewSpectralPlotModel.Annotations.Clear();
            OverviewIVPlotModel.Series.Clear();
            OverviewIVPlotModel.Annotations.Clear();
            OverviewILPlotModel.Series.Clear();
            OverviewILPlotModel.Annotations.Clear();
            OverviewVLPlotModel.Series.Clear();
            OverviewVLPlotModel.Annotations.Clear();
            // 刷新总览图
            OverviewSpectralPlotModel.InvalidatePlot(true);
            OverviewIVPlotModel.InvalidatePlot(true);
            OverviewILPlotModel.InvalidatePlot(true);
            OverviewVLPlotModel.InvalidatePlot(true);

            // 清空所有数据集合
            Measurements.Clear();
            ILMeasurements.Clear();
            IVMeasurements.Clear();
            VLMeasurements.Clear();
            IVLCameraMeasurements.Clear();
            SpectralGridItems?.Clear();

            // 清空选中状态
            SelectedMeasurement = null;
            SelectedCameraMeasurement = null;

            // 清空IVLCamera图像
            IVLCameraImageSrc = null;

            // 清空ScottPlot控件
            if (PlotControl != null)
            {
                PlotControl.Plot.Clear();
                PlotControl.Refresh();
            }

            // 清空SpectrumControl
            if (_spectralCtrl != null)
            {
                _spectralCtrl.SpectralData.SetData(new float[0], new float[0]);
                _spectralCtrl.InvalidateVisual();
            }
        }

        private void LoadCameraData(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                ClearAllDisplays();
                return;
            }
            var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
            if (results == null || results.Count == 0) return;
            List<VScgdAlgorithmResultMaster> lv_results = new List<VScgdAlgorithmResultMaster>();
            List<float> il_results = new List<float>();
            foreach (var result in results)
            {
                AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;
                if (resultType == AlgorithmResultType.POI_Y)
                {
                    lv_results.Add(result);
                }
                else if (resultType == AlgorithmResultType.PoiAnalysis)
                {
                    var details = AlgResultService.GetCommDetailResult(result.Id);
                    if (details != null && details.Count == 1)
                    {
                        DetailResult_CommFile_V2 detailResult_Comm = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(details[0].Result);
                        if (File.Exists(detailResult_Comm.ResultFileName))
                        {
                            PoiAnalysis<PoiAnalysis_Avg_Result_Data> poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis<PoiAnalysis_Avg_Result_Data>>(File.ReadAllText(detailResult_Comm.ResultFileName));
                            il_results.Add((float)poiAnalysis.result.average_lum);
                        }
                    }
                }
            }
            IL_viewModel.LoadData(lv_results, il_results);
            IV_viewModel.LoadData(serialNumber);
            VL_viewModel.LoadData(lv_results, il_results);
            IVLCamera_viewModel.LoadData(lv_results, il_results);
            InitializeOverviewSeries();
        }
        private static string ArrayToString(float[] array)
        {
            if (array == null || array.Length == 0)
                return ""; // 空数组返回空字符串
            return string.Join(",", array); // 用逗号拼接元素
        }
        public void LoadSpectrumData(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                ClearAllDisplays();
                return;
            }
            var results = SpectrumResultService.LoadResultByBatchCode(DeviceCode, serialNumber);
            if (results == null || results.Count == 0) return;

            IL_viewModel.LoadData(results);
            IV_viewModel.LoadData(serialNumber);
            VL_viewModel.LoadData(results);
            //
            int n = 1;
            foreach (var result in results)
            {
                 
                var measurement = new SpectrumMeasurement(n++)
                {
                    Timestamp = result.CreateDate,
                    Meas_Id = result.BatchCode,
                    Voltage = (float)result.VResult,
                    Current = (float)result.IResult,
                    Luminance = (float)result.FPh / 1,
                    // Luminance = (float)result.FPh,
                    IP = Math.Round((decimal)(result.FIp / 65535 * 100), 2).ToString() + "%",
                    // IP = ,
                    Blue = (float)result.FBR ,
                    //Blue = (float)Math.Round(sum1 / sum2 * 100 , 2),
                   
                    CIE_x = (float)result.Fx,
                    CIE_y = (float)result.Fy,
                    CIE_u = (float)result.Fu,
                    CIE_v = (float)result.Fv,
                    CCT = (float)result.FCCT,
                    PeakWavelength = (float)result.FLd,
                    fPur= (float)result.FPur,
                    //fPuPercent = $"{Math.Round((decimal)(result.FPur * 100), 2)}%",
                    PeakIntensity = (float)result.FLp,
                    FHW= (float)result.FHW,
                    Intensities = JsonConvert.DeserializeObject<float[]>(result.FPL),
                    Wavelengths = Wavelengths,
                    fPlambda= (float)result.FPlambda
                    
                };
                double sum1 = 0, sum2 = 0;
                for (int i = 35; i <= 75; i++)
                    sum1 += measurement.Intensities[i * 10];
                for (int i = 20; i <= 120; i++)
                    sum2 += measurement.Intensities[i * 10];
                measurement.Blue = (float)Math.Round(sum1 / sum2 * 100, 2);
                Measurements.Add(measurement);
                
                
            }
            
            if (Measurements.Any())
            {
                SelectedMeasurement = Measurements.First();
                if (_spectralCtrl != null)
                {
                    _spectralCtrl.SpectralData.SetData(Wavelengths, SelectedMeasurement.Intensities);
                    _spectralCtrl.InvalidateVisual();
                }
            }
            // 数据加载后，根据“显示所有”状态更新图表
            UpdateChartByShowAllState();
            // 子Tab数据加载完成后，重新初始化总览图Series
            InitializeOverviewSeries();

        }
        #region 新增：高亮相关辅助方法
        // 切换选中曲线高亮样式
        private void UpdateSelectedCurveHighlight()
        {
            if (_spectralSeriesCache.Count == 0 || !Measurements.Any())
                return;

            // 1. 重置所有曲线为未选中样式
            foreach (var (measNo, series) in _spectralSeriesCache)
            {
                // 找到对应的测量数据，获取颜色索引
                var measurement = Measurements.FirstOrDefault(m => m.No == measNo);
                if (measurement == null)
                    continue;

                int colorIndex = Measurements.IndexOf(measurement);
                // 未选中颜色（循环使用）
                var unselectedColors = new[]
                {
                    OxyColor.FromAColor(115, OxyColors.Blue),
                    OxyColor.FromAColor(115, OxyColors.Green),
                    OxyColor.FromAColor(115, OxyColors.Purple),
                    OxyColor.FromAColor(115, OxyColors.Orange),
                    OxyColor.FromAColor(115, OxyColors.Teal),
                    OxyColor.FromAColor(115, OxyColors.Magenta),
                    OxyColor.FromAColor(115, OxyColors.Gold),
                    OxyColor.FromAColor(115, OxyColors.Cyan)
                };

                // 未选中样式
                series.Color = unselectedColors[colorIndex % unselectedColors.Length];
                series.StrokeThickness = 1.2;
                series.MarkerType = MarkerType.None;
                series.Title = $"No:{measNo} | {measurement.Timestamp:yyyy-MM-dd HH:mm}";
            }

            // 2. 高亮当前选中曲线
            if (SelectedMeasurement != null && _spectralSeriesCache.ContainsKey(SelectedMeasurement.No))
            {
                var selectedSeries = _spectralSeriesCache[SelectedMeasurement.No];
                // 选中样式
                selectedSeries.Color = OxyColors.Red;
                selectedSeries.StrokeThickness = 2.0;
              
                selectedSeries.Title = $"No:{SelectedMeasurement.No} | {SelectedMeasurement.Timestamp:yyyy-MM-dd HH:mm}（选中）";

                // 移到顶层
                BringSeriesToFront(SelectedMeasurement.No);
            }

            PlotModel.InvalidatePlot(true); // 实时刷新图表
        }

        // 将指定曲线移到顶层（后添加的曲线在OxyPlot中优先级更高）
        private void BringSeriesToFront(int measNo)
        {
            if (!_spectralSeriesCache.ContainsKey(measNo))
                return;

            var series = _spectralSeriesCache[measNo];
            // 先移除，再重新添加（触发顶层显示）
            PlotModel.Series.Remove(series);
            PlotModel.Series.Add(series);
        }

        // 自动调整轴范围（适配所有数据，避免数据贴边）
     /*/   private void AutoAdjustAxisRange()
        {
            if (!PlotModel.Series.Any())
                return;

            // 收集所有有效数据点的X/Y值
            var allX = new List<double>();
            var allY = new List<double>();

            foreach (var series in PlotModel.Series.OfType<LineSeries>())
            {
                allX.AddRange(series.Points.Select(p => p.X));
                allY.AddRange(series.Points.Select(p => p.Y));
            }

            if (!allX.Any() || !allY.Any())
                return;

            // 获取X轴（波长）和Y轴（强度）
            var xAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as LinearAxis;
            var yAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) as LinearAxis;

            if (xAxis != null)
            {
                double xMin = allX.Min();
                double xMax = allX.Max();
                double xMargin = (xMax - xMin) * 0.05; // X轴5%边距
                xAxis.Minimum = xMin - xMargin;
                xAxis.Maximum = xMax + xMargin;
            }

            if (yAxis != null)
            {
                double yMin = Math.Max(0, allY.Min() * 0.9); // Y轴最低为0，10%边距
                double yMax = allY.Max() * 1.1; // Y轴10%边距
                yAxis.Minimum = yMin;
                yAxis.Maximum = yMax;
            }
        }*/
        #endregion
        public void ClearResult()
        {
            Clear();
        }

        public void UpdateImage()
        {
            OnPropertyChanged(nameof(IVLCameraImageSrc));
        }

        public void SetSpectrumCtrl(SpectrumControl spectralCtrl)
        {
            this._spectralCtrl = spectralCtrl;
        }
        // 新增：控制右侧DataGrid显示/隐藏的勾选状态
        private bool _isShowSpectralDetail;
        public bool IsShowSpectralDetail
        {
            get => _isShowSpectralDetail;
            set
            {
                _isShowSpectralDetail = value;
                OnPropertyChanged();
                // 勾选状态变化时，更新右侧DataGrid数据
                if (value && SelectedMeasurement != null)
                {
                    UpdateSpectralGridData(SelectedMeasurement);
                }
                else
                {
                    SpectralGridItems?.Clear();
                    
                }
            }
        }

        private void UpdateSpectralGridData(SpectrumMeasurement measurement)
        {
            if (string.IsNullOrWhiteSpace(measurement?.Meas_Id) || measurement == null)
            {
                SpectralGridItems?.Clear();
                return;
            }
            if (measurement == null || measurement.Wavelengths == null || measurement.Intensities == null)
            {
                SpectralGridItems?.Clear();
                return;
            }

            var gridItems = new ObservableCollection<SpectralGridItem>();
            int totalPoints = measurement.Wavelengths.Length;

            // 遍历波长数组，每10个点取1个（步长=10）
            for (int i = 0; i < totalPoints; i += 10)
            {
                // 波长值强制转换为整数（380.0→380，381.0→381）
                int wavelengthInt = (int)measurement.Wavelengths[i];

                // 只保留380~780nm范围内的有效数据
                if (wavelengthInt < 380 || wavelengthInt > 780)
                    continue;

                // 相对光谱：负强度转为0，保留4位小数
                float relative = measurement.Intensities[i] > 0 ? (float)Math.Round(measurement.Intensities[i], 4) : 0f;
                // 绝对光谱：相对强度 × fPlambda，保留4位小数
                float absolute = (float)Math.Round(relative * measurement.fPlambda, 4);

                gridItems.Add(new SpectralGridItem
                {
                    Wavelength = wavelengthInt,
                    RelativeSpectrum = relative,
                    AbsoluteSpectrum = absolute
                });
            }

            SpectralGridItems = gridItems;
        }

        // 新增：右侧DataGrid的数据源
        private ObservableCollection<SpectralGridItem> _spectralGridItems;
        public ObservableCollection<SpectralGridItem> SpectralGridItems
        {
            get => _spectralGridItems;
            set => SetProperty(ref _spectralGridItems, value);
        }

        // 新增：光谱详情数据模型（对应右侧DataGrid列）
        public class SpectralGridItem
        {
            public double Wavelength { get; set; } // 波长(nm)
            public float RelativeSpectrum { get; set; } // 相对光谱
            public float AbsoluteSpectrum { get; set; } // 绝对光谱
        }

    }
}
