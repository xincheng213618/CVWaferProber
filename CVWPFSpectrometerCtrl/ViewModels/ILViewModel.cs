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
        private PlotAxesCfg AxisX = new PlotAxesCfg() { DefaultMin = 50, DefaultMax = 200, DefaultMaxRange = 20000 };
        private PlotAxesCfg AxisY = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 6, DefaultMaxRange = 5000 };
      
        private void InitializePlotModel()
        {
          
            _plotModel = new PlotModel
            {
                Title = "IL曲线",
                TitleFontSize = 14,
                TitleFontWeight = OxyPlot.FontWeights.Bold,
               
            };

            // 设置X轴（I）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "电流/I (mA)",
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
                Title = "亮度/L (cd/m²)",
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
                LineStyle = OxyPlot.LineStyle.Solid,
                CanTrackerInterpolatePoints = true  // 跟踪器显示实际数据点

            };

            for (int i = 0; i < I.Length; i++)
            {
                lineSeries.Points.Add(new OxyPlot.DataPoint(I[i], L[i]));
            }

            PlotModel.Series.Clear();
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true);
        }
        private void ExecuteExport(object parameter)
        {
           
        }

        

        //private LineAnnotation _verticalLine;
        //private LineAnnotation _horizontalLine;
        //private TextAnnotation _coordText;
        //private void SetupMouseInteraction()
        //{
        //    // 创建垂直准线（X轴方向）
        //    _verticalLine = new LineAnnotation
        //    {
        //        Type = LineAnnotationType.Vertical,
        //        X = 0,
        //        LineStyle = OxyPlot.LineStyle.Dash,
        //        Color = OxyColors.Gray,
        //        StrokeThickness = 1,
        //        Text = "",
        //        //IsVisible = false // 初始不可见
        //    };

        //    // 创建水平准线（Y轴方向）
        //    _horizontalLine = new LineAnnotation
        //    {
        //        Type = LineAnnotationType.Horizontal,
        //        Y = 0,
        //        LineStyle = LineStyle.Dash,
        //        Color = OxyColors.Gray,
        //        StrokeThickness = 1,
        //        Text = "",
        //        //IsVisible = false // 初始不可见
        //    };

        //    // 创建坐标文本显示
        //    _coordText = new TextAnnotation
        //    {
        //        Text = "",
        //        TextPosition = new DataPoint(0, 0),
        //        TextColor = OxyColors.Red,
        //        Background = OxyColor.FromAColor(200, OxyColors.White),
        //        Stroke = OxyColors.Black,
        //        StrokeThickness = 1,
        //       // IsVisible = false // 初始不可见
        //    };

        //    // 将准线和文本添加到图表
        //    plotView.Model.Annotations.Add(_verticalLine);
        //    plotView.Model.Annotations.Add(_horizontalLine);
        //    plotView.Model.Annotations.Add(_coordText);

        //    // 订阅鼠标事件
        //    plotView.MouseMove += PlotView_MouseMove;
        //    plotView.MouseLeave += PlotView_MouseLeave;
        //}

        //private Crosshair _crosshair;
        //ScottPlot.Plottables.Marker MyHighlightMarker;
        //ScottPlot.Plottables.Text MyHighlightText;
        //private Dictionary<string, List<ILDataPoint>> _groupedData;
        //private Dictionary<string, Scatter> _scatterPlots;
        //private List<string> _seriesNames;
        //private bool _isILvMode = true;
        //private ScottPlot.WPF.WpfPlot wpfPlot;
        //public class ILDataPoint
        //{
        //    public double Current { get; set; }
        //    public double Luminance { get; set; }
        //   // public double Voltage { get; set; }
        //}

        //private class DataTableRow
        //{
        //    public string SeriesName { get; set; }
        //    public int Index { get; set; }
        //    public double Current { get; set; }
        //   // public double Voltage { get; set; }
        //    public double Luminance { get; set; }
        //}
        //private void SetupMouseInteraction()
        //{
        //    // Add crosshair for showing nearest data point
        //    _crosshair = wpfPlot.Plot.Add.Crosshair(0, 0);
        //    _crosshair.IsVisible = false;
        //    _crosshair.LineWidth = 1;
        //    _crosshair.LineColor = Color.FromColor(System.Drawing.Color.Gray);

        //    MyHighlightMarker = wpfPlot.Plot.Add.Marker(0, 0);
        //    MyHighlightMarker.Shape = MarkerShape.OpenCircle;
        //    MyHighlightMarker.Size = 17;
        //    MyHighlightMarker.LineWidth = 2;
        //    MyHighlightMarker.Color = Color.FromColor(System.Drawing.Color.Gray);

        //    // Create a text label to place near the highlighted value
        //    MyHighlightText = wpfPlot.Plot.Add.Text("", 0, 0);
        //    MyHighlightText.LabelAlignment = Alignment.LowerLeft;
        //    MyHighlightText.LabelBold = true;
        //    MyHighlightText.OffsetX = 7;
        //    MyHighlightText.OffsetY = -7;
        //    MyHighlightText.LabelFontColor = Color.FromColor(System.Drawing.Color.Gray);
        //    // Subscribe to mouse move events
        //    wpfPlot.MouseMove += WpfPlot_MouseMove;
        //    wpfPlot.MouseLeave += WpfPlot_MouseLeave;
        //}



        //private void WpfPlot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        //{
        //    // Get mouse position in plot coordinates
        //    var position = e.GetPosition(wpfPlot);

        //    position.X = position.X * dpiRadio;
        //    position.Y = position.Y * dpiRadio;

        //    var pixel = new Pixel((float)position.X, (float)position.Y);
        //    var coords = wpfPlot.Plot.GetCoordinates(pixel);

        //    // Find the nearest data point
        //    double minDistance = double.MaxValue;
        //    ILDataPoint? nearestPoint = null;
        //    string nearestSeriesName = string.Empty;

        //    foreach (var seriesName in _seriesNames)
        //    {
        //        if (!_groupedData.ContainsKey(seriesName))
        //            continue;

        //        foreach (var point in _groupedData[seriesName])
        //        {
        //            double x = point.Current;
        //            double y = point.Luminance;

        //            // Calculate distance in plot coordinates
        //            double dx = x - coords.X;
        //            double dy = y - coords.Y;
        //            double distance = Math.Sqrt(dx * dx + dy * dy);

        //            if (distance < minDistance)
        //            {
        //                minDistance = distance;
        //                nearestPoint = point;
        //                nearestSeriesName = seriesName;
        //            }
        //        }
        //    }

        //    // Show crosshair if a point is close enough
        //    if (nearestPoint != null)
        //    {
        //        double x = nearestPoint.Current;
        //        double y = nearestPoint.Luminance;
        //        var coords1 = new Coordinates(x, y);

        //        _crosshair.Position = coords1;
        //        _crosshair.IsVisible = true;

        //        string xLabel = _isILvMode ? "I" : "V";
        //        string xUnit = _isILvMode ? "mA" : "V";

        //        MyHighlightMarker.IsVisible = true;
        //        MyHighlightMarker.Location = coords1;

        //        MyHighlightText.IsVisible = true;
        //        MyHighlightText.Location = coords1;
        //        MyHighlightText.LabelText = $"{x:F2} {xUnit}\nLv: {y:F2} cd/m";
        //        MyHighlightText.LabelFontColor = Color.FromColor(System.Drawing.Color.Red);
        //        //MyHighlightText.LabelBorderColor = Color.FromColor(System.Drawing.Color.Red);

        //        wpfPlot.Refresh();
        //    }
        //    else
        //    {
        //        _crosshair.IsVisible = false;
        //        wpfPlot.Refresh();
        //    }
        //}

        //private void WpfPlot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        //{
        //    // Hide crosshair when mouse leaves the plot
        //    _crosshair.IsVisible = false;
        //    wpfPlot.Refresh();
        //}
        public void Clear()
        {
            no = 1;
            PlotModel.Series.Clear();
            Measurements.Clear();
        }
    }
}
