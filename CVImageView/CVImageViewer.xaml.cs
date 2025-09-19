using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CVImageView
{
    /// <summary>
    /// CVImageViewer.xaml 的交互逻辑
    /// </summary>
    public partial class CVImageViewer : UserControl
    {
        private Point? _lastDragPoint;
        private double _scale = 1.0;
        private const double ZoomSpeed = 0.001;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 10.0;

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(CVImageViewer));

        public static readonly DependencyProperty POIMarkersProperty =
            DependencyProperty.Register("POIMarkers", typeof(ObservableCollection<POIMarker>), typeof(CVImageViewer));

        public ImageSource ImageSource
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public ObservableCollection<POIMarker> POIMarkers
        {
            get => (ObservableCollection<POIMarker>)GetValue(POIMarkersProperty);
            set => SetValue(POIMarkersProperty, value);
        }
        public CVImageViewer()
        {
            InitializeComponent();
            InitializeZoomAndPan();
            POIMarkers = new ObservableCollection<POIMarker>();
        }
        private void InitializeZoomAndPan()
        {
            //scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            //scrollViewer.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            //scrollViewer.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            //scrollViewer.PreviewMouseMove += OnPreviewMouseMove;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var zoomFactor = e.Delta > 0 ? 1.2 : 0.8;
            Zoom(zoomFactor, e.GetPosition(container));
            e.Handled = true;
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _lastDragPoint = e.GetPosition(scrollViewer);
                scrollViewer.CaptureMouse();
                scrollViewer.Cursor = Cursors.Hand;
            }
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            scrollViewer.ReleaseMouseCapture();
            _lastDragPoint = null;
            scrollViewer.Cursor = Cursors.Arrow;
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_lastDragPoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(scrollViewer);
                Vector offset = currentPosition - _lastDragPoint.Value;

                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - offset.X);
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - offset.Y);

                _lastDragPoint = currentPosition;
            }
        }

        public void Zoom(double zoomFactor, Point zoomCenter)
        {
            _scale *= zoomFactor;
            _scale = Math.Max(MinZoom, Math.Min(MaxZoom, _scale));

            //scaleTransform.ScaleX = _scale;
            //scaleTransform.ScaleY = _scale;

            // Adjust scroll position to zoom towards mouse position
            var relative = zoomCenter;
            var absoluteX = relative.X * _scale + scrollViewer.HorizontalOffset;
            var absoluteY = relative.Y * _scale + scrollViewer.VerticalOffset;

            scrollViewer.ScrollToHorizontalOffset(absoluteX - relative.X);
            scrollViewer.ScrollToVerticalOffset(absoluteY - relative.Y);
        }

        public void ResetView()
        {
            _scale = 1.0;
            //scaleTransform.ScaleX = 1.0;
            //scaleTransform.ScaleY = 1.0;
            scrollViewer.ScrollToHorizontalOffset(0);
            scrollViewer.ScrollToVerticalOffset(0);
        }
    }
}
