using CVCommCore;
using CVDB.Services.Algorithm;
using CVDB.Services.Image;
using CVWaferProber.Config;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using CVWPFCamImageCtrl;
using log4net;
using Newtonsoft.Json;
using System.IO;
using Application = System.Windows.Application;

namespace CVWaferProber.Services
{
    public class AOIService : BaseSerivce
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(AOIService));

        public CVCamImagerViewModel CustomImageVM { get; private set; }

        public AOIService(CVCamImagerViewModel customImageVM, RCRestService rcService, MQTTService mqttService) : base(rcService, mqttService)
        {
            this.CustomImageVM = customImageVM;
        }

        public AOIService(RCRestService rcService, MQTTService mqttService) : this(new CVCamImagerViewModel(), rcService, mqttService)
        {
        }

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return GetDieResultStatus(serialNumber);
        }

        protected override async Task<ChipStatus> FlowResultDisplayAsync(DieViewModel dieViewModel)
        {
            // 移除外层批量Task.Run，避免所有逻辑阻塞后一次性更新
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
                                // 仅将文件读取放入异步，避免阻塞UI
                                string darkResultJson = await Task.Run(() => File.ReadAllText(detailResult.ResultFileName));
                                var darkResult = JsonConvert.DeserializeObject<DarkResultDto>(darkResultJson);

                                // 仅包裹纯UI更新代码，最小化Dispatcher阻塞
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
            // 关键：根据Die的AOI完成标记，判断是否一次性加载（已完成则true，未完成则false）
            bool isLoadAllAtOnce = dieViewModel.IsAOITestCompleted;
            if (dieViewModel.chipViewModel?.ChipData != null)
            {
                // 传递一次性加载开关给图片加载方法
                await LoadImageResultAsync(dieViewModel.chipViewModel.ChipData, dieViewModel.SerialNumber!, isLoadAllAtOnce);
            }
            // 首次测试完成后，标记该Die为AOI测试完成（后续切换则一次性加载）
            if (!dieViewModel.IsAOITestCompleted)
            {
                dieViewModel.IsAOITestCompleted = true;
                logger.InfoFormat("Die[{0}/{1}]AOI测试完成，标记为已完成，后续切换将一次性加载图片", dieViewModel.MapX, dieViewModel.MapY);
            }

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

        public override async Task StartTestingAsync(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool hasNext, bool tranStatus = true)
        {
            dieViewModel.ChangeStatus(ChipStatus.TESTING);
            ClearResult();
            await RunFlowAsync(_selectedWPFlow, dieViewModel, hasNext, tranStatus);
        }

        private void ClearResult()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                CustomImageVM?.ClearImageResult();
            });
        }

        // 修复原代码笔误：if (results != null) → if (aoi != null)，避免状态判断错误
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
                        // 原代码错误：判断了外层的results，改为判断当前的aoi
                        if (aoi != null && aoi.Count == 1)
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

            CustomImageVM?.ClearImageResult();
            // 异步执行，不阻塞UI线程
            _ = FlowResultDisplayAsync(dieViewModel);
        }

        /// <summary>
        /// 核心：异步逐张加载图像结果（改造后真正的异步方法，无批量Task.Run）
        /// </summary>
        private async Task LoadImageResultAsync(ChipData? chipData, string serialNumber, bool isLoadAllAtOnce = false)
        {
            if (string.IsNullOrEmpty(serialNumber) || CustomImageVM == null)
            {
                logger.Warn("LoadImageResult：Serial number is empty or CustomImageVM is not initialized");
                return;
            }
            try
            {
                // 传递一次性加载开关给子方法
                await LoadAnalysisImagesAsync(serialNumber, isLoadAllAtOnce);
                await LoadCameraMeasurementsAsync(serialNumber, isLoadAllAtOnce);
                await Task.Run(() => LoadPoiAnalysisData(serialNumber, chipData));

                logger.Info($"Serial number {serialNumber} image loading completed");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to load the image with serial number {serialNumber}", ex);
            }
        }

        /// <summary>
        /// 异步逐张加载Analysis Image（解决imageId报错，逐张更新UI）
        /// </summary>
        private async Task LoadAnalysisImagesAsync(string batchCode, bool isLoadAllAtOnce)
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

                int imageId = 1;
                foreach (var masterResult in algResults)
                {
                    if (masterResult == null) continue;

                    // 处理ImgFileType=46的直接图片
                    if (masterResult.ImgFileType == 46)
                    {
                        // 逐张处理：返回新的imageId，替代ref参数，解决语法报错
                        imageId = await ProcessSingleAnalysisImageAsync(imageId, masterResult.ImgResult);
                        //让出线程，给UI渲染时间（核心逐张显示逻辑）
                        if (!isLoadAllAtOnce) await Task.Delay(500);
                        continue;
                    }

                    // 处理POI CIE文件详情
                    var poiDetails = AlgResultService.GetPOIDetailResultFileByPid(masterResult.Id);
                    if (poiDetails == null || poiDetails.Count == 0) continue;

                    foreach (var poiDetail in poiDetails)
                    {
                        if (string.IsNullOrEmpty(poiDetail.FileUrl)) continue;
                        // 逐张处理，更新imageId
                        imageId = await ProcessSingleAnalysisImageAsync(imageId, poiDetail.FileUrl);
                        // 关键分支：仅未完成测试时，保留逐张延迟；已完成则跳过
                        if (!isLoadAllAtOnce) await Task.Delay(500);
                    }
                }

                logger.Info($"Batch {batchCode} analysis image loading completed, all images displayed one by one.");
            }
            catch (Exception ex)
            {
                logger.Error($"Loading batch {batchCode} Analysis Image failed", ex);
            }
        }

        /// <summary>
        /// 处理单张Analysis Image：校验+UI更新，返回递增后的imageId（替代ref，解决报错）
        /// </summary>
        private async Task<int> ProcessSingleAnalysisImageAsync(int currentImageId, string filePath)
        {
            // 异步校验文件存在性，不阻塞UI
            bool fileExists = await Task.Run(() => File.Exists(filePath));
            if (!fileExists)
            {
                logger.Warn($"Analysis Image File does not exist: {filePath}");
                return currentImageId; // 未加载，返回原ID
            }

            // 仅纯UI更新，立即渲染单张图片
            Application.Current.Dispatcher.Invoke(() =>
            {
                AddImageToDataGrid(currentImageId, filePath);
            });

            logger.Debug($"added Analysis Image (one by one): {Path.GetFileName(filePath)}");
            return currentImageId + 1; // 加载成功，返回递增后的ID
        }

        private void AddImageToDataGrid(int id, string filePath)
        {
            var imageItem = new ImageItem(id)
            {
                FileName = Path.GetFileName(filePath),
                ImagePath = filePath,
                FileSizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0),
                Status = "Ready"
            };
            CustomImageVM?.AddImage(imageItem);
        }

        /// <summary>
        /// 异步逐张加载Camera Measurement（解决imageId报错，逐张更新UI）
        /// </summary>
        private async Task LoadCameraMeasurementsAsync(string batchCode,bool isLoadAllAtOnce)
        {
            try
            {
                logger.Info($"Start loading Camera Measurement for batch {batchCode}");
                var cameraResults = ImageResultService.LoadResultByBatchCode(batchCode);

                if (cameraResults == null || cameraResults.Count == 0)
                {
                    logger.Warn($"Batch {batchCode} Camera Measurement result not found");
                    return;
                }

                int imageId = 1000;
                int addedCount = 0;

                foreach (var result in cameraResults)
                {
                    if (result == null || string.IsNullOrEmpty(result.FileUrl))
                    {
                        logger.Warn($"Camera Measurement file path is empty，FileType={result?.FileType}");
                        continue;
                    }

                    // 逐张处理，返回新的imageId，替代ref参数
                    var (isAdded, newImageId) = await ProcessSingleCameraImageAsync(imageId, result.FileUrl);
                    imageId = newImageId; // 更新ID
                    if (isAdded) addedCount++;

                    // 关键分支：仅未完成测试时，保留逐张延迟；已完成则跳过
                    if (!isLoadAllAtOnce) await Task.Delay(500);
                }

                logger.Info($"Batch {batchCode} Camera Measurement loaded successfully, {addedCount} files added one by one in total");
            }
            catch (Exception ex)
            {
                logger.Error($"Loading batch {batchCode} Camera Measurement failed", ex);
            }
        }

        /// <summary>
        /// 处理单张Camera Measurement：校验+UI更新，返回(是否成功, 新的imageId)（替代ref，解决报错）
        /// </summary>
        private async Task<(bool IsAdded, int NewImageId)> ProcessSingleCameraImageAsync(int currentImageId, string filePath)
        {
            // 异步校验文件+扩展名，不阻塞UI
            var (isValid, _) = await Task.Run(() =>
            {
                if (!File.Exists(filePath)) return (false, $"file not exist: {filePath}");
                var ext = Path.GetExtension(filePath).ToLower();
                if (!IsSupportedImageExtension(ext)) return (false, $"non-image file: {filePath}");
                return (true, string.Empty);
            });

            if (!isValid)
            {
                return (false, currentImageId); // 未加载，返回原ID
            }

            // 仅纯UI更新，立即渲染单张图片
            Application.Current.Dispatcher.Invoke(() =>
            {
                var imageItem = new ImageItem(currentImageId)
                {
                    FileName = Path.GetFileName(filePath),
                    ImagePath = filePath,
                    FileSizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0),
                    Status = "Ready"
                };
                CustomImageVM?.AddOriginalImageOnly(imageItem);
            });

            logger.Debug($"Added Camera Measurement (one by one): {Path.GetFileName(filePath)}");
            return (true, currentImageId + 1); // 加载成功，返回递增后的ID
        }

        /// <summary>
        /// 加载POI分析数据（原逻辑不变，无UI更新，无需逐张处理）
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
        /// 加载POI标记（原逻辑不变）
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

        public override void AutoExportData()
        {
            // 实现自动导出数据逻辑
        }

        // DTO类保持不变
        public class DetailResult_CommFile_V2
        {
            public string ResultFileName { get; set; }
        }

        public class DarkResultDto
        {
            public string GradeLevel { get; set; }
        }
    }
}