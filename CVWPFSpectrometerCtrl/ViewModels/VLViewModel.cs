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
        private PlotAxesCfg AxisX = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 6, DefaultMaxRange = 5000000000000000000};
        private PlotAxesCfg AxisY = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 10, DefaultMaxRange = 2000000000000000000 };

        private void InitializePlotModel()
        {
            _plotModel = new PlotModel
            {
                Title = "VL曲线",
                //Title = (string)Application.Current.FindResource(""),
                TitleFontSize = 14
            };

            // 设置X轴（V）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                //Title = "电压/V（V）",
                Title = (string)Application.Current.FindResource("Sp.Voltage"),
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
                Title = (string)Application.Current.FindResource("Sp.Luminance"),
                
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
            double maxVoltage = V.Max() * 1.01;// 电压最大值
            double minVoltage = Math.Max(0, V.Min() * 0.99);// 电压最小值
            double maxLuminance = L.Max() * 1.01;// 亮度最大值
            double minLuminance = Math.Max(0, L.Min() * 0.99);// 亮度最小值

            // 3. 获取初始化时创建的X轴和Y轴（通过标题匹配，确保准确性）
            var xAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Left);

            if (xAxis != null && yAxis != null)
            {
                // 4. 更新X轴范围：从0到电压最大值（如需从数据最小值开始，改为 V.Min()）
                xAxis.Minimum = minVoltage; 
                xAxis.Maximum = maxVoltage;
                xAxis.Minimum = minVoltage;
                xAxis.Maximum = maxVoltage;

                // 5. 更新Y轴范围：从0到亮度最大值（如需从数据最小值开始，改为 L.Min()）
                yAxis.Minimum = minLuminance;
                yAxis.Maximum = maxLuminance;
                yAxis.Minimum = minLuminance;
                yAxis.Maximum = maxLuminance;
            }

            // 6. 保留原有折线图配置
            var lineSeries = new LineSeries
            {
                Title = "VL曲线",
                Color = OxyColors.Purple,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Purple,
                MarkerStroke = OxyColors.Purple,
                MarkerStrokeThickness = 1.5,
                LineStyle = LineStyle.Solid,
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
