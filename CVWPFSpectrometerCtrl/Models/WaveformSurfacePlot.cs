using ScottPlot;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.Models
{
    public class WaveformSurfacePlot : IPlottable
    {
        public double[] Wavelengths { get; set; }
        public double[] Intensities { get; set; }
        public IColormap Colormap { get; set; }
        public bool IsVisible { get; set; } = true;
        public IAxes Axes { get; set; } = ScottPlot.Axes.Default;

        public IEnumerable<LegendItem> LegendItems => Enumerable.Empty<LegendItem>();

        public WaveformSurfacePlot(double[] wavelengths, double[] intensities, IColormap colormap)
        {
            Wavelengths = wavelengths;
            Intensities = intensities;
            Colormap = colormap;
        }

        public AxisLimits GetAxisLimits()
        {
            if (Wavelengths == null || Wavelengths.Length == 0)
                return AxisLimits.NoLimits;

            return new AxisLimits(
                Wavelengths.Min(), Wavelengths.Max(),
                Intensities.Min(), Intensities.Max() // Y轴范围
            );
        }

        public void Render(RenderPack rp)
        {
            if (Wavelengths == null || Intensities == null) return;

            int resolution = 200; // 波浪的细分分辨率

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                IsStroke = false
            };

            // 绘制波浪曲面
            for (int waveIndex = 0; waveIndex < Wavelengths.Length - 1; waveIndex++)
            {
                double wavelength = Wavelengths[waveIndex];
                double nextWavelength = Wavelengths[waveIndex + 1];
                double intensity = Intensities[waveIndex];
                double nextIntensity = Intensities[waveIndex + 1];

                // 为当前波长段创建颜色
                double normalizedWavelength = (wavelength - 380) / (780 - 380);
                var color = Colormap.GetColor(normalizedWavelength);
                paint.Color = new SKColor(color.Red, color.Green, color.Blue, 255); // 半透明

                // 创建波浪路径
                using var path = new SKPath();

                // 波浪的起点
                float startX = Axes.GetPixelX(wavelength);
                float startYBase = Axes.GetPixelY(0);

                path.MoveTo(startX, startYBase);

                // 创建波浪形状
                for (int i = 0; i <= resolution; i++)
                {
                    double t = (double)i / resolution;
                    double currentWavelength = wavelength + (nextWavelength - wavelength) * t;
                    double currentIntensity = intensity + (nextIntensity - intensity) * t;

                    // 波浪函数
                    double waveY = Math.Sin(t * Math.PI * 8) * 0.3 * currentIntensity;
                    double envelope = Math.Sin(t * Math.PI) * currentIntensity;

                    float x = Axes.GetPixelX(currentWavelength);
                    float y = Axes.GetPixelY(waveY + envelope * 0.5);

                    if (i == 0)
                        path.MoveTo(x, y);
                    else
                        path.LineTo(x, y);
                }

                // 闭合路径形成曲面
                path.LineTo(Axes.GetPixelX(nextWavelength), startYBase);
                path.Close();

                rp.Canvas.DrawPath(path, paint);
            }
        }
    }
}
