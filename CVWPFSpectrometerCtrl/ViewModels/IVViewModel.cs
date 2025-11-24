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

            // 设置X轴（V）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "电流/I",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = 0,
                Maximum = 8,
                MaximumRange = 10,
            };

            // 设置Y轴（I）
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "电压/V",
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
        private PlotAxesCfg AxisV = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 6, DefaultMaxRange = 10  };
        private PlotAxesCfg AxisI = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 100, DefaultMaxRange = 2000  };
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
            var lineSeries = new LineSeries
            {
                Title = "IV曲线",
                Color = OxyColors.Red,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.Circle,  // 标记类型
                MarkerSize = 4,                  // 标记大小
                MarkerFill = OxyColors.Red,      // 标记填充颜色
                MarkerStroke = OxyColors.Red,  // 标记边框颜色
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

            PlotModel.Series.Clear();
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true);
        }
        
        private void btnResetZoom_Click(object sender, RoutedEventArgs e)
        {
            // 重置所有轴的缩放
            _plotModel.ResetAllAxes();
            _plotModel.InvalidatePlot(true);
        }

        public void Clear()
        {
            no = 1;
            PlotModel.Series.Clear();
            Measurements.Clear();
        }
    }
}
