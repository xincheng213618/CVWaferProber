using System.Drawing;

namespace ChipMapping.Models
{
    public class CoordinateConverter
    {
        /// <summary>
        /// 屏幕尺寸
        /// </summary>
        public Size ScreenSize { get; private set; }

        /// <summary>
        /// 数学坐标系的范围
        /// </summary>
        public RectangleF MathBounds { get; private set; }

        /// <summary>
        /// 缩放比例
        /// </summary>
        public float ScaleX { get; private set; }
        public float ScaleY { get; private set; }

        public CoordinateConverter(Size screenSize, RectangleF mathBounds)
        {
            ScreenSize = screenSize;
            MathBounds = mathBounds;

            // 计算缩放比例
            ScaleX = screenSize.Width / mathBounds.Width;
            ScaleY = screenSize.Height / mathBounds.Height;
        }

        /// <summary>
        /// 将数学坐标转换为屏幕坐标
        /// </summary>
        public PointF MathToScreen(PointF mathPoint)
        {
            // 首先将数学坐标相对于数学坐标系原点进行归一化
            float normalizedX = mathPoint.X - MathBounds.Left;
            float normalizedY = mathPoint.Y - MathBounds.Top;

            // 缩放并翻转y轴
            float screenX = normalizedX * ScaleX;
            float screenY = ScreenSize.Height - (normalizedY * ScaleY);

            return new PointF(screenX, screenY);
        }

        /// <summary>
        /// 将屏幕坐标转换为数学坐标
        /// </summary>
        public PointF ScreenToMath(PointF screenPoint)
        {
            // 翻转y轴并反向缩放
            float normalizedX = screenPoint.X / ScaleX;
            float normalizedY = (ScreenSize.Height - screenPoint.Y) / ScaleY;

            // 加上数学坐标系的原点偏移
            float mathX = normalizedX + MathBounds.Left;
            float mathY = normalizedY + MathBounds.Top;

            return new PointF(mathX, mathY);
        }

        /// <summary>
        /// 批量转换数学坐标到屏幕坐标
        /// </summary>
        public PointF[] MathToScreen(PointF[] mathPoints)
        {
            PointF[] screenPoints = new PointF[mathPoints.Length];
            for (int i = 0; i < mathPoints.Length; i++)
            {
                screenPoints[i] = MathToScreen(mathPoints[i]);
            }
            return screenPoints;
        }
    }
}
