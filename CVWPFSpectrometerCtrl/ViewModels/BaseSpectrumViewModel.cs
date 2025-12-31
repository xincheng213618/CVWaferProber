using ColorVision.Core.Entities;
using CVCommCore;
using CVDB.Services.Spectrum;
using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using CVWPFSpectrumControl;
using CVWPFSpectrumControl.Models;
using log4net;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ScottPlot.WPF;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    /// <summary>
    /// 光谱/EQE通用基类，封装公共逻辑
    /// </summary>
    public abstract class BaseSpectrumViewModel : ViewModelBase
    {
        #region 公共字段/属性
        protected readonly ILog _log = LogManager.GetLogger(typeof(BaseSpectrumViewModel));
        protected readonly double _overviewSpectralXMin = 360;
        protected readonly double _overviewSpectralXMax = 800;

        // 通用轴配置
        protected PlotAxesCfg AxisX = new PlotAxesCfg() { DefaultMin = 350, DefaultMax = 800, DefaultMaxRange = 500 };
        protected PlotAxesCfg AxisY = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 1.0f, DefaultMaxRange = 1.1f };

        // 公共图表模型（总览图）
        public PlotModel OverviewSpectralPlotModel { get; private set; } = new PlotModel();
        public PlotModel OverviewIVPlotModel { get; private set; } = new PlotModel();
        public PlotModel OverviewILPlotModel { get; private set; } = new PlotModel();
        public PlotModel OverviewVLPlotModel { get; private set; } = new PlotModel();

        // 公共选中状态/显示控制
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

        // 右侧DataGrid数据源
        private ObservableCollection<SpectralGridItem> _spectralGridItems;
        public ObservableCollection<SpectralGridItem> SpectralGridItems
        {
            get => _spectralGridItems;
            set => SetProperty(ref _spectralGridItems, value);
        }

        // 颜色配置（通用转换逻辑）
        protected OxyColor ConvertToOxyColor(SolidColorBrush brush)
        {
            if (brush == null) return OxyColors.Blue;
            return OxyColor.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
        }

        // 命令
        public ICommand ExportCommand { get; protected set; }
        public ICommand BtnResetStatus { get; protected set; }

        // 子ViewModel（IV/IL/VL）
        protected ILViewModel IL_viewModel;
        protected IVViewModel IV_viewModel;
        protected VLViewModel VL_viewModel;

        public PlotModel IVPlotModel { get; protected set; }
        public PlotModel ILPlotModel { get; protected set; }
        public PlotModel VLPlotModel { get; protected set; }
        // 控件引用
        protected WpfPlot _plotControl;
        public WpfPlot PlotControl
        {
            get => _plotControl;
            set => SetProperty(ref _plotControl, value);
        }

        protected SpectrumControl _spectralCtrl;
        public float[] Wavelengths { get; protected set; }

        // 缓存（通用）
        protected Dictionary<int, LineSeries> _seriesCache = new Dictionary<int, LineSeries>();

        // 抽象属性（子类实现）
        public abstract PlotModel PlotModel { get; set; }
        public abstract object SelectedMeasurement { get; set; }
        public abstract ObservableCollection<IVMeasurement> IVMeasurements { get; set; }
        public abstract ObservableCollection<ILMeasurement> ILMeasurements { get; set; }
        public abstract ObservableCollection<VLMeasurement> VLMeasurements { get; set; }
        #endregion

        protected BaseSpectrumViewModel()
        {
            // 初始化通用波长数组
            Wavelengths = new float[4001];
            for (int i = 0; i < 4001; i++)
            {
                Wavelengths[i] = 380 + i / 10.0f;
            }

            // 初始化通用命令
            BtnResetStatus = new RelayCommand(IVResetStatus);
            SpectralGridItems = new ObservableCollection<SpectralGridItem>();

            // 初始化IV/IL/VL子ViewModel
            InitializeIVPlotModel();
            InitializeILPlotModel();
            InitializeVLPlotModel();

            // 初始化总览图
            InitializeOverviewPlotModels();
        }

        #region 公共方法（子类可重写）
        /// <summary>
        /// 初始化总览图（通用逻辑）
        /// </summary>
        protected void InitializeOverviewPlotModels()
        {
            string spectralTitle = (string)System.Windows.Application.Current.FindResource("Sp.Spectral");
            OverviewSpectralPlotModel = ClonePlotModel(PlotModel, spectralTitle);
            OverviewIVPlotModel = ClonePlotModel(IVPlotModel, "IV");
            OverviewILPlotModel = ClonePlotModel(ILPlotModel, "IL");
            OverviewVLPlotModel = ClonePlotModel(VLPlotModel, "VL");

            InitializeOverviewSeries();
            NotifyOverviewPlotChanged();
        }

        /// <summary>
        /// 克隆PlotModel（通用轴配置）
        /// </summary>
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

                    // 光谱总览图锁定X轴
                    if ((title == (string)System.Windows.Application.Current.FindResource("Sp.Spectral") || title == "光谱")
                        && clonedAxis.Position == AxisPosition.Bottom)
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

        /// <summary>
        /// 刷新所有图表
        /// </summary>
        public void RefreshAllPlots()
        {
            PlotModel?.InvalidatePlot(true);
            OverviewSpectralPlotModel?.InvalidatePlot(true);
            OverviewIVPlotModel?.InvalidatePlot(true);
            OverviewILPlotModel?.InvalidatePlot(true);
            OverviewVLPlotModel?.InvalidatePlot(true);

            OnPropertyChanged(nameof(PlotModel));
            OnPropertyChanged(nameof(OverviewSpectralPlotModel));
            PlotControl?.Refresh();
        }

        /// <summary>
        /// 重置图表视图（通用）
        /// </summary>
        protected void ResetPlotView()
        {
            PlotModel.Series.Clear();
            PlotModel.Annotations.Clear();
            ResetAxisToDefault();
            PlotModel.InvalidatePlot(true);
        }

        /// <summary>
        /// 重置轴到默认值
        /// </summary>
        protected void ResetAxisToDefault()
        {
            var xAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as LinearAxis;
            var yAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) as LinearAxis;

            if (xAxis != null) AxisCfg(xAxis, AxisX);
            if (yAxis != null) AxisCfg(yAxis, AxisY);
        }

        /// <summary>
        /// 轴配置（通用）
        /// </summary>
        protected void AxisCfg(LinearAxis axis, PlotAxesCfg axisCfg)
        {
            axis.Minimum = axisCfg.DefaultMin;
            axis.Maximum = axisCfg.DefaultMax;
            axis.MaximumRange = axisCfg.DefaultMaxRange;
            axis.ExtraGridlines = null;
        }

        /// <summary>
        /// 显示空图表提示（通用）
        /// </summary>
        protected void ShowEmptyChartMessage()
        {
            var textAnnotation = new TextAnnotation
            {
                Text = "请选择测量数据以显示曲线",
                TextPosition = new DataPoint((AxisX.DefaultMin + AxisX.DefaultMax) / 2, (AxisY.DefaultMin + AxisY.DefaultMax) / 2),
                TextColor = OxyColors.Gray,
                FontSize = 16,
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle
            };

            PlotModel.Annotations.Add(textAnnotation);
            PlotModel.InvalidatePlot(true);
        }

        /// <summary>
        /// 刷新总览图轴范围（通用）
        /// </summary>
        protected void RefreshAxisRange(PlotModel plotModel)
        {
            foreach (var axis in plotModel.Axes)
            {
                if (axis is LinearAxis linearAxis)
                {
                    linearAxis.Minimum = double.NaN;
                    linearAxis.Maximum = double.NaN;
                }
            }

            plotModel.InvalidatePlot(true);

            foreach (var axis in plotModel.Axes)
            {
                if (axis is LinearAxis linearAxis && !double.IsNaN(linearAxis.ActualMinimum) && !double.IsNaN(linearAxis.ActualMaximum))
                {
                    double range = linearAxis.ActualMaximum - linearAxis.ActualMinimum;
                    double margin = range * 0.05;
                    linearAxis.Minimum = linearAxis.ActualMinimum - margin;
                    linearAxis.Maximum = linearAxis.ActualMaximum + margin;
                }
            }

            plotModel.InvalidatePlot(true);
        }

        /// <summary>
        /// CSV值转义（通用）
        /// </summary>
        protected static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        /// <summary>
        /// 初始化IV PlotModel（通用）
        /// </summary>
        protected void InitializeIVPlotModel()
        {
            IV_viewModel = new IVViewModel();
            IVPlotModel = IV_viewModel.PlotModel;
            IVMeasurements = IV_viewModel.Measurements;
        }

        /// <summary>
        /// 初始化IL PlotModel（通用）
        /// </summary>
        protected void InitializeILPlotModel()
        {
            IL_viewModel = new ILViewModel();
            ILPlotModel = IL_viewModel.PlotModel;
            ILMeasurements = IL_viewModel.Measurements;
        }

        /// <summary>
        /// 初始化VL PlotModel（通用）
        /// </summary>
        protected void InitializeVLPlotModel()
        {
            VL_viewModel = new VLViewModel();
            VLMeasurements = VL_viewModel.Measurements;
            VLPlotModel = VL_viewModel.PlotModel;
        }

        /// <summary>
        /// 重置IV状态（通用）
        /// </summary>
        protected void IVResetStatus(object obj)
        {
            OverviewIVPlotModel?.InvalidatePlot(true);
        }

        /// <summary>
        /// 通知总览图属性变更
        /// </summary>
        protected void NotifyOverviewPlotChanged()
        {
            OnPropertyChanged(nameof(OverviewSpectralPlotModel));
            OnPropertyChanged(nameof(OverviewIVPlotModel));
            OnPropertyChanged(nameof(OverviewILPlotModel));
            OnPropertyChanged(nameof(OverviewVLPlotModel));
        }

        #endregion

        #region 抽象方法（子类必须实现）
        /// <summary>
        /// 根据显示所有数据的状态更新图表
        /// </summary>
        protected abstract void UpdateChartByShowAllState();

        /// <summary>
        /// 初始化主图表（光谱/EQE各自实现）
        /// </summary>
        protected abstract void InitializePlotModel();

        /// <summary>
        /// 初始化总览图数据系列
        /// </summary>
        public abstract void InitializeOverviewSeries();

        /// <summary>
        /// 加载数据（光谱/EQE不同数据源）
        /// </summary>
        /// <param name="serialNumber">批次号</param>
        public abstract void LoadData(string serialNumber);

        /// <summary>
        /// 清空所有显示
        /// </summary>
        public abstract void ClearAllDisplays();

        /// <summary>
        /// 更新右侧DataGrid数据
        /// </summary>
        /// <param name="measurement">选中的测量数据</param>
        protected abstract void UpdateSpectralGridData(object measurement);

        /// <summary>
        /// 导出CSV（光谱/EQE不同数据结构）
        /// </summary>
        /// <param name="fileName">文件路径</param>
        protected abstract void ExportToCsv(string fileName);
        #endregion

        #region 嵌套类
        public class SpectralGridItem
        {
            public double Wavelength { get; set; }
            public float RelativeSpectrum { get; set; }
            public float AbsoluteSpectrum { get; set; }
        }
        #endregion
    }
}