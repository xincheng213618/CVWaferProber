using CVCommCore.CVImage;
using CVWaferProber.Core.ViewModels;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

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
        // 【关键修改】使用静态线程安全计数器，替代原有id字段
        private static int _globalImageId = 0;

        // 1. 新增：当前视图模式（Analysis/Camera）
        private string _currentViewMode = "Camera"; // 默认值改为 Camera，对应原图
        public string CurrentViewMode
        {
            get => _currentViewMode;
            set
            {
                if (_currentViewMode != value)
                {
                    _currentViewMode = value;
                    OnPropertyChanged(nameof(CurrentViewMode));
                    OnPropertyChanged(nameof(CurrentDisplayCollection)); // 触发集合刷新
                    logger.Debug($"Switch view mode: {value}");
                }
            }
        }
        // 2. 新增：供外部调用的视图切换方法
        public void SwitchViewMode(string mode)
        {
            if (mode == "Analysis" || mode == "Camera")
            {
                CurrentViewMode = mode;
            }
            else
            {
                logger.Warn($"Invalid view mode: {mode}，Default Use Camera");
                CurrentViewMode = "Camera";
            }
        }

        // 3. 新增：当前显示的集合（绑定到 DataGrid 的 ItemsSource）
        public ObservableCollection<ImageItem> CurrentDisplayCollection
        {
            get
            {
                return CurrentViewMode == "Analysis"
                    ? _processedImageResults  // 分析图
                    : _originalImageResults;  // 原图（默认）
            }
        }
        // 【新增方法】获取下一个全局唯一ID（线程安全）
        public int GetNextImageId()
        {
            return Interlocked.Increment(ref _globalImageId);
        }

        // 【新增方法】重置ID计数器（清空图片时调用）
        public void ResetImageIdCounter()
        {
            Interlocked.Exchange(ref _globalImageId, 0);
        }
        // 新增：实时预览开关（绑定到CheckBox）
        private bool _isRealTimePreviewEnabled = false; // 默认勾选
        public bool IsRealTimePreviewEnabled
        {
            get => _isRealTimePreviewEnabled;
            set => SetProperty(ref _isRealTimePreviewEnabled, value);
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
            // 【关键修改】清空时重置ID计数器
            ResetImageIdCounter();
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
                }

                // 也添加到总集合
                if (!_imageResults.Contains(imageItem))
                {
                    _imageResults.Add(imageItem);
                }
            });
        }
      
        /// <summary>
        /// 实时添加单张Analysis Image（测试过程中调用）
        /// </summary>
        /// <param name="filePath">图片路径</param>
        /// <param name="delayMs">延迟毫秒数（仅实时预览开启时生效）</param>
        public async Task AddSingleAnalysisImageAsync(string filePath, int delayMs = 500)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                logger.Warn($"Analysis image file not exist: {filePath}");
                return;
            }

            // 过滤po.dat文件
            string fileName = Path.GetFileName(filePath)?.ToLower() ?? string.Empty;
            if (fileName.Equals("po.dat") || fileName.Equals("po"))
            {
                logger.Debug($"Filter po.dat file: {filePath}");
                return;
            }

            // 去重检查
            if (_processedImageResults.Any(item => item.ImagePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            // 构造ImageItem
            var imageItem = new ImageItem(GetNextImageId())
            {
                FileName = Path.GetFileName(filePath),
                ImagePath = filePath,
                FileSizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0),
                Status = "Loading"
            };

            // UI线程添加到集合
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _processedImageResults.Add(imageItem);
                _imageResults.Add(imageItem);
                logger.Debug($"Real-time add Analysis image: {imageItem.FileName}");

                SelectLatestImageItem(imageItem);

            }, DispatcherPriority.Normal); // 强制正常优先级，确保集合先刷新

            // 实时预览开启时，延迟500ms
            if (IsRealTimePreviewEnabled)
            {
                await Task.Delay(delayMs);
            }

            // 更新状态为已加载
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                imageItem.Status = "Loaded";
            });
        }
        /// <summary>
        /// 实时添加单张Camera Measurement（测试过程中调用）
        /// 新增：添加后自动选中最新图片
        /// </summary>
        /// <param name="filePath">图片路径</param>
        /// <param name="delayMs">延迟毫秒数（仅实时预览开启时生效）</param>
        public async Task AddSingleCameraImageAsync(string filePath, int delayMs = 500)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                logger.Warn($"Camera image file not exist: {filePath}");
                return;
            }

            // 去重检查
            if (_originalImageResults.Any(item => item.ImagePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            // 构造ImageItem
            var imageItem = new ImageItem(GetNextImageId())
            {
                FileName = Path.GetFileName(filePath),
                ImagePath = filePath,
                FileSizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0),
                Status = "Loading"
            };

            // UI线程添加到集合 + 自动选中
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _originalImageResults.Add(imageItem);
                _imageResults.Add(imageItem);
                logger.Debug($"Real-time add Camera image: {imageItem.FileName}");

                // 核心：自动选中最新添加的图片
                SelectLatestImageItem(imageItem);
            }, DispatcherPriority.Normal);

            // 实时预览开启时，延迟500ms
            if (IsRealTimePreviewEnabled)
            {
                await Task.Delay(delayMs);
            }

            // 更新状态为已加载
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                imageItem.Status = "Loaded";
            });
        }
        /// <summary>
        /// 核心方法：自动选中最新添加的图片项
        /// </summary>
        /// <param name="latestItem">最新添加的图片项</param>
        private void SelectLatestImageItem(ImageItem latestItem)
        {
            try
            {
                // 只做这一件事：绑定自动生效
                SelectedImageItem = latestItem;

                logger.Debug($"Auto selected latest image: {latestItem?.FileName}");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to auto select latest image: {ex.Message}", ex);
            }
        }
        // ========== 推荐添加：ViewModel中添加选中项绑定属性 ==========
        private ImageItem _selectedImageItem;
        /// <summary>
        /// 当前选中的图片项（绑定到DataGrid的SelectedItem）
        /// </summary>
        public ImageItem SelectedImageItem
        {
            get => _selectedImageItem;
            set
            {
                if (_selectedImageItem != value)
                {
                    _selectedImageItem = value;
                    OnPropertyChanged(nameof(SelectedImageItem));
                    logger.Debug($"SelectedImageItem changed to: {value?.FileName}");
                }
            }
        }
        public DataGrid MainImageDataGrid { get; set; }
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVCamImagerViewModel));
      
    }
     
}
