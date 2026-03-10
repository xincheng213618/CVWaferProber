using ColorVision.Core.Entities;
using CVDB.Services.SMU;
using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using OpenTK.Audio.OpenAL.Extensions.SOFT.DeviceClock;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class VLViewModel : ViewModelBase
    {
        private PlotModel _plotModel;
        private ObservableCollection<VLMeasurement> _measurements;


        public ObservableCollection<VLMeasurement> Measurements
        {
            get => _measurements;
            set
            {
                _measurements = value;
                OnPropertyChanged(nameof(Measurements));

            }
        }
        //private string DeviceCode { get; set; }

        public VLViewModel()
        {
            InitializePlotModel();

            Measurements = new ObservableCollection<VLMeasurement>();
            // DeviceCode = "DEV.SMU.Default";
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
        private PlotAxesCfg AxisX = new PlotAxesCfg() { DefaultMin = -100, DefaultMax = 6, DefaultMaxRange = 5000000000000000000};
        private PlotAxesCfg AxisY = new PlotAxesCfg() { DefaultMin = -100, DefaultMax = 10, DefaultMaxRange = 2000000000000000000 };

        private void InitializePlotModel()
        {
            _plotModel = new PlotModel
            {
                Title = (string)System.Windows.Application.Current.FindResource("Sp.VL Curve"),
                //Title = (string)Application.Current.FindResource(""),
                TitleFontSize = 14
            };

            // 设置X轴（V）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                //Title = "电压/V（V）",
                Title = (string)System.Windows.Application.Current.FindResource("Sp.Voltage"),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = AxisX.DefaultMin,
                Maximum = AxisX.DefaultMax,
                MaximumRange = AxisX.DefaultMaxRange,
                //IsZoomEnabled = false, // 禁用缩放（避免用户手动改变范围，如需保留可设为true）
                IsPanEnabled = false
            };
            AxisCfg(xAxis, AxisX);
            // 设置Y轴（L）
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                //Title = "亮度/L (cd/m²)",
                Title = (string)System.Windows.Application.Current.FindResource("Sp.Luminance"),
                
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = AxisY.DefaultMin,
                Maximum = AxisY.DefaultMax,
                MaximumRange = AxisY.DefaultMaxRange,
                //IsZoomEnabled = false, // 禁用缩放（避免用户手动改变范围，如需保留可设为true）
                IsPanEnabled = false
            };
            AxisCfg(yAxis, AxisY);

            _plotModel.Axes.Add(xAxis);
            _plotModel.Axes.Add(yAxis);
        }
        private void AxisCfg(LinearAxis axis, PlotAxesCfg axisCfg)
        {
            axis.Minimum = axisCfg.DefaultMin;
            axis.Maximum = axisCfg.DefaultMax;
            axis.MaximumRange = axisCfg.DefaultMaxRange;
            axis.ExtraGridlines = null; // 清除特殊网格线

        }
        int no = 1;
        public void LoadData(List<VScgdMeasureResultSpectrometer> results)
        {

            Clear();
            double[] V = new double[results.Count], L = new double[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.VResult == null)
                {
                    result.VResult = 0;
                }
                if (result.FPh == null)
                {
                    result.FPh = 0;
                }
                V[i] = (double)result.VResult;
                L[i] = (double)result.FPh;
                Measurements.Add(new VLMeasurement(no++, result.CreateDate, V[i], L[i]));
            }
            UpdateVLData(V, L);

        }
        public void LoadData(List<VScgdAlgorithmResultMaster> results, List<float> vl_results)
        {
            Clear();
            double[] V = new double[results.Count], L = new double[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.VResult == null)
                {
                    result.VResult = 0;
                }
                V[i] = (double)result.VResult;
                L[i] = vl_results[i];
                Measurements.Add(new VLMeasurement(no++, result.CreateDate, V[i], L[i]));
            }
            UpdateVLData(V, L);
        }
        private void UpdateVLData(double[] V, double[] L)
        {
            // 1. 数据校验：避免空数组或长度不匹配
            if (V == null || L == null || V.Length == 0 || L.Length == 0 || V.Length != L.Length)
            {
                PlotModel.Series.Clear();
                PlotModel.InvalidatePlot(true);
                return;
            }

            // 2. 计算X轴（电压）和Y轴（亮度）的最大值（核心）
            double maxVoltage = Measurements.Max(m => m.Voltage) * 1.01; // 电压最大值
            double minVoltage = Measurements.Min(m => m.Voltage) * 0.99; // 电压最小值
            double maxLuminance = Measurements.Max(m => m.Luminance) ; // 亮度最大值
            double minLuminance = Measurements.Min(m => m.Luminance) ;// 亮度最小值

            // 防止最大值和最小值相等导致 OxyPlot 报错（给定一个默认最小跨度，比如0.1）
            if (Math.Abs(maxVoltage - minVoltage) < 1e-6)
            {
                maxVoltage += 0.1;
                minVoltage -= 0.1;
            }
            if (Math.Abs(maxLuminance - minLuminance) < 1e-6)
            {
                maxLuminance += 0.1;
                minLuminance -= 0.1;
            }

            // 扩1%留边距（注意对于0和负数的处理）
            maxVoltage = maxVoltage > 0 ? maxVoltage * 1.01 : maxVoltage * 0.99;
            minVoltage = minVoltage > 0 ? minVoltage * 0.99 : minVoltage * 1.01;
            maxLuminance = maxLuminance > 0 ? maxLuminance * 1.01 : maxLuminance * 0.99;
            minLuminance = minLuminance > 0 ? minLuminance * 0.99 : minLuminance * 1.01;

            // 3. 获取初始化时创建的X轴和Y轴（通过标题匹配，确保准确性）
            var xAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Left);

            if (xAxis != null && yAxis != null)
            {
                // 4. 更新X轴范围：从0到电压最大值（如需从数据最小值开始，改为 V.Min()）
                xAxis.Minimum = minVoltage; 
                xAxis.Maximum = maxVoltage;
                xAxis.AbsoluteMinimum = minVoltage;
                xAxis.AbsoluteMaximum = maxVoltage;

                // 5. 更新Y轴范围：从0到亮度最大值（如需从数据最小值开始，改为 L.Min()）
                yAxis.Minimum = minLuminance;
                yAxis.Maximum = maxLuminance;
                yAxis.AbsoluteMinimum = minLuminance;
                yAxis.AbsoluteMaximum = maxLuminance;
            }

            // 6. 保留原有折线图配置
            var lineSeries = new LineSeries
            {
                Title = (string)System.Windows.Application.Current.FindResource("Sp.VL Curve"),
                Color = OxyColors.Purple,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Purple,
                MarkerStroke = OxyColors.Purple,
                MarkerStrokeThickness = 1.5,
                LineStyle = LineStyle.Solid,
                CanTrackerInterpolatePoints = true,
               // TrackerFormatString = "{0}\n {1}: {2:0.00}\n {3}: {4:0.00}"
            };

            // 7. 添加数据点（X=电压，Y=亮度，与原逻辑一致）
            for (int i = 0; i < V.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(V[i], L[i]));
            }

            // 8. 更新图表（保留原有逻辑）
            PlotModel.Series.Clear();
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true); // 强制刷新图表，应用新轴范围
        }

        /* public void Clear()
         {
             no = 1;
             PlotModel.Series.Clear();
             Measurements.Clear();
         }*/

        // 优化Clear方法：重置轴范围到默认值（可选，根据需求决定是否保留）
        public void Clear()
        {
            no = 1;
            PlotModel.Series.Clear();
            Measurements.Clear();

            // 重置轴范围到初始默认值
            var xAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Left);

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
