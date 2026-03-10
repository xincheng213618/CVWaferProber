using ColorVision.Core.Entities;
using CVDB.Services.SMU;
using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
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
    public class Power_LViewModel : ViewModelBase
    {
        private PlotModel _plotModel;
        private ObservableCollection<Power_LMeasurement> _measurements;

        public ObservableCollection<Power_LMeasurement> Measurements
        {
            get => _measurements;
            set
            {
                _measurements = value;
                OnPropertyChanged(nameof(Measurements));
            }
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

        // 修正：缩小 DefaultMaxRange，符合实际数据范围
        private PlotAxesCfg AxisX = new PlotAxesCfg() { DefaultMin = -1000, DefaultMax = 5, DefaultMaxRange = 5000000000000000000 };
        private PlotAxesCfg AxisY = new PlotAxesCfg() { DefaultMin = -1000, DefaultMax = 10, DefaultMaxRange = 2000000000000000000 };

        int no = 1;

        public Power_LViewModel()
        {
            InitializePlotModel();
            Measurements = new ObservableCollection<Power_LMeasurement>();
        }

        private void InitializePlotModel()
        {
            _plotModel = new PlotModel
            {
                Title = (string)Application.Current.FindResource("Sp.PowerL Curve"),
                TitleFontSize = 14
            };

            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = (string)Application.Current.FindResource("Sp.Power") , // 补充单位，更清晰
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = AxisX.DefaultMin,
                Maximum = AxisX.DefaultMax,
                MaximumRange = AxisX.DefaultMaxRange,
                IsPanEnabled = false
            };
            AxisCfg(xAxis, AxisX);

            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = (string)System.Windows.Application.Current.FindResource("Sp.Luminance"),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = AxisY.DefaultMin,
                Maximum = AxisY.DefaultMax,
                MaximumRange = AxisY.DefaultMaxRange,
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
            axis.ExtraGridlines = null;
        }

        // 修正：实例化 Power_LMeasurement 时，传入 Voltage 和 Current（匹配方案1的构造函数）
        public void LoadData(List<VScgdMeasureResultSpectrometer> results)
        {
            Clear();
            if (results == null || !results.Any()) return;

            double[] Power = new double[results.Count], L = new double[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var voltage = result.VResult ?? 0;
                var current = result.IResult ?? 0;
                var power = (double)(voltage * current) / 1000;
                var luminance = result.FPh ?? 0;

                Power[i] = power;
                L[i] = (double)luminance;

                // 修正：使用新的构造函数，传入 voltage 和 current
                Measurements.Add(new Power_LMeasurement(no++, result.CreateDate, voltage, current, luminance));
            }
            UpdatePowerLData(Power, L);
        }

        public void LoadData(List<VScgdAlgorithmResultMaster> results, List<float> currentList, List<float> pl_results)
        {
            Clear();
            if (results == null || !results.Any() || currentList == null || pl_results == null) return;
            if (results.Count != currentList.Count || results.Count != pl_results.Count)
            {
                MessageBox.Show("电压、电流、亮度数据长度不匹配！", "数据加载错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            double[] Power = new double[results.Count], L = new double[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var voltage = result.VResult ?? 0;
                var current = currentList[i];
                var power = (double)(voltage * current) / 1000;
                var luminance = pl_results[i];

                Power[i] = power;
                L[i] = luminance;

                // 修正：使用新的构造函数，传入 voltage 和 current
                Measurements.Add(new Power_LMeasurement(no++, DateTime.Now, voltage, current, luminance));
            }
            UpdatePowerLData(Power, L);
        }

        private void UpdatePowerLData(double[] Power, double[] L)
        {
            if (Power == null || L == null || Power.Length == 0 || L.Length == 0 || Power.Length != L.Length)
            {
                PlotModel.Series.Clear();
                PlotModel.InvalidatePlot(true);
                return;
            }

            // 优化：自动计算轴范围，适配实际数据
            double maxPower = Measurements.Max(m => m.Power);// 留10%边距，避免数据贴边
            double minPower = Measurements.Min(m => m.Power);
            double maxLuminance = Measurements.Max(m => m.Luminance); // 亮度最大值
            double minLuminance = Measurements.Min(m => m.Luminance);// 亮度最小值

            // 防止最大值和最小值相等导致 OxyPlot 报错（给定一个默认最小跨度，比如0.1）
            if (Math.Abs(maxPower - minPower) < 1e-6)
            {
                maxPower += 0.1;
                minPower -= 0.1;
            }
            if (Math.Abs(maxLuminance - minLuminance) < 1e-6)
            {
                maxLuminance += 0.1;
                minLuminance -= 0.1;
            }

            // 扩1%留边距（注意对于0和负数的处理）
            maxPower = maxPower > 0 ? maxPower * 1.01 : maxPower * 0.99;
            minPower = minPower > 0 ? minPower * 0.99 : minPower * 1.01;
            maxLuminance = maxLuminance > 0 ? maxLuminance * 1.01 : maxLuminance * 0.99;
            minLuminance = minLuminance > 0 ? minLuminance * 0.99 : minLuminance * 1.01;

            var xAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Left);

            if (xAxis != null && yAxis != null)
            {
                xAxis.Minimum = minPower;
                xAxis.Maximum = maxPower;
                xAxis.AbsoluteMinimum = minPower;
                xAxis.AbsoluteMaximum = maxPower;

                yAxis.Minimum = minLuminance;
                yAxis.Maximum = maxLuminance;
                yAxis.AbsoluteMinimum = minLuminance;
                yAxis.AbsoluteMaximum = maxLuminance;
            }

            var lineSeries = new LineSeries
            {
                Title = (string)Application.Current.FindResource("Sp.PowerL Curve"),
                Color = OxyColors.Blue,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Blue,
                MarkerStroke = OxyColors.Blue,
                MarkerStrokeThickness = 1.5,
                LineStyle = LineStyle.Solid,
                CanTrackerInterpolatePoints = true
            };

            for (int i = 0; i < Power.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(Power[i], L[i]));
            }

            PlotModel.Series.Clear();
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true);
        }

        public void Clear()
        {
            no = 1;
            PlotModel.Series.Clear();
            Measurements.Clear();

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
