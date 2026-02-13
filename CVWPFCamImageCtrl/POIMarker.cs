using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CVWPFCamImageCtrl
{
    public class POIMarker
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 10;
        public double Height { get; set; } = 10;
        public Brush Fill { get; set; } = Brushes.Red;
        public Brush Stroke { get; set; } = Brushes.Red;
        public double StrokeThickness { get; set; } = 1;
        public object Tag { get; set; }
        public string Label { get; set; } = string.Empty;
        public OpenCvSharp.Scalar Color { get; set; } = OpenCvSharp.Scalar.Red;
    }

    // 矩形标记
    public class RectangleMarker : POIMarker
    {
        // 可以添加矩形特定的属性
    }

    // 圆形标记
    public class CircleMarker : POIMarker
    {
        public double Radius => Width / 2.0;
   }
}
