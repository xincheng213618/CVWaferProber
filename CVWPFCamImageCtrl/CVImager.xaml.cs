using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CVWPFCamImageCtrl
{
    /// <summary>
    /// CVImager.xaml 的交互逻辑
    /// </summary>
    public partial class CVImager : UserControl
    {
        private Point _lastMousePosition;
        private bool _isDragging = false;
        private double _scale = 1.0;
        private const double MinScale = 0.01;
        private const double MaxScale = 50.0;
        private const double ScaleIncrement = 0.1;

        public static readonly DependencyProperty CurrentImageProperty =
            DependencyProperty.Register("CurrentImage", typeof(ImageSource), typeof(CVImager),
                new PropertyMetadata(null, OnCurrentImageChanged));

        public ImageSource? CurrentImage
        {
            get => (ImageSource)GetValue(CurrentImageProperty);
            set
            {
                //SetValue(CurrentImageProperty, value);
                //ImageChanged?.Invoke(this, EventArgs.Empty);
                // 检查当前线程是否是UI线程，若不是则切换到UI线程
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    SetValue(CurrentImageProperty, value);
                    ImageChanged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // 切换到UI线程执行操作
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SetValue(CurrentImageProperty, value);
                        ImageChanged?.Invoke(this, EventArgs.Empty);
                    });
                }
            }
        }
        // 新增属性：显示尺寸信息
        public Size OriginalImageSize => CurrentImage is BitmapSource bitmap ?
            new Size(bitmap.PixelWidth, bitmap.PixelHeight) : new Size(0, 0);

        public Size DisplayImageSize => CalculateCurrentDisplaySize();

        public double DisplayScale => ImageScaleTransform.ScaleX;

        // 新增事件：显示尺寸变化
        public event EventHandler<DisplaySizeChangedEventArgs> DisplaySizeChanged;
        public event EventHandler<double> ZoomChanged;
        public event EventHandler ImageChanged;

        public double Scale
        {
            get => _scale;
            set
            {
                _scale = Math.Max(MinScale, Math.Min(MaxScale, value));
                //UpdateScaleText();
            }
        }

        public CVImager()
        {
            InitializeComponent();
            this.DataContext = this;
            InitializeEventHandlers();
            //UpdateScaleText();
        }

        private void InitializeEventHandlers()
        {
            // 原有事件处理
            DisplayImage.MouseLeftButtonDown += OnMouseLeftButtonDown;
            DisplayImage.MouseLeftButtonUp += OnMouseLeftButtonUp;
            DisplayImage.MouseMove += OnMouseMove;
            DisplayImage.MouseWheel += OnMouseWheel;

            // 新增：监听变换变化
            ImageScaleTransform.Changed += OnTransformChanged;
            ImageTranslateTransform.Changed += OnTransformChanged;

            // 监听ScrollViewer滚动
            MainScrollViewer.ScrollChanged += OnScrollChanged;
        }

        private static void OnCurrentImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CVImager)d;
            control.ResetView();
            //control.UpdateImageInfo();
        }
        private void ResetView()
        {
            Scale = 1.0;
            ImageScaleTransform.ScaleX = 1.0;
            ImageScaleTransform.ScaleY = 1.0;
            ImageTranslateTransform.X = 0;
            ImageTranslateTransform.Y = 0;

            Dispatcher.BeginInvoke(() =>
            {
                MainScrollViewer.ScrollToHorizontalOffset(0);
                MainScrollViewer.ScrollToVerticalOffset(0);
            }, DispatcherPriority.Background);
        }
        private void OnTransformChanged(object? sender, EventArgs e)
        {
            UpdateDisplayInfo();
        }

        private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            UpdateDisplayInfo();
        }

        /// <summary>
        /// 计算当前显示尺寸
        /// </summary>
        private Size CalculateCurrentDisplaySize()
        {
            if (CurrentImage is not BitmapSource bitmap)
                return new Size(0, 0);

            double scaleX = ImageScaleTransform.ScaleX;
            double scaleY = ImageScaleTransform.ScaleY;

            return new Size(
                bitmap.PixelWidth * scaleX,
                bitmap.PixelHeight * scaleY
            );
        }

        /// <summary>
        /// 计算图像在屏幕上的实际显示矩形（考虑平移）
        /// </summary>
        public Rect GetImageDisplayRect()
        {
            if (CurrentImage is not BitmapSource bitmap)
                return new Rect(0, 0, 0, 0);

            Size displaySize = CalculateCurrentDisplaySize();

            // 计算图像在容器中的位置（考虑平移变换）
            double x = ImageTranslateTransform.X;
            double y = ImageTranslateTransform.Y;

            return new Rect(x, y, displaySize.Width, displaySize.Height);
        }

        /// <summary>
        /// 计算可见区域占整个图像的比例
        /// </summary>
        public double GetVisiblePercentage()
        {
            if (CurrentImage is not BitmapSource bitmap)
                return 0;

            Size displaySize = CalculateCurrentDisplaySize();
            Size viewportSize = new Size(MainScrollViewer.ViewportWidth, MainScrollViewer.ViewportHeight);

            // 如果图像比视口小，则100%可见
            if (displaySize.Width <= viewportSize.Width &&
                displaySize.Height <= viewportSize.Height)
                return 100.0;

            double visibleArea = viewportSize.Width * viewportSize.Height;
            double totalArea = displaySize.Width * displaySize.Height;

            return Math.Min(100.0, (visibleArea / totalArea) * 100.0);
        }

        /// <summary>
        /// 获取当前视图的缩放级别描述
        /// </summary>
        public string GetZoomDescription()
        {
            double scale = DisplayScale * 100;

            if (scale < 25) return $"缩小 ({scale:F0}%)";
            if (scale > 400) return $"放大 ({scale:F0}%)";
            if (scale > 100) return $"放大 {scale:F0}%";
            if (scale < 100) return $"缩小 {scale:F0}%";

            return "原始大小";
        }

        /// <summary>
        /// 更新显示信息并触发事件
        /// </summary>
        private void UpdateDisplayInfo()
        {
            var originalSize = OriginalImageSize;
            var displaySize = DisplayImageSize;
            var visiblePercent = GetVisiblePercentage();

            // 更新界面显示
            //UpdateScaleText();
            //UpdateImageInfo();

            // 触发尺寸变化事件
            DisplaySizeChanged?.Invoke(this, new DisplaySizeChangedEventArgs
            {
                OriginalSize = originalSize,
                DisplaySize = displaySize,
                Scale = DisplayScale,
                VisiblePercentage = visiblePercent,
                DisplayRect = GetImageDisplayRect()
            });
        }

        #region 缩放操作
        public void ZoomIn()
        {
            if (CurrentImage == null) return;
            Point center = new Point(DisplayImage.ActualWidth / 2, DisplayImage.ActualHeight / 2);
            Zoom(center, ScaleIncrement);
        }

        public void ZoomOut()
        {
            if (CurrentImage == null) return;
            Point center = new Point(DisplayImage.ActualWidth / 2, DisplayImage.ActualHeight / 2);
            Zoom(center, -ScaleIncrement);
        }

        public void ZoomToOriginal()
        {
            if (CurrentImage == null) return;
            Scale = 1.0;
            ApplyScale();
            CenterImage();
        }

        public void ZoomToFit()
        {
            if (CurrentImage == null || CurrentImage is not BitmapSource bitmap) return;

            double scaleX = MainScrollViewer.ViewportWidth / bitmap.PixelWidth;
            double scaleY = MainScrollViewer.ViewportHeight / bitmap.PixelHeight;
            Scale = Math.Min(scaleX, scaleY) * 0.95; // 留一点边距

            ApplyScale();
            CenterImage();
        }

        private void Zoom(Point center, double delta)
        {
            double oldScale = Scale;
            Scale += delta;

            if (Math.Abs(oldScale - Scale) > 0.001)
            {
                // 计算缩放中心点偏移
                double scaleRatio = Scale / oldScale;
                ImageTranslateTransform.X = center.X - (center.X - ImageTranslateTransform.X) * scaleRatio;
                ImageTranslateTransform.Y = center.Y - (center.Y - ImageTranslateTransform.Y) * scaleRatio;

                ApplyScale();
            }
        }
        private void ApplyScale()
        {
            ImageScaleTransform.ScaleX = Scale;
            ImageScaleTransform.ScaleY = Scale;
            //UpdateScaleText();

            // 触发缩放变化事件
            ZoomChanged?.Invoke(this, Scale);
        }
        private void CenterImage()
        {
            ImageTranslateTransform.X = 0;
            ImageTranslateTransform.Y = 0;
        }
        #endregion
        ///// <summary>
        ///// 增强的缩放文本显示
        ///// </summary>
        //private void UpdateScaleText()
        //{
        //    ScaleTextBlock.Text = $"缩放: {DisplayScale * 100:0}%";

        //    if (CurrentImage is BitmapSource bitmap)
        //    {
        //        var displaySize = DisplayImageSize;
        //        ScaleTextBlock.ToolTip = $"原始: {bitmap.PixelWidth} × {bitmap.PixelHeight} 像素\n" +
        //                               $"显示: {displaySize.Width:F0} × {displaySize.Height:F0} 像素\n" +
        //                               $"可见: {GetVisiblePercentage():F1}%";
        //    }
        //}

        ///// <summary>
        ///// 增强的图像信息显示
        ///// </summary>
        //private void UpdateImageInfo()
        //{
        //    if (CurrentImage is BitmapSource bitmap)
        //    {
        //        var displaySize = DisplayImageSize;
        //        ImageInfoTextBlock.Text =
        //            $"{bitmap.PixelWidth} × {bitmap.PixelHeight} → " +
        //            $"{displaySize.Width:F0} × {displaySize.Height:F0}";
        //    }
        //    else
        //    {
        //        ImageInfoTextBlock.Text = "";
        //    }
        //}

        #region 鼠标事件处理
        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (CurrentImage == null) return;

            DisplayImage.CaptureMouse();
            _isDragging = true;
            _lastMousePosition = e.GetPosition(ImageContainer);
            DisplayImage.Cursor = Cursors.Hand;
            e.Handled = true;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                DisplayImage.ReleaseMouseCapture();
                _isDragging = false;
                DisplayImage.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(ImageContainer);
                double deltaX = currentPosition.X - _lastMousePosition.X;
                double deltaY = currentPosition.Y - _lastMousePosition.Y;

                ImageTranslateTransform.X += deltaX;
                ImageTranslateTransform.Y += deltaY;

                _lastMousePosition = currentPosition;
                e.Handled = true;
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (CurrentImage == null) return;

            Point mousePos = e.GetPosition(DisplayImage);
            Zoom(mousePos, e.Delta > 0 ? ScaleIncrement : -ScaleIncrement);
            e.Handled = true;
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && CurrentImage != null)
            {
                Point mousePos = e.GetPosition(DisplayImage);
                Zoom(mousePos, e.Delta > 0 ? ScaleIncrement : -ScaleIncrement);
                e.Handled = true;
            }
        }
        #endregion

        #region 新增的公共方法
        /// <summary>
        /// 获取详细的显示信息
        /// </summary>
        public ImageDisplayInfo GetDisplayInfo()
        {
            return new ImageDisplayInfo
            {
                OriginalSize = OriginalImageSize,
                DisplaySize = DisplayImageSize,
                Scale = DisplayScale,
                VisiblePercentage = GetVisiblePercentage(),
                ViewportSize = new Size(MainScrollViewer.ViewportWidth, MainScrollViewer.ViewportHeight),
                ScrollOffset = new Point(MainScrollViewer.HorizontalOffset, MainScrollViewer.VerticalOffset),
                DisplayRect = GetImageDisplayRect()
            };
        }

        /// <summary>
        /// 设置精确的缩放级别
        /// </summary>
        public void SetZoomLevel(double zoomLevel, Point? centerPoint = null)
        {
            if (CurrentImage == null) return;

            Point center = centerPoint ?? new Point(DisplayImage.ActualWidth / 2, DisplayImage.ActualHeight / 2);
            double oldScale = Scale;
            Scale = Math.Max(MinScale, Math.Min(MaxScale, zoomLevel));

            if (Math.Abs(oldScale - Scale) > 0.001)
            {
                double scaleRatio = Scale / oldScale;
                ImageTranslateTransform.X = center.X - (center.X - ImageTranslateTransform.X) * scaleRatio;
                ImageTranslateTransform.Y = center.Y - (center.Y - ImageTranslateTransform.Y) * scaleRatio;

                ApplyScale();
            }
        }
        #endregion
    }

    /// <summary>
    /// 显示尺寸变化事件参数
    /// </summary>
    public class DisplaySizeChangedEventArgs : EventArgs
    {
        public Size OriginalSize { get; set; }
        public Size DisplaySize { get; set; }
        public double Scale { get; set; }
        public double VisiblePercentage { get; set; }
        public Rect DisplayRect { get; set; }
    }

    /// <summary>
    /// 图像显示信息类
    /// </summary>
    public class ImageDisplayInfo
    {
        public Size OriginalSize { get; set; }
        public Size DisplaySize { get; set; }
        public double Scale { get; set; }
        public double VisiblePercentage { get; set; }
        public Size ViewportSize { get; set; }
        public Point ScrollOffset { get; set; }
        public Rect DisplayRect { get; set; }

        public override string ToString()
        {
            return $"原始: {OriginalSize.Width}×{OriginalSize.Height} | " +
                   $"显示: {DisplaySize.Width:F0}×{DisplaySize.Height:F0} | " +
                   $"缩放: {Scale * 100:F0}% | 可见: {VisiblePercentage:F1}%";
        }
    }
}
