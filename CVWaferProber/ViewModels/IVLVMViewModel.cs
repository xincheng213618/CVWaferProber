using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System.Collections.ObjectModel;

namespace CVWaferProber.ViewModels
{
    public class IVLVMViewModel
    {
        public PlotModel SpectrumPlot { get; set; } = new PlotModel { Title = "光谱" };

        public IVLVMViewModel()
        {
            // 初始化示例数据（实际项目中从业务逻辑加载）
            SpectrumPlot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "波长(nm)" });
            SpectrumPlot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "强度" });
            SpectrumPlot.Series.Add(new LineSeries
            {
                Title = "光谱数据",
                Points = { new DataPoint(400, 20), new DataPoint(600, 50), new DataPoint(800, 30) }
            });
            SpectrumPlot.Legends.Add(new Legend { LegendPosition = LegendPosition.RightMiddle });
        }

        /// <summary>
        /// 克隆PlotModel（解决OxyPlot资源冲突）
        /// </summary>
      
    }

}