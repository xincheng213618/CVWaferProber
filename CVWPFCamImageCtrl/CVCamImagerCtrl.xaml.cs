using CVCommCore.CVImage;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using OpenCvSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CVWPFCamImageCtrl
{
    /// <summary>
    /// CVCamImagerCtrl.xaml 的交互逻辑
    /// </summary>
    public partial class CVCamImagerCtrl : UserControl
    {
        //private ObservableCollection<ImageItem> _imageItems;
        private int _currentImageIndex = -1;
        private bool _isUpdatingZoomSlider = false;
        private int _nextImageId = 1;
        private CVCamImagerViewModel _model;

        public static bool IsChineseMode = false; // false=英文，true=中文
        // 支持的图像格式
        private readonly string[] _supportedImageExtensions = {
            ".tif", ".tiff", ".jpg", ".jpeg", ".png", ".bmp",
            ".gif", ".webp", ".ico", ".exif"
        };
        public CVCamImagerCtrl()
        {
            InitializeComponent();
            InitializeData();
            SetupKeyboardShortcuts();
            //StartMemoryMonitoring();

            ImageDisplay.ZoomChanged += ImageDisplay_ZoomChanged;
            this.Loaded += CVCamImagerCtrl_Loaded;
        }

        private void CVCamImagerCtrl_Loaded(object sender, RoutedEventArgs e)
        {
            if(this.DataContext is CVCamImagerViewModel model)
            {
                _model = model;
                model.SetImageCtrl(ImageDisplay);
            }
        }

        private void InitializeData()
        {
            //_imageItems = new ObservableCollection<ImageItem>();

            //AddImageFolder(@"F:\img\晶圆台\陈高\0826\1");

            //ImageDataGrid.ItemsSource = _imageItems;

            //// 自动选择第一项
            //if (_imageItems.Any())
            //{
            //    ImageDataGrid.SelectedIndex = 0;
            //}
        }
        #region 文件操作功能
        private async void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = IsChineseMode ? "选择图像文件": "Select image file",
                    Filter = GetImageFilterString(),
                    Multiselect = true, // 支持多选
                    CheckFileExists = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // 显示加载进度
                    //LoadingProgressBar.Value = 0;
                    //LoadingProgressBar.Maximum = openFileDialog.FileNames.Length;

                    // 异步加载文件
                    Task<List<ImageItem>> task = LoadImageFilesAsync(openFileDialog.FileNames);
                    await task;

                    ImageDataGrid.ItemsSource = _model.ImageResults;
                    ImageDataGrid.Items.Refresh();
                }
               
            }
            catch (Exception ex)
            {
                ShowErrorMessage(IsChineseMode ? "打开文件时发生错误": "An error occurred when opening the file.", ex);
            }
        }
        private async void LoadFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new CommonOpenFileDialog())
                {
                    dialog.IsFolderPicker = true; // 关键：设置为选择文件夹
                    dialog.Title = IsChineseMode ? "请选择一个文件夹" : "Please select a folder.";

                    if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        //string selectedFolderPath = dialog.FileName;
                        _model.ImageResults.Clear();
                        var folderPath = dialog.FileName;
                        if (Directory.Exists(folderPath))
                        {
                            // 搜索所有支持的图像文件
                            var imageFiles = _supportedImageExtensions
                                .SelectMany(ext => Directory.GetFiles(folderPath, "*" + ext, SearchOption.AllDirectories))
                                .ToArray();

                            if (imageFiles.Length > 0)
                            {
                                // 显示加载进度
                                //LoadingProgressBar.Value = 0;
                                //LoadingProgressBar.Maximum = imageFiles.Length;
                                _nextImageId = 1;
                                //StatusText.Text = $"找到 {imageFiles.Length} 个图像文件，正在加载...";
                                Task<List<ImageItem>> task= LoadImageFilesAsync(imageFiles);
                                await task;

                                ImageDataGrid.ItemsSource = _model.ImageResults;
                                ImageDataGrid.Items.Refresh();
                                //foreach (var imageItem in task.Result)
                                //{
                                //    _imageItems.Add(imageItem);
                                //}
                            }
                            else
                            {
                                MessageBox.Show(IsChineseMode?"在选择的文件夹中未找到支持的图像文件。": "No supported image files were found in the selected folder.", IsChineseMode ? "提示": "Prompt",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //ShowErrorMessage("打开文件夹时发生错误", ex);
            }
        }
        private async Task<List<ImageItem>> LoadImageFilesAsync(string[] filePaths)
        {
            List<ImageItem> results = new List<ImageItem>();
            try
            {
                int loadedCount = 0;
                int totalCount = filePaths.Length;
                await System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (string filePath in filePaths)
                    {
                        try
                        {
                            // 检查文件是否已经在列表中
                            //if (_imageItems.Any(item => item.ImagePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                            //{
                            //    Dispatcher.Invoke(() =>
                            //    {
                            //        //LoadingProgressBar.Value++;
                            //        loadedCount++;
                            //        UpdateProgressText(loadedCount, totalCount);
                            //    });
                            //    continue;
                            //}

                            // 获取文件信息
                            var fileInfo = new FileInfo(filePath);
                            if (fileInfo.Exists)
                            {
                                var imageItem = new ImageItem(_nextImageId++)
                                {
                                    //Id = _nextImageId++,
                                    ImagePath = filePath,
                                    FileName = System.IO.Path.GetFileName(filePath),
                                    FileSizeMB = fileInfo.Length / (1024.0 * 1024.0),
                                    Status = IsChineseMode?"待加载": "Loading"
                                };
                                //results.Add(imageItem);
                                // 在UI线程上添加项目
                                //Dispatcher.Invoke(() =>
                                //{
                                _model.ImageResults.Add(imageItem);
                                //    //LoadingProgressBar.Value++;
                                //    loadedCount++;
                                //    UpdateProgressText(loadedCount, totalCount);
                                //    //UpdateImageCount();
                                //});
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(IsChineseMode?$"加载文件失败 {filePath}: {ex.Message}": $"Failed to load file {filePath}: {ex.Message}");
                        }
                    }
                });

                //加载完成后选择第一个文件
                if (_model.ImageResults.Any())
                {
                    ImageDataGrid.SelectedIndex = 0;
                    //StatusText.Text = $"成功加载 {loadedCount} 个图像文件";
                }
            }
            catch (Exception ex)
            {
                //ShowErrorMessage("加载图像文件时发生错误", ex);
            }
            finally
            {
                // 重置进度条
                //await System.Threading.Tasks.Task.Delay(2000); // 显示2秒完成状态
                //Dispatcher.Invoke(() =>
                //{
                //    //LoadingProgressBar.Value = 0;
                //    LoadingProgressText.Text = "";
                //});

            }
            return results;
        }
        private void ReloadImage_Click(object sender, RoutedEventArgs e)
        {
            if (ImageDataGrid.SelectedItem is ImageItem selectedImage)
            {
                // 重新加载当前选中的图像
                LoadSelectedImage(selectedImage, true);
            }
            else
            {
                MessageBox.Show(IsChineseMode? "请先选择一个图像文件。": "Please select an image file first.", IsChineseMode ? "提示" : "Prompt", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(IsChineseMode ? "确定要清除所有图像吗？这个操作不可撤销。" : "Are you sure you want to delete all images? This action cannot be undone.", IsChineseMode ? "确认清除" : "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ClearAllImages();
            }
        }
        // 增强的图像加载方法
        private async void LoadSelectedImage(ImageItem imageItem, bool isReload = false)
        {
           
            if (imageItem == null) return;

            _currentImageIndex = ImageDataGrid.SelectedIndex;
            CurrentFileNameText.Text = imageItem.FileName;
            FileSizeText.Text = $"{imageItem.FileSizeMB:F1} MB";
            //StatusText.Text = isReload ? "重新加载图像..." : "正在加载图像...";

            try
            {
                var imageInfo = await System.Threading.Tasks.Task.Run(() =>
                {
                    // 获取图像信息
                    var info = OpenCVImageLoader.GetImageInfo(imageItem.ImagePath);
                    var bitmapSource = OpenCVImageLoader.LoadTiffImage(imageItem.ImagePath, 0.1);
                    return (info, bitmapSource);
                });

                if (imageInfo.bitmapSource != null)
                {
                    // 更新图像显示
                    ImageDisplay.CurrentImage = imageInfo.bitmapSource;

                    // 更新图像信息显示
                    UpdateImageInfoDisplay(imageInfo.info, imageInfo.bitmapSource);

                    imageItem.Status = IsChineseMode? "已加载":"isReload";
                    //StatusText.Text = isReload ? "图像重新加载完成" : "图像加载完成";
                }
                else
                {
                    ClearImageInfoDisplay();
                    imageItem.Status = IsChineseMode ? "加载失败" : "Loading failed";
                    //StatusText.Text = "图像加载失败";
                    MessageBox.Show(IsChineseMode ? "无法加载指定的图像文件" : "Unable to load the specified image file", IsChineseMode ? "错误" : "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ClearImageInfoDisplay();
                imageItem.Status = IsChineseMode ? "错误" : "Error";
                //StatusText.Text = $"加载错误: {ex.Message}";
                ShowErrorMessage(IsChineseMode ? "加载图像时发生错误" : "An error occurred while loading the image", ex);
            }
        }
        #endregion
        private void ClearAllImages()
        {
            _model.ImageResults.Clear();
            ImageDisplay.CurrentImage = null;
            ClearImageInfoDisplay();
            //UpdateImageCount();
            _nextImageId = 1;

            //StatusText.Text = "已清除所有图像";
        }
        #region 辅助方法
        private string GetImageFilterString()
        {
            var extensions = string.Join(";", _supportedImageExtensions.Select(ext => "*" + ext));
            return $"图像文件 ({extensions})|{extensions}|所有文件 (*.*)|*.*";
        }
        private void ShowErrorMessage(string title, Exception ex)
        {
            string message = $"{title}:\n{ex.Message}";
            if (ex.InnerException != null)
            {
                message += $"\n\n详细信息:\n{ex.InnerException.Message}";
            }

            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            //StatusText.Text = title;
        }
        private void UpdateProgressText(int loaded, int total)
        {
            LoadingProgressText.Text = $"{loaded}/{total}";
        }
        private void AddImageFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif" };

            try
            {
                var imageFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => imageExtensions.Contains(System.IO.Path.GetExtension(file).ToLower()))
                    .OrderBy(file => file);

                foreach (var file in imageFiles)
                {
                    var imageData = ImageItem.CreateFromFile(file);
                    if (imageData != null)
                    {
                        _model.ImageResults.Add(imageData);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载文件夹失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SetupKeyboardShortcuts()
        {
            // 快捷键支持
            this.KeyDown += (s, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    switch (e.Key)
                    {
                        case Key.OemPlus:
                        case Key.Add:
                            ZoomIn_Click(null, null);
                            e.Handled = true;
                            break;
                        case Key.OemMinus:
                        case Key.Subtract:
                            ZoomOut_Click(null, null);
                            e.Handled = true;
                            break;
                        case Key.D0:
                            ZoomOriginal_Click(null, null);
                            e.Handled = true;
                            break;
                        case Key.F:
                            ZoomFit_Click(null, null);
                            e.Handled = true;
                            break;
                        case Key.Left:
                            PreviousImage_Click(null, null);
                            e.Handled = true;
                            break;
                        case Key.Right:
                            NextImage_Click(null, null);
                            e.Handled = true;
                            break;
                    }
                }
            };
        }
        #endregion
        private void ImageDisplay_ZoomChanged(object? sender, double newZoomLevel)
        {
            ZoomSlider.Value = newZoomLevel * 100;

            UpdateZoomDisplay(newZoomLevel);

            var displayInfo = ImageDisplay.GetDisplayInfo();
            DisplayDimensionsText.Text = $"{displayInfo.DisplaySize.Width:F0} × {displayInfo.DisplaySize.Height:F0} 像素";
        }
        private void UpdateZoomDisplay(double zoomLevel)
        {
            // 更新百分比显示
            ZoomPercentageText.Text = $"{zoomLevel * 100:F0}%";

            // 更新滑块（避免循环事件）
            if (!_isUpdatingZoomSlider)
            {
                _isUpdatingZoomSlider = true;
                ZoomSlider.Value = zoomLevel * 100;
                _isUpdatingZoomSlider = false;
            }

            // 根据缩放级别改变颜色提示
            if (zoomLevel < 0.3)
                ZoomPercentageText.Foreground = System.Windows.Media.Brushes.Red;
            else if (zoomLevel > 3.0)
                ZoomPercentageText.Foreground = System.Windows.Media.Brushes.Orange;
            else
                ZoomPercentageText.Foreground = System.Windows.Media.Brushes.Green;
        }

        private async void ImageDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ImageDataGrid.SelectedItem is ImageItem selectedImage)
            {
                _currentImageIndex = ImageDataGrid.SelectedIndex;
                CurrentFileNameText.Text = selectedImage.FileName;
                FileSizeText.Text = $"{selectedImage.FileSizeMB:F1} MB";
                //StatusText.Text = "正在加载图像...";

                try
                {
                    //var imageInfo = await System.Threading.Tasks.Task.Run(() =>
                    //{
                    //    // 获取图像信息

                    //});
                    string fExt = System.IO.Path.GetExtension(selectedImage.ImagePath).ToLower();
                    if (fExt == ".cvcie" || fExt == ".cvraw")
                    {
                        CVCIEFileInfo fileInfo = new CVCIEFileInfo();
                        if(CVImageFileUtil.LoadImgFile_Raw(selectedImage.ImagePath,ref fileInfo))
                        {
                            var infoCV = (fileInfo.FrameInfo.widthInt, fileInfo.FrameInfo.heightInt, fileInfo.FrameInfo.channelsInt);
                            Mat src = OpenCvSharp.Mat.FromPixelData(fileInfo.FrameInfo.heightInt, fileInfo.FrameInfo.widthInt, OpenCvMatTools.GetMatType(fileInfo.FrameInfo.bppInt, fileInfo.FrameInfo.channelsInt), fileInfo.data);
                            var bitmapSourceCV = OpenCVImageLoader.ConvertMatToBitmap(OpenCvMatTools.ConvertImage32To8ByNorm(src));
                            var imageInfoCV = (infoCV, bitmapSourceCV);
                            if (imageInfoCV.bitmapSourceCV != null)
                            {
                                // 更新图像显示
                                ImageDisplay.CurrentImage = imageInfoCV.bitmapSourceCV;

                                // 更新图像信息显示
                                UpdateImageInfoDisplay(imageInfoCV.infoCV, imageInfoCV.bitmapSourceCV);

                                selectedImage.Status = IsChineseMode ? "已加载" : "isReload";
                                //StatusText.Text = "图像加载完成";
                                ImageDisplay.ZoomToFit();
                            }
                            else
                            {
                                ImageDisplay.CurrentImage = null;
                                ClearImageInfoDisplay();
                                selectedImage.Status = IsChineseMode ? "加载失败" : "Loading failed";
                                //StatusText.Text = "图像加载失败";
                            }
                        }
                    }
                    else
                    {
                        var info = OpenCVImageLoader.GetImageInfo(selectedImage.ImagePath);
                        var bitmapSource = OpenCVImageLoader.LoadTiffImage(selectedImage.ImagePath, 0.1);
                        var imageInfo = (info, bitmapSource);
                        if (imageInfo.bitmapSource != null)
                        {
                            // 更新图像显示
                            ImageDisplay.CurrentImage = imageInfo.bitmapSource;

                            // 更新图像信息显示
                            UpdateImageInfoDisplay(imageInfo.info, imageInfo.bitmapSource);

                            selectedImage.Status = IsChineseMode ? "已加载" : "isReload";
                            //StatusText.Text = "图像加载完成";
                            ImageDisplay.ZoomToFit();
                        }
                        else
                        {
                            ImageDisplay.CurrentImage = null;
                            ClearImageInfoDisplay();
                            selectedImage.Status = IsChineseMode ? "加载失败" : "Loading failed";
                            //StatusText.Text = "图像加载失败";
                        }
                    }
                }
                catch (Exception ex)
                {
                    ImageDisplay.CurrentImage = null;
                    ClearImageInfoDisplay();
                    selectedImage.Status = IsChineseMode ? "错误" : "Error";
                    //StatusText.Text = $"加载错误: {ex.Message}";
                }
            }
        }
        private void ClearImageInfoDisplay()
        {
            ImageDimensionsText.Text = "0 × 0 ";
            DisplayDimensionsText.Text = "0 × 0 ";
            ResolutionText.Text = "0 DPI";
            ZoomPercentageText.Text = "100%";
            ZoomSlider.Value = 100;
        }
        private void UpdateImageInfoDisplay((int width, int height, int channels) info, BitmapSource bitmapSource)
        {
            // 显示原始尺寸
            ImageDimensionsText.Text = $"{info.width} × {info.height}";

            // 显示当前显示尺寸（实时计算）
            var displayInfo = ImageDisplay.GetDisplayInfo();
            DisplayDimensionsText.Text = $"{displayInfo.DisplaySize.Width:F0} × {displayInfo.DisplaySize.Height:F0} ";

            // 显示分辨率
            ResolutionText.Text = $"{bitmapSource.DpiX:F0} DPI";

            // 显示缩放信息和可见区域
            ZoomPercentageText.Text = $"{displayInfo.Scale * 100:F0}%";
            ZoomPercentageText.ToolTip =IsChineseMode? $"可见区域: {displayInfo.VisiblePercentage:F1}%": $"Visible area: {displayInfo.VisiblePercentage:F1}%";

            // 显示通道信息
            if (info.channels > 0)
            {
                string channelInfo = info.channels switch
                {
                    1 => IsChineseMode?"灰度":"grayscale",
                    3 => "RGB",
                    4 => "RGBA",
                    _ => $"{info.channels}通道"
                };
                ResolutionText.ToolTip = $"色彩模式: {channelInfo}\n分辨率: {bitmapSource.DpiX:F0} DPI";
            }
        }


        #region 缩放控制事件
        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isUpdatingZoomSlider && ImageDisplay != null && ImageDisplay.CurrentImage != null)
            {
                double newZoom = e.NewValue / 100.0;
                ImageDisplay.SetZoomLevel(newZoom);
            }
        }
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ImageDisplay.ZoomIn();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ImageDisplay.ZoomOut();
        }

        private void ZoomOriginal_Click(object sender, RoutedEventArgs e)
        {
            ImageDisplay.ZoomToOriginal();
        }

        private void ZoomFit_Click(object sender, RoutedEventArgs e)
        {
            ImageDisplay.ZoomToFit();
        }

        private void PreviousImage_Click(object sender, RoutedEventArgs e)
        {
            if (_model.ImageResults.Any() && _currentImageIndex > 0)
            {
                ImageDataGrid.SelectedIndex = _currentImageIndex - 1;
            }
        }

        private void NextImage_Click(object sender, RoutedEventArgs e)
        {
            if (_model.ImageResults.Any() && _currentImageIndex < _model.ImageResults.Count - 1)
            {
                ImageDataGrid.SelectedIndex = _currentImageIndex + 1;
            }
        }
        #endregion
    }
}
