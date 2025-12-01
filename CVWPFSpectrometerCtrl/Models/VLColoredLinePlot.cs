using ScottPlot;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.Models
{
    public class VLColoredLinePlot : IPlottable
    {
        #region 公共属性（外部配置用）
        /// <summary>
        /// X轴数据：电压（单位V，如0~5V）
        /// </summary>
        public double[] Voltages { get; set; } // 对应原Xs，更语义化的命名

        /// <summary>
        /// Y轴数据：亮度（单位cd/m²或相对值）
        /// </summary>
        public double[] Luminances { get; set; } // 对应原Ys，更语义化的命名

        /// <summary>
        /// 颜色映射表（默认：蓝→绿→黄→红，适配V-L数据的递增趋势）
        /// </summary>
        public ScottPlot.IColormap Colormap { get; set; }

        /// <summary>
        /// 线条宽度（默认3像素）
        /// </summary>
        public double LineWidth { get; set; } = 3;

        /// <summary>
        /// 渐变模式：按电压渐变 / 按亮度渐变
        /// </summary>
        public GradientMode Mode { get; set; } = GradientMode.ByVoltage; // 默认按电压渐变

        /// <summary>
        /// 手动指定电压范围（可选，不指定则自动取数据的Min/Max）
        /// 用于多组数据对比时，保证颜色映射标准一致（如统一0~5V）
        /// </summary>
        public (double Min, double Max) CustomVoltageRange { get; set; } = (double.NaN, double.NaN);

        /// <summary>
        /// 手动指定亮度范围（可选，不指定则自动取数据的Min/Max）
        /// 用于多组数据对比时，保证颜色映射标准一致（如统一0~1000cd/m²）
        /// </summary>
        public (double Min, double Max) CustomLuminanceRange { get; set; } = (double.NaN, double.NaN);
        #endregion

        #region IPlottable 接口强制实现（ScottPlot 绘图核心）
        /// <summary>
        /// 线条是否可见（默认可见）
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 绘图坐标系（绑定ScottPlot默认笛卡尔坐标系，负责数据→像素转换）
        /// </summary>
        public IAxes Axes { get; set; } = ScottPlot.Axes.Default;

        /// <summary>
        /// 获取V-L数据的轴范围（ScottPlot自动缩放时使用）
        /// </summary>
        public AxisLimits GetAxisLimits() =>
            // 数据校验：电压和亮度数组需非空且长度一致
            Voltages != null && Luminances != null && Voltages.Length > 0 && Voltages.Length == Luminances.Length
                ? new AxisLimits(
                    Voltages.Min(), // X轴（电压）最小值
                    Voltages.Max(), // X轴（电压）最大值
                    Luminances.Min(), // Y轴（亮度）最小值
                    Luminances.Max()  // Y轴（亮度）最大值
                  )
                : AxisLimits.NoLimits; // 数据无效时返回无限制

        /// <summary>
        /// 图例项（V-L曲线通常不需要单独图例，返回空集合）
        /// </summary>
        public IEnumerable<LegendItem> LegendItems => Enumerable.Empty<LegendItem>();
        #endregion

        #region 渐变模式枚举（明确两种渐变逻辑）
        /// <summary>
        /// 渐变模式：按电压或亮度决定颜色
        /// </summary>
        public enum GradientMode
        {
            /// <summary>
            /// 按电压（X轴）渐变：电压从低到高 → 颜色从蓝到红
            /// </summary>
            ByVoltage,
            /// <summary>
            /// 按亮度（Y轴）渐变：亮度从低到高 → 颜色从蓝到红
            /// </summary>
            ByLuminance
        }
        #endregion

        #region 构造函数（初始化核心参数）
        /// <summary>
        /// 构造函数：创建V-L彩色线条实例
        /// </summary>
        /// <param name="voltages">电压数据数组（X轴）</param>
        /// <param name="luminances">亮度数据数组（Y轴）</param>
        /// <param name="colormap">颜色映射表（默认提供V-L常用渐变）</param>
        /// <param name="mode">渐变模式（默认按电压）</param>
        public VLColoredLinePlot(
            double[] voltages,
            double[] luminances,
            ScottPlot.IColormap colormap = null,
            GradientMode mode = GradientMode.ByVoltage)
        {
            Voltages = voltages ?? throw new ArgumentNullException(nameof(voltages), "电压数据不能为null");
            Luminances = luminances ?? throw new ArgumentNullException(nameof(luminances), "亮度数据不能为null");
            if (voltages.Length != luminances.Length)
                throw new ArgumentException("电压和亮度数组长度必须一致");

            // 若未指定颜色映射表，默认使用「蓝→绿→黄→红」（适配V-L递增趋势）
            Colormap = colormap ?? CreateDefaultVLColormap();
            Mode = mode;
        }
        #endregion

        #region 核心辅助方法（创建默认颜色映射、计算归一化值）
        /// <summary>
        /// 创建V-L场景默认颜色映射表：蓝（低电压/低亮度）→ 绿 → 黄 → 红（高电压/高亮度）
        /// </summary>
        private ScottPlot.IColormap CreateDefaultVLColormap()
        {
            ScottPlot.Color[] vlColors = {
            ScottPlot.Color.FromHex("#0000FF"), // 蓝色：低电压/低亮度
            ScottPlot.Color.FromHex("#00FF00"), // 绿色：中低电压/亮度
            ScottPlot.Color.FromHex("#FFFF00"), // 黄色：中高电压/亮度
            ScottPlot.Color.FromHex("#FF0000")  // 红色：高电压/高亮度
        };
            return new ScottPlot.Colormaps.Custom(vlColors);
        }

        /// <summary>
        /// 计算归一化值（将目标值映射到0~1范围，用于颜色查找）
        /// </summary>
        /// <param name="value">当前值（电压或亮度）</param>
        /// <param name="min">最小值（数据自动计算或手动指定）</param>
        /// <param name="max">最大值（数据自动计算或手动指定）</param>
        /// <returns>归一化后的值（0~1）</returns>
        private double NormalizeValue(double value, double min, double max)
        {
            // 避免除以0（数据全部相同的极端情况）
            if (Math.Abs(max - min) < 1e-6)
                return 0.5; // 全部返回中间色

            // 归一化公式：(当前值 - 最小值) / (最大值 - 最小值)
            double normalized = (value - min) / (max - min);
            // 限制在0~1之间（避免异常值导致颜色映射错误）
            return Math.Max(0, Math.Min(1, normalized));
        }
        #endregion

        #region 核心绘图方法（ScottPlot自动调用）
        /// <summary>
        /// 渲染V-L彩色线条：逐段绘制相邻数据点，按模式分配颜色
        /// </summary>
        /// <param name="rp">ScottPlot绘图资源包（含画布、画笔等）</param>
        public void Render(RenderPack rp)
        {
            // 数据校验：排除空值、长度不匹配、不可见的情况
            if (!IsVisible || Voltages == null || Luminances == null || Voltages.Length < 2)
                return;

            // 创建SkiaSharp画笔（控制线条样式）
            // using关键字：自动释放资源，避免内存泄漏
            using var paint = new SKPaint
            {
                IsAntialias = true, // 启用抗锯齿（线条更平滑）
                StrokeWidth = (float)LineWidth, // 线条宽度（适配SkiaSharp的float类型）
                Style = SKPaintStyle.Stroke, // 仅绘制线条（不填充）
                StrokeCap = SKStrokeCap.Round // 线条端点圆角（避免尖锐边缘）
            };

            // 计算电压/亮度的有效范围（优先使用手动指定范围，否则自动计算）
            double voltMin = double.IsNaN(CustomVoltageRange.Min) ? Voltages.Min() : CustomVoltageRange.Min;
            double voltMax = double.IsNaN(CustomVoltageRange.Max) ? Voltages.Max() : CustomVoltageRange.Max;
            double lumMin = double.IsNaN(CustomLuminanceRange.Min) ? Luminances.Min() : CustomLuminanceRange.Min;
            double lumMax = double.IsNaN(CustomLuminanceRange.Max) ? Luminances.Max() : CustomLuminanceRange.Max;

            // 逐段绘制V-L线条（相邻两个数据点组成一条小线段）
            for (int i = 0; i < Voltages.Length - 1; i++)
            {
                #region 步骤1：数据坐标 → 画布像素坐标
                // 获取当前点和下一个点的电压、亮度值
                double v1 = Voltages[i];
                double lum1 = Luminances[i];
                double v2 = Voltages[i + 1];
                double lum2 = Luminances[i + 1];

                // 通过Axes接口将数值坐标转换为画布像素坐标（ScottPlot自动处理屏幕坐标系）
                float x1 = Axes.GetPixelX(v1);
                float y1 = Axes.GetPixelY(lum1);
                float x2 = Axes.GetPixelX(v2);
                float y2 = Axes.GetPixelY(lum2);
                #endregion

                #region 步骤2：按模式计算当前线段的颜色
                double normalizedValue; // 归一化值（0~1），用于查找颜色
                switch (Mode)
                {
                    case GradientMode.ByVoltage:
                        // 按电压渐变：使用当前线段起始点的电压计算颜色
                        normalizedValue = NormalizeValue(v1, voltMin, voltMax);
                        break;
                    case GradientMode.ByLuminance:
                        // 按亮度渐变：使用当前线段起始点的亮度计算颜色
                        normalizedValue = NormalizeValue(lum1, lumMin, lumMax);
                        break;
                    default:
                        normalizedValue = 0;
                        break;
                }

                // 从颜色映射表中获取当前归一化值对应的颜色
                ScottPlot.Color lineColor = Colormap.GetColor(normalizedValue);
                // 将ScottPlot.Color转换为SkiaSharp的SKColor（画笔需要该类型）
                paint.Color = new SKColor(lineColor.Red, lineColor.Green, lineColor.Blue, lineColor.Alpha);
                #endregion

                #region 步骤3：绘制线段
                // 在画布上绘制从(x1,y1)到(x2,y2)的线段
                rp.Canvas.DrawLine(x1, y1, x2, y2, paint);
                #endregion
            }
        }
        #endregion
    }
}
