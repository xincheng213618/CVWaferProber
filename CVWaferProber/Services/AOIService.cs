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

                                    // 仅更新ViewModel属性时切回UI线程
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
            // 图像加载也放到后台线程，仅UI更新切回
            LoadImageResult(dieViewModel.chipViewModel!.ChipData, dieViewModel.SerialNumber!);
            return ChipStatus.OK;
         
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
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
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
            if (!System.IO.File.Exists(imgFile)) return;

            string fileName = Path.GetFileNameWithoutExtension(imgFile).ToLower();
            if (fileName.Equals("po") || fileName.Equals("po.dat"))
            {
                Debug.WriteLine($"Skipping po.dat file: {Path.GetFileName(imgFile)}");
                return;
            }

            // 构造ImageItem（无UI操作）
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
                    CustomImageVM?.AddOriginalImageOnly(loc);
                }
                else
                {
                    CustomImageVM?.AddImage(loc);
                }
            });
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

        //private void LoadImageResult(ChipData? chipData, string serialNumber)
        //{
        //    // 后台线程处理图像加载逻辑
        //    Task.Run(() =>
        //    {
        //        string? resultImageFile = null;
        //        DateTime? TestTime = null;
        //        string? ImageDisplayBrightnessUniformity = null;
        //        var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
        //        if (results == null || results.Count == 0) return;
        //        List<POIMarker> POIMarkers = new List<POIMarker>();
        //        int id = 1;

        //        // 原有图像解析逻辑（无UI操作）
        //        foreach (var result in results)
        //        {
        //            // 1. 先加载Analysis image（从TScgdAlgorithmResultDetailPoiCieFile获取）
        //            LoadAnalysisImages(results, ref id, ref resultImageFile, ref TestTime);

        //            // 2. 再加载Camera Measurement（从VScgdAlgorithmResultMaster获取关联的原图）
        //            LoadCameraMeasurementImages(results, ref id);
        //        }

        //        // 仅UI更新操作切回UI线程
        //        Application.Current.Dispatcher.Invoke(() =>
        //        {
        //            if (!string.IsNullOrEmpty(resultImageFile) && !string.IsNullOrEmpty(ImageDisplayBrightnessUniformity))
        //            {
        //                OpenCvSharp.Mat? image = null;
        //                if (CVImageFileUtil.LoadImgFile(resultImageFile, ref image))
        //                {
        //                    image = OpenCvMatTools.ConvertImageTo8UC3(image);
        //                    CustomImageVM?.UpdatePOIImage(image, POIMarkers, ImageDisplayBrightnessUniformity);
        //                }
        //            }

        //            // 图像添加到ViewModel的操作也在UI线程
        //            if (!string.IsNullOrEmpty(resultImageFile))
        //                AddResultImage(id++, resultImageFile);
        //        });
        //    });
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
        /// 加载Analysis image（分析后图像）- 从TScgdAlgorithmResultDetailPoiCieFile获取，过滤po.dat
        /// </summary>
        private void LoadAnalysisImages(List<VScgdAlgorithmResultMaster> results, ref int id, ref string? resultImageFile, ref DateTime? TestTime)
        {
            foreach (var result in results)
            {
                // 处理OLED_RebuildPixelsMem类型（关联TScgdAlgorithmResultDetailPoiCieFile）
                AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;
                if (resultType == AlgorithmResultType.OLED_RebuildPixelsMem)
                {
                    var details = AlgResultService.GetPOIDetailResultFileByPid(result.Id);
                    if (details != null && details.Count == 1)
                    {
                        string filePath = details[0].FileUrl;
                        // 过滤po.dat后缀的文件
                        if (!string.IsNullOrEmpty(filePath)
                            && File.Exists(filePath)
                            && !Path.GetFileName(filePath).ToLower().Equals("po.dat"))
                        {
                            AddAnalysisImage(id++, filePath);
                        }
                    }
                }

                // 记录基础信息（原有逻辑）
                if (result.ImgFileType >= 42 && result.ImgFileType <= 45)
                {
                    resultImageFile = result.ImgFile;
                    TestTime = result.CreateDate;
                }
            }
        }

        /// <summary>
        /// 加载Camera Measurement（原始图像）- 从VScgdAlgorithmResultMaster获取关联原图
        /// </summary>
        private void LoadCameraMeasurementImages(List<VScgdAlgorithmResultMaster> results, ref int id)
        {
            foreach (var result in results)
            {
                AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;

                // 匹配Camera Measurement的原图类型（可根据实际业务调整类型范围）
                List<AlgorithmResultType> cameraTypes = new List<AlgorithmResultType>
                {
                    AlgorithmResultType.OLED_FindDotsArrayOutFile, // 定位原图
                    AlgorithmResultType.OLED_CombineQuaterImages   // 拼接原图
                    // 可添加其他原始图像类型
                };

                if (cameraTypes.Contains(resultType) && !string.IsNullOrEmpty(result.ImgFile) && File.Exists(result.ImgFile))
                {
                    AddCameraMeasurementImage(id++, result.ImgFile);
                }
            }
        }

        /// <summary>
        /// 添加Analysis image到ViewModel的ProcessedImageResults集合
        /// </summary>
        private void AddAnalysisImage(int id, string imgFile)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ImageItem loc = new ImageItem(id);
                loc.FileName = Path.GetFileName(imgFile);
                loc.ImagePath = imgFile;
                // 使用AddImage方法（自动过滤po.dat并加入ProcessedImageResults）
                CustomImageVM?.AddImage(loc);
            });
        }

        /// <summary>
        /// 添加Camera Measurement到ViewModel的OriginalImageResults集合
        /// </summary>
        private void AddCameraMeasurementImage(int id, string imgFile)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ImageItem loc = new ImageItem(id);
                loc.FileName = Path.GetFileName(imgFile);
                loc.ImagePath = imgFile;
                // 直接添加到原始图像集合（包含所有类型）
                CustomImageVM?.AddOriginalImageOnly(loc);
            });
        }
        public override void AutoExportData()
        {

        }

        /// <summary>
        /// 核心：加载AOI测试结果图片（后台线程处理，仅UI操作切回主线程）
        /// 功能：解析所有关联图像文件、POI标记、亮度均匀性数据，正确展示到CustomImageVM
        /// </summary>
        /// <param name="chipData">芯片数据（用于亮度均匀性显示）</param>
        /// <param name="serialNumber">芯片序列号（关联算法结果）</param>
        private void LoadImageResult(ChipData? chipData, string serialNumber)
        {
            // 空值快速判断，直接返回避免无效操作
            if (string.IsNullOrEmpty(serialNumber) || CustomImageVM == null)
            {
                logger.Warn("LoadImageResult：序列号为空或CustomImageVM未初始化，跳过图片加载");
                return;
            }

            // 所有耗时/非UI操作放到后台线程
            Task.Run(() =>
            {
                try
                {
                    // 1. 初始化核心变量（后台线程内声明，避免跨线程作用域问题）
                    int imageId = 1; // 图片唯一标识ID
                    string? mainResultImageFile = null; // 主分析图文件路径
                    DateTime? testTime = null; // 测试时间
                    string? brightnessUniformityText = null; // 亮度均匀性显示文本
                    List<POIMarker> poiMarkers = new List<POIMarker>(); // POI标记集合

                    // 2. 从数据库加载算法主结果
                    var algResults = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
                    if (algResults == null || algResults.Count == 0)
                    {
                        logger.Info($"LoadImageResult：序列号{serialNumber}未查询到算法结果，无图片可加载");
                        return;
                    }

                    // 3. 遍历主结果，解析所有图像、POI标记、业务数据
                    foreach (var masterResult in algResults)
                    {
                        if (masterResult == null) continue;
                        AlgorithmResultType resultType = (AlgorithmResultType)masterResult.ImgFileType;
                        int masterResultId = masterResult.Id;

                        // 3.1 记录主分析图基础信息（ImgFileType 42-45）
                        RecordMainAnalysisImageInfo(masterResult, ref mainResultImageFile, ref testTime);

                        // 3.2 加载【拼接后处理图】OLED_CombineQuaterImages
                        LoadCombineQuaterImage(masterResult, ref imageId);

                        // 3.3 加载【像素重建处理图】OLED_RebuildPixelsMem（关联明细表）
                        LoadRebuildPixelsImage(masterResultId, ref imageId);

                        // 3.4 解析【POI标记】POI_Y（用于图像标注）
                        ParsePOIMarkers(masterResultId, resultType, ref poiMarkers);

                        // 3.5 解析【POI分析数据】PoiAnalysis（亮度均匀性）
                        ParsePoiAnalysisData(masterResultId, resultType, chipData, ref brightnessUniformityText);

                        // 3.6 加载【原始相机图】OLED_FindDotsArrayOutFile（区分.cvraw后缀）
                        LoadOriginalCameraImage(masterResult, resultType, ref imageId);
                    }

                    // 4. UI线程操作：渲染POI标记+亮度均匀性、更新主分析图
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RenderPoiAndBrightnessImage(mainResultImageFile, brightnessUniformityText, poiMarkers);
                    });

                    logger.Info($"LoadImageResult：序列号{serialNumber}图片加载完成，共处理{imageId - 1}张图片");
                }
                catch (Exception ex)
                {
                    logger.Error($"LoadImageResult：加载序列号{serialNumber}图片失败", ex);
                    // 异常不中断程序，仅打印日志
                }
            });
        }

        #region 私有辅助方法（职责单一，便于维护和调试）
        /// <summary>
        /// 记录主分析图信息（ImgFileType 42-45）
        /// </summary>
        private void RecordMainAnalysisImageInfo(VScgdAlgorithmResultMaster masterResult, ref string? mainImageFile, ref DateTime? testTime)
        {
            if (masterResult.ImgFileType >= 42 && masterResult.ImgFileType <= 45
                && !string.IsNullOrEmpty(masterResult.ImgFile) && File.Exists(masterResult.ImgFile))
            {
                mainImageFile = masterResult.ImgFile;
                testTime = masterResult.CreateDate;
            }
        }

        /// <summary>
        /// 加载【拼接后处理图】OLED_CombineQuaterImages
        /// </summary>
        private void LoadCombineQuaterImage(VScgdAlgorithmResultMaster masterResult, ref int imageId)
        {
            AlgorithmResultType resultType = (AlgorithmResultType)masterResult.ImgFileType;
            if (resultType == AlgorithmResultType.OLED_CombineQuaterImages
                && !string.IsNullOrEmpty(masterResult.ImgResult) && File.Exists(masterResult.ImgResult))
            {
                AddImageToVM(imageId++, masterResult.ImgResult, isRawImage: false);
            }
        }

        /// <summary>
        /// 加载【像素重建处理图】OLED_RebuildPixelsMem（从POI明细表获取）
        /// </summary>
        private void LoadRebuildPixelsImage(int masterResultId, ref int imageId)
        {
            var poiDetails = AlgResultService.GetPOIDetailResultFileByPid(masterResultId);
            if (poiDetails == null || poiDetails.Count == 0) return;

            var detail = poiDetails[0];
            if (!string.IsNullOrEmpty(detail.FileUrl) && File.Exists(detail.FileUrl)
                && !Path.GetFileName(detail.FileUrl).ToLower().Equals("po.dat")) // 过滤po.dat无效文件
            {
                AddImageToVM(imageId++, detail.FileUrl, isRawImage: false);
            }
        }

        /// <summary>
        /// 解析【POI标记】POI_Y（圆形/矩形标记，用于图像标注）
        /// </summary>
        private void ParsePOIMarkers(int masterResultId, AlgorithmResultType resultType, ref List<POIMarker> poiMarkers)
        {
            if (resultType != AlgorithmResultType.POI_Y) return;

            var poiDetails = AlgResultService.GetPOIDetailResult(masterResultId);
            if (poiDetails == null || poiDetails.Count == 0) return;

            foreach (var poi in poiDetails)
            {
                if (poi.PoiX == null || poi.PoiY == null || poi.PoiWidth == null || poi.PoiHeight == null) continue;

                // 0=圆形标记，1=矩形标记
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
        }

        /// <summary>
        /// 解析【POI分析数据】PoiAnalysis（获取亮度均匀性值，更新chipData和显示文本）
        /// </summary>
        private void ParsePoiAnalysisData(int masterResultId, AlgorithmResultType resultType, ChipData? chipData, ref string? brightnessText)
        {
            if (resultType != AlgorithmResultType.PoiAnalysis || chipData == null) return;

            var commDetails = AlgResultService.GetCommDetailResult(masterResultId);
            if (commDetails == null || commDetails.Count == 0) return;

            var commDetail = commDetails[0];
            if (string.IsNullOrEmpty(commDetail.Result)) return;

            // 解析明细表的Result字段，获取分析文件路径
            var detailFile = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(commDetail.Result);
            if (detailFile == null || string.IsNullOrEmpty(detailFile.ResultFileName) || !File.Exists(detailFile.ResultFileName))
            {
                logger.Warn($"PoiAnalysis分析文件不存在：{detailFile?.ResultFileName}");
                return;
            }

            // 解析分析文件，获取亮度值并更新显示文本
            string poiAnalysisJson = File.ReadAllText(detailFile.ResultFileName);
            var poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(poiAnalysisJson);
            if (poiAnalysis != null && poiAnalysis.result != null)
            {
                chipData.DataValue = poiAnalysis.result.Value;
                brightnessText = $"[{chipData.Row},{chipData.Column}]={chipData.DataValue:F4}";
            }
        }

        /// <summary>
        /// 加载【原始相机图】OLED_FindDotsArrayOutFile（.cvraw后缀归为原始图）
        /// </summary>
        private void LoadOriginalCameraImage(VScgdAlgorithmResultMaster masterResult, AlgorithmResultType resultType, ref int imageId)
        {
            if (resultType != AlgorithmResultType.OLED_FindDotsArrayOutFile || string.IsNullOrEmpty(masterResult.ImgFile)) return;

            bool isRawImage = Path.GetExtension(masterResult.ImgFile).ToLower() == ".cvraw";
            if (File.Exists(masterResult.ImgFile))
            {
                AddImageToVM(imageId++, masterResult.ImgFile, isRawImage);
            }
        }

        /// <summary>
        /// 通用添加图片到VM方法（后台线程调用，内部自动切回UI线程）
        /// 统一处理：文件校验、ImageItem创建、VM集合更新
        /// </summary>
        /// <param name="id">图片唯一ID</param>
        /// <param name="imgFile">图片文件路径</param>
        /// <param name="isRawImage">是否为原始相机图（true=OriginalImageResults，false=ProcessedImageResults）</param>
        private void AddImageToVM(int id, string imgFile, bool isRawImage)
        {
            // 最终UI更新必须切回主线程
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!File.Exists(imgFile))
                {
                    logger.Warn($"图片文件不存在，跳过添加：{imgFile}");
                    return;
                }
                string ext = Path.GetExtension(imgFile).ToLower();
                // 过滤po/po.dat文件（全局无效文件）
                string fileName = Path.GetFileNameWithoutExtension(imgFile).ToLower();
                if (ext == ".dat" || fileName.Contains("po.dat") || fileName.Contains("pos.dat"))
                {
                    Debug.WriteLine($"跳过无效数据文件：{imgFile}");
                    return;
                }

                // 创建ImageItem并赋值基础属性
                var imageItem = new ImageItem(id)
                {
                    FileName = Path.GetFileName(imgFile),
                    ImagePath = imgFile,
                    FileSizeMB = new FileInfo(imgFile).Length / (1024.0 * 1024.0),
                    Status = "Ready"
                };

                // 区分原始图/处理图，添加到对应VM集合
                if (isRawImage)
                {
                    CustomImageVM?.AddOriginalImageOnly(imageItem);
                }
                else
                {
                    CustomImageVM?.AddImage(imageItem);
                }

                logger.Debug($"成功添加图片到VM：{imageItem.FileName}（类型：{(isRawImage ? "原始图" : "处理图")}）");
            });
        }

        /// <summary>
        /// UI线程操作：渲染POI标记+亮度均匀性到主分析图
        /// </summary>
        private void RenderPoiAndBrightnessImage(string? mainImageFile, string? brightnessText, List<POIMarker> poiMarkers)
        {
            if (string.IsNullOrEmpty(mainImageFile) || string.IsNullOrEmpty(brightnessText) || CustomImageVM == null)
            {
                return;
            }

            OpenCvSharp.Mat? cvImage = null;
            try
            {
                // 加载图片并转换为8UC3格式（OpenCV通用显示格式）
                if (CVImageFileUtil.LoadImgFile(mainImageFile, ref cvImage) && cvImage != null)
                {
                    cvImage = OpenCvMatTools.ConvertImageTo8UC3(cvImage);
                    // 更新VM的POI图像（带标记和亮度文本）
                    CustomImageVM.UpdatePOIImage(cvImage, poiMarkers, brightnessText);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"渲染POI标记+亮度均匀性图片失败：{mainImageFile}", ex);
            }
            finally
            {
                // 释放OpenCV Mat资源，避免内存泄漏
                cvImage?.Dispose();
            }
        }
        #endregion
    }
}
