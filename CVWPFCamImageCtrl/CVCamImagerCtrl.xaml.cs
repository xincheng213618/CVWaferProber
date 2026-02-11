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
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVCamImagerCtrl));
        //private ObservableCollection<ImageItem> _imageItems;
        private int _currentImageIndex = -1;
        private bool _isUpdatingZoomSlider = false;
        private int _nextImageId = 1;
        private CVCamImagerViewModel _model;
        // 新增：当前视图标记（区分 Analysis / Camera）
        private string _currentViewType = "Analysis";

        public static bool IsChineseMode = false; // false=英文，true=中文
        /// <summary>
        /// 获取当前视图对应的活动图像集合
        /// </summary>
        /// <returns>当前视图对应的 ObservableCollection<ImageItem></returns>
        private ObservableCollection<ImageItem> GetCurrentActiveCollection()
        {
            if (_model == null)
            {
                return new ObservableCollection<ImageItem>();
            }

            // 根据当前视图类型返回对应的集合
            return _currentViewType switch
            {
                "Camera" => _model.OriginalImageResults,   // Camera Measurement：原图集合（.cvraw）
                "Analysis" => _model.ProcessedImageResults, // Analysis Image：处理后集合（.cvcie）
                _ => new ObservableCollection<ImageItem>()  // 默认返回空集合
            };
        }
        // 支持的图像格式
        private readonly string[] _supportedImageExtensions = {
            ".tif", ".tiff", ".jpg", ".jpeg", ".png", ".bmp",
            ".gif", ".webp", ".ico", ".exif",".cvraw", ".cvcie" //支持相机原始图和标定后图格式
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
            if (this.DataContext is CVCamImagerViewModel model)
            {
                _model = model;
                model.SetImageCtrl(ImageDisplay);
                // 初始化 DataGrid 数据源为 ProcessedImageResults
                MainImageDataGrid.ItemsSource = _model.ProcessedImageResults;
                // 关键1：绑定图像新增事件（核心）
                _model.ImageItemAdded += Model_ImageItemAdded;
                // 初始化视图类型同步
                _model.CurrentViewType = _currentViewType;
            }
        }
        // 新增：图像新增事件回调——自动选中最新项（核心逻辑）
        private void Model_ImageItemAdded(ImageItem newImageItem)
        {
            // 必须在UI线程执行选中操作，避免跨线程异常
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 1. 获取当前视图的最新项（兼容Analysis/Camera视图）
                    var latestItem = _model.GetCurrentViewLatestImageItem();
                    if (latestItem == null) return;

                    // 2. 选中最新项（设置SelectedItem比SelectedIndex更稳定，避免索引错位）
                    MainImageDataGrid.SelectedItem = latestItem;
                    // 3. 滚动到最新项，确保用户能看到
                    MainImageDataGrid.ScrollIntoView(latestItem);
                    // 4. 更新当前索引，保证上一张/下一张按钮正常工作
                    _currentImageIndex = MainImageDataGrid.SelectedIndex;

                   // logger.Info($"AOI图像自动选中最新项：{latestItem.FileName}，索引：{_currentImageIndex}");
                }
                catch (Exception ex)
                {
                    logger.Error("The automatic selection of AOI images failed.", ex);
                }
            });
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
        #region 新增：下拉框切换 DataGrid 数据源
        private void ViewSwitchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_model == null || MainImageDataGrid == null)
            {
                //logger.Info("Error: _model or MainImageDataGrid is null");
                return;
            }

            // 切换视图前清空当前显示
            ImageDisplay.CurrentImage = null;
            ClearImageInfoDisplay();

            var selectedItem = ViewSwitchComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null)
            {
                logger.Info("Error: Failed to convert selected item to ComboBoxItem");
                return;
            }

            _currentViewType = selectedItem.Tag.ToString() ?? "Analysis";
            // 关键2：同步更新ViewModel的视图类型，确保GetLatest方法正确
            _model.CurrentViewType = _currentViewType;
            logger.Info($"Current view type：{_currentViewType}");

            // 获取当前活动的集合（用于加载图像）
            var currentActiveCollection = GetCurrentActiveCollection();

            // 保存当前选中的图像（如果有）
            ImageItem previouslySelectedImage = MainImageDataGrid.SelectedItem as ImageItem;
            string previousImagePath = previouslySelectedImage?.ImagePath;

            // 核心：视图与对应分类集合绑定，仅显示对应类型图像
            switch (_currentViewType)
            {
                case "Analysis":
                    logger.Info($"切换到 Analysis 视图，绑定 ProcessedImageResults");
                    // 直接绑定集合，而不是通过 ItemsSource 属性
                    MainImageDataGrid.ItemsSource = _model.ProcessedImageResults;

                    // 检查绑定是否成功
                    if (MainImageDataGrid.ItemsSource != _model.ProcessedImageResults)
                    {
                        logger.Info("警告：绑定 ProcessedImageResults 失败");
                        // 强制重新绑定
                        MainImageDataGrid.ItemsSource = null;
                        MainImageDataGrid.ItemsSource = _model.ProcessedImageResults;
                    }
                    break;

                case "Camera":
                    logger.Info($"切换到 Camera 视图，绑定 OriginalImageResults");
                    MainImageDataGrid.ItemsSource = _model.OriginalImageResults;

                    // 检查绑定是否成功
                    if (MainImageDataGrid.ItemsSource != _model.OriginalImageResults)
                    {
                        logger.Info("警告：绑定 OriginalImageResults 失败");
                        // 强制重新绑定
                        MainImageDataGrid.ItemsSource = null;
                        MainImageDataGrid.ItemsSource = _model.OriginalImageResults;
                    }
                    break;
            }

            // 强制刷新 DataGrid，确保数据更新
            MainImageDataGrid.Items.Refresh();
            MainImageDataGrid.UpdateLayout();

            // 延迟选中逻辑
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var currentItemsSource = MainImageDataGrid.ItemsSource as ObservableCollection<ImageItem>;
                if (currentItemsSource == null || currentItemsSource.Count == 0)
                {
                    // 如果当前集合为空，清除图像显示
                    ImageDisplay.CurrentImage = null;
                    ClearImageInfoDisplay();
                    CurrentFileNameText.Text = IsChineseMode ? "无图像" : "No Image";
                    logger.Info("当前视图集合无数据，无法选中");
                    return;
                }

                // 尝试重新选中之前选中的图像（如果存在于当前集合中）
                if (!string.IsNullOrEmpty(previousImagePath))
                {
                    var sameImageInNewCollection = currentItemsSource
                        .FirstOrDefault(item => item.ImagePath.Equals(previousImagePath, StringComparison.OrdinalIgnoreCase));

                    if (sameImageInNewCollection != null)
                    {
                        MainImageDataGrid.SelectedItem = sameImageInNewCollection;
                        MainImageDataGrid.SelectedIndex = currentItemsSource.IndexOf(sameImageInNewCollection);
                        logger.Info($"视图切换后重新选中同一图像: {sameImageInNewCollection.FileName}");
                        return;
                    }
                }

                // 如果没有找到之前的图像，选中第一项
                MainImageDataGrid.SelectedIndex = 0;
                logger.Info("视图切换后自动选中第一项");

            }), DispatcherPriority.Loaded);
            //if (_model == null || MainImageDataGrid == null)
            //{
            //    Debug.WriteLine("异常：_model 或 MainImageDataGrid 为 null");
            //    return;
            //}

            //var selectedItem = ViewSwitchComboBox.SelectedItem as ComboBoxItem;
            //if (selectedItem == null)
            //{
            //    Debug.WriteLine("异常：选中项转换为 ComboBoxItem 失败");
            //    return;
            //}

            //_currentViewType = selectedItem.Tag.ToString() ?? "Analysis";
            //Debug.WriteLine($"当前视图类型：{_currentViewType}");

            //// 核心：视图与对应分类集合绑定，仅显示对应类型图像
            //switch (_currentViewType)
            //{
            //    case "Analysis":
            //        // 绑定处理后集合（仅 .cvcie 标定图）
            //        MainImageDataGrid.ItemsSource = _model.ProcessedImageResults;
            //        break;
            //    case "Camera":
            //        // 绑定原图集合（仅 .cvraw 相机原始图）
            //        MainImageDataGrid.ItemsSource = _model.OriginalImageResults;
            //        break;
            //}

            //// 调试输出：查看对应集合数据量
            //Debug.WriteLine($"ProcessedImageResults（cvcie）数量：{_model.ProcessedImageResults.Count}");
            //Debug.WriteLine($"OriginalImageResults（cvraw）数量：{_model.OriginalImageResults.Count}");

            //// 强制刷新 DataGrid，确保数据更新
            //MainImageDataGrid.Items.Refresh();
            //MainImageDataGrid.UpdateLayout();

            //// 延迟选中第一项（确保绑定完成）
            //Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            //{
            //    if (MainImageDataGrid.Items.Count > 0)
            //    {
            //        MainImageDataGrid.SelectedIndex = 0;
            //        Debug.WriteLine("视图切换后自动选中第一项");
            //    }
            //    else
            //    {
            //        Debug.WriteLine("当前视图集合无数据，无法选中");
            //    }
            //}), DispatcherPriority.Loaded);
        }

        #endregion
        #region 文件操作    
        private async void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = IsChineseMode ? "选择图像文件" : "Select image file",
                    Filter = GetImageFilterString(),
                    Multiselect = true, // 支持多选
                    CheckFileExists = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    Task<List<ImageItem>> task = LoadImageFilesAsync(openFileDialog.FileNames);
                    await task;

                    // 刷新当前视图的 DataGrid（根据下拉框选中状态）
                    MainImageDataGrid.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage(IsChineseMode ? "打开文件时发生错误" : "An error occurred when opening the file.", ex);
            }
        }

        private async void LoadFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new CommonOpenFileDialog())
                {
                    dialog.IsFolderPicker = true;
                    dialog.Title = IsChineseMode ? "请选择一个文件夹" : "Please select a folder.";

                    if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        // 核心：清空所有集合（包括分类集合）
                        _model.ImageResults.Clear();
                        _model.ProcessedImageResults.Clear();
                        _model.OriginalImageResults.Clear();

                        var folderPath = dialog.FileName;
                        if (Directory.Exists(folderPath))
                        {
                            // 搜索所有支持的图像文件（包含 .cvraw/.cvcie）
                            var imageFiles = _supportedImageExtensions
                                .SelectMany(ext => Directory.GetFiles(folderPath, "*" + ext, SearchOption.AllDirectories))
                                .ToArray();

                            if (imageFiles.Length > 0)
                            {
                                _nextImageId = 1;
                                Task<List<ImageItem>> task = LoadImageFilesAsync(imageFiles);
                                await task;

                                MainImageDataGrid.Items.Refresh();
                            }
                            else
                            {
                                MessageBox.Show(IsChineseMode ? "在选择的文件夹中未找到支持的图像文件。" : "No supported image files were found in the selected folder.", IsChineseMode ? "提示" : "Prompt",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Info($"加载文件夹失败：{ex.Message}");
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
                            var fileInfo = new FileInfo(filePath);
                            if (fileInfo.Exists)
                            {
                                // 获取文件扩展名
                                string fileExt = Path.GetExtension(filePath).ToLower();
                                string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();

                                // 检查是否是 po.dat 文件
                                bool isPoDatFile = fileName.Equals("po") || fileName.Equals("po.dat");

                                // 如果是 po.dat 文件，跳过不加载到 DataGrid
                                if (isPoDatFile)
                                {
                                    logger.Info($"Skipping po.dat file: {Path.GetFileName(filePath)}");
                                    continue;
                                }

                                var imageItem = new ImageItem(_nextImageId++)
                                {
                                    ImagePath = filePath,
                                    FileName = Path.GetFileName(filePath),
                                    FileSizeMB = fileInfo.Length / (1024.0 * 1024.0),
                                    Status = IsChineseMode ? "待加载" : "Loading"
                                };

                                // 调用 ViewModel 的 AddImage 方法，自动分配到对应集合
                                _model.AddImage(imageItem);

                                loadedCount++;
                                UpdateProgressText(loadedCount, totalCount);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Info(IsChineseMode ? $"加载文件失败 {filePath}: {ex.Message}" :
                                $"Failed to load file {filePath}: {ex.Message}");
                        }
                    }
                });

                // 加载完成后，根据当前视图类型选择第一项
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var currentActiveCollection = GetCurrentActiveCollection();
                    if (currentActiveCollection.Count > 0)
                    {
                        // 确保 DataGrid 绑定的是当前活动集合
                        if (_currentViewType == "Analysis")
                        {
                            MainImageDataGrid.ItemsSource = _model.ProcessedImageResults;
                        }
                        else if (_currentViewType == "Camera")
                        {
                            MainImageDataGrid.ItemsSource = _model.OriginalImageResults;
                        }

                        MainImageDataGrid.SelectedIndex = 0;
                        MainImageDataGrid.Items.Refresh();
                    }
                });
            }
            catch (Exception ex)
            {
                ShowErrorMessage(IsChineseMode ? "加载图像文件时发生错误" :
                    "An error occurred while loading the image", ex);
            }

            return results;
            //List<ImageItem> results = new List<ImageItem>();
            //try
            //{
            //    int loadedCount = 0;
            //    int totalCount = filePaths.Length;
            //    await System.Threading.Tasks.Task.Run(() =>
            //    {
            //        foreach (string filePath in filePaths)
            //        {
            //            try
            //            {
            //                var fileInfo = new FileInfo(filePath);
            //                if (fileInfo.Exists)
            //                {
            //                    var imageItem = new ImageItem(_nextImageId++)
            //                    {
            //                        ImagePath = filePath,
            //                        FileName = System.IO.Path.GetFileName(filePath),
            //                        FileSizeMB = fileInfo.Length / (1024.0 * 1024.0),
            //                        Status = IsChineseMode ? "待加载" : "Loading"
            //                    };
            //                    // 调用 ViewModel 的 AddImage 方法，自动分配到对应集合
            //                    _model.AddImage(imageItem);

            //                    loadedCount++;
            //                    UpdateProgressText(loadedCount, totalCount);
            //                }
            //            }
            //            catch (Exception ex)
            //            {
            //                Debug.WriteLine(IsChineseMode ? $"加载文件失败 {filePath}: {ex.Message}" : $"Failed to load file {filePath}: {ex.Message}");
            //            }
            //        }
            //    });

            //    // 加载完成后选择第一项
            //    if (MainImageDataGrid.Items.Count > 0)
            //    {
            //        MainImageDataGrid.SelectedIndex = 0;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    ShowErrorMessage(IsChineseMode ? "加载图像文件时发生错误" : "An error occurred while loading the image", ex);
            //}
            //finally
            //{
            //}
            //return results;
        }

        private void ReloadImage_Click(object sender, RoutedEventArgs e)
        {
            // 获取当前视图的活动集合
            var currentCollection = GetCurrentActiveCollection();
            if (currentCollection.Count == 0)
            {
                MessageBox.Show(IsChineseMode ? "当前视图中无图像可重新加载。" : "No images to reload in the current view.", IsChineseMode ? "提示" : "Prompt", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 获取当前DataGrid选中的项（仅来自当前活动集合）
            if (MainImageDataGrid.SelectedItem is ImageItem selectedImage)
            {
                // 验证选中项是否存在于当前活动集合中（防止跨集合异常）
                if (currentCollection.Contains(selectedImage))
                {
                    // 重新加载当前选中的图像（逻辑不变，仅限定当前集合）
                    LoadSelectedImage(selectedImage, true);
                    logger.Info(IsChineseMode ? "图像重新加载完成。" : "Image reloaded successfully.");
                }
                else
                {
                    MessageBox.Show(IsChineseMode ? "选中的图像不存在于当前视图中。" : "The selected image does not exist in the current view.", IsChineseMode ? "提示" : "Prompt", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show(IsChineseMode ? "请先在当前视图中选择一个图像文件。" : "Please select an image file in the current view first.", IsChineseMode ? "提示" : "Prompt", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            // 获取当前视图的活动集合
            var currentCollection = GetCurrentActiveCollection();
            if (currentCollection.Count == 0)
            {
                MessageBox.Show(IsChineseMode ? "当前视图中无图像可清除。" : "No images to clear in the current view.", IsChineseMode ? "提示" : "Prompt", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 拼接提示文本，明确当前清除的视图
            string viewName = _currentViewType switch
            {
                "Camera" => "Camera Measurement",
                _ => "Analysis Image"
            };
            var result = MessageBox.Show(
                IsChineseMode ? $"确定要清除当前「{viewName}」视图中的所有图像吗？这个操作不可撤销。" : $"Are you sure you want to clear all images in the current \"{viewName}\" view? This action cannot be undone.",
                IsChineseMode ? "确认清除" : "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 1. 仅清空当前视图对应的集合
                currentCollection.Clear();

                // 2. 清空当前视图的UI显示（图像、信息面板、DataGrid）
                ImageDisplay.CurrentImage = null;
                ClearImageInfoDisplay();
                MainImageDataGrid.Items.Refresh();
                MainImageDataGrid.UpdateLayout();

                // 3. 重置当前视图的图像ID（仅影响后续添加到当前集合的项）
                _nextImageId = 1;

                // 4. 提示清除完成
                MessageBox.Show(
                    IsChineseMode ? $"当前「{viewName}」视图的图像已全部清除。" : $"All images in the current \"{viewName}\" view have been cleared.",
                    IsChineseMode ? "清除完成" : "Clear Completed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        #endregion
        //#region 文件操作功能
        //private async void LoadFile_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        var openFileDialog = new OpenFileDialog
        //        {
        //            Title = IsChineseMode ? "选择图像文件": "Select image file",
        //            Filter = GetImageFilterString(),
        //            Multiselect = true, // 支持多选
        //            CheckFileExists = true
        //        };

        //        if (openFileDialog.ShowDialog() == true)
        //        {
        //            // 显示加载进度
        //            //LoadingProgressBar.Value = 0;
        //            //LoadingProgressBar.Maximum = openFileDialog.FileNames.Length;

        //            // 异步加载文件
        //            Task<List<ImageItem>> task = LoadImageFilesAsync(openFileDialog.FileNames);
        //            await task;

        //            ImageDataGrid.ItemsSource = _model.ImageResults;
        //            ImageDataGrid.Items.Refresh();
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        ShowErrorMessage(IsChineseMode ? "打开文件时发生错误": "An error occurred when opening the file.", ex);
        //    }
        //}
        //private async void LoadFolder_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        using (var dialog = new CommonOpenFileDialog())
        //        {
        //            dialog.IsFolderPicker = true; // 关键：设置为选择文件夹
        //            dialog.Title = IsChineseMode ? "请选择一个文件夹" : "Please select a folder.";

        //            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        //            {
        //                //string selectedFolderPath = dialog.FileName;
        //                _model.ImageResults.Clear();
        //                var folderPath = dialog.FileName;
        //                if (Directory.Exists(folderPath))
        //                {
        //                    // 搜索所有支持的图像文件
        //                    var imageFiles = _supportedImageExtensions
        //                        .SelectMany(ext => Directory.GetFiles(folderPath, "*" + ext, SearchOption.AllDirectories))
        //                        .ToArray();

        //                    if (imageFiles.Length > 0)
        //                    {
        //                        // 显示加载进度
        //                        //LoadingProgressBar.Value = 0;
        //                        //LoadingProgressBar.Maximum = imageFiles.Length;
        //                        _nextImageId = 1;
        //                        //StatusText.Text = $"找到 {imageFiles.Length} 个图像文件，正在加载...";
        //                        Task<List<ImageItem>> task= LoadImageFilesAsync(imageFiles);
        //                        await task;

        //                        ImageDataGrid.ItemsSource = _model.ImageResults;
        //                        ImageDataGrid.Items.Refresh();
        //                        //foreach (var imageItem in task.Result)
        //                        //{
        //                        //    _imageItems.Add(imageItem);
        //                        //}
        //                    }
        //                    else
        //                    {
        //                        MessageBox.Show(IsChineseMode?"在选择的文件夹中未找到支持的图像文件。": "No supported image files were found in the selected folder.", IsChineseMode ? "提示": "Prompt",
        //                            MessageBoxButton.OK, MessageBoxImage.Information);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        //ShowErrorMessage("打开文件夹时发生错误", ex);
        //    }
        //}
        //private async Task<List<ImageItem>> LoadImageFilesAsync(string[] filePaths)
        //{
        //    List<ImageItem> results = new List<ImageItem>();
        //    try
        //    {
        //        int loadedCount = 0;
        //        int totalCount = filePaths.Length;
        //        await System.Threading.Tasks.Task.Run(() =>
        //        {
        //            foreach (string filePath in filePaths)
        //            {
        //                try
        //                {
        //                    // 检查文件是否已经在列表中
        //                    //if (_imageItems.Any(item => item.ImagePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
        //                    //{
        //                    //    Dispatcher.Invoke(() =>
        //                    //    {
        //                    //        //LoadingProgressBar.Value++;
        //                    //        loadedCount++;
        //                    //        UpdateProgressText(loadedCount, totalCount);
        //                    //    });
        //                    //    continue;
        //                    //}

        //                    // 获取文件信息
        //                    var fileInfo = new FileInfo(filePath);
        //                    if (fileInfo.Exists)
        //                    {
        //                        var imageItem = new ImageItem(_nextImageId++)
        //                        {
        //                            //Id = _nextImageId++,
        //                            ImagePath = filePath,
        //                            FileName = System.IO.Path.GetFileName(filePath),
        //                            FileSizeMB = fileInfo.Length / (1024.0 * 1024.0),
        //                            Status = IsChineseMode?"待加载": "Loading"
        //                        };
        //                        //results.Add(imageItem);
        //                        // 在UI线程上添加项目
        //                        //Dispatcher.Invoke(() =>
        //                        //{
        //                        _model.ImageResults.Add(imageItem);
        //                        //    //LoadingProgressBar.Value++;
        //                        //    loadedCount++;
        //                        //    UpdateProgressText(loadedCount, totalCount);
        //                        //    //UpdateImageCount();
        //                        //});
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    Debug.WriteLine(IsChineseMode?$"加载文件失败 {filePath}: {ex.Message}": $"Failed to load file {filePath}: {ex.Message}");
        //                }
        //            }
        //        });

        //        //加载完成后选择第一个文件
        //        if (_model.ImageResults.Any())
        //        {
        //            ImageDataGrid.SelectedIndex = 0;
        //            //StatusText.Text = $"成功加载 {loadedCount} 个图像文件";
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        //ShowErrorMessage("加载图像文件时发生错误", ex);
        //    }
        //    finally
        //    {
        //        // 重置进度条
        //        //await System.Threading.Tasks.Task.Delay(2000); // 显示2秒完成状态
        //        //Dispatcher.Invoke(() =>
        //        //{
        //        //    //LoadingProgressBar.Value = 0;
        //        //    LoadingProgressText.Text = "";
        //        //});

        //    }
        //    return results;
        //}
        //private void ReloadImage_Click(object sender, RoutedEventArgs e)
        //{
        //    if (ImageDataGrid.SelectedItem is ImageItem selectedImage)
        //    {
        //        // 重新加载当前选中的图像
        //        LoadSelectedImage(selectedImage, true);
        //    }
        //    else
        //    {
        //        MessageBox.Show(IsChineseMode? "请先选择一个图像文件。": "Please select an image file first.", IsChineseMode ? "提示" : "Prompt", MessageBoxButton.OK, MessageBoxImage.Information);
        //    }
        //}
        //private void ClearAll_Click(object sender, RoutedEventArgs e)
        //{
        //    var result = MessageBox.Show(IsChineseMode ? "确定要清除所有图像吗？这个操作不可撤销。" : "Are you sure you want to delete all images? This action cannot be undone.", IsChineseMode ? "确认清除" : "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Question);

        //    if (result == MessageBoxResult.Yes)
        //    {
        //        ClearAllImages();
        //    }
        //}
        //// 增强的图像加载方法
        //private async void LoadSelectedImage(ImageItem imageItem, bool isReload = false)
        //{

        //    if (imageItem == null) return;

        //    _currentImageIndex = ImageDataGrid.SelectedIndex;
        //    CurrentFileNameText.Text = imageItem.FileName;
        //    FileSizeText.Text = $"{imageItem.FileSizeMB:F1} MB";
        //    //StatusText.Text = isReload ? "重新加载图像..." : "正在加载图像...";

        //    try
        //    {
        //        var imageInfo = await System.Threading.Tasks.Task.Run(() =>
        //        {
        //            // 获取图像信息
        //            var info = OpenCVImageLoader.GetImageInfo(imageItem.ImagePath);
        //            var bitmapSource = OpenCVImageLoader.LoadTiffImage(imageItem.ImagePath, 0.1);
        //            return (info, bitmapSource);
        //        });

        //        if (imageInfo.bitmapSource != null)
        //        {
        //            // 更新图像显示
        //            ImageDisplay.CurrentImage = imageInfo.bitmapSource;

        //            // 更新图像信息显示
        //            UpdateImageInfoDisplay(imageInfo.info, imageInfo.bitmapSource);

        //            imageItem.Status = IsChineseMode? "已加载":"isReload";
        //            //StatusText.Text = isReload ? "图像重新加载完成" : "图像加载完成";
        //        }
        //        else
        //        {
        //            ClearImageInfoDisplay();
        //            imageItem.Status = IsChineseMode ? "加载失败" : "Loading failed";
        //            //StatusText.Text = "图像加载失败";
        //            MessageBox.Show(IsChineseMode ? "无法加载指定的图像文件" : "Unable to load the specified image file", IsChineseMode ? "错误" : "Error",
        //                MessageBoxButton.OK, MessageBoxImage.Error);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        ClearImageInfoDisplay();
        //        imageItem.Status = IsChineseMode ? "错误" : "Error";
        //        //StatusText.Text = $"加载错误: {ex.Message}";
        //        ShowErrorMessage(IsChineseMode ? "加载图像时发生错误" : "An error occurred while loading the image", ex);
        //    }
        //}
        //#endregion

        private async void LoadSelectedImage(ImageItem imageItem, bool isReload = false)
        {
            if (imageItem == null) return;

            // 1. 记录当前选中索引（UI线程操作）
            _currentImageIndex = MainImageDataGrid.SelectedIndex;

            try
            {
                // 2. 耗时操作：明确指定Func类型，支持返回元组
                var loadResult = await Task.Run<(
                    (int width, int height, int channels) info,
                    BitmapSource bitmapSource,
                    bool success,
                    Exception error
                )>(() =>
                {
                    try
                    {
                        // 获取图像信息（后台线程执行）
                        var info = OpenCVImageLoader.GetImageInfo(imageItem.ImagePath);
                        var bitmapSource = OpenCVImageLoader.LoadTiffImage(imageItem.ImagePath, 0.1);
                        return (info: info, bitmapSource: bitmapSource, success: true, error: null);
                    }
                    catch (Exception ex)
                    {
                        return (info: (0, 0, 0), bitmapSource: null, success: false, error: ex);
                    }
                });

                // 3. UI操作切回主线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (loadResult.success && loadResult.bitmapSource != null)
                    {
                        CurrentFileNameText.Text = imageItem.FileName;
                        FileSizeText.Text = $"{imageItem.FileSizeMB:F1} MB";
                        ImageDisplay.CurrentImage = loadResult.bitmapSource;
                        UpdateImageInfoDisplay(loadResult.info, loadResult.bitmapSource);
                        imageItem.Status = IsChineseMode ? "已加载" : "Loaded";
                    }
                    else
                    {
                        ClearImageInfoDisplay();
                        imageItem.Status = IsChineseMode ? "加载失败" : "Loading failed";
                        ShowErrorMessage(IsChineseMode ? "加载图像时发生错误" : "An error occurred while loading the image", loadResult.error);
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ClearImageInfoDisplay();
                    imageItem.Status = IsChineseMode ? "错误" : "Error";
                    ShowErrorMessage(IsChineseMode ? "加载图像时发生错误" : "An error occurred while loading the image", ex);
                });
            }
        }
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
            if (_currentViewType == "Camera")
            {
                // Camera Measurement 模式：只显示相机原始图格式
                return IsChineseMode ?
                    "相机原始图 (*.cvraw)|*.cvraw|所有文件 (*.*)|*.*" :
                    "Camera Raw Images (*.cvraw)|*.cvraw|All Files (*.*)|*.*";
            }
            else
            {
                // Analysis Image 模式：显示标定后图和其他图像格式
                return IsChineseMode ?
                    "标定后图像 (*.cvcie;*.tif;*.tiff;*.jpg;*.jpeg;*.png)|*.cvcie;*.tif;*.tiff;*.jpg;*.jpeg;*.png|所有文件 (*.*)|*.*" :
                    "Calibrated Images (*.cvcie;*.tif;*.tiff;*.jpg;*.jpeg;*.png)|*.cvcie;*.tif;*.tiff;*.jpg;*.jpeg;*.png|All Files (*.*)|*.*";
            }
        }
        private void ShowErrorMessage(string title, Exception ex)
        {
            // 处理ex为null的情况
            string message = ex == null
                ? $"{title}：未知错误"
                : $"{title}:\n{ex.Message}";

            // 若ex不为null，再处理InnerException
            if (ex != null && ex.InnerException != null)
            {
                message += $"\n\n详细信息:\n{ex.InnerException.Message}";
            }

            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            //StatusText.Text = title;
        }
        private void UpdateProgressText(int loaded, int total)
        {
            //LoadingProgressText.Text = $"{loaded}/{total}";
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

        private void MainImageDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 避免空选中触发事件
            if (e.AddedItems.Count == 0 || MainImageDataGrid.SelectedItem == null)
            {
                return;
            }

            if (MainImageDataGrid.SelectedItem is ImageItem selectedImage)
            {
                _currentImageIndex = MainImageDataGrid.SelectedIndex;
                CurrentFileNameText.Text = selectedImage.FileName;
                FileSizeText.Text = $"{selectedImage.FileSizeMB:F1} MB";

                try
                {
                    // 耗时操作放入 Task.Run，避免阻塞UI


                    string fExt = Path.GetExtension(selectedImage.ImagePath)?.ToLower() ?? string.Empty;
                    BitmapSource targetBitmap = null;
                    (int width, int height, int channels) imageInfo = (0, 0, 0);

                    try
                    {
                        // 根据文件扩展名决定加载方式
                        if (fExt == ".cvraw")
                        {
                            // 相机原始图（16bit LZW TIFF）
                            CVCIEFileInfo fileInfo = new CVCIEFileInfo();
                            if (CVImageFileUtil.LoadImgFile_Raw(selectedImage.ImagePath, ref fileInfo))
                            {
                                imageInfo = (fileInfo.FrameInfo.widthInt, fileInfo.FrameInfo.heightInt, fileInfo.FrameInfo.channelsInt);

                                // 创建 Mat 并转换为8位显示
                                using (Mat src = Mat.FromPixelData(
                                    fileInfo.FrameInfo.heightInt,
                                    fileInfo.FrameInfo.widthInt,
                                    OpenCvMatTools.GetMatType(fileInfo.FrameInfo.bppInt, fileInfo.FrameInfo.channelsInt),
                                    fileInfo.data))
                                {
                                    // 对16位图像进行归一化显示
                                    using (Mat normalizedMat = OpenCvMatTools.ConvertImage32To8ByNorm(src))
                                    {
                                        targetBitmap = OpenCVImageLoader.ConvertMatToBitmap(normalizedMat);
                                    }
                                }
                            }
                        }
                        else if (fExt == ".cvcie")
                        {
                            // 标定后图（32bit float）
                            CVCIEFileInfo fileInfo = new CVCIEFileInfo();
                            if (CVImageFileUtil.LoadImgFile_Raw(selectedImage.ImagePath, ref fileInfo))
                            {
                                imageInfo = (fileInfo.FrameInfo.widthInt, fileInfo.FrameInfo.heightInt, fileInfo.FrameInfo.channelsInt);

                                // 创建 Mat 并转换为8位显示
                                using (Mat src = Mat.FromPixelData(
                                    fileInfo.FrameInfo.heightInt,
                                    fileInfo.FrameInfo.widthInt,
                                    OpenCvMatTools.GetMatType(fileInfo.FrameInfo.bppInt, fileInfo.FrameInfo.channelsInt),
                                    fileInfo.data))
                                {
                                    // 对32位浮点图像进行归一化显示
                                    using (Mat normalizedMat = OpenCvMatTools.ConvertImage32To8ByNorm(src))
                                    {
                                        targetBitmap = OpenCVImageLoader.ConvertMatToBitmap(normalizedMat);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // 其他格式图像（TIFF、JPEG、PNG等）
                            var info = OpenCVImageLoader.GetImageInfo(selectedImage.ImagePath);
                            targetBitmap = OpenCVImageLoader.LoadTiffImage(selectedImage.ImagePath, 0.1);
                            imageInfo = info;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"加载图像失败：{ex.Message}");
                    }

                    // UI 操作切回主线程
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (targetBitmap != null)
                        {
                            ImageDisplay.CurrentImage = targetBitmap;
                            UpdateImageInfoDisplay(imageInfo, targetBitmap);
                            selectedImage.Status = IsChineseMode ? "已加载" : "Loaded";

                            // 如果是 Analysis 视图，自动调整缩放以适应
                            if (_currentViewType == "Analysis")
                            {
                                ImageDisplay.ZoomToFit();
                            }
                        }
                        else
                        {
                            ImageDisplay.CurrentImage = null;
                            ClearImageInfoDisplay();
                            selectedImage.Status = IsChineseMode ? "加载失败" : "Loading failed";
                            MessageBox.Show(
                                IsChineseMode ? $"无法加载图像：{selectedImage.FileName}" :
                                $"Cannot load image: {selectedImage.FileName}",
                                IsChineseMode ? "错误" : "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    });

                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ImageDisplay.CurrentImage = null;
                        ClearImageInfoDisplay();
                        selectedImage.Status = IsChineseMode ? "错误" : "Error";
                        MessageBox.Show(
                            IsChineseMode ? $"加载图像时发生错误：{ex.Message}" :
                            $"Error loading image: {ex.Message}",
                            IsChineseMode ? "错误" : "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
            }
            //// 避免空选中触发事件
            //if (e.AddedItems.Count == 0 || MainImageDataGrid.SelectedItem == null)
            //{
            //    return;
            //}

            //if (MainImageDataGrid.SelectedItem is ImageItem selectedImage)
            //{
            //    _currentImageIndex = MainImageDataGrid.SelectedIndex;
            //    CurrentFileNameText.Text = selectedImage.FileName;
            //    FileSizeText.Text = $"{selectedImage.FileSizeMB:F1} MB";

            //    try
            //    {
            //        // 耗时操作放入 Task.Run，避免阻塞UI

            //            string fExt = System.IO.Path.GetExtension(selectedImage.ImagePath)?.ToLower() ?? string.Empty;
            //            BitmapSource targetBitmap = null;
            //            (int width, int height, int channels) imageInfo = (0, 0, 0);

            //            if (fExt == ".cvcie" || fExt == ".cvraw")
            //            {
            //                CVCIEFileInfo fileInfo = new CVCIEFileInfo();
            //                if (CVImageFileUtil.LoadImgFile_Raw(selectedImage.ImagePath, ref fileInfo))
            //                {
            //                    imageInfo = (fileInfo.FrameInfo.widthInt, fileInfo.FrameInfo.heightInt, fileInfo.FrameInfo.channelsInt);
            //                    Mat src = OpenCvSharp.Mat.FromPixelData(
            //                        fileInfo.FrameInfo.heightInt,
            //                        fileInfo.FrameInfo.widthInt,
            //                        OpenCvMatTools.GetMatType(fileInfo.FrameInfo.bppInt, fileInfo.FrameInfo.channelsInt),
            //                        fileInfo.data
            //                    );

            //                    // 专属格式归一化（保持原有逻辑，确保加载成功）
            //                    Mat normalizedMat = OpenCvMatTools.ConvertImage32To8ByNorm(src);
            //                    targetBitmap = OpenCVImageLoader.ConvertMatToBitmap(normalizedMat);

            //                    // 释放 Mat 内存
            //                    src.Release();
            //                    normalizedMat.Release();
            //                }
            //            }
            //            else
            //            {
            //                // 其他格式加载（保持原有逻辑）
            //                var info = OpenCVImageLoader.GetImageInfo(selectedImage.ImagePath);
            //                targetBitmap = OpenCVImageLoader.LoadTiffImage(selectedImage.ImagePath, 0.1);
            //                imageInfo = info;
            //            }

            //            // UI 操作切回主线程
            //            Application.Current.Dispatcher.Invoke(() =>
            //            {
            //                if (targetBitmap != null)
            //                {
            //                    ImageDisplay.CurrentImage = targetBitmap;
            //                    UpdateImageInfoDisplay(imageInfo, targetBitmap);
            //                    selectedImage.Status = IsChineseMode ? "已加载" : "Loaded";
            //                    ImageDisplay.ZoomToFit();
            //                }
            //                else
            //                {
            //                    ImageDisplay.CurrentImage = null;
            //                    ClearImageInfoDisplay();
            //                    selectedImage.Status = IsChineseMode ? "加载失败" : "Loading failed";
            //                }
            //            });

            //    }
            //    catch (Exception ex)
            //    {
            //        Application.Current.Dispatcher.Invoke(() =>
            //        {
            //            ImageDisplay.CurrentImage = null;
            //            ClearImageInfoDisplay();
            //            selectedImage.Status = IsChineseMode ? "错误" : "Error";
            //            Debug.WriteLine($"加载图像失败：{ex.Message}");
            //        });
            //    }
            //}
        }
        private void ClearImageInfoDisplay()
        {
            ImageDimensionsText.Text = "0 × 0 ";
            DisplayDimensionsText.Text = "0 × 0 ";
            //ResolutionText.Text = "0 DPI";
            ZoomPercentageText.Text = "100%";
            ZoomSlider.Value = 100;
        }
        private void UpdateImageInfoDisplay((int width, int height, int channels) info, BitmapSource bitmapSource)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => UpdateImageInfoDisplay(info, bitmapSource));
                return;
            }
            // 显示原始尺寸
            ImageDimensionsText.Text = $"{info.width} × {info.height}";

            // 显示当前显示尺寸（实时计算）
            var displayInfo = ImageDisplay.GetDisplayInfo();
            DisplayDimensionsText.Text = $"{displayInfo.DisplaySize.Width:F0} × {displayInfo.DisplaySize.Height:F0} ";

            // 显示分辨率
            //ResolutionText.Text = $"{bitmapSource.DpiX:F0} DPI";

            // 显示缩放信息和可见区域
            ZoomPercentageText.Text = $"{displayInfo.Scale * 100:F0}%";
            ZoomPercentageText.ToolTip = IsChineseMode ? $"可见区域: {displayInfo.VisiblePercentage:F1}%" : $"Visible area: {displayInfo.VisiblePercentage:F1}%";

            // 显示通道信息
            if (info.channels > 0)
            {
                string channelInfo = info.channels switch
                {
                    1 => IsChineseMode ? "灰度" : "grayscale",
                    3 => "RGB",
                    4 => "RGBA",
                    _ => IsChineseMode ? $"{info.channels}通道" : $"{info.channels}Channel"
                };
                //ResolutionText.ToolTip = IsChineseMode ? $"色彩模式: {channelInfo}\n分辨率: {bitmapSource.DpiX:F0} DPI" : $"Color mode: {channelInfo}\nResolution: {bitmapSource.DpiX:F0} DPI";
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
            var currentCollection = GetCurrentActiveCollection(); // 获取当前视图的集合
            if (currentCollection.Any() && _currentImageIndex > 0)
            {
                MainImageDataGrid.SelectedIndex = _currentImageIndex - 1;
            }
        }

        private void NextImage_Click(object sender, RoutedEventArgs e)
        {
            var currentCollection = GetCurrentActiveCollection(); // 获取当前视图的集合
            if (currentCollection.Any() && _currentImageIndex < currentCollection.Count - 1)
            {
                MainImageDataGrid.SelectedIndex = _currentImageIndex + 1;
            }
        }
        #endregion
    }
}
