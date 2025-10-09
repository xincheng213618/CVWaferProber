using CVWPFCameraImage.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CVWPFCameraImage
{
    /// <summary>
    /// CVCameraImageViewer.xaml 的交互逻辑
    /// </summary>
    public partial class CVCameraImageViewer : UserControl
    {
        private Point? _lastDragPoint;
        private double _scale = 1.0;
        private const double ZoomSpeed = 0.001;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 10.0;

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(CVCameraImageViewer));

        public static readonly DependencyProperty POIMarkersProperty =
            DependencyProperty.Register("POIMarkers", typeof(ObservableCollection<POIMarker>), typeof(CVCameraImageViewer));

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
        public CVCameraImageViewer()
        {
            InitializeComponent();
            POIMarkers = new ObservableCollection<POIMarker>();
        }
    }
}
