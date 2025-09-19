using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CVImageView
{
    public class POIMarker
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 10;
        public double Height { get; set; } = 10;
        public Brush Fill { get; set; } = Brushes.Red;
        public Brush Stroke { get; set; } = Brushes.White;
        public double StrokeThickness { get; set; } = 2;
        public object Tag { get; set; }
    }

    // 矩形标记
    public class RectangleMarker : POIMarker
    {
        // 可以添加矩形特定的属性
    }

    // 圆形标记
    public class CircleMarker : POIMarker
    {
        // 可以添加圆形特定的属性
    }
}
