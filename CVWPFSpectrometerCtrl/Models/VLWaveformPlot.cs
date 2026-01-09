using ScottPlot;
using ScottPlot.Colormaps;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.Models
{
    public class VLWaveformPlot : IPlottable
    {
        #region 公共属性（配置与数据）
        /// <summary>
        /// 电压数组（X轴数据源，如 0~5V）
        /// </summary>
        public double[] Voltages { get; set; }

        /// <summary>
        /// 亮度数组（与Voltages一一对应，单位如 cd/m²，决定波浪起伏幅度）
        /// </summary>
        public double[] Luminances { get; set; }

        /// <summary>
        /// 色图（将电压值映射为颜色，默认使用Jet色图）
        /// </summary>
        public IColormap Colormap { get; set; }
        /// <summary>
        /// 组件是否可见（ScottPlot标准属性）
        /// </summary>
        public bool IsVisible { get; set; } = true;
        public IAxes Axes { get; set; } = ScottPlot.Axes.Default;

        /// <summary>
        /// 图例项（当前组件无需图例，返回空集合）
        /// </summary>
        public IEnumerable<LegendItem> LegendItems => Enumerable.Empty<LegendItem>();
        /// <summary>
        /// 波浪细分分辨率（值越大波浪越平滑，默认200）
        /// </summary>
        public int WaveResolution { get; set; } = 200;

        /// <summary>
        /// 波纹幅度系数（值越大高频波纹越明显，默认0.3）
        /// </summary>
        public double WaveAmplitudeFactor { get; set; } = 0.3;

        /// <summary>
        /// 包络线系数（值越大整体轮廓越突出，默认0.5）
        /// </summary>
        public double EnvelopeFactor { get; set; } = 0.5;
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化电压-亮度波浪曲面图
        /// </summary>
        /// <param name="voltages">电压数组（如 0~5V）</param>
        /// <param name="luminances">亮度数组（与电压一一对应）</param>
        /// <param name="colormap">色图（可选，默认Jet色图）</param>
        public static bool IsEnglishMode = false;
        public VLWaveformPlot(double[] voltages, double[] luminances, IColormap colormap =null)
        {
            // 校验参数合法性（避免后续绘图异常）
            if (voltages == null || voltages.Length == 0)
                throw new ArgumentException(IsEnglishMode? "The voltage array cannot be null or empty" : "电压数组不能为空或空数组", nameof(voltages));
            if (luminances == null || luminances.Length == 0)
                throw new ArgumentException(IsEnglishMode? "The brightness array cannot be null or empty" : "亮度数组不能为空或空数组", nameof(luminances));
            if (voltages.Length != luminances.Length)
                throw new ArgumentException(IsEnglishMode? "The length of the voltage array and the brightness array must be the same." : "电压数组和亮度数组长度必须一致", nameof(luminances));

            Voltages = voltages;
            Luminances = luminances;
            if (colormap != null)
                Colormap = colormap;
        }
        #endregion

        #region 接口实现：获取坐标轴范围
        /// <summary>
        /// 计算并返回组件所需的坐标轴范围（ScottPlot自动适配显示）
        /// </summary>
        /// <returns>电压-亮度的坐标轴范围</returns>
        public AxisLimits GetAxisLimits()
        {
            double minV = Voltages.Min();
            double maxV = Voltages.Max();
            double minLv = Luminances.Min();
            double maxLv = Luminances.Max();

            // 扩展10%的边距，避免图形贴边
            double vMargin = (maxV - minV) * 0.1;
            double lvMargin = (maxLv - minLv) * 0.1;

            return new AxisLimits(
                minV - vMargin, maxV + vMargin,
                minLv - lvMargin, maxLv + lvMargin
            );
        }
        #endregion

        #region 核心接口：渲染波浪曲面图
        /// <summary>
        /// 渲染电压-亮度波浪曲面（ScottPlot调用此方法绘制）
        /// </summary>
        /// <param name="rp">ScottPlot渲染包（包含画布、渲染参数等）</param>
        public void Render(RenderPack rp)
        {
            // 空值校验（避免空引用异常）
            if (!IsVisible) return;
            if (Voltages == null || Luminances == null) return;
            if (Voltages.Length < 2 || Luminances.Length < 2) return; // 至少需要2个点才能绘制线段

            // 创建Skia绘图工具（填充样式，抗锯齿）
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                IsStroke = false,
                Color = SKColors.White // 默认颜色（后续会按电压动态替换）
            };

            // 遍历所有相邻的电压对，逐段绘制波浪曲面
            for (int vIndex = 0; vIndex < Voltages.Length - 1; vIndex++)
            {
                // 获取当前电压段的核心数据（起点+终点）
                double currentV = Voltages[vIndex];
                double nextV = Voltages[vIndex + 1];
                double currentLv = Luminances[vIndex];
                double nextLv = Luminances[vIndex + 1];

                // 1. 电压值归一化（0~1范围），用于色图映射
                double minV = Voltages.Min();
                double maxV = Voltages.Max();
                double normalizedV = (currentV - minV) / (maxV - minV);
                normalizedV = Math.Clamp(normalizedV, 0, 1); // 确保值在0~1之间（避免超出色图范围）

                // 2. 从色图获取当前电压对应的颜色（设置绘图颜色）
                var color = Colormap.GetColor(normalizedV);
                paint.Color = new SKColor(color.Red, color.Green, color.Blue, 220); // 220=半透明（优化视觉效果）

                // 3. 创建当前电压段的波浪路径（封闭区域用于填充）
                using var path = new SKPath();

                // 3.1 曲面底部基准线起点（电压起点，亮度0的像素位置）
                float baseStartX = Axes.GetPixelX(currentV);
                float baseY = Axes.GetPixelY(0); // 亮度为0的基准线
                path.MoveTo(baseStartX, baseY);

                // 3.2 构建波浪形状（细分当前电压段为多个小片段，实现平滑波浪）
                for (int i = 0; i <= WaveResolution; i++)
                {
                    // 插值参数t（0~1，从当前电压过渡到下一个电压）
                    double t = (double)i / WaveResolution;

                    // 线性插值计算当前片段的电压和亮度（平滑过渡）
                    double interpolatedV = currentV + (nextV - currentV) * t;
                    double interpolatedLv = currentLv + (nextLv - currentLv) * t;

                    // 4. 波浪函数：高频波纹 + 低频包络线（亮度决定起伏幅度）
                    double wave = Math.Sin(t * Math.PI * 8) * WaveAmplitudeFactor * interpolatedLv; // 高频波纹（4个波峰波谷）
                    double envelope = Math.Sin(t * Math.PI) * interpolatedLv; // 低频包络线（小山丘形状）
                    double totalYData = wave + envelope * EnvelopeFactor; // 总Y轴数据坐标

                    // 5. 数据坐标转换为画布像素坐标
                    float pixelX = Axes.GetPixelX(interpolatedV);
                    float pixelY = Axes.GetPixelY(totalYData);

                    // 6. 构建波浪路径（第一个点移动，后续点连线）
                    if (i == 0)
                        path.MoveTo(pixelX, pixelY);
                    else
                        path.LineTo(pixelX, pixelY);
                }

                // 3.3 闭合路径（连接到底部基准线终点，形成封闭区域）
                float baseEndX = Axes.GetPixelX(nextV);
                path.LineTo(baseEndX, baseY);
                path.Close(); // 自动连接起点和终点，完成封闭

                // 7. 绘制当前电压段的曲面（填充颜色）
                rp.Canvas.DrawPath(path, paint);
            }
        }
        #endregion
    }
}
