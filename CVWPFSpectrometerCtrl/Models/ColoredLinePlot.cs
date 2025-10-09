using ScottPlot;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.Models
{
    // 自定义彩色线条绘图类
    public class ColoredLinePlot : IPlottable
    {
        public double[] Xs { get; set; }
        public double[] Ys { get; set; }
        public ScottPlot.IColormap Colormap { get; set; }
        public double LineWidth { get; set; } = 3;

        // 实现IPlottable接口所需的成员
        public bool IsVisible { get; set; } = true;
        //public IAxes Axes { get; set; } = new StandardAxes.Cartesian(); // 关键：实现Axes属性
        public IAxes Axes { get; set; } = ScottPlot.Axes.Default; // 旧版或不存在的方式
        public AxisLimits GetAxisLimits() => Xs != null && Ys != null && Xs.Length > 0
            ? new AxisLimits(Xs.Min(), Xs.Max(), Ys.Min(), Ys.Max())
            : AxisLimits.NoLimits;

        public IEnumerable<LegendItem> LegendItems => Enumerable.Empty<LegendItem>();

        public ColoredLinePlot(double[] xs, double[] ys, ScottPlot.IColormap colormap)
        {
            Xs = xs;
            Ys = ys;
            Colormap = colormap;
        }

        public void Render(RenderPack rp)
        {
            if (Xs == null || Ys == null || Xs.Length == 0) return;

            using var paint = new SKPaint
            {
                IsAntialias = true,
                StrokeWidth = (float)LineWidth,
                Style = SKPaintStyle.Stroke
            };

            for (int i = 0; i < Xs.Length - 1; i++)
            {
                // 使用Axes属性进行坐标转换
                float x1 = Axes.GetPixelX(Xs[i]);
                float y1 = Axes.GetPixelY(Ys[i]);
                float x2 = Axes.GetPixelX(Xs[i + 1]);
                float y2 = Axes.GetPixelY(Ys[i + 1]);

                double normalizedWavelength = (Xs[i] - 380) / (780 - 380);
                normalizedWavelength = Math.Max(0, Math.Min(1, normalizedWavelength));

                var color = Colormap.GetColor(normalizedWavelength);
                paint.Color = new SKColor(color.Red, color.Green, color.Blue, color.Alpha);

                rp.Canvas.DrawLine(x1, y1, x2, y2, paint);
            }
        }
    }
}
