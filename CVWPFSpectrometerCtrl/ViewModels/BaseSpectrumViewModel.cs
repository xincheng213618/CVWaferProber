using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ScottPlot.WPF;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class BaseSpectrumViewModel : ViewModelBase
    {
        #region 公共属性
        protected readonly double _overviewSpectralXMin = 360;
        protected readonly double _overviewSpectralXMax = 800;
        protected PlotAxesCfg AxisX = new PlotAxesCfg() { DefaultMin = 350, DefaultMax = 800, DefaultMaxRange = 500 };
        protected PlotAxesCfg AxisY = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 1.0f, DefaultMaxRange = 1.1f };

        // 基础图表
        private PlotModel _plotModel;
        public PlotModel PlotModel
        {
            get => _plotModel;
            set => SetProperty(ref _plotModel, value);
        }

        // 总览图
        public PlotModel OverviewSpectralPlotModel { get; protected set; } = new PlotModel();
        public PlotModel OverviewIVPlotModel { get; protected set; } = new PlotModel();
        public PlotModel OverviewILPlotModel { get; protected set; } = new PlotModel();
        public PlotModel OverviewVLPlotModel { get; protected set; } = new PlotModel();

        // 数据集合
        public ObservableCollection<SpectralGridItem> SpectralGridItems { get; set; } = new ObservableCollection<SpectralGridItem>();
        public float[] Wavelengths { get; protected set; }
        public WpfPlot PlotControl { get; set; }
        public string DeviceCode { get; set; } = "DEV.Spectrum.Default";

        // 公共配置
        private bool _isShowAllData;
        public bool IsShowAllData
        {
            get => _isShowAllData;
            set
            {
                _isShowAllData = value;
                OnPropertyChanged();
                UpdateChartByShowAllState();
            }
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

        // 线条颜色
        private SolidColorBrush _spectralLineColor = new SolidColorBrush(Colors.Red);
        public SolidColorBrush SpectralLineColor
        {
            get => _spectralLineColor;
            set
            {
                if (_spectralLineColor != value)
                {
                    _spectralLineColor = value;
                    OnPropertyChanged(nameof(SpectralLineColor));
                    OxyColor newOxyColor = ConvertToOxyColor(value);
                    UpdateLineColor(newOxyColor);
                }
            }
        }

        // 选中的测量数据（泛型适配光谱/EQE）
        private object _selectedMeasurement;
        public object SelectedMeasurement
        {
            get => _selectedMeasurement;
            set
            {
                if (SetProperty(ref _selectedMeasurement, value))
                {
                    OnPropertyChanged(nameof(SelectedMeasurement));
                    OnSelectedMeasurementChanged(value);
                }
            }
        }

        // 缓存
        protected Dictionary<int, LineSeries> _spectralSeriesCache = new Dictionary<int, LineSeries>();
        //protected SpectrumControl _spectralCtrl;
        #endregion

        #region 构造函数 & 初始化
        public BaseSpectrumViewModel()
        {
            // 初始化波长数组
            Wavelengths = new float[4001];
            for (int i = 0; i < 4001; i++)
            {
                Wavelengths[i] = 380 + i / 10.0f;
            }

            // 初始化基础图表
            InitializePlotModel();
            InitializeOverviewPlotModels();
        }

        // 初始化基础光谱图表
        protected virtual void InitializePlotModel()
        {
            PlotModel = new PlotModel
            {
                Title = (string)Application.Current.FindResource("Sp.SpectralCurve"),
                TitleFontSize = 14
            };

            // X轴（波长）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = (string)Application.Current.FindResource("Sp.Wavelength"),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
            };
            AxisCfg(xAxis, AxisX);

            // Y轴（强度/EQE值）
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
        }

        // 初始化总览图
        protected void InitializeOverviewPlotModels()
        {
            string spectralTitle = (string)Application.Current.FindResource("Sp.Spectral");
            OverviewSpectralPlotModel = ClonePlotModel(PlotModel, spectralTitle);
            OverviewIVPlotModel = ClonePlotModel(new PlotModel(), "IV");
            OverviewILPlotModel = ClonePlotModel(new PlotModel(), "IL");
            OverviewVLPlotModel = ClonePlotModel(new PlotModel(), "VL");
        }
         
        // 克隆图表配置（通用方法）
        protected PlotModel ClonePlotModel(PlotModel sourceModel, string title)
        {
            var targetModel = new PlotModel
            {
                Title = title,
                Padding = new OxyThickness(5),
                PlotAreaBorderThickness = new OxyThickness(1)
            };

            foreach (var axis in sourceModel.Axes)
            {
                if (axis is LinearAxis linearAxis)
                {
                    var clonedAxis = new LinearAxis
                    {
                        Position = linearAxis.Position,
                        Title = linearAxis.Title,
                        Minimum = double.NaN,
                        Maximum = double.NaN,
                        FontSize = 10,
                        MajorGridlineStyle = linearAxis.MajorGridlineStyle,
                        MinorGridlineStyle = linearAxis.MinorGridlineStyle,
                        IsZoomEnabled = false,
                        IsPanEnabled = false
                    };

                    // 总览图X轴固定范围
                    if (title.Contains("光谱") && clonedAxis.Position == AxisPosition.Bottom)
                    {
                        clonedAxis.Minimum = _overviewSpectralXMin;
                        clonedAxis.Maximum = _overviewSpectralXMax;
                        clonedAxis.AbsoluteMinimum = _overviewSpectralXMin;
                        clonedAxis.AbsoluteMaximum = _overviewSpectralXMax;
                    }

                    targetModel.Axes.Add(clonedAxis);
                }
            }

            return targetModel;
        }
        #endregion

        #region 公共方法（数据处理/图表更新）
        // 更新线条颜色（子类可重写）
        protected virtual void UpdateLineColor(OxyColor newColor)
        {
            if (IsShowAllData && SelectedMeasurement != null)
            {
                UpdateSelectedCurveColor(newColor);
            }
            else
            {
                UpdateAllMeasurementsLineColor(newColor);
                UpdateSeriesColor(PlotModel, newColor);
            }
        }

        // 更新选中曲线颜色
        protected void UpdateSelectedCurveColor(OxyColor newColor)
        {
            if (SelectedMeasurement is SpectrumMeasurement meas && _spectralSeriesCache.TryGetValue(meas.No, out LineSeries series))
            {
                series.Color = newColor;
                series.MarkerFill = newColor;
                series.MarkerStroke = newColor;
                PlotModel.InvalidatePlot(true);
            }
        }

        // 更新所有曲线颜色
        protected virtual void UpdateAllMeasurementsLineColor(OxyColor newColor)
        {
            // 子类实现具体逻辑
        }

        // 更新图表系列颜色
        protected void UpdateSeriesColor(PlotModel plotModel, OxyColor color)
        {
            if (plotModel?.Series == null) return;
            foreach (var series in plotModel.Series.OfType<LineSeries>())
            {
                series.Color = color;
                series.MarkerFill = color;
                series.MarkerStroke = color;
            }
            plotModel.InvalidatePlot(true);
        }

        // 刷新所有图表
        public void RefreshAllPlots()
        {
            PlotModel?.InvalidatePlot(true);
            OverviewSpectralPlotModel?.InvalidatePlot(true);
            OverviewIVPlotModel?.InvalidatePlot(true);
            OverviewILPlotModel?.InvalidatePlot(true);
            OverviewVLPlotModel?.InvalidatePlot(true);
            OnPropertyChanged(nameof(PlotModel));
            PlotControl?.Refresh();
        }

        // 清空所有数据/图表
        public virtual void ClearAllDisplays()
        {
            // 清空图表
            PlotModel.Series.Clear();
            PlotModel.Annotations.Clear();
            ResetAxisToDefault();
            PlotModel.InvalidatePlot(true);

            // 清空总览图
            OverviewSpectralPlotModel.Series.Clear();
            OverviewIVPlotModel.Series.Clear();
            OverviewILPlotModel.Series.Clear();
            OverviewVLPlotModel.Series.Clear();

            // 清空数据
            SpectralGridItems.Clear();
            _spectralSeriesCache.Clear();
            SelectedMeasurement = null;

            // 刷新
            RefreshAllPlots();
        }

        // 更新右侧DataGrid数据
        protected virtual void UpdateSpectralGridData(object measurement)
        {
            // 子类实现具体数据转换
        }

        // 选中数据变化时的回调（子类重写）
        protected virtual void OnSelectedMeasurementChanged(object selectedItem)
        {
            if (IsShowAllData)
            {
                UpdateSelectedCurveHighlight();
            }
            else
            {
                ResetAndUpdateChart();
            }

            if (IsShowSpectralDetail && selectedItem != null)
            {
                UpdateSpectralGridData(selectedItem);
            }
        }

        // 重置图表并更新
        protected virtual void ResetAndUpdateChart()
        {
            ResetPlotView();
            if (SelectedMeasurement != null)
            {
                UpdateChartFromSelectedMeasurement();
            }
            else
            {
                ShowEmptyChartMessage();
            }
        }

        // 绘制选中数据到图表
        protected virtual void UpdateChartFromSelectedMeasurement()
        {
            // 子类实现具体绘制逻辑
        }

        // 显示空图表提示
        protected void ShowEmptyChartMessage()
        {
            var annotation = new TextAnnotation
            {
                Text = "请选择测量数据以显示曲线",
                TextPosition = new DataPoint((AxisX.DefaultMin + AxisX.DefaultMax) / 2, 50),
                TextColor = OxyColors.Gray,
                FontSize = 16,
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle
            };
            PlotModel.Annotations.Add(annotation);
            PlotModel.InvalidatePlot(true);
        }

        // 转换WPF颜色到OxyColor
        protected OxyColor ConvertToOxyColor(SolidColorBrush brush)
        {
            if (brush == null) return OxyColors.Blue;
            return OxyColor.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
        }

        // CSV导出公共方法
        protected void ExportToCsv(string fileName, List<string> headers, List<List<string>> rows)
        {
            try
            {
                var csv = new StringBuilder();
                csv.AppendLine(string.Join(",", headers));
                foreach (var row in rows)
                {
                    csv.AppendLine(string.Join(",", row.Select(EscapeCsvValue)));
                }
                File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
                MessageBox.Show($"数据已导出至：\n{fileName}", "导出成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误");
            }
        }

        // CSV值转义
        protected string EscapeCsvValue(string value)
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
        protected void AxisCfg(LinearAxis axis, PlotAxesCfg axisCfg)
        {
            axis.Minimum = axisCfg.DefaultMin;
            axis.Maximum = axisCfg.DefaultMax;
            axis.MaximumRange = axisCfg.DefaultMaxRange;
            axis.ExtraGridlines = null;
        }

        protected void ResetAxisToDefault()
        {
            var xAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as LinearAxis;
            var yAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) as LinearAxis;
            if (xAxis != null) AxisCfg(xAxis, AxisX);
            if (yAxis != null) AxisCfg(yAxis, AxisY);
        }

        protected void ResetPlotView()
        {
            PlotModel.Series.Clear();
            PlotModel.Annotations.Clear();
            ResetAxisToDefault();
            PlotModel.InvalidatePlot(true);
        }

        protected virtual void UpdateChartByShowAllState()
        {
            // 子类实现具体逻辑
        }

        protected virtual void UpdateSelectedCurveHighlight()
        {
            // 子类实现高亮逻辑
        }
        #endregion

        // 光谱详情Grid模型（共用）
        public class SpectralGridItem
        {
            public double Wavelength { get; set; }
            public float RelativeSpectrum { get; set; }
            public float AbsoluteSpectrum { get; set; }
        }
    }

}
