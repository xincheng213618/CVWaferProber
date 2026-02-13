using CVCommCore.CVImage;
using CVWaferProber.Core.ViewModels;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace CVWPFCamImageCtrl
{
    public class CVCamImagerViewModel : ViewModelBase
    {
        private ImageSource? _imageSource;
        private ObservableCollection<ImageItem> _imageResults;
        private ObservableCollection<ImageItem> _processedImageResults; // Analysis Image集合
        private ObservableCollection<ImageItem> _originalImageResults;  // Camera Measurement集合
        private ObservableCollection<POIMarker> _poiMarkers;
        private CVImager? _imageDisplay;
        //private uint id = 1;


        // 新增：图像新增事件（通知UI层选中最新项）
        public event Action<ImageItem> ImageItemAdded;
        // 新增：当前视图类型（与CVCamImagerCtrl保持一致，用于判断选中哪个集合的最新项）
        public string CurrentViewType { get; set; } = "Analysis";
        // 新增：获取当前视图的最新图像项（核心方法）
        public ImageItem? GetCurrentViewLatestImageItem()
        {
            return CurrentViewType switch
            {
                "Analysis" => ProcessedImageResults.LastOrDefault(),
                "Camera" => OriginalImageResults.LastOrDefault(),
                _ => ProcessedImageResults.LastOrDefault()
            };
        }
        public CVCamImagerViewModel()
        {
            _imageSource = null;
            _imageDisplay = null;
            _poiMarkers = new ObservableCollection<POIMarker>();
            _imageResults = new ObservableCollection<ImageItem>();
            _processedImageResults = new ObservableCollection<ImageItem>();
            _originalImageResults = new ObservableCollection<ImageItem>();
        }

        public ObservableCollection<POIMarker> POIMarkers
        {
            get => _poiMarkers;
            set => SetProperty(ref _poiMarkers, value);
        }

        public ObservableCollection<ImageItem> ImageResults
        {
            get => _imageResults;
            set => SetProperty(ref _imageResults, value);
        }

        public ImageSource? ImageSrc
        {
            get => _imageSource;
            set => SetProperty(ref _imageSource, value);
        }

        // Analysis Image集合（处理后图像，过滤po.dat）
        public ObservableCollection<ImageItem> ProcessedImageResults
        {
            get => _processedImageResults;
            set => SetProperty(ref _processedImageResults, value);
        }

        // Camera Measurement集合（原始图像，包含所有类型）
        public ObservableCollection<ImageItem> OriginalImageResults
        {
            get => _originalImageResults;
            set => SetProperty(ref _originalImageResults, value);
        }

        public void ClearImageResult()
        {
            //id = 1;
            ImageSrc = null;

            // 清空所有图像集合
            _imageResults.Clear();
            _processedImageResults.Clear();
            _originalImageResults.Clear();
            _poiMarkers.Clear();

            if (_imageDisplay != null)
                _imageDisplay.CurrentImage = null;
        }

        public void SetImageCtrl(CVImager imageDisplay)
        {
            _imageDisplay = imageDisplay;
        }

        public void UpdatePOIImage(Mat image, List<POIMarker> POIMarkers, string imageDisplayBrightnessUniformity)
        {
            PutPOIToImage(ref image, POIMarkers);
            OpenCvMatTools.PutTextToImage(imageDisplayBrightnessUniformity, ref image, Scalar.Green);

            Application.Current.Dispatcher.Invoke(() =>
            {
                ImageSrc = image.ToBitmapSource();
                if (_imageDisplay != null)
                {
                    _imageDisplay.CurrentImage = ImageSrc;
                }
            });
        }

        private void PutPOIToImage(ref Mat image, List<POIMarker> poiMarkers)
        {
            if (image == null || image.Empty() || poiMarkers == null || poiMarkers.Count == 0)
                return;

            if (image.Channels() == 1)
            {
                Cv2.CvtColor(image, image, ColorConversionCodes.GRAY2BGR);
            }

            foreach (var poi in poiMarkers)
            {
                if (poi == null) continue;
                if (poi is CircleMarker poiC) DrawCircleToImage(ref image, poiC);
                else if (poi is RectangleMarker poiR) DrawRectToImage(ref image, poiR);
            }
        }

        private void DrawCircleToImage(ref Mat image, CircleMarker poi)
        {
            OpenCvSharp.Point cvPoint = new OpenCvSharp.Point(poi.X, poi.Y);
            Cv2.Circle(image, cvPoint, (int)poi.Radius, poi.Color, 1);

            if (!string.IsNullOrEmpty(poi.Label))
            {
                var textSize = Cv2.GetTextSize(poi.Label, HersheyFonts.HersheySimplex, 0.5, 1, out int baseline);
                OpenCvSharp.Point textOrg = new OpenCvSharp.Point(
                    (int)(cvPoint.X - textSize.Width / 2),
                    (int)(cvPoint.Y + poi.Radius + textSize.Height + 5)
                );
                Cv2.PutText(image, poi.Label, textOrg, HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);
            }
        }

        private void DrawRectToImage(ref Mat image, RectangleMarker poi)
        {
            OpenCvSharp.Point cvPoint = new OpenCvSharp.Point(poi.X - poi.Width / 2, poi.Y - poi.Height / 2);
            OpenCvSharp.Point cvPoint2 = new OpenCvSharp.Point(poi.X + poi.Width / 2, poi.Y + poi.Height / 2);
            Cv2.Rectangle(image, cvPoint, cvPoint2, poi.Color, 1);

            if (!string.IsNullOrEmpty(poi.Label))
            {
                var textSize = Cv2.GetTextSize(poi.Label, HersheyFonts.HersheySimplex, 0.5, 1, out int baseline);
                OpenCvSharp.Point textOrg = new OpenCvSharp.Point(
                    (int)(poi.X - textSize.Width / 2),
                    (int)(cvPoint2.Y + textSize.Height + 5)
                );
                Cv2.PutText(image, poi.Label, textOrg, HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);
            }
        }

        // 添加Analysis Image（处理后图像）
        public void AddImage(ImageItem imageItem)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string fileName = imageItem.FileName?.ToLower() ?? string.Empty;

                // 过滤po.dat文件
                if (fileName.Equals("po.dat") || fileName.Equals("po"))
                {
                    logger.Debug($"Filter the po.dat file: {imageItem.FileName}");
                    return;
                }

                // 去重检查
                if (!_processedImageResults.Any(item => item.ImagePath.Equals(imageItem.ImagePath, StringComparison.OrdinalIgnoreCase)))
                {
                    _processedImageResults.Add(imageItem);
                    // 触发事件：通知UI选中最新项
                    ImageItemAdded?.Invoke(imageItem);
                }

                // 也添加到总集合
                if (!_imageResults.Contains(imageItem))
                {
                    _imageResults.Add(imageItem);
                }
            });
        }

        // 添加Camera Measurement（原始图像）
        public void AddOriginalImageOnly(ImageItem imageItem)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 去重检查
                if (!_originalImageResults.Any(item => item.ImagePath.Equals(imageItem.ImagePath, StringComparison.OrdinalIgnoreCase)))
                {
                    _originalImageResults.Add(imageItem);
                    // 触发事件：通知UI选中最新项
                    ImageItemAdded?.Invoke(imageItem);
                }

                // 也添加到总集合
                if (!_imageResults.Contains(imageItem))
                {
                    _imageResults.Add(imageItem);
                }
            });
        }

        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVCamImagerViewModel));
    }
}
