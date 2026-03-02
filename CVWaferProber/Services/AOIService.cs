using CVCommCore;
using CVDB.Services.Algorithm;
using CVDB.Services.Image;
using CVWaferProber.Config;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using CVWPFCamImageCtrl;
using CVWPFSpectrometerCtrl.ViewModels;
using log4net;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application = System.Windows.Application;

namespace CVWaferProber.Services
{
    public class AOIService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AOIService));
        // 标记当前是否已经完成测试（完成后切换Die一律批量加载）
        private bool _testCompletedForCurrentDie;
        private DieViewModel _currentDieVM;
        public CVCamImagerViewModel CustomImageVM { get; private set; }
        public CVSpectrumViewModel CustomIVLVM { get; private set; }
        public AOIService(MainViewModel mainVM, IFlowService flowService) : base(mainVM, flowService)
        {
            this.CustomImageVM = mainVM.CustomImageVM;
            this.CustomIVLVM = mainVM.CustomIVLVM;
        }

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return GetDieResultStatus(serialNumber);
        }

        protected override async Task<ChipStatus> FlowResultDisplayAsync(DieViewModel dieViewModel)
        {
            await Task.Run(() =>
            {
                var results = AlgResultService.LoadAlgResultByBatchCode(dieViewModel.SerialNumber!);
                if (results != null && results.Count > 0)
                {
                    foreach (var result in results)
                    {
                        AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;
                        var aoiDetails = AlgResultService.GetCommDetailResult(result.Id);
                        if (aoiDetails != null && aoiDetails.Count == 1)
                        {
                            var resultJson = aoiDetails[0].Result;
                            if (!string.IsNullOrEmpty(resultJson))
                            {
                                var detailResult = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(resultJson);
                                if (detailResult != null && !string.IsNullOrEmpty(detailResult.ResultFileName) && File.Exists(detailResult.ResultFileName))
                                {
                                    string darkResultJson = File.ReadAllText(detailResult.ResultFileName);
                                    var darkResult = JsonConvert.DeserializeObject<DarkResultDto>(darkResultJson);

                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        dieViewModel.AOIGradeLevel = darkResult?.GradeLevel ?? "na";
                                        dieViewModel.BlackPattern = darkResult?.GradeLevel ?? "na";
                                    });
                                }
                            }
                            break;
                        }
                    }
                }
            });

            // 加载图像结果（改为await，确保异步执行）
            await LoadImageResultAsync(dieViewModel.chipViewModel!.ChipData, dieViewModel.SerialNumber!);
            // 新增：标记当前Die测试完成
            _testCompletedForCurrentDie = true;
            return ChipStatus.OK;
        }

        /// <summary>
        /// 获取导出目录
        /// </summary>
        private string GetExportDirectory()
        {
            string baseDirectory = ConfigManager.Config.ExportPathSettings.AoiExportPath;
            string dateDirectory = DateTime.Now.ToString("yyyyMMdd");
            string fullPath = Path.Combine(baseDirectory, dateDirectory);

            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            return fullPath;
        }

        public override async Task StartTestingAsync(DieViewModel dieViewModel, WPFlowViewModel selectedWPFlow, bool hasNext, bool tranStatus = true)
        {
            // 新增：开始测试时标记未完成
            _testCompletedForCurrentDie = false;

            dieViewModel.ChangeStatus(ChipStatus.TESTING);
            ClearResult();
            await RunFlowAsync(selectedWPFlow, dieViewModel, hasNext, tranStatus);
        }

        private void ClearResult()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                CustomImageVM?.ClearImageResult();
            });
        }

        private ChipStatus GetDieResultStatus(string serialNumber)
        {
            ChipStatus status = ChipStatus.FAILED;
            var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
            if (results != null && results.Count > 0)
            {
                foreach (var result in results)
                {
                    if (result.ResultCode.HasValue && result.ResultCode.Value != 0)
                    {
                        var aoi = AlgResultService.GetCommDetailResult(result.Id);
                        if (results != null && results.Count == 1)
                        {
                            OLED_AOI_Result_E eResult = JsonConvert.DeserializeObject<OLED_AOI_Result_E>(aoi[0].Result);
                            status = ChipStatusTool.GetStatusFromErrCode(eResult.ResultCode);
                            if (logger.IsInfoEnabled) logger.InfoFormat("AOI Result => {0}", status.ToString());
                            break;
                        }
                    }
                }
            }
            return status;
        }

        public override void ResultDisplay(DieViewModel dieViewModel)
        {
            if (string.IsNullOrEmpty(dieViewModel.SerialNumber))
            {
                CustomImageVM?.ClearImageResult();
                return;
            }
            else
            {
                CustomImageVM?.ClearImageResult();
                // 改为异步执行
                _ = FlowResultDisplayAsync(dieViewModel);
            }
        }

        /// <summary>
        /// 核心：加载AOI测试结果图片
        /// 实时预览模式：逐张加载+500ms间隔；非实时模式：批量加载
        /// </summary>
        private async Task LoadImageResultAsync(ChipData? chipData, string serialNumber)
        {
            if (string.IsNullOrEmpty(serialNumber) || CustomImageVM == null)
            {
                logger.Warn("LoadImageResult：Serial number is empty or CustomImageVM is not initialized");
                return;
            }

            try
            {
                // 判断是否开启实时预览
                if (CustomImageVM.IsRealTimePreviewEnabled && !_testCompletedForCurrentDie)
                {
                    logger.Info($"Real-time preview enabled, loading images one by one during test for {serialNumber}");
                    // 实时模式：逐张加载Analysis Image（带500ms间隔）
                    await LoadAnalysisImagesRealTimeAsync(serialNumber);
                    // 实时模式：逐张加载Camera Measurement（带500ms间隔）
                    await LoadCameraMeasurementsRealTimeAsync(serialNumber);
                }
                else
                {
                    // 非实时模式：原有批量加载逻辑
                    await LoadAnalysisImagesAsync(serialNumber);
                    await LoadCameraMeasurementsAsync(serialNumber);
                }

                // 加载POI分析数据（通用逻辑）
                await Task.Run(() => LoadPoiAnalysisData(serialNumber, chipData));

                logger.Info($"Serial number {serialNumber} image loading completed");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to load the image with serial number {serialNumber}", ex);
            }
        }

        /// <summary>
        /// 测试过程中实时加载单张Analysis Image
        /// </summary>
        /// <param name="filePath">图片路径</param>
        public async Task LoadSingleAnalysisImageAsync(string filePath)
        {
            if (CustomImageVM == null || string.IsNullOrEmpty(filePath))
            {
                logger.Warn("CustomImageVM is null or filePath is empty");
                return;
            }

            await CustomImageVM.AddSingleAnalysisImageAsync(filePath, 500);
        }

        /// <summary>
        /// 测试过程中实时加载单张Camera Measurement
        /// </summary>
        /// <param name="filePath">图片路径</param>
        public async Task LoadSingleCameraImageAsync(string filePath)
        {
            if (CustomImageVM == null || string.IsNullOrEmpty(filePath))
            {
                logger.Warn("CustomImageVM is null or filePath is empty");
                return;
            }

            await CustomImageVM.AddSingleCameraImageAsync(filePath, 500);
        }

        #region 批量加载逻辑（非实时模式）
        /// <summary>
        /// 批量加载Analysis Image（非实时模式）
        /// </summary>
        private async Task LoadAnalysisImagesAsync(string batchCode)
        {
            try
            {
                logger.Info($"Start loading Analysis Image for batch {batchCode}");

                var algResults = AlgResultService.LoadAlgResultByBatchCode(batchCode);
                if (algResults == null || algResults.Count == 0)
                {
                    logger.Warn($"Batch {batchCode} algorithm main result not found");
                    return;
                }

                //int imageId = 1;

                foreach (var masterResult in algResults)
                {
                    if (masterResult == null) continue;

                    if (masterResult.ImgFileType == 46)
                    {
                        string filePath = masterResult.ImgResult;
                        if (!File.Exists(filePath))
                        {
                            logger.Warn($"Analysis Image File does not exist: {filePath}");
                            continue;
                        }
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddImageToDataGrid(filePath);
                        });
                        continue;
                    }

                    var poiDetails = AlgResultService.GetPOIDetailResultFileByPid(masterResult.Id);
                    if (poiDetails == null || poiDetails.Count == 0) continue;

                    foreach (var poiDetail in poiDetails)
                    {
                        if (string.IsNullOrEmpty(poiDetail.FileUrl)) continue;

                        string filePath = poiDetail.FileUrl;
                        if (!File.Exists(filePath))
                        {
                            logger.Warn($"Analysis Image File does not exist: {filePath}");
                            continue;
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddImageToDataGrid(filePath);
                        });

                        logger.Debug($"added Analysis Image: {Path.GetFileName(filePath)}");
                    }
                }

                logger.Info($"Batch {batchCode} analysis image loading completed, totaling {algResults.Count} results processed.");
            }
            catch (Exception ex)
            {
                logger.Error($"Loading batch {batchCode} Analysis Image failed", ex);
            }
        }

        /// <summary>
        /// 批量加载Camera Measurement（非实时模式）
        /// </summary>
        private async Task LoadCameraMeasurementsAsync(string batchCode)
        {
            try
            {
                logger.Info($"Start loading Camera Measurement for batch {batchCode}");

                var cameraResults = ImageResultService.LoadResultByBatchCode(batchCode);

                if (cameraResults == null || cameraResults.Count == 0)
                {
                    logger.Warn($"Start loading Camera Measurement for batch {batchCode}");
                    return;
                }

             
                int addedCount = 0;

                foreach (var result in cameraResults)
                {
                    if (result == null) continue;

                    string filePath = result.FileUrl;

                    if (string.IsNullOrEmpty(filePath))
                    {
                        logger.Warn($"Camera Measurement file path is empty，FileType={result.FileType}");
                        continue;
                    }

                    if (!File.Exists(filePath))
                    {
                        logger.Warn($"Camera Measurement File does not exist: {filePath}");
                        continue;
                    }

                    string fileExt = Path.GetExtension(filePath).ToLower();
                    if (!IsSupportedImageExtension(fileExt))
                    {
                        logger.Debug($"Skip non-image files: {filePath}");
                        continue;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddOriginalImageToDataGrid(filePath);
                    });

                    addedCount++;
                    logger.Debug($"Added Camera Measurement: {Path.GetFileName(filePath)} (FileType: {result.FileType})");
                }

                logger.Info($"Batch {batchCode} Camera Measurement loaded successfully, {addedCount} files added in total");
            }
            catch (Exception ex)
            {
                logger.Error($"Loading batch {batchCode} Camera Measurement failed", ex);
            }
        }
        #endregion

        #region 实时加载逻辑（逐张+500ms间隔）
        /// <summary>
        /// 实时加载Analysis Image（逐张+500ms间隔）
        /// </summary>
        private async Task LoadAnalysisImagesRealTimeAsync(string batchCode)
        {
            try
            {
                logger.Info($"Start real-time loading Analysis Image for batch {batchCode}");

                var algResults = AlgResultService.LoadAlgResultByBatchCode(batchCode);
                if (algResults == null || algResults.Count == 0)
                {
                    logger.Warn($"Batch {batchCode} algorithm main result not found");
                    return;
                }

                foreach (var masterResult in algResults)
                {
                    if (masterResult == null) continue;

                    // 处理ImgFileType=46的情况
                    if (masterResult.ImgFileType == 46)
                    {
                        string filePath = masterResult.ImgResult;
                        if (File.Exists(filePath))
                        {
                            // 实时加载单张图片，间隔500ms
                            await LoadSingleAnalysisImageAsync(filePath);
                        }
                        else
                        {
                            logger.Warn($"Analysis Image File does not exist: {filePath}");
                        }
                        continue;
                    }

                    // 处理POI详情
                    var poiDetails = AlgResultService.GetPOIDetailResultFileByPid(masterResult.Id);
                    if (poiDetails == null || poiDetails.Count == 0) continue;

                    foreach (var poiDetail in poiDetails)
                    {
                        if (string.IsNullOrEmpty(poiDetail.FileUrl)) continue;

                        string filePath = poiDetail.FileUrl;
                        if (File.Exists(filePath))
                        {
                            // 实时加载单张图片，间隔500ms
                            await LoadSingleAnalysisImageAsync(filePath);
                            logger.Debug($"Real-time added Analysis Image: {Path.GetFileName(filePath)}");
                        }
                        else
                        {
                            logger.Warn($"Analysis Image File does not exist: {filePath}");
                        }
                    }
                }

                logger.Info($"Batch {batchCode} real-time analysis image loading completed");
            }
            catch (Exception ex)
            {
                logger.Error($"Real-time loading batch {batchCode} Analysis Image failed", ex);
            }
        }

        /// <summary>
        /// 实时加载Camera Measurement（逐张+500ms间隔）
        /// </summary>
        private async Task LoadCameraMeasurementsRealTimeAsync(string batchCode)
        {
            try
            {
                logger.Info($"Start real-time loading Camera Measurement for batch {batchCode}");

                var cameraResults = ImageResultService.LoadResultByBatchCode(batchCode);

                if (cameraResults == null || cameraResults.Count == 0)
                {
                    logger.Warn($"No Camera Measurement found for batch {batchCode}");
                    return;
                }

                int addedCount = 0;

                foreach (var result in cameraResults)
                {
                    if (result == null) continue;

                    string filePath = result.FileUrl;

                    if (string.IsNullOrEmpty(filePath))
                    {
                        logger.Warn($"Camera Measurement file path is empty，FileType={result.FileType}");
                        continue;
                    }

                    if (!File.Exists(filePath))
                    {
                        logger.Warn($"Camera Measurement File does not exist: {filePath}");
                        continue;
                    }

                    string fileExt = Path.GetExtension(filePath).ToLower();
                    if (!IsSupportedImageExtension(fileExt))
                    {
                        logger.Debug($"Skip non-image files: {filePath}");
                        continue;
                    }

                    // 实时加载单张图片，间隔500ms
                    await LoadSingleCameraImageAsync(filePath);
                    addedCount++;
                    logger.Debug($"Real-time added Camera Measurement: {Path.GetFileName(filePath)} (FileType: {result.FileType})");
                }

                logger.Info($"Batch {batchCode} real-time Camera Measurement loaded successfully, {addedCount} files added in total");
            }
            catch (Exception ex)
            {
                logger.Error($"Real-time loading batch {batchCode} Camera Measurement failed", ex);
            }
        }
        #endregion

        private void AddImageToDataGrid(string filePath)
        {
            // 调用ViewModel的ID生成器获取唯一ID
            int imageId = CustomImageVM.GetNextImageId();

            var imageItem = new ImageItem(imageId)
            {
                FileName = Path.GetFileName(filePath),
                ImagePath = filePath,
                FileSizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0),
                Status = "Ready"
            };

            CustomImageVM?.AddImage(imageItem);
        }
        // 【新增方法】批量添加原始图片时使用统一ID
        private void AddOriginalImageToDataGrid(string filePath)
        {
            int imageId = CustomImageVM.GetNextImageId();

            var imageItem = new ImageItem(imageId)
            {
                FileName = Path.GetFileName(filePath),
                ImagePath = filePath,
                FileSizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0),
                Status = "Ready"
            };

            CustomImageVM?.AddOriginalImageOnly(imageItem);
        }

        /// <summary>
        /// 加载POI分析数据
        /// </summary>
        private void LoadPoiAnalysisData(string batchCode, ChipData? chipData)
        {
            try
            {
                var algResults = AlgResultService.LoadAlgResultByBatchCode(batchCode);
                if (algResults == null || algResults.Count == 0) return;

                string? brightnessText = null;

                foreach (var masterResult in algResults)
                {
                    if (masterResult == null) continue;

                    AlgorithmResultType resultType = (AlgorithmResultType)masterResult.ImgFileType;
                    if (resultType != AlgorithmResultType.PoiAnalysis || chipData == null) continue;

                    var commDetails = AlgResultService.GetCommDetailResult(masterResult.Id);
                    if (commDetails == null || commDetails.Count == 0) continue;

                    var commDetail = commDetails[0];
                    if (string.IsNullOrEmpty(commDetail.Result)) continue;

                    var detailFile = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(commDetail.Result);
                    if (detailFile == null || string.IsNullOrEmpty(detailFile.ResultFileName) ||
                        !File.Exists(detailFile.ResultFileName))
                    {
                        logger.Warn($"PoiAnalysis analysis file does not exist：{detailFile?.ResultFileName}");
                        continue;
                    }

                    string poiAnalysisJson = File.ReadAllText(detailFile.ResultFileName);
                    var poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(poiAnalysisJson);
                    if (poiAnalysis != null && poiAnalysis.result != null)
                    {
                        chipData.DataValue = poiAnalysis.result.Value;
                        brightnessText = $"[{chipData.Row},{chipData.Column}]={chipData.DataValue:F4}";

                        LoadPoiMarkers(masterResult.Id);
                    }
                }

                if (!string.IsNullOrEmpty(brightnessText))
                {
                    logger.Info($"POI data analysis: {brightnessText}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to load POI analysis data for batch {batchCode}", ex);
            }
        }

        /// <summary>
        /// 加载POI标记
        /// </summary>
        private void LoadPoiMarkers(int masterResultId)
        {
            try
            {
                var poiDetails = AlgResultService.GetPOIDetailResult(masterResultId);
                if (poiDetails == null || poiDetails.Count == 0) return;

                List<POIMarker> poiMarkers = new List<POIMarker>();

                foreach (var poi in poiDetails)
                {
                    if (poi.PoiX == null || poi.PoiY == null ||
                        poi.PoiWidth == null || poi.PoiHeight == null) continue;

                    if (poi.PoiType == 0)
                    {
                        poiMarkers.Add(new CircleMarker
                        {
                            Label = poi.PoiName,
                            X = (double)poi.PoiX,
                            Y = (double)poi.PoiY,
                            Width = (double)poi.PoiWidth,
                            Height = (double)poi.PoiHeight,
                            Fill = null
                        });
                    }
                    else if (poi.PoiType == 1)
                    {
                        poiMarkers.Add(new RectangleMarker
                        {
                            Label = poi.PoiName,
                            X = (double)poi.PoiX,
                            Y = (double)poi.PoiY,
                            Width = (double)poi.PoiWidth,
                            Height = (double)poi.PoiHeight,
                            Fill = null
                        });
                    }
                }

                if (poiMarkers.Count > 0)
                {
                    logger.Debug($"Loaded {poiMarkers.Count} POI markers");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to load POI markers", ex);
            }
        }

        /// <summary>
        /// 检查是否是支持的图像扩展名
        /// </summary>
        private bool IsSupportedImageExtension(string extension)
        {
            string[] supportedExtensions = {
                ".tif", ".tiff", ".jpg", ".jpeg", ".png", ".bmp", ".gif",
                ".webp", ".ico", ".exif", ".cvraw", ".cvcie"
            };

            return supportedExtensions.Contains(extension.ToLower());
        }

        public override void AutoExportData(DieViewModel dieViewModel)
        {
            string serialNumber = dieViewModel.SerialNumber ?? "Unknown";
            CustomIVLVM.LoadSpectrumData(dieViewModel.SerialNumber);

            //// 实现自动导出数据逻辑
            var Measurements = CustomIVLVM.Measurements;
            var Wavelengths = CustomIVLVM.Wavelengths;
            if (Measurements == null || !Measurements.Any() || Wavelengths == null || Wavelengths.Length == 0)
            {
                logger.Info("No valid IVL data available for export");
                return;
            }


            // 读取全局配置的IVL导出路径（核心修改点2）
            string ivlRootPath = ConfigManager.Config.ExportPathSettings?.IvlExportPath ?? @"D:\Project\IVL";
            // 确保目录存在
            if (!Directory.Exists(ivlRootPath))
            {
                Directory.CreateDirectory(ivlRootPath);
                logger.Info($"Create IVL export directory：{ivlRootPath}");
            }
            // 构造文件名（包含SerialNumber+时间戳）
            string fileName = $"IVL_Data_{serialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string fullExportPath = Path.Combine(ivlRootPath, fileName);
            // 执行导出
            CustomIVLVM.ExportToCsv(fullExportPath, Measurements, Wavelengths);
            logger.Info($"IVL data exported to：{fullExportPath}");
        }

        // DTO类
        public class DetailResult_CommFile_V2
        {
            public string ResultFileName { get; set; } = string.Empty;
        }

        public class DarkResultDto
        {
            public string GradeLevel { get; set; } = string.Empty;
        }
    }
}