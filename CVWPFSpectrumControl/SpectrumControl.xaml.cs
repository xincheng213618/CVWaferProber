using CVWPFSpectrumControl.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CVWPFSpectrumControl
{
    /// <summary>
    /// SpectrumControl.xaml 的交互逻辑
    /// </summary>
    public partial class SpectrumControl : UserControl
    {
        public static readonly DependencyProperty SpectralDataProperty =
             DependencyProperty.Register("SpectralData", typeof(SpectralData),
                 typeof(SpectrumControl),
                 new PropertyMetadata(null, OnSpectralDataChanged));

        public static readonly DependencyProperty ShowSurfaceProperty =
            DependencyProperty.Register("ShowSurface", typeof(bool),
                typeof(SpectrumControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowGridProperty =
            DependencyProperty.Register("ShowGrid", typeof(bool),
                typeof(SpectrumControl),
                new PropertyMetadata(true));

        public SpectralData SpectralData
        {
            get => (SpectralData)GetValue(SpectralDataProperty);
            set => SetValue(SpectralDataProperty, value);
        }

        public bool ShowSurface
        {
            get => (bool)GetValue(ShowSurfaceProperty);
            set => SetValue(ShowSurfaceProperty, value);
        }

        public bool ShowGrid
        {
            get => (bool)GetValue(ShowGridProperty);
            set => SetValue(ShowGridProperty, value);
        }

        public SpectrumControl()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdateCurve();
            SizeChanged += (s, e) => UpdateCurve();
        }

        private static void OnSpectralDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpectrumControl control)
            {
                control.UpdateCurve();
            }
        }

        private void UpdateCurve()
        {
            if (PART_Canvas == null) return;

            PART_Canvas.Children.Clear();

            if (SpectralData?.DataPoints == null || !SpectralData.DataPoints.Any())
            {
                PART_NoDataText.Visibility = Visibility.Visible;
                return;
            }

            PART_NoDataText.Visibility = Visibility.Collapsed;
            DrawSpectrum();
        }

        private void DrawSpectrum()
        {
            double width = PART_Canvas.ActualWidth;
            double height = PART_Canvas.ActualHeight;
            double margin = 40;

            if (width <= 2 * margin || height <= 2 * margin) return;

            double plotWidth = width - 2 * margin;
            double plotHeight = height - 2 * margin;

            if (ShowGrid)
            {
                DrawGrid(margin, plotWidth, plotHeight);
            }

            DrawAxes(margin, plotWidth, plotHeight);

            if (ShowSurface)
            {
                DrawSurface2(margin, plotWidth, plotHeight);
            }

            DrawCurve(margin, plotWidth, plotHeight);
            DrawLabels(margin, plotWidth, plotHeight);
        }

        private void DrawGrid(double margin, double plotWidth, double plotHeight)
        {
            // 水平网格线
            for (int i = 0; i <= 5; i++)
            {
                double y = margin + (i * plotHeight / 5);

                Line gridLine = new Line
                {
                    X1 = margin,
                    Y1 = y,
                    X2 = margin + plotWidth,
                    Y2 = y,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 0.5,
                    Opacity = 0.5
                };

                PART_Canvas.Children.Add(gridLine);
            }

            // 垂直网格线
            for (int i = 0; i <= 8; i++)
            {
                double x = margin + (i * plotWidth / 8);

                Line gridLine = new Line
                {
                    X1 = x,
                    Y1 = margin,
                    X2 = x,
                    Y2 = margin + plotHeight,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 0.5,
                    Opacity = 0.5
                };

                PART_Canvas.Children.Add(gridLine);
            }
        }

        private void DrawAxes(double margin, double plotWidth, double plotHeight)
        {
            // X轴
            Line xAxis = new Line
            {
                X1 = margin,
                Y1 = margin + plotHeight,
                X2 = margin + plotWidth,
                Y2 = margin + plotHeight,
                Stroke = Brushes.White,
                StrokeThickness = 2
            };

            // Y轴
            Line yAxis = new Line
            {
                X1 = margin,
                Y1 = margin,
                X2 = margin,
                Y2 = margin + plotHeight,
                Stroke = Brushes.White,
                StrokeThickness = 2
            };

            PART_Canvas.Children.Add(xAxis);
            PART_Canvas.Children.Add(yAxis);
        }
        private void DrawSurface(double margin, double plotWidth, double plotHeight)
        {
            // 使用 GeometryGroup 来组合多个小矩形，每个矩形有自己的颜色
            var geometryGroup = new GeometryGroup();
            double baseY = margin + plotHeight;

            var previousPoint = SpectralData.DataPoints.First();

            for (int i = 1; i < SpectralData.DataPoints.Count; i++)
            {
                var currentPoint = SpectralData.DataPoints[i];

                double x1 = margin + (previousPoint.Wavelength - SpectralData.MinWavelength) /
                    (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;
                double y1 = margin + plotHeight - (previousPoint.Intensity / Math.Max(SpectralData.MaxIntensity, 0.1) * plotHeight * 0.9);

                double x2 = margin + (currentPoint.Wavelength - SpectralData.MinWavelength) /
                    (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;
                double y2 = margin + plotHeight - (currentPoint.Intensity / Math.Max(SpectralData.MaxIntensity, 0.1) * plotHeight * 0.9);

                // 创建四边形几何图形
                var rectangleGeometry = new StreamGeometry();
                using (var context = rectangleGeometry.Open())
                {
                    context.BeginFigure(new Point(x1, baseY), true, true);
                    context.LineTo(new Point(x1, y1), true, true);
                    context.LineTo(new Point(x2, y2), true, true);
                    context.LineTo(new Point(x2, baseY), true, true);
                }
                rectangleGeometry.Freeze();

                geometryGroup.Children.Add(rectangleGeometry);
                previousPoint = currentPoint;
            }

            // 由于每个小矩形需要不同颜色，我们需要分别绘制它们
            PART_Canvas.Children.Add(CreateColoredSurface(geometryGroup, margin, plotWidth, baseY));
        }

        private Path CreateColoredSurface(GeometryGroup geometryGroup, double margin, double plotWidth, double baseY)
        {
            // 使用 VisualBrush 或 DrawingBrush 来实现逐点颜色渐变
            var drawingGroup = new DrawingGroup();

            // 为每个数据点创建一个小矩形绘制
            for (int i = 0; i < SpectralData.DataPoints.Count - 1; i++)
            {
                var point1 = SpectralData.DataPoints[i];
                var point2 = SpectralData.DataPoints[i + 1];

                double x1 = margin + (point1.Wavelength - SpectralData.MinWavelength) /
                    (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;
                double y1 = baseY - (point1.Intensity / Math.Max(SpectralData.MaxIntensity, 0.1) * (baseY - margin) * 0.9);

                double x2 = margin + (point2.Wavelength - SpectralData.MinWavelength) /
                    (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;

                // 计算平均颜色
                var avgColor = AverageColor(
                    point1.Color,
                    point2.Color
                );

                var brush = new SolidColorBrush(avgColor);

                // 创建矩形几何图形
                var rectGeometry = new RectangleGeometry(new Rect(
                    new Point(x1, y1),
                    new Point(x2, baseY)
                ));

                var geometryDrawing = new GeometryDrawing(brush, null, rectGeometry);
                drawingGroup.Children.Add(geometryDrawing);
            }

            var drawingBrush = new DrawingBrush(drawingGroup)
            {
                Stretch = Stretch.None
            };

            return new Path
            {
                Data = geometryGroup,
                Fill = drawingBrush,
                Opacity = 0.7
            };
        }

        private Color AverageColor(Color color1, Color color2)
        {
            return Color.FromRgb(
                (byte)((color1.R + color2.R) / 2),
                (byte)((color1.G + color2.G) / 2),
                (byte)((color1.B + color2.B) / 2)
            );
        }
        private void DrawSurface2(double margin, double plotWidth, double plotHeight)
        {
            // 创建曲面几何图形
            var geometry = new StreamGeometry();

            using (var context = geometry.Open())
            {
                bool isFirst = true;
                double baseY = margin + plotHeight;

                // 先绘制曲线上的点
                foreach (var point in SpectralData.DataPoints)
                {
                    double x = margin + (point.Wavelength - SpectralData.MinWavelength) /
                        (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;
                    double y = margin + plotHeight - (point.Intensity / Math.Max(SpectralData.MaxIntensity, 0.1) * plotHeight * 0.9);

                    if (isFirst)
                    {
                        context.BeginFigure(new Point(x, baseY), true, true); // 从底部开始
                        context.LineTo(new Point(x, y), true, true); // 连接到曲线点
                        isFirst = false;
                    }
                    else
                    {
                        context.LineTo(new Point(x, y), true, true);
                    }
                }

                // 闭合路径：从最后一个曲线点到底部右角，再回到起点
                if (!isFirst)
                {
                    var lastPoint = SpectralData.DataPoints.Last();
                    double lastX = margin + (lastPoint.Wavelength - SpectralData.MinWavelength) /
                        (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;

                    context.LineTo(new Point(lastX, baseY), true, true); // 到底部右角
                }
            }

            geometry.Freeze();

            // 创建正确的渐变画刷 - 关键修复！
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),  // 从左到右
                EndPoint = new Point(1, 0.5),
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };

            // 添加关键波长点的颜色
            double[] keyWavelengths = { 380, 440, 490, 510, 580, 645, 780 };
            foreach (double wavelength in keyWavelengths)
            {
                if (wavelength >= SpectralData.MinWavelength && wavelength <= SpectralData.MaxWavelength)
                {
                    double offset = (wavelength - SpectralData.MinWavelength) /
                        (SpectralData.MaxWavelength - SpectralData.MinWavelength);

                    var color = Converters.WavelengthToColorConverter.ConvertWavelengthToColor(wavelength);
                    gradient.GradientStops.Add(new GradientStop(color, offset));
                }
            }

            // 添加更多渐变点以获得平滑过渡
            for (int wavelength = 380; wavelength <= 780; wavelength += 10)
            {
                if (wavelength >= SpectralData.MinWavelength && wavelength <= SpectralData.MaxWavelength)
                {
                    double offset = (wavelength - SpectralData.MinWavelength) /
                        (SpectralData.MaxWavelength - SpectralData.MinWavelength);

                    var color = Converters.WavelengthToColorConverter.ConvertWavelengthToColor(wavelength);
                    gradient.GradientStops.Add(new GradientStop(color, offset));
                }
            }

            var surfacePath = new Path
            {
                Data = geometry,
                Fill = gradient,
                Opacity = 0.8
            };

            PART_Canvas.Children.Add(surfacePath);
        }
        private void DrawSurface_old(double margin, double plotWidth, double plotHeight)
        {
            var geometry = new StreamGeometry();

            using (var context = geometry.Open())
            {
                bool isFirst = true;
                double baseY = margin + plotHeight;

                foreach (var point in SpectralData.DataPoints)
                {
                    double x = margin + (point.Wavelength - SpectralData.MinWavelength) /
                        (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;
                    double y = margin + plotHeight - (point.Intensity / Math.Max(SpectralData.MaxIntensity, 0.1) * plotHeight * 0.9);

                    if (isFirst)
                    {
                        context.BeginFigure(new Point(x, baseY), true, true);
                        context.LineTo(new Point(x, y), true, true);
                        isFirst = false;
                    }
                    else
                    {
                        context.LineTo(new Point(x, y), true, true);
                    }
                }

                // 闭合路径
                if (!isFirst)
                {
                    var lastPoint = SpectralData.DataPoints.Last();
                    double lastX = margin + (lastPoint.Wavelength - SpectralData.MinWavelength) /
                        (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;
                    context.LineTo(new Point(lastX, baseY), true, true);
                }
            }

            geometry.Freeze();

            // 创建渐变画刷
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                MappingMode = BrushMappingMode.Absolute
            };

            // 简化渐变点，避免性能问题
            for (int i = 0; i < SpectralData.DataPoints.Count; i += 20)
            {
                var point = SpectralData.DataPoints[i];
                double offset = (point.Wavelength - SpectralData.MinWavelength) /
                    (SpectralData.MaxWavelength - SpectralData.MinWavelength);
                gradient.GradientStops.Add(new GradientStop(point.Color, offset));
            }

            var surfacePath = new Path
            {
                Data = geometry,
                Fill = gradient,
                Opacity = 0.6
            };

            PART_Canvas.Children.Add(surfacePath);
        }

        private void DrawCurve_old(double margin, double plotWidth, double plotHeight)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure();
            bool isFirst = true;

            foreach (var point in SpectralData.DataPoints)
            {
                double x = margin + (point.Wavelength - SpectralData.MinWavelength) /
                    (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;
                double y = margin + plotHeight - (point.Intensity / Math.Max(SpectralData.MaxIntensity, 0.1) * plotHeight * 0.9);

                if (isFirst)
                {
                    figure.StartPoint = new Point(x, y);
                    isFirst = false;
                }
                else
                {
                    figure.Segments.Add(new LineSegment(new Point(x, y), true));
                }
            }

            geometry.Figures.Add(figure);

            var curvePath = new Path
            {
                Data = geometry,
                Stroke = Brushes.White,
                StrokeThickness = 3,
                StrokeLineJoin = PenLineJoin.Round
            };

            PART_Canvas.Children.Add(curvePath);
        }
        private void DrawCurve(double margin, double plotWidth, double plotHeight)
        {
            // 将曲线分成小段，每段使用不同的颜色
            for (int i = 0; i < SpectralData.DataPoints.Count - 1; i++)
            {
                var point1 = SpectralData.DataPoints[i];
                var point2 = SpectralData.DataPoints[i + 1];

                double x1 = margin + (point1.Wavelength - SpectralData.MinWavelength) /
                    (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;
                double y1 = margin + plotHeight - (point1.Intensity / Math.Max(SpectralData.MaxIntensity, 0.1) * plotHeight * 0.9);

                double x2 = margin + (point2.Wavelength - SpectralData.MinWavelength) /
                    (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;
                double y2 = margin + plotHeight - (point2.Intensity / Math.Max(SpectralData.MaxIntensity, 0.1) * plotHeight * 0.9);

                // 创建线段
                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = new SolidColorBrush(point1.Color), // 使用第一个点的颜色
                    StrokeThickness = 3,
                    StrokeEndLineCap = PenLineCap.Round
                };

                PART_Canvas.Children.Add(line);
            }
        }
        private void DrawLabels(double margin, double plotWidth, double plotHeight)
        {
            // X轴标签
            double[] wavelengthLabels = { 400, 500, 600, 700 };
            foreach (double wavelength in wavelengthLabels)
            {
                double x = margin + (wavelength - SpectralData.MinWavelength) /
                    (SpectralData.MaxWavelength - SpectralData.MinWavelength) * plotWidth;

                var label = new TextBlock
                {
                    Text = $"{wavelength} nm",
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold
                };

                Canvas.SetLeft(label, x - 25);
                Canvas.SetTop(label, margin + plotHeight + 5);
                PART_Canvas.Children.Add(label);
            }

            // Y轴标签
            for (double intensity = 0; intensity <= 1; intensity += 0.2)
            {
                double y = margin + plotHeight - (intensity * plotHeight * 0.9);

                var label = new TextBlock
                {
                    Text = $"{intensity:F1}",
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold
                };

                Canvas.SetLeft(label, margin - 30);
                Canvas.SetTop(label, y - 8);
                PART_Canvas.Children.Add(label);
            }

            // 标题
            var title = new TextBlock
            {
                Text = "光谱分析曲线",
                Foreground = Brushes.Cyan,
                FontSize = 16,
                FontWeight = FontWeights.Bold
            };

            Canvas.SetLeft(title, margin + plotWidth / 2 - 50);
            Canvas.SetTop(title, margin - 30);
            PART_Canvas.Children.Add(title);
        }
    }
}
