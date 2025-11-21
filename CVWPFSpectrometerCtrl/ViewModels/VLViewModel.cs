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
    public class VLViewModel: ViewModelBase
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
        private PlotAxesCfg AxisX = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 6, DefaultMaxRange = 5000 };
        private PlotAxesCfg AxisY = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 10, DefaultMaxRange = 20000 };
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
                Title = "电压/V",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = AxisX.DefaultMin,
                Maximum = AxisX.DefaultMax,
                MaximumRange = AxisX.DefaultMaxRange,
            };
            AxisCfg(xAxis, AxisX);
            // 设置Y轴（L）
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "亮度/L",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = AxisY.DefaultMin,
                Maximum = AxisY.DefaultMax,
                MaximumRange = AxisY.DefaultMaxRange,
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
            var lineSeries = new LineSeries
            {
                Title = "VL曲线",
                Color = OxyColors.Red,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.Circle,  // 标记类型
                MarkerSize = 4,                  // 标记大小
                MarkerFill = OxyColors.Red,      // 标记填充颜色
                MarkerStroke = OxyColors.Red,  // 标记边框颜色
                MarkerStrokeThickness = 1.5,     // 标记边框厚度
                LineStyle = LineStyle.Solid,
              
            };

            for (int i = 0; i < V.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(V[i], L[i]));
            }

            PlotModel.Series.Clear();// 将数据点加入折线
            PlotModel.Series.Add(lineSeries); // 将折线加入绘图模型
            PlotModel.InvalidatePlot(true);
        }

        public void Clear()
        {
            no = 1;
            PlotModel.Series.Clear();
            Measurements.Clear();
        }
    }
}
