using ColorVision.Core.Entities;
using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.ObjectModel;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class ILViewModel : ViewModelBase
    {
        private PlotModel _plotModel;
        private ObservableCollection<ILMeasurement> _measurements;


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
        private PlotAxesCfg AxisX = new PlotAxesCfg() { DefaultMin = 50, DefaultMax = 200, DefaultMaxRange = 20000 };
        private PlotAxesCfg AxisY = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 6, DefaultMaxRange = 5000 };

    private void InitializePlotModel()
        {
            _plotModel = new PlotModel
            {
                Title = "IL曲线",
                TitleFontSize = 14
            };

            // 设置X轴（I）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "电流/I",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = AxisX.DefaultMin,
                Maximum = AxisX.DefaultMax,
                MaximumRange = AxisX.DefaultMaxRange,
            };

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

            _plotModel.Axes.Add(xAxis);
            _plotModel.Axes.Add(yAxis);
        }
        int no = 1;
        public void LoadData(List<VScgdMeasureResultSpectrometer> results)
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
            var lineSeries = new LineSeries
            {
                Title = "IL曲线",
                Color = OxyColors.Red,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.Circle,  // 标记类型
                MarkerSize = 4,                  // 标记大小
                MarkerFill = OxyColors.Red,      // 标记填充颜色
                MarkerStroke = OxyColors.Red,  // 标记边框颜色
                MarkerStrokeThickness = 1.5,     // 标记边框厚度
                LineStyle = LineStyle.Solid,

            };

            for (int i = 0; i < I.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(I[i], L[i]));
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
        }
    }
}
