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
                            // 解析 Common 表中的 Result 字段，获取 JSON 文件路径
                            var detailResult = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(resultJson);
                            if (detailResult != null && !string.IsNullOrEmpty(detailResult.ResultFileName) && File.Exists(detailResult.ResultFileName))
                            {
                                // 读取并解析 Darkresult.json 文件
                                string darkResultJson = File.ReadAllText(detailResult.ResultFileName);
                                var darkResult = JsonConvert.DeserializeObject<DarkResultDto>(darkResultJson);

                                // 提取 GradeLevel
                                dieViewModel.AOIGradeLevel = darkResult?.GradeLevel ?? "na";
                                dieViewModel.BlackPattern = darkResult?.GradeLevel ?? "na";
                            }
                        }
                        break;
                    }
                }
            }

            // 自动导出CSV结果
            // AutoExportCSVResult(dieViewModel, results);

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
        public override Task StartTesting(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool isEnd = true)
        {
            dieViewModel.ChangeStatus(ChipStatus.TESTING);
            CustomImageVM?.ClearImageResult();
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel, isEnd);
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
                FlowResultDisplay(dieViewModel);
            }
            //CustomImageVM?.ClearImageResult();
            //LoadImageResult(dieViewModel.chipViewModel!.ChipData, dieViewModel.SerialNumber!);
        }

        private void AddResultImage(int id, string imgFile, bool isRawImage = false)
        {
            if (!System.IO.File.Exists(imgFile)) return;

            // 检查是否是 po.dat 文件
            string fileName = Path.GetFileName(imgFile).ToLower();
            if (fileName.Equals("po") || fileName.Equals("po.dat"))
            {
                Debug.WriteLine($"Skipping po.dat file: {Path.GetFileName(imgFile)}");
                return;
            }

            ImageItem loc = new ImageItem(id)
            {
                FileName = Path.GetFileName(imgFile),
                ImagePath = imgFile,
                FileSizeMB = new FileInfo(imgFile).Length / (1024.0 * 1024.0),
                Status = "Ready"
            };

            // 根据图像类型添加到对应的集合
            if (isRawImage)
            {
                // 原始相机图 → 添加到 OriginalImageResults
                CustomImageVM?.AddOriginalImageOnly(loc);
            }
            else
            {
                // 标定后图 → 添加到 ProcessedImageResults
                CustomImageVM?.AddImage(loc);
            }
        }

        private void LoadImageResult(ChipData? chipData, string serialNumber)
        {
            // 后台线程处理图像加载逻辑
            Task.Run(() =>
            {
                string? resultImageFile = null;
                DateTime? TestTime = null;
                string? ImageDisplayBrightnessUniformity = null;
                var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
                if (results == null || results.Count == 0) return;
                List<POIMarker> POIMarkers = new List<POIMarker>();
                int id = 1;

                // 原有图像解析逻辑（无UI操作）
                foreach (var result in results)
                {
                    // 1. 先加载Analysis image（从TScgdAlgorithmResultDetailPoiCieFile获取）
                    LoadAnalysisImages(results, ref id, ref resultImageFile, ref TestTime);

                    // 2. 再加载Camera Measurement（从VScgdAlgorithmResultMaster获取关联的原图）
                    LoadCameraMeasurementImages(results, ref id);
                }

                // 仅UI更新操作切回UI线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(resultImageFile) && !string.IsNullOrEmpty(ImageDisplayBrightnessUniformity))
                    {
                        OpenCvSharp.Mat? image = null;
                        if (CVImageFileUtil.LoadImgFile(resultImageFile, ref image))
                        {
                            image = OpenCvMatTools.ConvertImageTo8UC3(image);
                            CustomImageVM?.UpdatePOIImage(image, POIMarkers, ImageDisplayBrightnessUniformity);
                        }
                    }

                    // 图像添加到ViewModel的操作也在UI线程
                    if (!string.IsNullOrEmpty(resultImageFile))
                        AddResultImage(id++, resultImageFile);
                });
            });
            //string? resultImageFile = null;
            //DateTime? TestTime = null;
            //string? ImageDisplayBrightnessUniformity = null;
            //var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
            //if (results == null || results.Count == 0) return;
            //List<POIMarker> POIMarkers = new List<POIMarker>();
            //int id = 1;
            //// 1. 先加载Analysis image（从TScgdAlgorithmResultDetailPoiCieFile获取）
            //LoadAnalysisImages(results, ref id, ref resultImageFile, ref TestTime);

            //// 2. 再加载Camera Measurement（从VScgdAlgorithmResultMaster获取关联的原图）
            //LoadCameraMeasurementImages(results, ref id);

            //// 3. 处理POI标记和亮度均匀性显示（原有逻辑保留）
            //if (!string.IsNullOrEmpty(resultImageFile) && !string.IsNullOrEmpty(ImageDisplayBrightnessUniformity))
            //{
            //    OpenCvSharp.Mat? image = null;
            //    if (!CVImageFileUtil.LoadImgFile(resultImageFile, ref image)) return;
            //    image = OpenCvMatTools.ConvertImageTo8UC3(image);
            //    CustomImageVM?.UpdatePOIImage(image, POIMarkers, ImageDisplayBrightnessUniformity);
            //}
            //if (!string.IsNullOrEmpty(resultImageFile) && !string.IsNullOrEmpty(ImageDisplayBrightnessUniformity))
            //{
            //    OpenCvSharp.Mat? image = null;
            //    if (!CVImageFileUtil.LoadImgFile(resultImageFile, ref image)) return;
            //    image = OpenCvMatTools.ConvertImageTo8UC3(image);
            //    CustomImageVM?.UpdatePOIImage(image, POIMarkers, ImageDisplayBrightnessUniformity);
            //}
            //foreach (var result in results)
            //{
            //    AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;
            //    if (result.ImgFileType >= 42 && result.ImgFileType <= 45)
            //    {
            //        resultImageFile = result.ImgFile;
            //        TestTime = result.CreateDate;
            //    }
            //    else if (resultType == AlgorithmResultType.OLED_CombineQuaterImages)
            //    {
            //        AddResultImage(id++, result.ImgResult);
            //    }
            //    else if (resultType == AlgorithmResultType.OLED_RebuildPixelsMem)
            //    {
            //        var details = AlgResultService.GetPOIDetailResultFileByPid(result.Id);
            //        if (details != null && details.Count == 1)
            //        {
            //            if (System.IO.File.Exists(details[0].FileUrl))
            //            {
            //                AddResultImage(id++, details[0].FileUrl);
            //            }
            //        }
            //    }
            //    else if (resultType == AlgorithmResultType.POI_Y)
            //    {
            //        var details = AlgResultService.GetPOIDetailResult(result.Id);
            //        foreach (var poi in details)
            //        {
            //            if (poi.PoiType == 0) POIMarkers.Add(new CircleMarker() { Label = poi.PoiName, X = (double)poi.PoiX, Y = (double)poi.PoiY, Width = (double)poi.PoiWidth, Height = (double)poi.PoiHeight, Fill = null });
            //            else if (poi.PoiType == 1) POIMarkers.Add(new RectangleMarker() { Label = poi.PoiName, X = (double)poi.PoiX, Y = (double)poi.PoiY, Width = (double)poi.PoiWidth, Height = (double)poi.PoiHeight, Fill = null });
            //        }
            //    }
            //    else if (resultType == AlgorithmResultType.PoiAnalysis)
            //    {
            //        var details = AlgResultService.GetCommDetailResult(result.Id);
            //        if (details != null && details.Count == 1)
            //        {
            //            DetailResult_CommFile_V2 detailResult_Comm = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(details[0].Result);
            //            if (System.IO.File.Exists(detailResult_Comm.ResultFileName))
            //            {
            //                //ImageItem imageResultViewModel = new ImageItem(id++);

            //                PoiAnalysis poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(System.IO.File.ReadAllText(detailResult_Comm.ResultFileName));
            //                chipData.DataValue = poiAnalysis.result.Value;
            //                ImageDisplayBrightnessUniformity = string.Format("[{0},{1}]={2:F4}", chipData.Row, chipData.Column, chipData.DataValue);
            //                //
            //                //imageResultViewModel.FileName = System.IO.Path.GetFileName(resultImageFile);
            //                //imageResultViewModel.ImagePath = resultImageFile;
            //                //imageResultViewModel.SerialNumber = serialNumber;
            //                //imageResultViewModel.TestTime = TestTime;
            //                //imageResultViewModel.ResultType = "数据提取";
            //                // 在UI线程更新集合
            //                //CustomImageVM?.AddImage(imageResultViewModel);

            //                if (!string.IsNullOrEmpty(resultImageFile)) AddResultImage(id++, resultImageFile);
            //            }
            //        }
            //    }
            //    //定位
            //    else if (resultType == AlgorithmResultType.OLED_FindDotsArrayOutFile)
            //    {
            //        //ImageItem loc = new ImageItem(id++);
            //        //loc.FileName = System.IO.Path.GetFileName(result.ImgFile);
            //        //loc.ImagePath = result.ImgFile;
            //        ////loc.ResultType = "定位";
            //        ////loc.SerialNumber = serialNumber;
            //        ////loc.TestTime = result.CreateDate;
            //        //// 在UI线程更新集合
            //        //CustomImageVM?.AddImage(loc);
            //       // AddResultImage(id++, result.ImgFile);
            //        bool isRawImage = Path.GetExtension(result.ImgFile).ToLower() == ".cvraw";
            //        AddResultImage(id++, result.ImgFile, isRawImage);

            //    }
            //}


        }
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
                if (!File.Exists(imgFile)) return;
                // 过滤掉 po.dat 文件
                string fileName = Path.GetFileName(imgFile).ToLower();
                if (fileName.Equals("po.dat") || fileName.Equals("po"))
                {
                    logger.Info($"Skipping po.dat file in Camera Measurement: {fileName}");
                    return;
                }

                ImageItem loc = new ImageItem(id);
                loc.FileName = Path.GetFileName(imgFile);
                loc.ImagePath = imgFile;
                loc.FileSizeMB = new FileInfo(imgFile).Length / (1024.0 * 1024.0);
                loc.Status = "Ready";
                CustomImageVM?.AddOriginalImageOnly(loc);
            });
            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //    ImageItem loc = new ImageItem(id);
            //    loc.FileName = Path.GetFileName(imgFile);
            //    loc.ImagePath = imgFile;
            //    // 直接添加到原始图像集合（包含所有类型）
            //    CustomImageVM?.AddOriginalImageOnly(loc);
            //});
        }
        public override void AutoExportData()
        {
            throw new NotImplementedException();
        }
    }
}
