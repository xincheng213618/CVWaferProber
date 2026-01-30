using ColorVision.Core.Entities;
using CVCommCore;
using CVCommCore.CVImage;
using CVDB.Services.Algorithm;
using CVDB.Services.Image;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.ViewModels;
using CVWPFCamImageCtrl;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;

namespace CVWaferProber.Services
{

    public class AOIService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AOIService));

        public CVCamImagerViewModel CustomImageVM { get; private set; }

        public AOIService(CVCamImagerViewModel customImageVM, RCRestService rcService) : base(rcService)
        {
            this.CustomImageVM = customImageVM;
        }

        public AOIService(RCRestService rcService) : this(new CVCamImagerViewModel(), rcService)
        {
        }

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return GetDieResultStatus(serialNumber);
        }

        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            Task.Run(() =>
            {
                var results = AlgResultService.LoadAlgResultByBatchCode(dieViewModel.SerialNumber!);
                if (results != null && results.Count > 0)
                {
                    foreach (var result in results)
                    {
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

            // 加载图像结果
            LoadImageResult(dieViewModel.chipViewModel!.ChipData, dieViewModel.SerialNumber!);
            return ChipStatus.OK;
        }

        public override Task StartTesting(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool hasNext)
        {
            dieViewModel.ChangeStatus(ChipStatus.TESTING);
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                CustomImageVM?.ClearImageResult();
            });
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel, hasNext);
            return task;
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
                FlowResultDisplay(dieViewModel);
            }
        }

        /// <summary>
        /// 核心：加载AOI测试结果图片
        /// 从两个数据库表分别获取Analysis Image和Camera Measurement
        /// </summary>
        private void LoadImageResult(ChipData? chipData, string serialNumber)
        {
            if (string.IsNullOrEmpty(serialNumber) || CustomImageVM == null)
            {
                logger.Warn("LoadImageResult：序列号为空或CustomImageVM未初始化");
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    // 1. 加载Analysis Image（从TScgdAlgorithmResultDetailPoiCieFile获取）
                    LoadAnalysisImages(serialNumber);

                    // 2. 加载Camera Measurement（从VScgdMeasureResultImg获取）
                    LoadCameraMeasurements(serialNumber);

                    // 3. 加载POI分析数据
                    LoadPoiAnalysisData(serialNumber, chipData);

                    logger.Info($"序列号{serialNumber}图片加载完成");
                }
                catch (Exception ex)
                {
                    logger.Error($"加载序列号{serialNumber}图片失败", ex);
                }
            });
        }

        /// <summary>
        /// 加载Analysis Image（分析后图像）- 从TScgdAlgorithmResultDetailPoiCieFile获取
        /// 过滤po.dat类型的图片
        /// </summary>
        private void LoadAnalysisImages(string batchCode)
        {
            try
            {
                logger.Info($"开始加载批次{batchCode}的Analysis Image");

                // 首先获取算法主结果
                var algResults = AlgResultService.LoadAlgResultByBatchCode(batchCode);
                if (algResults == null || algResults.Count == 0)
                {
                    logger.Warn($"批次{batchCode}未找到算法主结果");
                    return;
                }

                int imageId = 1;

                foreach (var masterResult in algResults)
                {
                    if (masterResult == null) continue;

                    // 通过主结果ID获取POI CIE文件详情
                    var poiDetails = AlgResultService.GetPOIDetailResultFileByPid(masterResult.Id);
                    if (poiDetails == null || poiDetails.Count == 0) continue;

                    foreach (var poiDetail in poiDetails)
                    {
                        if (string.IsNullOrEmpty(poiDetail.FileUrl)) continue;

                        string filePath = poiDetail.FileUrl;

                        // 过滤po.dat文件
                        string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
                        if (fileName.Equals("po") || fileName.Equals("po.dat"))
                        {
                            logger.Debug($"过滤po.dat文件: {Path.GetFileName(filePath)}");
                            continue;
                        }

                        // 检查文件是否存在
                        if (!File.Exists(filePath))
                        {
                            logger.Warn($"Analysis Image文件不存在: {filePath}");
                            continue;
                        }

                        // 添加到ViewModel
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var imageItem = new ImageItem(imageId++)
                            {
                                FileName = Path.GetFileName(filePath),
                                ImagePath = filePath,
                                FileSizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0),
                                Status = "Ready"
                            };

                            // Analysis Image添加到ProcessedImageResults集合
                            CustomImageVM?.AddImage(imageItem);
                        });

                        logger.Debug($"已添加Analysis Image: {Path.GetFileName(filePath)}");
                    }
                }

                logger.Info($"批次{batchCode}的Analysis Image加载完成，共处理{algResults.Count}个主结果");
            }
            catch (Exception ex)
            {
                logger.Error($"加载批次{batchCode}的Analysis Image失败", ex);
            }
        }

        /// <summary>
        /// 加载Camera Measurement（原始图像）- 从VScgdMeasureResultImg获取
        /// </summary>
        private void LoadCameraMeasurements(string batchCode)
        {
            try
            {
                logger.Info($"开始加载批次{batchCode}的Camera Measurement");

                // 从ImageResultService获取相机测量结果
                var cameraResults = ImageResultService.LoadResultByBatchCode(batchCode);

                if (cameraResults == null || cameraResults.Count == 0)
                {
                    logger.Warn($"批次{batchCode}未找到Camera Measurement数据");
                    return;
                }

                int imageId = 1000; // 使用不同的ID范围区分
                int addedCount = 0;

                foreach (var result in cameraResults)
                {
                    if (result == null) continue;

                    string filePath = result.FileUrl;

                    if (string.IsNullOrEmpty(filePath))
                    {
                        logger.Warn($"Camera Measurement文件路径为空，FileType={result.FileType}");
                        continue;
                    }

                    if (!File.Exists(filePath))
                    {
                        logger.Warn($"Camera Measurement文件不存在: {filePath}");
                        continue;
                    }

                    // 检查文件扩展名，确保是图像文件
                    string fileExt = Path.GetExtension(filePath).ToLower();
                    if (!IsSupportedImageExtension(fileExt))
                    {
                        logger.Debug($"跳过非图像文件: {filePath}");
                        continue;
                    }

                    // 添加到ViewModel
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var imageItem = new ImageItem(imageId++)
                        {
                            FileName = Path.GetFileName(filePath),
                            ImagePath = filePath,
                            FileSizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0),
                            Status = "Ready"
                        };

                        // Camera Measurement添加到OriginalImageResults集合
                        CustomImageVM?.AddOriginalImageOnly(imageItem);
                    });

                    addedCount++;
                    logger.Debug($"已添加Camera Measurement: {Path.GetFileName(filePath)} (FileType: {result.FileType})");
                }

                logger.Info($"批次{batchCode}的Camera Measurement加载完成，共添加{addedCount}个文件");
            }
            catch (Exception ex)
            {
                logger.Error($"加载批次{batchCode}的Camera Measurement失败", ex);
            }
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
                        logger.Warn($"PoiAnalysis分析文件不存在：{detailFile?.ResultFileName}");
                        continue;
                    }

                    string poiAnalysisJson = File.ReadAllText(detailFile.ResultFileName);
                    var poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(poiAnalysisJson);
                    if (poiAnalysis != null && poiAnalysis.result != null)
                    {
                        chipData.DataValue = poiAnalysis.result.Value;
                        brightnessText = $"[{chipData.Row},{chipData.Column}]={chipData.DataValue:F4}";

                        // 如果有POI标记，也需要加载
                        LoadPoiMarkers(masterResult.Id);
                    }
                }

                // 如果需要显示亮度均匀性文本，可以在这里处理
                if (!string.IsNullOrEmpty(brightnessText))
                {
                    logger.Info($"POI分析数据: {brightnessText}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"加载批次{batchCode}的POI分析数据失败", ex);
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

                    if (poi.PoiType == 0) // 圆形标记
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
                    else if (poi.PoiType == 1) // 矩形标记
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

                // 如果有POI标记，可以在这里更新UI
                if (poiMarkers.Count > 0)
                {
                    logger.Debug($"加载了{poiMarkers.Count}个POI标记");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"加载POI标记失败", ex);
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
