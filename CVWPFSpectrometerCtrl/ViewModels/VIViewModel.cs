using CVDB.Services.SMU;
using CVWaferProber.Core.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    /// <summary>
    /// VI曲线视图模型（X轴电压，Y轴电流）
    /// 仿照IVViewModel实现，核心为电压-电流轴位互换
    /// </summary>
    public class VIViewModel : ViewModelBase
    {
        private PlotModel _plotModel;
        private ObservableCollection<IVMeasurement> _measurements;

        // 测量序号自增
        int no = 1;

        /// <summary>
        /// VI测量数据集合
        /// 复用IVMeasurement模型（包含Voltage/Current/Timestamp）
        /// </summary>
        public ObservableCollection<IVMeasurement> Measurements
        {
            get => _measurements;
            set
            {
                _measurements = value;
                OnPropertyChanged(nameof(Measurements));
            }
        }

        /// <summary>
        /// 设备编码（与IVViewModel保持一致）
        /// </summary>
        public string DeviceCode { get; set; }

        /// <summary>
        /// 图表模型
        /// </summary>
        public PlotModel PlotModel
        {
            get => _plotModel;
            set
            {
                _plotModel = value;
                OnPropertyChanged(nameof(PlotModel));
            }
        }

        public VIViewModel()
        {
            // 初始化图表模型
            InitializeVIPlotModel();
            // 初始化测量数据集合
            Measurements = new ObservableCollection<IVMeasurement>();
            // 设备编码默认值与IVViewModel保持一致
            DeviceCode = "DEV.SMU.Default";
        }

        /// <summary>
        /// 初始化VI图表模型（核心：X轴电压，Y轴电流）
        /// </summary>
        private void InitializeVIPlotModel()
        {
            _plotModel = new PlotModel
            {
                // 绑定VI曲线国际化资源，与IV的Sp.IV Curve对应
                Title = (string)System.Windows.Application.Current.FindResource("Sp.VI Curve"),
                TitleFontSize = 14
            };

            // 设置X轴（电压）- 对应IV的Y轴配置
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = (string)System.Windows.Application.Current.FindResource("Sp.Voltage"),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = 0,
                Maximum = 8,
                MaximumRange = 10,
            };

            // 设置Y轴（电流）- 对应IV的X轴配置
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = (string)System.Windows.Application.Current.FindResource("Sp.Current"),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = 0,
                Maximum = 8,
                MaximumRange = 10,
            };

            _plotModel.Axes.Add(xAxis);
            _plotModel.Axes.Add(yAxis);
        }

        /// <summary>
        /// 加载VI数据（与IV逻辑一致，仅图表轴位不同，复用SMU数据加载）
        /// </summary>
        /// <param name="serialNumber">批次编号</param>
        public void LoadData(string serialNumber)
        {
            // 清空原有数据和图表
            Clear();
            // 从SMU服务加载数据，与IV复用同一接口
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
                // 与IV完全一致的数值赋值逻辑，复用测量模型
                if (isSourceV)
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

            // 重置轴默认范围 + 更新VI图表数据
            ResetAxisToDefault(isSourceV);
            UpdateVIData(isSourceV);
        }

        // 轴默认配置（与IV一致，仅轴映射关系互换）
        private PlotAxesCfg AxisV = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 6, DefaultMaxRange = 10000000000000000 };
        private PlotAxesCfg AxisI = new PlotAxesCfg() { DefaultMin = 0, DefaultMax = 100, DefaultMaxRange = 200000000000000000 };

        /// <summary>
        /// 重置VI轴到默认范围（核心：X轴=电压配置，Y轴=电流配置）
        /// </summary>
        /// <param name="isSourceV">是否为源电压模式</param>
        private void ResetAxisToDefault(bool isSourceV)
        {
            var xAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as LinearAxis;
            var yAxis = PlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) as LinearAxis;

            if (xAxis != null && yAxis != null)
            {
                // 无论源电压/源电流模式，VI的X轴始终是电压、Y轴始终是电流
                xAxis.Minimum = AxisV.DefaultMin;
                xAxis.Maximum = AxisV.DefaultMax;
                xAxis.MaximumRange = AxisV.DefaultMaxRange;
                xAxis.ExtraGridlines = null;

                yAxis.Minimum = AxisI.DefaultMin;
                yAxis.Maximum = AxisI.DefaultMax;
                yAxis.MaximumRange = AxisI.DefaultMaxRange;
                yAxis.ExtraGridlines = null;
            }
        }

        /// <summary>
        /// 更新VI图表数据（核心：X=Voltage，Y=Current）
        /// </summary>
        /// <param name="isSourceV">是否为源电压模式</param>
        public void UpdateVIData(bool isSourceV)
        {
            // 1. 数据校验：空集合则清空图表
            if (Measurements == null || Measurements.Count == 0)
            {
                PlotModel.Series.Clear();
                PlotModel.InvalidatePlot(true);
                return;
            }

            // 2. 动态计算电压（X轴）和电流（Y轴）的极值（扩1%留边距）
            double maxVoltage = Measurements.Max(m => m.Voltage) * 1.01;
            double minVoltage = Measurements.Min(m => m.Voltage) * 0.99;
            double maxCurrent = Measurements.Max(m => m.Current) * 1.01;
            double minCurrent = Measurements.Min(m => m.Current) * 0.99;

            // 3. 获取X/Y轴
            var xAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = PlotModel.Axes.OfType<LinearAxis>().FirstOrDefault(a => a.Position == AxisPosition.Left);

            if (xAxis != null && yAxis != null)
            {
                // 4. 动态锁定VI轴范围：X轴（电压）、Y轴（电流）
                xAxis.Minimum = minVoltage;
                xAxis.Maximum = maxVoltage;
                xAxis.AbsoluteMinimum = minVoltage;
                xAxis.AbsoluteMaximum = maxVoltage;

                yAxis.Minimum = minCurrent;
                yAxis.Maximum = maxCurrent;
                yAxis.AbsoluteMinimum = minCurrent;
                yAxis.AbsoluteMaximum = maxCurrent;
            }

            // 5. 创建VI曲线系列（样式与IV保持一致，红色带圆形标记）
            var lineSeries = new LineSeries
            {
                Title = (string)System.Windows.Application.Current.FindResource("Sp.VI Curve"),
                Color = OxyColors.Red,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Red,
                MarkerStroke = OxyColors.Red,
                MarkerStrokeThickness = 1.5,
                LineStyle = LineStyle.Solid,
                CanTrackerInterpolatePoints = true
            };

            // 6. 添加VI数据点：核心差异【X=电压，Y=电流】
            foreach (var measurement in Measurements)
            {
                lineSeries.Points.Add(new DataPoint(measurement.Voltage, measurement.Current));
            }

            // 7. 保持最近50个数据点（与IV逻辑一致）
            if (lineSeries.Points.Count > 50)
            {
                lineSeries.Points.RemoveRange(0, lineSeries.Points.Count - 50);
            }

            // 8. 刷新图表
            PlotModel.Series.Clear();
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true);
        }

        /// <summary>
        /// 重置缩放（恢复默认轴范围）
        /// </summary>
        public void btnResetZoom()
        {
            // 判断源模式，无数据则默认源电压
            bool isSourceV = Measurements.Any()
                ? Measurements.First().Voltage == (double)SMUResultService.LoadResultByBatchCode(DeviceCode, "").First().SrcValue
                : true;
            // 重置默认轴范围并刷新
            ResetAxisToDefault(isSourceV);
            PlotModel.InvalidatePlot(true);
        }

        /// <summary>
        /// 清空所有数据和图表，恢复初始轴范围
        /// </summary>
        public void Clear()
        {
            // 重置测量序号
            no = 1;
            // 清空图表系列和测量数据
            PlotModel.Series.Clear();
            Measurements.Clear();

            // 恢复初始轴范围（与IV初始化一致）
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

            // 强制刷新图表
            PlotModel.InvalidatePlot(true);
        }
    }
}