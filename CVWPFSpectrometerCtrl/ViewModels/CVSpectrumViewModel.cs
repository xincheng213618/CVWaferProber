using ColorVision.Core.Entities;
using CVCommCore;
using CVDB.Services.Algorithm;
using CVDB.Services.Spectrum;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using CVWPFSpectrumControl;
using CVWPFSpectrumControl.Models;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ScottPlot.WPF;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class CVSpectrumViewModel : ViewModelBase
    {
        private PlotModel _plotModel;
        private PlotModel _IVPlotModel;
        private PlotModel _ILPlotModel;

        private SpectrumMeasurement _selectedMeasurement;
        private ObservableCollection<SpectrumMeasurement> _measurements;
        private ObservableCollection<ILMeasurement> _ILMeasurements;
        private ObservableCollection<IVMeasurement> _IVMeasurements;
        private ObservableCollection<IVLCameraMeasurement> _IVLCameraMeasurements;
        private float[] Wavelengths;
        //private double[] Intensities;

        private ILViewModel IL_viewModel;
        private IVViewModel IV_viewModel;
        private IVLCameraViewModel IVLCamera_viewModel;

        private SpectrumControl _spectralCtrl;

        private WpfPlot _plotControl;

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
        public SpectrumMeasurement SelectedMeasurement
        {
            get => _selectedMeasurement;
            set
            {
                _selectedMeasurement = value;
                OnPropertyChanged(nameof(SelectedMeasurement));
                ResetAndUpdateChart();
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
        public CVSpectrumViewModel()
        {
            Measurements = new ObservableCollection<SpectrumMeasurement>();
            InitializePlotModel();
            InitializeIVPlotModel();
            InitializeILPlotModel();
            InitializeIVLCameraModel();
            DeviceCode = "DEV.Spectrum.Default";

            // 初始化颜色映射（可见光谱：紫->蓝->绿->黄->红）
            ScottPlot.Color[] visibleSpectrumColors = {
            ScottPlot.Color.FromHex("#750085"), // 紫色
            ScottPlot.Color.FromHex("#0000FF"), // 蓝色
            ScottPlot.Color.FromHex("#00FF00"), // 绿色
            ScottPlot.Color.FromHex("#FFFF00"), // 黄色
            ScottPlot.Color.FromHex("#FF0000"),// 红色
            };
            //VisibleSpectrumColormap = ScottPlot.Colormap.FromColors(visibleSpectrumColors);
            VisibleSpectrumColormap = new ScottPlot.Colormaps.Custom(visibleSpectrumColors);

            // 初始化数据
            //SpectraCollection = new ObservableCollection<SpectraData>();
            InitializeSampleData();

            InitializePlot();
        }
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
        //光谱
        private void InitializePlotModel()
        {
            PlotModel = new PlotModel
            {
                Title = "光谱曲线",
                TitleFontSize = 14
            };

            // 设置X轴（波长）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "波长 (nm)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
            };
            AxisCfg(xAxis, AxisX);

            // 设置Y轴（强度）
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "光谱",
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
                Title = "光谱数据",
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
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Middle
            };

            PlotModel.Annotations.Add(textAnnotation);
            PlotModel.InvalidatePlot(true);
        }
        private void UpdateChartFromSelectedMeasurement()
        {
            if (SelectedMeasurement == null) return;

            var lineSeries = new LineSeries
            {
                Title = $"测量 {SelectedMeasurement.MeasurementId}",
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
            IVLCamera_viewModel.Clear();
            IVLCameraImageSrc = null;
        }
        public void LoadData(string serialNumber, bool isIVLCameraEnabled)
        {
            Clear();
            if (isIVLCameraEnabled) LoadCameraData(serialNumber); 
            else LoadSpectrumData(serialNumber);
        }

        private void LoadCameraData(string serialNumber)
        {
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
            IVLCamera_viewModel.LoadData(lv_results, il_results);
        }

        public void LoadSpectrumData(string serialNumber)
        {
            var results = SpectrumResultService.LoadResultByBatchCode(DeviceCode, serialNumber);
            if (results == null || results.Count == 0) return;

            IL_viewModel.LoadData(results);
            IV_viewModel.LoadData(serialNumber);
            //
            foreach (var result in results)
            {
                var measurement = new SpectrumMeasurement
                {
                    Timestamp = result.CreateDate,
                    MeasurementId = result.BatchCode,
                    PeakWavelength = (float)result.FLd,
                    PeakIntensity = (float)result.FLp,
                    V = (float)result.VResult,
                    I = (float)result.IResult,
                    IP = string.Format("{0:F2}%",result.FIp),
                    Luminance = (float)result.FPh,
                    CIE_u = (float)result.Fu,
                    CIE_v = (float)result.Fv,
                    CIE_x = (float)result.Fx,
                    CIE_y = (float)result.Fy,
                    CCT = (float)result.FCCT,

                    Intensities = JsonConvert.DeserializeObject<float[]>(result.FPL),
                    Wavelengths = Wavelengths,
                };

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

        }

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
    }
}
