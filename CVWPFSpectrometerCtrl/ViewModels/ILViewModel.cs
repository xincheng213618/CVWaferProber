using ColorVision.Core.Entities;
using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class ILViewModel : ViewModelBase
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ILViewModel));
        private PlotModel _plotModel;
        private ObservableCollection<ILMeasurement> _measurements;
        private LineSeries _dataSeries;
        public ObservableCollection<ILMeasurement> Measurements
        {
            get => _measurements;
            set
            {
                _measurements = value;
                OnPropertyChanged(nameof(Measurements));
            }
        }

        public ILViewModel()
        {
            InitializePlotModel();
            Measurements = new ObservableCollection<ILMeasurement>();
           
        }

        public PlotModel PlotModel
        {
            get => _plotModel;
            set
            {
                _plotModel = value;
                OnPropertyChanged(nameof(PlotModel));
            }
        }
        private PlotAxesCfg AxisX = new PlotAxesCfg() { DefaultMin = 0, DefaultMaxRange = 200000000000000000 };
        private PlotAxesCfg AxisY = new PlotAxesCfg() { DefaultMin = 0, DefaultMaxRange = 6000000000000000 };
        string I = (string)Application.Current.FindResource("Sp.Current");
        string L = (string)Application.Current.FindResource("Sp.Luminance");
        private void InitializePlotModel()
        {
          
            _plotModel = new PlotModel
            {
                Title = (string)Application.Current.FindResource("Sp.IL Curve"),
                TitleFontSize = 14,
                TitleFontWeight = OxyPlot.FontWeights.Bold,
               
            };

            // 设置X轴（I）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = I,
                TitleFontSize = 12,
                TitleFontWeight = OxyPlot.FontWeights.Normal,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.Dot,
                Minimum = AxisX.DefaultMin,
                Maximum = AxisX.DefaultMax,
                MaximumRange = AxisX.DefaultMaxRange,
                MajorGridlineColor = OxyColor.FromRgb(200, 200, 200),
                MinorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                AxislineColor = OxyColors.Black,
                AxislineThickness = 1
            };

            // 设置Y轴（L）
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = L,
                TitleFontSize = 12,
                TitleFontWeight = OxyPlot.FontWeights.Normal,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.Dot,
                Minimum = AxisY.DefaultMin,
                Maximum = AxisY.DefaultMax,
                MaximumRange = AxisY.DefaultMaxRange,
                MajorGridlineColor = OxyColor.FromRgb(200, 200, 200),
                MinorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                AxislineColor = OxyColors.Black,
                AxislineThickness = 1
            };

            _plotModel.Axes.Add(xAxis);
            _plotModel.Axes.Add(yAxis);
        }
        int no = 1;
        //double dpiRadio = 1;
        public void LoadData(List<VScgdMeasureResultSpectrometer> results)
        {
            Clear();
           // using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
           // dpiRadio = graphics.DpiY / 96.0;
            double[] I = new double[results.Count], L = new double[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.IResult == null)
                {
                    result.IResult = 0;
                }
                if (result.FPh == null)
                {
                    result.FPh = 0;
                }
                I[i] = (double)result.IResult;
                L[i] = (double)result.FPh;
                Measurements.Add(new ILMeasurement(no++,result.CreateDate, I[i], L[i]));
            }
            UpdateILData(I,L);
        } 
        public void LoadData(List<VScgdAlgorithmResultMaster> results, List<float> il_results)
        {
            Clear();
            double[] I = new double[results.Count], L = new double[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.IResult == null)
                {
                    result.IResult = 0;
                }
                I[i] = (double)result.IResult;
                L[i] = il_results[i];
                Measurements.Add(new ILMeasurement(no++, result.CreateDate, I[i], L[i]));
            }
            UpdateILData(I,L);
        }

        public void UpdateILData(double[] I, double[] L)
        {
            // 1. 数据校验（保留原有逻辑）
            if (I == null || L == null || I.Length == 0 || L.Length == 0 || I.Length != L.Length)
            {
                PlotModel.Series.Clear();
                PlotModel.InvalidatePlot(true);
                return;
            }

            // 2. 计算轴范围（保留1%边距，处理最小值为0的情况）
            double maxCurrent = I.Max() * 1.01;  
            double minCurrent = Math.Max(0, I.Min() * 0.99); 
            double maxLuminance = L.Max() * 1.01; 
            double minLuminance = Math.Max(0, L.Min() * 0.99);
            

            // 3. 修复轴匹配：用完整标题（含单位）匹配，或用Position匹配（更稳定）
            var xAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Left);

            // 备选方案：用轴位置匹配（避免标题修改导致失效）
            //if (xAxis == null) xAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            //if (yAxis == null) yAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Left);

            if (xAxis != null && yAxis != null)
            {
                // 4. 更新X轴（电流）范围：锁定范围，防止自动扩展
                xAxis.Minimum = minCurrent;
                xAxis.Maximum = maxCurrent;
                xAxis.AbsoluteMinimum = minCurrent; // 锁定最小范围
                xAxis.AbsoluteMaximum = maxCurrent; // 锁定最大范围（关键：避免OxyPlot自动加边距）

                // 5. 更新Y轴（亮度）范围：锁定范围
                yAxis.Minimum = minLuminance;
                yAxis.Maximum = maxLuminance;
                yAxis.AbsoluteMinimum = minLuminance;
                yAxis.AbsoluteMaximum = maxLuminance;

               
            }

            // 6. 保留原有折线图配置
            var lineSeries = new LineSeries
            {
                Title = (string)Application.Current.FindResource("Sp.IL Curve"),
                Color = OxyColors.Green,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Green,
                MarkerStroke = OxyColors.Green,
                MarkerStrokeThickness = 1.5,
                LineStyle = OxyPlot.LineStyle.Solid,
                CanTrackerInterpolatePoints = true,
               // TrackerFormatString = "{0}\n {1}: {2:0.00}\n {3}: {4:0.00}"
            };

            // 7. 添加数据点（X=电流，Y=亮度，与原逻辑一致）
            for (int i = 0; i < I.Length; i++)
            {
                lineSeries.Points.Add(new OxyPlot.DataPoint(I[i], L[i]));
            }

            // 8. 更新图表（保留原有逻辑）
            PlotModel.Series.Clear();
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true); // 强制刷新，应用新轴范围
            logger.Info($"maxCurrent:{maxCurrent};minCurrent:{minCurrent}" );
        }

        public void Clear()
        {
            no = 1;
            PlotModel.Series.Clear();
            Measurements.Clear();
           
            // 重置轴范围到初始默认值（如需保留上次范围，可删除此部分）
            var xAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Left);
            //var xAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as LinearAxis;
            //var yAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) as LinearAxis;
            if (xAxis != null && yAxis != null)
            {
                xAxis.Minimum = AxisX.DefaultMin;
                xAxis.Maximum = AxisX.DefaultMax;
                xAxis.AbsoluteMinimum = AxisX.DefaultMin;
                xAxis.AbsoluteMaximum = AxisX.DefaultMaxRange;

                yAxis.Minimum = AxisY.DefaultMin;
                yAxis.Maximum = AxisY.DefaultMax;
                yAxis.AbsoluteMinimum = AxisY.DefaultMin;
                yAxis.AbsoluteMaximum = AxisY.DefaultMaxRange;
            }

            PlotModel.InvalidatePlot(true);
        }
    }
}
