using CVDB.Services.SMU;
using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.IO;
using System.Text;
using System.Windows;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class IVViewModel : ViewModelBase
    {
        private PlotModel _plotModel;
        private ObservableCollection<IVMeasurement> _measurements;
        private Random _random;

        public ObservableCollection<IVMeasurement> Measurements
        {
            get => _measurements;
            set
            {
                _measurements = value;
                OnPropertyChanged(nameof(Measurements));
            }
        }

        public string DeviceCode { get; set; }
        public IVViewModel()
        {
            InitializeIVPlotModel();
            Measurements = new ObservableCollection<IVMeasurement>();
            DeviceCode = "DEV.SMU.Default";
            //ExportData();
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

        private void InitializeIVPlotModel()
        {
            _plotModel = new PlotModel
            {
                Title = "IV曲线",
                TitleFontSize = 14
            };

            // 设置X轴（I）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = (string)Application.Current.FindResource("Sp.Current"),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = 0,
                Maximum = 8,
                MaximumRange = 10,
            };

            // 设置Y轴（V）
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = (string)Application.Current.FindResource("Sp.Voltage"),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = 0,
                Maximum = 8,
                MaximumRange = 10,
            };

            _plotModel.Axes.Add(xAxis);
            _plotModel.Axes.Add(yAxis);
        }
      
        int no = 1;
        public void LoadData(string serialNumber)
        {

            Clear();
            var results = SMUResultService.LoadResultByBatchCode(DeviceCode, serialNumber);
            if (results == null || results.Count == 0) return;
            bool isSourceV = true;
            foreach (var result in results)
            {
                isSourceV = result.IsSourceV == 1 ? true : false;
                var measurement = new IVMeasurement(no++)
                {
                    Timestamp = result.CreateDate,
                };
                if(isSourceV)
                {
                    measurement.Voltage = (double)result.SrcValue;
                    measurement.Current = (double)result.IResult;
                }
                else
                {
                    measurement.Voltage = (double)result.VResult;
                    measurement.Current = (double)result.SrcValue;
                }
                Measurements.Add(measurement);
            }
            ResetAxisToDefault(isSourceV);
            UpdateIVData(isSourceV);

        }
        // 默认轴范围
        private PlotAxesCfg AxisV = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 6, DefaultMaxRange = 10000000000000000};
        private PlotAxesCfg AxisI = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 100, DefaultMaxRange = 200000000000000000 };
        private void ResetAxisToDefault(bool isSourceV)
        {
            var xAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as LinearAxis;
            var yAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) as LinearAxis;

            if (isSourceV)
            {
                if (xAxis != null)
                {
                    xAxis.Minimum = AxisV.DefaultMin;
                    xAxis.Maximum = AxisV.DefaultMax;
                    xAxis.MaximumRange = AxisV.DefaultMaxRange;
                    xAxis.ExtraGridlines = null; // 清除特殊网格线
                }

                if (yAxis != null)
                {
                    yAxis.Minimum = AxisI.DefaultMin;      // 默认Y轴最小值
                    yAxis.Maximum = AxisI.DefaultMax;  // 默认Y轴最大值
                    yAxis.MaximumRange = AxisI.DefaultMaxRange;
                    yAxis.ExtraGridlines = null; // 清除特殊网格线
                }
            }
            else
            {
                if (xAxis != null)
                {
                    xAxis.Minimum = AxisI.DefaultMin;      // 默认Y轴最小值
                    xAxis.Maximum = AxisI.DefaultMax;  // 默认Y轴最大值
                    xAxis.MaximumRange = AxisI.DefaultMaxRange;
                    xAxis.ExtraGridlines = null; // 清除特殊网格线
                }

                if (yAxis != null)
                {
                    yAxis.Minimum = AxisV.DefaultMin;
                    yAxis.Maximum = AxisV.DefaultMax;
                    yAxis.MaximumRange = AxisV.DefaultMaxRange;
                    yAxis.ExtraGridlines = null; // 清除特殊网格线
                }
            }               
        }
        public void UpdateIVData(bool isSourceV)
        {
            /*var lineSeries = new LineSeries
            {
                Title = "IV曲线",
                Color = OxyColors.Blue,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.Circle,  // 标记类型
                MarkerSize = 4,                  // 标记大小
                MarkerFill = OxyColors.Blue,      // 标记填充颜色
                MarkerStroke = OxyColors.Blue,  // 标记边框颜色
                MarkerStrokeThickness = 1.5,     // 标记边框厚度
                LineStyle = LineStyle.Solid,
                //TrackerFormatString = "{1:0.00}V, {2:0.00}A",
            };

            for (int i = 0; i < Measurements.Count; i++)
            {
                if (isSourceV)
                {
                    lineSeries.Points.Add(new DataPoint(Measurements[i].Current, Measurements[i].Voltage));
                }
                else
                {
                    lineSeries.Points.Add(new DataPoint(Measurements[i].Current, Measurements[i].Voltage));
                }
            }

            // 保持最近的50个数据点
            if (lineSeries.Points.Count > 50)
            {
                lineSeries.Points.RemoveAt(0);
            }
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (sender, e) =>
            {
                UpdateIVData(isSourceV);
            };
            timer.Start();
            // 自动调整轴范围

            //PlotModel.Series.Clear();
            //PlotModel.Series.Add(lineSeries);
            //PlotModel.InvalidatePlot(true);*/

            // 1. 数据校验：避免空集合
            if (Measurements == null || Measurements.Count == 0)
            {
                PlotModel.Series.Clear();
                PlotModel.InvalidatePlot(true);
                return;
            }

            // 2. 动态计算X轴（电流）和Y轴（电压）的最大值（核心）
            double maxCurrent = Measurements.Max(m => m.Current) * 1.01; // 电流最大值（X轴）
            double minCurrent = Measurements.Min(m => m.Current) * 0.99; //电流最小值（X轴）
            double maxVoltage = Measurements.Max(m => m.Voltage) * 1.01; // 电压最大值（Y轴）
            double minVoltage = Measurements.Min(m => m.Voltage) * 0.99; // 电压最小值（Y轴）

            // 3. 获取X轴和Y轴（通过位置匹配，兼容两种模式）
            var xAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Left);

            if (xAxis != null && yAxis != null)
            {
                // 4. 动态更新轴范围：从0到数据最大值（如需从最小值开始，改为 Min()）
                // X轴（电流）：无论哪种模式，X轴都是电流，范围0~maxCurrent
                xAxis.Minimum = minCurrent;
                xAxis.Maximum = maxCurrent;
                xAxis.AbsoluteMinimum = minCurrent;       // 锁定最小范围，不自动扩展
                xAxis.AbsoluteMaximum = maxCurrent; // 锁定最大范围，不自动扩展

                // Y轴（电压）：无论哪种模式，Y轴都是电压，范围0~maxVoltage
                yAxis.Minimum = minVoltage;
                yAxis.Maximum = maxVoltage;
                yAxis.AbsoluteMinimum = minVoltage;       // 锁定最小范围
                yAxis.AbsoluteMaximum = maxVoltage; // 锁定最大范围

            }

            // 5. 创建线形系列（保留原有样式）
            var lineSeries = new LineSeries
            {
                Title = "IV曲线",
                Color = OxyColors.Red,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Red,
                MarkerStroke = OxyColors.Red,
                MarkerStrokeThickness = 1.5,
                LineStyle = LineStyle.Solid,
            };

            // 6. 添加数据点（X=电流，Y=电压，两种模式下一致）
            foreach (var measurement in Measurements)
            {
                lineSeries.Points.Add(new DataPoint(measurement.Current, measurement.Voltage));
            }

            // 7. 保持最近50个数据点（保留原有逻辑）
            if (lineSeries.Points.Count > 50)
            {
                lineSeries.Points.RemoveRange(0, lineSeries.Points.Count - 50);
            }

            // 8. 更新图表（移除原定时器死循环！）
            PlotModel.Series.Clear();
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true); // 强制刷新，应用新轴范围
        }
        
        private void btnResetZoom_Click(object sender, RoutedEventArgs e)
        {
            // 重置所有轴的缩放
            
            bool isSourceV = Measurements.Any() ? Measurements.First().Voltage == (double)SMUResultService.LoadResultByBatchCode(DeviceCode, "").First().SrcValue : true;
            ResetAxisToDefault(isSourceV); // 重置为默认范围
            PlotModel.InvalidatePlot(true);
        }

        /*public void Clear()
        {
            no = 1;
            PlotModel.Series.Clear();
            Measurements.Clear();
        }*/
        public void Clear()
        {
            no = 1;
            PlotModel.Series.Clear();
            Measurements.Clear();

            // 清空时重置轴范围到默认值（可选）
            var xAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Left);
            if (xAxis != null && yAxis != null)
            {
                xAxis.Minimum = 0;
                xAxis.Maximum = 8;
                xAxis.AbsoluteMinimum = 0;
                xAxis.AbsoluteMaximum = 10;

                yAxis.Minimum = 0;
                yAxis.Maximum = 8;
                yAxis.AbsoluteMinimum = 0;
                yAxis.AbsoluteMaximum = 10;
            }

            PlotModel.InvalidatePlot(true);
        }
    }
}
