
using ColorVision.Core.Entities;
using CVDB.Services.Spectrum;
using CVWaferProber.Core.Events;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using log4net;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WaferComm.Core;
using static CVWPFSpectrometerCtrl.ViewModels.CVSpectrumViewModel;

namespace CVWPFSpectrometerCtrl.ViewModels
{

    public class CVEQEViewModel : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CVEQEViewModel));
        private static CVEQEViewModel _instance;
        private static readonly object _locker = new();
        public static CVEQEViewModel GetInstance() { lock (_locker) { return _instance ??= new CVEQEViewModel(); } }


        #region 核心字段
        private PlotModel _eqePlotModel;
        private SpectrumMeasurement _selectedMeasurement;
        private ObservableCollection<SpectrumMeasurement> _measurements;
        private bool _isShowAllEQEData = true;
        private bool _isEQEMeasured;
        private SolidColorBrush _eqeLineColor = new SolidColorBrush(Colors.Red);
        private Dictionary<int, LineSeries> _eqeSeriesCache = new Dictionary<int, LineSeries>();
        public float[] Wavelengths;

        // 轴默认配置
        private readonly PlotAxesCfg _axisX = new PlotAxesCfg { DefaultMin = 360, DefaultMax = 800, DefaultMaxRange = 500 };
        private readonly PlotAxesCfg _axisY = new PlotAxesCfg { DefaultMin = 0, DefaultMax = 1.0f, DefaultMaxRange = 1.1f };
        #endregion

        #region 公共属性
        // EQE图表
        public PlotModel EQEPlotModel
        {
            get => _eqePlotModel;
            set => SetProperty(ref _eqePlotModel, value);
        }

        // EQE测量数据集合
        public ObservableCollection<SpectrumMeasurement> Measurements
        {
            get => _measurements;
            set => SetProperty(ref _measurements, value);
        }

        // 选中的EQE测量数据
        public SpectrumMeasurement SelectedMeasurement
        {
            get => _selectedMeasurement;
            set
            {
                if (SetProperty(ref _selectedMeasurement, value))
                {
                    if (_isShowAllEQEData)
                    {
                        UpdateEQESelectedCurveHighlight();
                    }
                    else
                    {
                        ResetEQEPlotView();
                        UpdateEQEChartFromSelectedMeasurement();
                    }
                }
            }
        }

        // 是否显示所有EQE数据
        public bool IsShowAllEQEData
        {
            get => _isShowAllEQEData;
            set
            {
                _isShowAllEQEData = value;
                OnPropertyChanged();
                UpdateEQEChartByShowAllState();
            }
        }
        // 1. 新增：绑定DataGrid选中项的属性
        private SpectrumMeasurement _selectedEQERow;
        public SpectrumMeasurement SelectedEQERow
        {
            get => _selectedEQERow;
            set
            {
                if (SetProperty(ref _selectedEQERow, value))
                {
                    OnPropertyChanged(nameof(SelectedEQERow));
                    if (IsShowAllEQEData)
                    {
                        // 显示所有数据时：置顶+高亮选中曲线
                        UpdateEQESelectedCurveHighlight();
                    }
                    else
                    {
                        // 未显示所有数据时：清空图表，仅显示当前选中项的曲线
                        ResetEQEPlotView(); // 先清空图表
                        if (value != null)
                        {
                            DrawSingleEQECurve(value); // 仅绘制选中项的曲线
                        }
                        else
                        {
                            ShowEQEEmptyChartMessage(); // 无选中项时显示提示
                        }
                    }
                }
            }
        }
        // 新增：未显示所有数据时，重置EQE图表并显示单条选中曲线
        private void ResetAndUpdateEQEChart()
        {
            // 完全重置EQE图表
            ResetEQEPlotView();

            if (SelectedEQERow != null)
            {
                // 仅绘制选中行的EQE曲线
                UpdateEQEChartFromSelectedMeasurement();
            }
            else
            {
                // 无选中项时显示空提示
                ShowEQEEmptyChartMessage();
            }
        }
        // 新增：仅绘制单条EQE曲线的方法
        private void DrawSingleEQECurve(SpectrumMeasurement selectedItem)
        {
            if (selectedItem == null) return;

            // 创建当前选中项的曲线
            var lineSeries = new LineSeries
            {
                Title = $"EQE-{selectedItem.No}",
                Color = ConvertToOxyColor(EQELineColor),
                StrokeThickness = 2.0,
                IsVisible = true
            };

            // 填充选中项的波长+强度数据
            for (int i = 0; i < selectedItem.Wavelengths.Length; i++)
            {
                if (!float.IsNaN(selectedItem.Intensities[i]) && !float.IsInfinity(selectedItem.Intensities[i]))
                {
                    lineSeries.Points.Add(new DataPoint(
                        selectedItem.Wavelengths[i],
                        selectedItem.Intensities[i]
                    ));
                }
            }

            // 添加到EQE图表（此时图表已被清空）
            EQEPlotModel.Series.Add(lineSeries);
            EQEPlotModel.InvalidatePlot(true); // 刷新图表
        }

        // EQE曲线颜色
        public SolidColorBrush EQELineColor
        {
            get => _eqeLineColor;
            set
            {
                if (_eqeLineColor != value)
                {
                    _eqeLineColor = value;
                    OnPropertyChanged(nameof(EQELineColor));
                    OxyColor newOxyColor = ConvertToOxyColor(value);

                    if (_isShowAllEQEData && SelectedMeasurement != null)
                    {
                        UpdateSelectedEQECurveColor(newOxyColor);
                    }
                    else
                    {
                        UpdateEQEChartLineColor();
                    }
                }
            }
        }

        // EQE测量完成标记
        public bool IsEQEMeasured
        {
            get => _isEQEMeasured;
            set
            {
                _isEQEMeasured = value;
                if (value)
                {
                    // 可在此触发自动导出
                }
            }
        }

        // 导出命令
        public ICommand EQEExportCommand { get; }
        public string DeviceCode { get; set; } = "DEV.Spectrum.Default";
        #endregion

        #region 构造函数
        public CVEQEViewModel()
        {
            // 初始化波长数组（380~780nm，步长0.1）
            Wavelengths = new float[4001];
            for (int i = 0; i < 4001; i++)
            {
                Wavelengths[i] = 380 + i / 10.0f;
            }
            // 初始化数据集合
            Measurements = new ObservableCollection<SpectrumMeasurement>();

            // 初始化EQE图表
            InitializeEQEPlotModel();

            // 初始化导出命令
            EQEExportCommand = new RelayCommand(_ => ExportEQEData());
        }
        #endregion

        #region EQE图表初始化
        private void InitializeEQEPlotModel()
        {
            EQEPlotModel = new PlotModel
            {
                Title = "EQE光谱曲线",
                TitleFontSize = 14
            };

            // X轴（波长）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "波长(nm)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = _axisX.DefaultMin,
                Maximum = _axisX.DefaultMax,
                MaximumRange = _axisX.DefaultMaxRange
            };

            // 设置Y轴（强度）
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = (string)System.Windows.Application.Current.FindResource("Sp.Spectral"),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
            };

            EQEPlotModel.Axes.Add(xAxis);
            EQEPlotModel.Axes.Add(yAxis);
        }
        #endregion

        #region EQE数据加载
        public void LoadEQEData(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                ClearAllDisplays();
                return;
            }

            // 从数据库加载EQE数据
            var results = SpectrumResultService.LoadEQEResultByBatchCode(DeviceCode, serialNumber);
            if (results == null || results.Count == 0) return;

            // 清空原有数据
            Measurements.Clear();
            _eqeSeriesCache.Clear();

            // 解析数据并添加到集合
            int n = 1;
            foreach (var result in results)
            {
                var measurement = new SpectrumMeasurement(n++)
                {
                    Timestamp = result.CreateDate,
                    Meas_Id = result.BatchCode,
                    Voltage = (float)result.VResult,
                    Current = (float)result.IResult,
                    LuminousFlux= (float)result.LuminousFlux,
                    EQE= (float)result.Eqe,
                    LuminousEfficacy= (float)result.LuminousEfficacy,
                    IP = Math.Round((decimal)(result.FIp / 65535 * 100), 2).ToString() + "%",
                    Blue = (float)result.FBR,
                    CIE_x = (float)result.Fx,
                    CIE_y = (float)result.Fy,
                    CIE_u = (float)result.Fu,
                    CIE_v = (float)result.Fv,
                    CCT = (float)result.FCCT,
                    PeakWavelength = (float)result.FLd,
                    fPur = (float)result.FPur,
                    PeakIntensity = (float)result.FLp,
                    FHW = (float)result.FHW,
                    Intensities = GetIntensitiesFromFileOrOriginal(result),
                    Wavelengths = Wavelengths,
                    fPlambda = (float)result.FPlambda,
                    RowLineColor = ConvertToOxyColor(EQELineColor)
                };

                // 计算蓝光占比
                double sum1 = 0, sum2 = 0;
                for (int i = 35; i <= 75; i++) sum1 += measurement.Intensities[i * 10];
                for (int i = 20; i <= 120; i++) sum2 += measurement.Intensities[i * 10];
                measurement.Blue = (float)Math.Round(sum1 / sum2 * 100, 2);

                Measurements.Add(measurement);
            }

            // 选中第一条数据并更新图表
            if (Measurements.Any())
            {
                SelectedMeasurement = Measurements.First();
                UpdateEQEChartByShowAllState();
                IsEQEMeasured = true;
            }
        }

        // 读取强度数据（优先从文件，失败则用原始数据）
        private float[] GetIntensitiesFromFileOrOriginal(dynamic result)
        {
            if (!string.IsNullOrWhiteSpace(result.FPLFileName))
            {
                try
                {
                    if (File.Exists(result.FPLFileName))
                    {
                        string content = File.ReadAllText(result.FPLFileName, Encoding.UTF8);
                        var intensities = JsonConvert.DeserializeObject<float[]>(content);
                        if (intensities != null && intensities.Length > 0)
                            return intensities;
                    }
                }
                catch (Exception ex)
                {
                    log.Error("读取EQE强度文件失败", ex);
                }
            }

            return JsonConvert.DeserializeObject<float[]>(result.FPL) ?? Array.Empty<float>();
        }
        #endregion

        #region EQE图表更新
        // 根据显示状态更新图表
        private void UpdateEQEChartByShowAllState()
        {
            ResetEQEPlotView();

            if (_isShowAllEQEData)
            {
                DrawAllEQEMeasurementsInChart();
            }
            else
            {
                if (SelectedMeasurement != null)
                {
                    UpdateEQEChartFromSelectedMeasurement();
                }
                else
                {
                    ShowEQEEmptyChartMessage();
                }
            }
        }

        // 绘制所有EQE数据
        private void DrawAllEQEMeasurementsInChart()
        {
            if (!Measurements.Any())
            {
                ShowEQEEmptyChartMessage();
                return;
            }

            _eqeSeriesCache.Clear();
            EQEPlotModel.Series.Clear();

            // 未选中曲线颜色集合
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
                bool isSelected = SelectedMeasurement != null && measurement.No == SelectedMeasurement.No;
                var lineSeries = new LineSeries
                {
                    Title = $"EQE {SelectedMeasurement.Meas_Id}",
                    Color = isSelected ? measurement.RowLineColor : unselectedColors[colorIndex % unselectedColors.Length],
                    StrokeThickness = isSelected ? 2.0 : 1.5,
                    IsVisible = true
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

                _eqeSeriesCache.Add(measurement.No, lineSeries);
                EQEPlotModel.Series.Add(lineSeries);
                EQEPlotModel.Title = $"EQE {measurement.Meas_Id}";
                if (!isSelected) colorIndex++;
            }

            // 选中曲线置顶
            if (SelectedMeasurement != null && _eqeSeriesCache.ContainsKey(SelectedMeasurement.No))
            {
                BringEQESeriesToFront(SelectedMeasurement.No);
            }

            EQEPlotModel.InvalidatePlot(true);
        }

        // 从选中数据更新图表
        private void UpdateEQEChartFromSelectedMeasurement()
        {
            if (SelectedMeasurement == null) return;

            var lineSeries = new LineSeries
            {
                Title = $"EQE {SelectedMeasurement.Meas_Id}",
                Color = OxyColors.Blue,
                StrokeThickness = 1.5
            };

            for (int i = 0; i < SelectedMeasurement.Wavelengths.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(
                    SelectedMeasurement.Wavelengths[i],
                    SelectedMeasurement.Intensities[i]));
            }

            EQEPlotModel.Series.Clear();
            EQEPlotModel.Series.Add(lineSeries);
            EQEPlotModel.InvalidatePlot(true);

        }

        // 高亮选中曲线
        private void UpdateEQESelectedCurveHighlight()
        {
            if (_eqeSeriesCache.Count == 0 || SelectedMeasurement == null) return;

            // 重置所有曲线样式
            foreach (var (measNo, series) in _eqeSeriesCache)
            {
                bool isSelected = measNo == SelectedMeasurement.No;
                series.Color = isSelected ? ConvertToOxyColor(EQELineColor) : GetUnselectedEQEColor(measNo);
                series.StrokeThickness = isSelected ? 2.5 : 1.5;
            }

            // 选中曲线置顶
            BringEQESeriesToFront(SelectedMeasurement.No);
            EQEPlotModel.InvalidatePlot(true);
        }

        // 更新选中曲线颜色
        private void UpdateSelectedEQECurveColor(OxyColor newColor)
        {
            if (_eqeSeriesCache.TryGetValue(SelectedMeasurement.No, out LineSeries series))
            {
                series.Color = newColor;
                series.MarkerFill = newColor;
                series.MarkerStroke = newColor;
                EQEPlotModel.InvalidatePlot(true);
            }
        }

        // 更新所有曲线颜色
        private void UpdateEQEChartLineColor()
        {
            if (EQEPlotModel?.Series == null) return;

            OxyColor oxyColor = ConvertToOxyColor(EQELineColor);
            foreach (var series in EQEPlotModel.Series.OfType<LineSeries>())
            {
                series.Color = oxyColor;
                series.MarkerFill = oxyColor;
                series.MarkerStroke = oxyColor;
            }

            EQEPlotModel.InvalidatePlot(true);
        }

        // 选中曲线置顶
        private void BringEQESeriesToFront(int measNo)
        {
            if (_eqeSeriesCache.TryGetValue(measNo, out LineSeries series))
            {
                EQEPlotModel.Series.Remove(series);
                EQEPlotModel.Series.Add(series);
            }
        }

        // 获取未选中曲线颜色
        private OxyColor GetUnselectedEQEColor(int measNo)
        {
            var colors = new[]
            {
                OxyColor.FromAColor(115, OxyColors.Blue),
                OxyColor.FromAColor(115, OxyColors.Green),
                OxyColor.FromAColor(115, OxyColors.Purple),
                OxyColor.FromAColor(115, OxyColors.Orange)
            };
            return colors[measNo % colors.Length];
        }

        // 重置EQE图表
        private void ResetEQEPlotView()
        {
            EQEPlotModel.Series.Clear();
            EQEPlotModel.Annotations.Clear();

            var xAxis = EQEPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as LinearAxis;
            var yAxis = EQEPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) as LinearAxis;

            if (xAxis != null)
            {
                xAxis.Minimum = _axisX.DefaultMin;
                xAxis.Maximum = _axisX.DefaultMax;
            }

            if (yAxis != null)
            {
                yAxis.Minimum = _axisY.DefaultMin;
                yAxis.Maximum = _axisY.DefaultMax;
            }

            EQEPlotModel.InvalidatePlot(true);
        }
       
        // 右侧DataGrid数据源
        private ObservableCollection<SpectralGridItem> _spectralGridItems;
        public ObservableCollection<SpectralGridItem> SpectralGridItems
        {
            get => _spectralGridItems;
            set => SetProperty(ref _spectralGridItems, value);
        }
        private bool _isShowSpectralDetail;
        public bool IsShowSpectralDetail
        {
            get => _isShowSpectralDetail;
            set
            {
                _isShowSpectralDetail = value;
                OnPropertyChanged();
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
        // 显示空图表提示
        private void ShowEQEEmptyChartMessage()
        {
            var annotation = new TextAnnotation
            {
                Text = "请选择测量数据以显示EQE曲线",
                TextPosition = new DataPoint((_axisX.DefaultMin + _axisX.DefaultMax) / 2, 50),
                TextColor = OxyColors.Gray,
                FontSize = 16,
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle
            };

            EQEPlotModel.Annotations.Add(annotation);
            EQEPlotModel.InvalidatePlot(true);
        }
        #endregion

        #region EQE数据导出
        private void ExportEQEData()
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV Files|*.csv",
                Title = "Save EQE Data to CSV",
                FileName = $"EQE_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                ExportEQEToCsv(saveFileDialog.FileName, Measurements, Wavelengths);
            }
        }

        // 导出EQE数据到CSV
        private void ExportEQEToCsv(string fileName, ObservableCollection<SpectrumMeasurement> measurements, float[] wavelengths)
        {
            if (measurements == null || !measurements.Any() || wavelengths == null || wavelengths.Length == 0)
            {
                MessageBox.Show("无有效EQE数据可导出！", "提示");
                return;
            }

            const int Step = 10;
            const int MinWave = 380;
            const int MaxWave = 780;

            try
            {
                // 构造表头
                var fixedHeaders = new List<string>
                {
                    "Time","Meas_Id", "Voltage/V", "Current/mA", "Luminous Flux(lm)",
                    "EQE(%)","Efficacy(lm/watt)","IP","BlueLight","cx","cy","u'","v'",
                    "CCT(K)","Dominant Wavelength(nm)","Saturation(%)","Peak Wavelength(nm)","FWHM"
                };

                // 构造波长表头
                var waveHeaders = new List<string>();
                var selectedIndexes = new List<int>();
                for (int i = 0; i <= (MaxWave - MinWave) * 10; i += Step)
                {
                    double targetWave = i / 10.0 + MinWave;
                    int originalIndex = Array.FindIndex(wavelengths, w => Math.Abs(w - targetWave) < 0.001);
                    if (originalIndex != -1)
                    {
                        waveHeaders.Add($"{targetWave:F0}");
                        selectedIndexes.Add(originalIndex);
                    }
                }

                if (waveHeaders.Count == 0)
                {
                    MessageBox.Show("未找到≤780nm的有效波长点！", "错误");
                    return;
                }

                // 写入CSV
                var allHeaders = fixedHeaders.Concat(waveHeaders);
                var csv = new StringBuilder();
                csv.AppendLine(string.Join(",", allHeaders));

                for (int rowIndex = 0; rowIndex < measurements.Count; rowIndex++)
                {
                    var item = measurements[rowIndex];
                    int measId = rowIndex + 1;

                    // 计算EQE相关值
                    double luminousFlux = item.Luminance * 0.01;
                    double eqeValue = CalculateEQEValue(item);
                    double power = (item.Voltage * item.Current) / 1000;
                    double efficacy = power > 0 ? luminousFlux / power : 0;

                    // 固定字段值
                    var fixedValues = new List<string>
                    {
                        item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        measId.ToString(),
                        item.Voltage.ToString("F6"),
                        item.Current.ToString("F3"),
                        luminousFlux.ToString("F4"),
                        eqeValue.ToString("F2"),
                        efficacy.ToString("F2"),
                        EscapeCsvValue(item.IP ?? ""),
                        item.Blue.ToString("F2"),
                        item.CIE_x.ToString("F4"),
                        item.CIE_y.ToString("F4"),
                        item.CIE_u.ToString("F4"),
                        item.CIE_v.ToString("F4"),
                        item.CCT.ToString("F0"),
                        item.PeakWavelength.ToString("F1"),
                        (item.fPur * 100).ToString("F2"),
                        item.PeakWavelength.ToString("F1"),
                        item.FHW.ToString("F2")
                    };

                    // 波长对应的EQE值
                    var waveValues = new List<string>();
                    if (item.Intensities != null && item.Intensities.Length == wavelengths.Length)
                    {
                        foreach (int idx in selectedIndexes)
                        {
                            double waveEQE = CalculateWaveEQE(item.Wavelengths[idx], item.Intensities[idx]);
                            waveValues.Add(waveEQE.ToString("F4"));
                        }
                    }
                    else
                    {
                        waveValues = Enumerable.Repeat("0.0000", waveHeaders.Count).ToList();
                    }

                    csv.AppendLine(string.Join(",", fixedValues.Concat(waveValues)));
                }

                File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
                MessageBox.Show($"EQE数据已导出至：\n{fileName}", "导出成功");
            }
            catch (Exception ex)
            {
                log.Error("导出EQE数据失败", ex);
                MessageBox.Show($"导出失败：{ex.Message}", "错误");
            }
        }



        // 计算EQE值（示例逻辑，需根据实际公式调整）
        private double CalculateEQEValue(SpectrumMeasurement measurement)
        {
            double avgIntensity = measurement.Intensities.Average();
            return Math.Min(100, avgIntensity * 10);
        }

        // 计算单个波长的EQE值
        private double CalculateWaveEQE(float wavelength, float intensity)
        {
            double wavelengthFactor = wavelength / 1000;
            return intensity * wavelengthFactor * 100;
        }

        // CSV值转义
        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
        #endregion

        #region 辅助方法
        // 清空所有显示
        public void ClearAllDisplays()
        {
            ResetEQEPlotView();
            Measurements.Clear();
            _eqeSeriesCache.Clear();
            SelectedMeasurement = null;
            IsEQEMeasured = false;
        }

        // 颜色转换（WPF→OxyPlot）
        private OxyColor ConvertToOxyColor(SolidColorBrush brush)
        {
            if (brush == null) return OxyColors.Red;
            return OxyColor.FromArgb(
                brush.Color.A,
                brush.Color.R,
                brush.Color.G,
                brush.Color.B);
        }
        #endregion
    }
    public class SpectrumMeasureParam
    {
        /// <summary>
        /// SMU 数据
        /// </summary>
        public SMUMasterResultData SMUData { get; set; }
    }
    public class SMUMasterResultData
    {
        public double V { set; get; }
        public double I { set; get; }
        public int MasterId { get; set; }
        public int MasterResultType { get; set; }
    }
}

    

    

