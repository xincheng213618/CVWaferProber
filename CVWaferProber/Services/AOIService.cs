using ColorVision.Core.Entities;
using CVCommCore;
using CVCommCore.CVImage;
using CVDB.Services.Algorithm;
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

        private readonly AOICSVExportService _csvExportService;

        private readonly GlobalConfigModel _globalConfig;
        public CVCamImagerViewModel CustomImageVM { get; private set; }

        public AOIService(CVCamImagerViewModel customImageVM, RCRestService rcService) : base(rcService)
        {
            this.CustomImageVM = customImageVM;
            this._csvExportService = new AOICSVExportService();
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
            // 所有耗时操作（数据库查询、文件读取、JSON解析）放到后台线程
            // 后台线程处理数据查询和文件解析
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
            //var results = AlgResultService.LoadAlgResultByBatchCode(dieViewModel.SerialNumber!);
            //if (results != null && results.Count > 0)
            //{
            //    foreach (var result in results)
            //    {
            //        var aoiDetails = AlgResultService.GetCommDetailResult(result.Id);
            //        if (aoiDetails != null && aoiDetails.Count == 1)
            //        {
            //            var resultJson = aoiDetails[0].Result;
            //            if (!string.IsNullOrEmpty(resultJson))
            //            {
            //                // 解析 Common 表中的 Result 字段，获取 JSON 文件路径
            //                var detailResult = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(resultJson);
            //                if (detailResult != null && !string.IsNullOrEmpty(detailResult.ResultFileName) && File.Exists(detailResult.ResultFileName))
            //                {
            //                    // 读取并解析 Darkresult.json 文件
            //                    string darkResultJson = File.ReadAllText(detailResult.ResultFileName);
            //                    var darkResult = JsonConvert.DeserializeObject<DarkResultDto>(darkResultJson);

            //                    // 提取 GradeLevel
            //                    dieViewModel.AOIGradeLevel = darkResult?.GradeLevel ?? "na";
            //                    dieViewModel.BlackPattern = darkResult?.GradeLevel ?? "na";
            //                }
            //            }
            //            break;
            //        }
            //    }
            //}

            // 自动导出CSV结果
            // AutoExportCSVResult(dieViewModel, results);

            //LoadImageResult(dieViewModel.chipViewModel!.ChipData, dieViewModel.SerialNumber!);
            //return ChipStatus.OK;
            //var results = AlgResultService.LoadAlgResultByBatchCode(dieViewModel.SerialNumber!);
            //if (results != null && results.Count > 0)
            //{
            //    foreach (var result in results)
            //    {
            //        var aoiDetails = AlgResultService.GetCommDetailResult(result.Id);
            //        if (aoiDetails != null && aoiDetails.Count == 1)
            //        {
            //            var resultJson = aoiDetails[0].Result;
            //            if (!string.IsNullOrEmpty(resultJson))
            //            {
            //                // 解析 Common 表中的 Result 字段，获取 JSON 文件路径
            //                var detailResult = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(resultJson);
            //                if (detailResult != null && !string.IsNullOrEmpty(detailResult.ResultFileName) && File.Exists(detailResult.ResultFileName))
            //                {
            //                    // 读取并解析 Darkresult.json 文件
            //                    string darkResultJson = File.ReadAllText(detailResult.ResultFileName);
            //                    var darkResult = JsonConvert.DeserializeObject<DarkResultDto>(darkResultJson);

            //                    // 提取 GradeLevel
            //                    dieViewModel.AOIGradeLevel = darkResult?.GradeLevel ?? "na";
            //                    dieViewModel.BlackPattern = darkResult?.GradeLevel ?? "na";
            //                }
            //            }
            //            break;
            //        }
            //    }
            //}
            ////AOIResultDisplay(dieViewModel);
            ////EventAggregator?.Publish(new EQEFlowCompletedEvent(results));
            //LoadImageResult(dieViewModel.chipViewModel!.ChipData, dieViewModel.SerialNumber!);
            //return ChipStatus.OK;
        }
        /// <summary>
        /// 自动导出CSV结果
        /// </summary>
        //private void AutoExportCSVResult(DieViewModel dieViewModel, List<AlgorithmResult> results)
        //{
        //    try
        //    {
        //        // 配置导出路径（可以根据需求从配置文件或设置中读取）
        //        string exportDirectory = GetExportDirectory();

        //        // 导出单个Die的结果
        //        _csvExportService.ExportDieResult(dieViewModel, exportDirectory);

        //        logger.Info($"AOI测试完成，结果已自动导出");
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error($"自动导出CSV结果失败: {ex.Message}");
        //    }
        //}
        /// <summary>
        /// 获取导出目录
        /// </summary>
        private string GetExportDirectory()
        {
            // 这里可以根据你的需求配置导出路径
            // 例如：从配置文件读取、使用固定路径、按日期创建目录等

            // 示例：按日期创建目录
            string baseDirectory = _globalConfig?.AoiExportPath;// 可以改为从配置读取
            string dateDirectory = DateTime.Now.ToString("yyyyMMdd");
            string fullPath = Path.Combine(baseDirectory, dateDirectory);

            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            return fullPath;
        }

        /// <summary>
        /// 批量导出所有Die的完整CSV文件
        /// </summary>
        //public void ExportAllResultsCSV(List<DieViewModel> allDieViewModels, string outputPath = null)
        //{
        //    try
        //    {
        //        if (allDieViewModels == null || allDieViewModels.Count == 0)
        //            return;

        //        // 如果没有指定输出路径，使用默认路径
        //        if (string.IsNullOrEmpty(outputPath))
        //        {
        //            string exportDirectory = GetExportDirectory();
        //            outputPath = Path.Combine(exportDirectory, $"AOI-2_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        //        }

        //        // 导出完整CSV文件
        //        _csvExportService.ExportFullCSV(allDieViewModels, outputPath);

        //        logger.Info($"所有AOI结果已导出到: {outputPath}");
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error($"导出所有AOI结果失败: {ex.Message}");
        //        throw;
        //    }
        //}

        public class DetailResult_CommFile_V2
        {
            public string ResultFileName { get; set; }

        }

        // 用于解析 Darkresult.json 的 DTO
        public class DarkResultDto
        {
            public string GradeLevel { get; set; }

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
                //EventAggregator?.Publish(new EQEResultGUIClearEvent());
                return;
            }
            else
            {
                // 关键：在加载新数据前，先清空之前的数据
                CustomImageVM?.ClearImageResult();
                FlowResultDisplay(dieViewModel);
            }
            //CustomImageVM?.ClearImageResult();
            //LoadImageResult(dieViewModel.chipViewModel!.ChipData, dieViewModel.SerialNumber!);
        }

        private void AddResultImage(int id, string imgFile, bool isRawImage = false)
        {
            if (!File.Exists(imgFile)) return;

            string fileName = Path.GetFileNameWithoutExtension(imgFile).ToLower();

            // ========== 关键修改：统一过滤 po.dat 文件 ==========
            if (fileName.Equals("po") || fileName.Equals("po.dat"))
            {
                Debug.WriteLine($"Skipping po.dat file in AOI service: {Path.GetFileName(imgFile)}");
                return; // 在AOI服务中也跳过 po.dat 文件
            }
            // =================================================

            // 构造ImageItem
            ImageItem loc = new ImageItem(id)
            {
                FileName = Path.GetFileName(imgFile),
                ImagePath = imgFile,
                FileSizeMB = new FileInfo(imgFile).Length / (1024.0 * 1024.0),
                Status = "Ready"
            };

            // 仅添加到UI集合时切回UI线程
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (isRawImage)
                {
                    // 原始相机图 → Camera Measurement 视图
                    CustomImageVM?.AddOriginalImageOnly(loc);
                }
                else
                {
                    // 标定后图 → Analysis Image 视图
                    CustomImageVM?.AddImage(loc);
                }
            });
            //if (!System.IO.File.Exists(imgFile)) return;

            //string fileName = Path.GetFileNameWithoutExtension(imgFile).ToLower();
            //if (fileName.Equals("po") || fileName.Equals("po.dat"))
            //{
            //    Debug.WriteLine($"Skipping po.dat file: {Path.GetFileName(imgFile)}");
            //    return;
            //}

            //// 构造ImageItem（无UI操作）
            //ImageItem loc = new ImageItem(id)
            //{
            //    FileName = Path.GetFileName(imgFile),
            //    ImagePath = imgFile,
            //    FileSizeMB = new FileInfo(imgFile).Length / (1024.0 * 1024.0),
            //    Status = "Ready"
            //};

            //// 仅添加到UI集合时切回UI线程
            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //    if (isRawImage)
            //    {
            //        CustomImageVM?.AddOriginalImageOnly(loc);
            //    }
            //    else
            //    {
            //        CustomImageVM?.AddImage(loc);
            //    }
            //});
            //if (!System.IO.File.Exists(imgFile)) return;

            //// 检查是否是 po.dat 文件
            //string fileName = Path.GetFileNameWithoutExtension(imgFile).ToLower();
            //if (fileName.Equals("po") || fileName.Equals("po.dat"))
            //{
            //    Debug.WriteLine($"Skipping po.dat file: {Path.GetFileName(imgFile)}");
            //    return;
            //}

            //ImageItem loc = new ImageItem(id)
            //{
            //    FileName = Path.GetFileName(imgFile),
            //    ImagePath = imgFile,
            //    FileSizeMB = new FileInfo(imgFile).Length / (1024.0 * 1024.0),
            //    Status = "Ready"
            //};

            //// 根据图像类型添加到对应的集合
            //if (isRawImage)
            //{
            //    // 原始相机图 → 添加到 OriginalImageResults
            //    CustomImageVM?.AddOriginalImageOnly(loc);
            //}
            //else
            //{
            //    // 标定后图 → 添加到 ProcessedImageResults
            //    CustomImageVM?.AddImage(loc);
            //}
        }
        private void LoadImageResult(ChipData? chipData, string serialNumber)
        {
            // 后台线程处理所有耗时操作（数据库查询、文件判断），避免阻塞UI
            Task.Run(() =>
            {
                try
                {
                    string? resultImageFile = null;
                    DateTime? TestTime = null;
                    string? ImageDisplayBrightnessUniformity = null;
                    List<POIMarker> POIMarkers = new List<POIMarker>();
                    int imageId = 1; // 图像唯一ID，自增

                    // 1. 查询当前批次的所有算法主记录
                    var masterResults = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
                    if (masterResults == null || masterResults.Count == 0)
                    {
                        logger.Warn($"AOIService: 未找到批次[{serialNumber}]的算法结果记录");
                        return;
                    }
                    logger.Info($"AOIService: 找到批次[{serialNumber}]的{masterResults.Count}条算法主记录");

                    // 2. 定义【相机原图/算法图】的类型枚举（核心区分依据，从现有代码提取）
                    var cameraImageTypes = new List<AlgorithmResultType>
                    {
                        AlgorithmResultType.OLED_FindDotsArrayOutFile, // 定位原图
                        AlgorithmResultType.OLED_CombineQuaterImages    // 拼接原图
                    };
                    var algorithmImageTypes = new List<AlgorithmResultType>
                    {
                        AlgorithmResultType.OLED_RebuildPixelsMem, // 重建像素图（算法图核心）
                        AlgorithmResultType.PoiAnalysis,            // POI分析图（算法图）
                        AlgorithmResultType.POI_Y                   // POI标记图（算法图）
                    };

                    // 3. 遍历所有主记录，按类型分类处理图像
                    foreach (var masterResult in masterResults)
                    {
                        AlgorithmResultType currentType = (AlgorithmResultType)masterResult.ImgFileType;
                        logger.Info($"AOIService: 处理主记录[ID:{masterResult.Id}]，类型：{currentType.ToString()}");

                        // ========== 核心1：处理【相机原图】→ 调用AddOriginalImageOnly → Camera Measurement ==========
                        if (cameraImageTypes.Contains(currentType))
                        {
                            if (!string.IsNullOrEmpty(masterResult.ImgFile) && File.Exists(masterResult.ImgFile))
                            {
                                string fileName = Path.GetFileNameWithoutExtension(masterResult.ImgFile).ToLower();
                                // 过滤po.dat文件（统一过滤逻辑）
                                if (fileName.Equals("po") || fileName.Equals("po.dat"))
                                {
                                    logger.Debug($"AOIService: 跳过po.dat文件：{Path.GetFileName(masterResult.ImgFile)}");
                                    continue;
                                }
                                // 构造ImageItem（相机原图专属）
                                ImageItem cameraImage = new ImageItem(imageId++)
                                {
                                    FileName = Path.GetFileName(masterResult.ImgFile),
                                    ImagePath = masterResult.ImgFile,
                                    FileSizeMB = new FileInfo(masterResult.ImgFile).Length / (1024.0 * 1024.0),
                                    Status = "Ready"
                                };
                                // 调用ViewModel专属方法 → 直接加入OriginalImageResults（Camera Measurement）
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    CustomImageVM?.AddOriginalImageOnly(cameraImage);
                                });
                                logger.Info($"AOIService: 相机原图添加成功：{masterResult.ImgFile} → Camera Measurement");
                            }
                            continue; // 相机原图处理完成，跳过后续逻辑
                        }

                        // ========== 核心2：处理【算法图】→ 调用AddImage → Analysis Image ==========
                        if (algorithmImageTypes.Contains(currentType))
                        {
                            // 通过Pid查询算法图的文件记录（从TScgdAlgorithmResultDetailPoiCieFile表获取）
                            var poiFileDetails = AlgResultService.GetPOIDetailResultFileByPid(masterResult.Id);
                            if (poiFileDetails == null || poiFileDetails.Count == 0)
                            {
                                logger.Warn($"AOIService: 主记录[ID:{masterResult.Id}]未查询到关联的算法图文件记录");
                                continue;
                            }
                            // 遍历算法图文件记录，逐个添加
                            foreach (var detail in poiFileDetails)
                            {
                                if (!string.IsNullOrEmpty(detail.FileUrl) && File.Exists(detail.FileUrl))
                                {
                                    string fileName = Path.GetFileNameWithoutExtension(detail.FileUrl).ToLower();
                                    // 过滤po.dat文件（统一过滤逻辑）
                                    if (fileName.Equals("po") || fileName.Equals("po.dat"))
                                    {
                                        logger.Debug($"AOIService: 跳过po.dat文件：{Path.GetFileName(detail.FileUrl)}");
                                        continue;
                                    }
                                    // 构造ImageItem（算法图专属）
                                    ImageItem algorithmImage = new ImageItem(imageId++)
                                    {
                                        FileName = Path.GetFileName(detail.FileUrl),
                                        ImagePath = detail.FileUrl,
                                        FileSizeMB = new FileInfo(detail.FileUrl).Length / (1024.0 * 1024.0),
                                        Status = "Ready"
                                    };
                                    // 调用ViewModel专属方法 → 直接加入ProcessedImageResults（Analysis Image）
                                    // AddImage已内置：去重、过滤po.dat、UI线程安全
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        CustomImageVM?.AddImage(algorithmImage);
                                    });
                                    logger.Info($"AOIService: 算法图添加成功：{detail.FileUrl} → Analysis Image");
                                }
                            }
                        }

                        // ========== 原有业务逻辑保留：POI标记、亮度均匀性、JSON解析等 ==========
                        if (currentType == AlgorithmResultType.POI_Y)
                        {
                            var poiMarkers = AlgResultService.GetPOIDetailResult(masterResult.Id);
                            foreach (var poi in poiMarkers)
                            {
                                if (poi.PoiType == 0)
                                    POIMarkers.Add(new CircleMarker()
                                    {
                                        Label = poi.PoiName,
                                        X = (double)poi.PoiX,
                                        Y = (double)poi.PoiY,
                                        Width = (double)poi.PoiWidth,
                                        Height = (double)poi.PoiHeight,
                                        Fill = null
                                    });
                                else if (poi.PoiType == 1)
                                    POIMarkers.Add(new RectangleMarker()
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

                        if (currentType == AlgorithmResultType.PoiAnalysis)
                        {
                            var details = AlgResultService.GetCommDetailResult(masterResult.Id);
                            if (details != null && details.Count > 0)
                            {
                                foreach (var detail in details)
                                {
                                    if (!string.IsNullOrEmpty(detail.Result))
                                    {
                                        try
                                        {
                                            DetailResult_CommFile_V2 detailResult_Comm = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(detail.Result);
                                            if (!string.IsNullOrEmpty(detailResult_Comm?.ResultFileName) && File.Exists(detailResult_Comm.ResultFileName))
                                            {
                                                PoiAnalysis poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(File.ReadAllText(detailResult_Comm.ResultFileName));
                                                if (chipData != null && poiAnalysis?.result != null)
                                                {
                                                    chipData.DataValue = poiAnalysis.result.Value;
                                                    ImageDisplayBrightnessUniformity = string.Format("[{0},{1}]={2:F4}", chipData.Row, chipData.Column, chipData.DataValue);
                                                    logger.Info($"AOIService: PoiAnalysis数据提取成功：{ImageDisplayBrightnessUniformity}");
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.Error($"AOIService: 解析PoiAnalysis失败：{ex.Message}\n{ex.StackTrace}");
                                        }
                                    }
                                }
                            }
                        }

                        // 记录基础图像信息（用于后续POI标记显示）
                        if (masterResult.ImgFileType >= 42 && masterResult.ImgFileType <= 45)
                        {
                            resultImageFile = masterResult.ImgFile;
                            TestTime = masterResult.CreateDate;
                        }
                    }

                    // 4. 最后：更新POI标记图像（UI线程，原有逻辑保留）
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!string.IsNullOrEmpty(resultImageFile) && !string.IsNullOrEmpty(ImageDisplayBrightnessUniformity))
                        {
                            OpenCvSharp.Mat? image = null;
                            if (CVImageFileUtil.LoadImgFile(resultImageFile, ref image))
                            {
                                image = OpenCvMatTools.ConvertImageTo8UC3(image);
                                CustomImageVM?.UpdatePOIImage(image, POIMarkers, ImageDisplayBrightnessUniformity);
                                logger.Info($"AOIService: POI标记图像更新成功，显示亮度均匀性：{ImageDisplayBrightnessUniformity}");
                            }
                            else
                            {
                                logger.Warn($"AOIService: 加载POI标记底图失败：{resultImageFile}");
                            }
                        }
                        else
                        {
                            logger.Warn($"AOIService: 无POI标记底图或亮度信息，跳过图像更新");
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.Error($"AOIService: LoadImageResult执行失败：{ex.Message}\n{ex.StackTrace}");
                }
            });
        }

        //private void LoadImageResult(ChipData? chipData, string serialNumber)
        //{

        //    // 后台线程处理图像加载逻辑
        //    Task.Run(() =>
        //    {
        //        try
        //        {
        //            string? resultImageFile = null;
        //            DateTime? TestTime = null;
        //            string? ImageDisplayBrightnessUniformity = null;

        //            // 1. 首先从 VScgdAlgorithmResultMaster 获取主记录
        //            var masterResults = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
        //            if (masterResults == null || masterResults.Count == 0)
        //            {
        //                logger.Warn($"No master results found for serial number: {serialNumber}");
        //                return;
        //            }

        //            logger.Info($"Found {masterResults.Count} master results for {serialNumber}");

        //            List<POIMarker> POIMarkers = new List<POIMarker>();
        //            int id = 1;

        //            // 2. 处理每个主记录
        //            foreach (var masterResult in masterResults)
        //            {
        //                AlgorithmResultType resultType = (AlgorithmResultType)masterResult.ImgFileType;
        //                logger.Info($"Processing result type: {resultType} (ID: {masterResult.Id})");

        //                // 2.1 从 VScgdAlgorithmResultMaster 获取原图（Camera Measurement）
        //                if (resultType == AlgorithmResultType.OLED_FindDotsArrayOutFile ||
        //                    resultType == AlgorithmResultType.OLED_CombineQuaterImages)
        //                {
        //                    if (!string.IsNullOrEmpty(masterResult.ImgFile) && File.Exists(masterResult.ImgFile))
        //                    {
        //                        string fileName = Path.GetFileNameWithoutExtension(masterResult.ImgFile).ToLower();
        //                        // 过滤 po.dat 文件
        //                        if (!fileName.Equals("po") && !fileName.Equals("po.dat"))
        //                        {
        //                            logger.Info($"Adding camera measurement image: {masterResult.ImgFile}");
        //                            AddImageToCameraMeasurement(id++, masterResult.ImgFile);
        //                        }
        //                    }
        //                }

        //                // 2.2 记录基础图像信息（用于后续显示）
        //                if (masterResult.ImgFileType >= 42 && masterResult.ImgFileType <= 45)
        //                {
        //                    resultImageFile = masterResult.ImgFile;
        //                    TestTime = masterResult.CreateDate;
        //                    logger.Info($"Base image found: {resultImageFile}");
        //                }

        //                // 2.3 从 TScgdAlgorithmResultDetailPoiCieFile 获取算法图（Analysis Image）
        //                // 关键：通过 Pid = masterResult.Id 来查找对应的明细记录
        //                var poiDetails = AlgResultService.GetPOIDetailResultFileByPid(masterResult.Id);
        //                logger.Info($"Found {poiDetails?.Count ?? 0} POI detail records for master ID: {masterResult.Id}");

        //                if (poiDetails != null && poiDetails.Count > 0)
        //                {
        //                    foreach (var detail in poiDetails)
        //                    {
        //                        if (!string.IsNullOrEmpty(detail.FileUrl) && File.Exists(detail.FileUrl))
        //                        {
        //                            string fileName = Path.GetFileNameWithoutExtension(detail.FileUrl).ToLower();
        //                            // 过滤 po.dat 文件
        //                            if (!fileName.Equals("po") && !fileName.Equals("po.dat"))
        //                            {
        //                                logger.Info($"Adding analysis image: {detail.FileUrl}");
        //                                AddImageToAnalysisImage(id++, detail.FileUrl);
        //                            }
        //                        }
        //                    }
        //                }

        //                // 2.4 处理其他类型的结果
        //                if (resultType == AlgorithmResultType.POI_Y)
        //                {
        //                    var poiMarkers = AlgResultService.GetPOIDetailResult(masterResult.Id);
        //                    foreach (var poi in poiMarkers)
        //                    {
        //                        if (poi.PoiType == 0)
        //                            POIMarkers.Add(new CircleMarker()
        //                            {
        //                                Label = poi.PoiName,
        //                                X = (double)poi.PoiX,
        //                                Y = (double)poi.PoiY,
        //                                Width = (double)poi.PoiWidth,
        //                                Height = (double)poi.PoiHeight,
        //                                Fill = null
        //                            });
        //                        else if (poi.PoiType == 1)
        //                            POIMarkers.Add(new RectangleMarker()
        //                            {
        //                                Label = poi.PoiName,
        //                                X = (double)poi.PoiX,
        //                                Y = (double)poi.PoiY,
        //                                Width = (double)poi.PoiWidth,
        //                                Height = (double)poi.PoiHeight,
        //                                Fill = null
        //                            });
        //                    }
        //                }

        //                // 2.5 处理 PoiAnalysis
        //                if (resultType == AlgorithmResultType.PoiAnalysis)
        //                {
        //                    var details = AlgResultService.GetCommDetailResult(masterResult.Id);
        //                    if (details != null && details.Count > 0)
        //                    {
        //                        foreach (var detail in details)
        //                        {
        //                            if (!string.IsNullOrEmpty(detail.Result))
        //                            {
        //                                try
        //                                {
        //                                    DetailResult_CommFile_V2 detailResult_Comm = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(detail.Result);
        //                                    if (!string.IsNullOrEmpty(detailResult_Comm?.ResultFileName) &&
        //                                        File.Exists(detailResult_Comm.ResultFileName))
        //                                    {
        //                                        PoiAnalysis poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(
        //                                            File.ReadAllText(detailResult_Comm.ResultFileName));

        //                                        if (chipData != null && poiAnalysis?.result != null)
        //                                        {
        //                                            chipData.DataValue = poiAnalysis.result.Value;
        //                                            ImageDisplayBrightnessUniformity = string.Format(
        //                                                "[{0},{1}]={2:F4}",
        //                                                chipData.Row, chipData.Column, chipData.DataValue);
        //                                            logger.Info($"PoiAnalysis data: {ImageDisplayBrightnessUniformity}");
        //                                        }
        //                                    }
        //                                }
        //                                catch (Exception ex)
        //                                {
        //                                    logger.Error($"Error parsing PoiAnalysis: {ex.Message}");
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //            }

        //            // 3. 更新图像显示（UI线程）
        //            Application.Current.Dispatcher.Invoke(() =>
        //            {
        //                if (!string.IsNullOrEmpty(resultImageFile) && !string.IsNullOrEmpty(ImageDisplayBrightnessUniformity))
        //                {
        //                    OpenCvSharp.Mat? image = null;
        //                    if (CVImageFileUtil.LoadImgFile(resultImageFile, ref image))
        //                    {
        //                        image = OpenCvMatTools.ConvertImageTo8UC3(image);
        //                        CustomImageVM?.UpdatePOIImage(image, POIMarkers, ImageDisplayBrightnessUniformity);
        //                        logger.Info($"Image displayed with POI markers and brightness info");
        //                    }
        //                    else
        //                    {
        //                        logger.Warn($"Failed to load image: {resultImageFile}");
        //                    }
        //                }
        //                else
        //                {
        //                    logger.Warn($"No image or brightness info to display. Image: {resultImageFile}, Brightness: {ImageDisplayBrightnessUniformity}");
        //                }
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            logger.Error($"Error in LoadImageResult: {ex.Message}\n{ex.StackTrace}");
        //        }
        //    });
        //    // 后台线程处理图像加载逻辑
        //    //Task.Run(() =>
        //    //{
        //    //    string? resultImageFile = null;
        //    //    DateTime? TestTime = null;
        //    //    string? ImageDisplayBrightnessUniformity = null;
        //    //    var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
        //    //    if (results == null || results.Count == 0) return;
        //    //    List<POIMarker> POIMarkers = new List<POIMarker>();
        //    //    int id = 1;

        //    //    // 原有图像解析逻辑（无UI操作）
        //    //    foreach (var result in results)
        //    //    {
        //    //        // 1. 先加载Analysis image（从TScgdAlgorithmResultDetailPoiCieFile获取）
        //    //        LoadAnalysisImages(results, ref id, ref resultImageFile, ref TestTime);

        //    //        // 2. 再加载Camera Measurement（从VScgdAlgorithmResultMaster获取关联的原图）
        //    //        LoadCameraMeasurementImages(results, ref id);
        //    //    }

        //    //    // 仅UI更新操作切回UI线程
        //    //    Application.Current.Dispatcher.Invoke(() =>
        //    //    {
        //    //        if (!string.IsNullOrEmpty(resultImageFile) && !string.IsNullOrEmpty(ImageDisplayBrightnessUniformity))
        //    //        {
        //    //            OpenCvSharp.Mat? image = null;
        //    //            if (CVImageFileUtil.LoadImgFile(resultImageFile, ref image))
        //    //            {
        //    //                image = OpenCvMatTools.ConvertImageTo8UC3(image);
        //    //                CustomImageVM?.UpdatePOIImage(image, POIMarkers, ImageDisplayBrightnessUniformity);
        //    //            }
        //    //        }

        //    //        // 图像添加到ViewModel的操作也在UI线程
        //    //        if (!string.IsNullOrEmpty(resultImageFile))
        //    //            AddResultImage(id++, resultImageFile);
        //    //    });
        //    //});
        //    //string? resultImageFile = null;
        //    //DateTime? TestTime = null;
        //    //string? ImageDisplayBrightnessUniformity = null;
        //    //var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
        //    //if (results == null || results.Count == 0) return;
        //    //List<POIMarker> POIMarkers = new List<POIMarker>();
        //    //int id = 1;
        //    //// 1. 先加载Analysis image（从TScgdAlgorithmResultDetailPoiCieFile获取）
        //    //LoadAnalysisImages(results, ref id, ref resultImageFile, ref TestTime);

        //    //// 2. 再加载Camera Measurement（从VScgdAlgorithmResultMaster获取关联的原图）
        //    //LoadCameraMeasurementImages(results, ref id);

        //    //// 3. 处理POI标记和亮度均匀性显示（原有逻辑保留）
        //    //if (!string.IsNullOrEmpty(resultImageFile) && !string.IsNullOrEmpty(ImageDisplayBrightnessUniformity))
        //    //{
        //    //    OpenCvSharp.Mat? image = null;
        //    //    if (!CVImageFileUtil.LoadImgFile(resultImageFile, ref image)) return;
        //    //    image = OpenCvMatTools.ConvertImageTo8UC3(image);
        //    //    CustomImageVM?.UpdatePOIImage(image, POIMarkers, ImageDisplayBrightnessUniformity);
        //    //}

        //    ////foreach (var result in results)
        //    ////{
        //    ////    AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;
        //    ////    if (result.ImgFileType >= 42 && result.ImgFileType <= 45)
        //    ////    {
        //    ////        resultImageFile = result.ImgFile;
        //    ////        TestTime = result.CreateDate;
        //    ////    }
        //    ////    else if (resultType == AlgorithmResultType.OLED_CombineQuaterImages)
        //    ////    {
        //    ////        AddResultImage(id++, result.ImgResult);
        //    ////    }
        //    ////    else if (resultType == AlgorithmResultType.OLED_RebuildPixelsMem)
        //    ////    {
        //    ////        var details = AlgResultService.GetPOIDetailResultFileByPid(result.Id);
        //    ////        if (details != null && details.Count == 1)
        //    ////        {
        //    ////            if (System.IO.File.Exists(details[0].FileUrl))
        //    ////            {
        //    ////                AddResultImage(id++, details[0].FileUrl);
        //    ////            }
        //    ////        }
        //    ////    }
        //    ////    else if (resultType == AlgorithmResultType.POI_Y)
        //    ////    {
        //    ////        var details = AlgResultService.GetPOIDetailResult(result.Id);
        //    ////        foreach (var poi in details)
        //    ////        {
        //    ////            if (poi.PoiType == 0) POIMarkers.Add(new CircleMarker() { Label = poi.PoiName, X = (double)poi.PoiX, Y = (double)poi.PoiY, Width = (double)poi.PoiWidth, Height = (double)poi.PoiHeight, Fill = null });
        //    ////            else if (poi.PoiType == 1) POIMarkers.Add(new RectangleMarker() { Label = poi.PoiName, X = (double)poi.PoiX, Y = (double)poi.PoiY, Width = (double)poi.PoiWidth, Height = (double)poi.PoiHeight, Fill = null });
        //    ////        }
        //    ////    }
        //    ////    else if (resultType == AlgorithmResultType.PoiAnalysis)
        //    ////    {
        //    ////        var details = AlgResultService.GetCommDetailResult(result.Id);
        //    ////        if (details != null && details.Count == 1)
        //    ////        {
        //    ////            DetailResult_CommFile_V2 detailResult_Comm = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(details[0].Result);
        //    ////            if (System.IO.File.Exists(detailResult_Comm.ResultFileName))
        //    ////            {
        //    ////                //ImageItem imageResultViewModel = new ImageItem(id++);

        //    ////                PoiAnalysis poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(System.IO.File.ReadAllText(detailResult_Comm.ResultFileName));
        //    ////                chipData.DataValue = poiAnalysis.result.Value;
        //    ////                ImageDisplayBrightnessUniformity = string.Format("[{0},{1}]={2:F4}", chipData.Row, chipData.Column, chipData.DataValue);
        //    ////                //
        //    ////                //imageResultViewModel.FileName = System.IO.Path.GetFileName(resultImageFile);
        //    ////                //imageResultViewModel.ImagePath = resultImageFile;
        //    ////                //imageResultViewModel.SerialNumber = serialNumber;
        //    ////                //imageResultViewModel.TestTime = TestTime;
        //    ////                //imageResultViewModel.ResultType = "数据提取";
        //    ////                // 在UI线程更新集合
        //    ////                //CustomImageVM?.AddImage(imageResultViewModel);

        //    ////                if (!string.IsNullOrEmpty(resultImageFile)) AddResultImage(id++, resultImageFile);
        //    ////            }
        //    ////        }
        //    ////    }
        //    ////    //定位
        //    ////    else if (resultType == AlgorithmResultType.OLED_FindDotsArrayOutFile)
        //    ////    {
        //    ////        //ImageItem loc = new ImageItem(id++);
        //    ////        //loc.FileName = System.IO.Path.GetFileName(result.ImgFile);
        //    ////        //loc.ImagePath = result.ImgFile;
        //    ////        ////loc.ResultType = "定位";
        //    ////        ////loc.SerialNumber = serialNumber;
        //    ////        ////loc.TestTime = result.CreateDate;
        //    ////        //// 在UI线程更新集合
        //    ////        //CustomImageVM?.AddImage(loc);
        //    ////       // AddResultImage(id++, result.ImgFile);
        //    ////        bool isRawImage = Path.GetExtension(result.ImgFile).ToLower() == ".cvraw";
        //    ////        AddResultImage(id++, result.ImgFile, isRawImage);

        //    ////    }
        //    ////}

        //    //if (!string.IsNullOrEmpty(resultImageFile) && !string.IsNullOrEmpty(ImageDisplayBrightnessUniformity))
        //    //{
        //    //    OpenCvSharp.Mat? image = null;
        //    //    if (!CVImageFileUtil.LoadImgFile(resultImageFile, ref image)) return;
        //    //    image = OpenCvMatTools.ConvertImageTo8UC3(image);
        //    //    CustomImageVM?.UpdatePOIImage(image, POIMarkers, ImageDisplayBrightnessUniformity);             
        //    //}
        //}
        /// <summary>
        /// 添加图像到 Camera Measurement（原图）
        /// </summary>
        private void AddImageToCameraMeasurement(int id, string imgFile)
        {
            if (!File.Exists(imgFile)) return;

            string fileName = Path.GetFileNameWithoutExtension(imgFile).ToLower();

            // 过滤 po.dat 文件
            if (fileName.Equals("po") || fileName.Equals("po.dat"))
            {
                Debug.WriteLine($"Skipping po.dat file in Camera Measurement: {Path.GetFileName(imgFile)}");
                return;
            }

            ImageItem loc = new ImageItem(id)
            {
                FileName = Path.GetFileName(imgFile),
                ImagePath = imgFile,
                FileSizeMB = new FileInfo(imgFile).Length / (1024.0 * 1024.0),
                Status = "Ready"
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                // 添加到 OriginalImageResults（Camera Measurement 视图）
                CustomImageVM?.AddOriginalImageOnly(loc);
            });
        }

        /// <summary>
        /// 添加图像到 Analysis Image（算法图）
        /// </summary>
        private void AddImageToAnalysisImage(int id, string imgFile)
        {
            if (!File.Exists(imgFile)) return;

            string fileName = Path.GetFileNameWithoutExtension(imgFile).ToLower();

            // 过滤 po.dat 文件
            if (fileName.Equals("po") || fileName.Equals("po.dat"))
            {
                Debug.WriteLine($"Skipping po.dat file in Analysis Image: {Path.GetFileName(imgFile)}");
                return;
            }

            ImageItem loc = new ImageItem(id)
            {
                FileName = Path.GetFileName(imgFile),
                ImagePath = imgFile,
                FileSizeMB = new FileInfo(imgFile).Length / (1024.0 * 1024.0),
                Status = "Ready"
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                // 直接添加到 ProcessedImageResults，绕过 AddImage 的分类逻辑
                if (!CustomImageVM?.ProcessedImageResults.Any(item =>
                    item.ImagePath.Equals(imgFile, StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    CustomImageVM?.ProcessedImageResults.Add(loc);
                }
            });
        }

        /// <summary>
        /// 加载Analysis image（分析后图像）- 从TScgdAlgorithmResultDetailPoiCieFile获取，过滤po.dat
        /// </summary>
        //private void LoadAnalysisImages(List<VScgdAlgorithmResultMaster> results, ref int id, ref string? resultImageFile, ref DateTime? TestTime)
        //{
        //    foreach (var result in results)
        //    {
        //        AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;

        //        // 处理OLED_RebuildPixelsMem类型（关联TScgdAlgorithmResultDetailPoiCieFile）
        //        if (resultType == AlgorithmResultType.OLED_RebuildPixelsMem)
        //        {
        //            var details = AlgResultService.GetPOIDetailResultFileByPid(result.Id);
        //            if (details != null && details.Count == 1)
        //            {
        //                string filePath = details[0].FileUrl;

        //                // ========== 增强过滤逻辑 ==========
        //                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        //                {
        //                    string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
        //                    if (!fileName.Equals("po") && !fileName.Equals("po.dat"))
        //                    {
        //                        AddAnalysisImage(id++, filePath);
        //                    }
        //                }
        //                // =================================
        //            }
        //        }

        //        // 记录基础信息（原有逻辑）
        //        if (result.ImgFileType >= 42 && result.ImgFileType <= 45)
        //        {
        //            resultImageFile = result.ImgFile;
        //            TestTime = result.CreateDate;
        //        }
        //    }
        //    //foreach (var result in results)
        //    //{
        //    //    // 处理OLED_RebuildPixelsMem类型（关联TScgdAlgorithmResultDetailPoiCieFile）
        //    //    AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;
        //    //    if (resultType == AlgorithmResultType.OLED_RebuildPixelsMem)
        //    //    {
        //    //        var details = AlgResultService.GetPOIDetailResultFileByPid(result.Id);
        //    //        if (details != null && details.Count == 1)
        //    //        {
        //    //            string filePath = details[0].FileUrl;
        //    //            // 过滤po.dat后缀的文件
        //    //            if (!string.IsNullOrEmpty(filePath)
        //    //                && File.Exists(filePath)
        //    //                && !Path.GetFileName(filePath).ToLower().Equals("po.dat"))
        //    //            {
        //    //                AddAnalysisImage(id++, filePath);
        //    //            }
        //    //        }
        //    //    }

        //    //    // 记录基础信息（原有逻辑）
        //    //    if (result.ImgFileType >= 42 && result.ImgFileType <= 45)
        //    //    {
        //    //        resultImageFile = result.ImgFile;
        //    //        TestTime = result.CreateDate;
        //    //    }
        //    //}
        //}
        /// <summary>
        /// 添加图像到对应的视图集合
        /// </summary>
        private void AddImageToViewModel(int id, string imgFile, bool isFromTScgd = false)
        {
            if (!File.Exists(imgFile)) return;

            string fileName = Path.GetFileNameWithoutExtension(imgFile).ToLower();

            // ========== 关键修改：统一过滤 po.dat 文件 ==========
            if (fileName.Equals("po") || fileName.Equals("po.dat"))
            {
                Debug.WriteLine($"Skipping po.dat file: {Path.GetFileName(imgFile)}");
                return; // 跳过 po.dat 文件
            }
            // =================================================

            // 构造ImageItem
            ImageItem loc = new ImageItem(id)
            {
                FileName = Path.GetFileName(imgFile),
                ImagePath = imgFile,
                FileSizeMB = new FileInfo(imgFile).Length / (1024.0 * 1024.0),
                Status = "Ready"
            };

            // 根据来源添加到对应集合
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (isFromTScgd)
                {
                    // 从 TScgdAlgorithmResultDetailPoiCieFile 获取的 → Analysis image 视图
                    CustomImageVM?.AddImage(loc); // 这会进入 ProcessedImageResults
                }
                else
                {
                    // 从 VScgdAlgorithmResultMaster 获取的 → Camera Measurement 视图
                    CustomImageVM?.AddOriginalImageOnly(loc); // 这会进入 OriginalImageResults
                }
            });
        }
        /// <summary>
        /// 加载Camera Measurement（原始图像）- 从VScgdAlgorithmResultMaster获取关联原图
        /// </summary>
        //private void LoadCameraMeasurementImages(List<VScgdAlgorithmResultMaster> results, ref int id)
        //{
        //    foreach (var result in results)
        //    {
        //        AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;

        //        // 匹配Camera Measurement的原图类型
        //        List<AlgorithmResultType> cameraTypes = new List<AlgorithmResultType>
        //        {
        //            AlgorithmResultType.OLED_FindDotsArrayOutFile, // 定位原图
        //            AlgorithmResultType.OLED_CombineQuaterImages   // 拼接原图
        //        };

        //        if (cameraTypes.Contains(resultType) &&
        //            !string.IsNullOrEmpty(result.ImgFile) &&
        //            File.Exists(result.ImgFile))
        //        {
        //            // ========== 增强过滤逻辑 ==========
        //            string fileName = Path.GetFileNameWithoutExtension(result.ImgFile).ToLower();
        //            if (!fileName.Equals("po") && !fileName.Equals("po.dat"))
        //            {
        //                AddCameraMeasurementImage(id++, result.ImgFile);
        //            }
        //            // =================================
        //        }
        //    }
        //    //foreach (var result in results)
        //    //{
        //    //    AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;

        //    //    // 匹配Camera Measurement的原图类型（可根据实际业务调整类型范围）
        //    //    List<AlgorithmResultType> cameraTypes = new List<AlgorithmResultType>
        //    //    {
        //    //        AlgorithmResultType.OLED_FindDotsArrayOutFile, // 定位原图
        //    //        AlgorithmResultType.OLED_CombineQuaterImages   // 拼接原图
        //    //        // 可添加其他原始图像类型
        //    //    };

        //    //    if (cameraTypes.Contains(resultType) && !string.IsNullOrEmpty(result.ImgFile) && File.Exists(result.ImgFile))
        //    //    {
        //    //        AddCameraMeasurementImage(id++, result.ImgFile);
        //    //    }
        //    //}
        //}

        /// <summary>
        /// 添加Analysis image到ViewModel的ProcessedImageResults集合
        /// </summary>
        //private void AddAnalysisImage(int id, string imgFile)
        //{
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        ImageItem loc = new ImageItem(id);
        //        loc.FileName = Path.GetFileName(imgFile);
        //        loc.ImagePath = imgFile;
        //        // 使用AddImage方法（自动过滤po.dat并加入ProcessedImageResults）
        //        CustomImageVM?.AddImage(loc);
        //    });
        //}

        /// <summary>
        /// 添加Camera Measurement到ViewModel的OriginalImageResults集合
        /// </summary>
        //private void AddCameraMeasurementImage(int id, string imgFile)
        //{
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        ImageItem loc = new ImageItem(id);
        //        loc.FileName = Path.GetFileName(imgFile);
        //        loc.ImagePath = imgFile;
        //        // 直接添加到原始图像集合（包含所有类型）
        //        CustomImageVM?.AddOriginalImageOnly(loc);
        //    });
        //}
        public override void AutoExportData()
        {

        }
    }
}
