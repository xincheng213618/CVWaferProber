using ChipMapping.ViewModels;
using ColorVision.Core.Entities;
using CVCommCore;
using CVDB.Services.Algorithm;
using CVDB.Services.Image;
using CVMysql;
using CVWaferProber.Config;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using CVWPFCamImageCtrl;
using CVWPFSpectrometerCtrl.ViewModels;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application = System.Windows.Application;

namespace CVWaferProber.Services
{
    public static class CVAlgorithmNative
    {
        //定义返回值枚举（根据CV_algorithm.dll实际定义调整）
        public enum CV_AliResType : int
        {
            /*        算法整体返回值说明：*/
            SUCCESS = 1,            //完全成功;
            FAILED = 0,             //失败;
            PART_SUCCESS = 2,       //部分成功（如计算不同类型的畸变）;
            ERR_LENGTH = -1,        //接收的内存长度不够;
            ERR_FILE = -2,          //结果存文件失败;
            ERR_JSON = -3           //JSON格式异常
        };
        private const string LIBRARY_CV_Ali = "CV_algorithm.dll";
        // 导入CV_algorithm.dll的核心接口
        [DllImport(LIBRARY_CV_Ali, EntryPoint = "CV_Ali_calcSingle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        
        public static extern CV_AliResType CV_Ali_calcSingle(
            IntPtr handle,                // 句柄（若无需句柄可传IntPtr.Zero，需确认dll要求）
             string staticJson,  // 输入JSON字符串
             StringBuilder result, // 输出结果缓冲区
            ref int resultLength          // 缓冲区长度（输入：缓冲区大小；输出：实际结果长度）
        );
        public static CV_AliResType CV_Ali_calcSingle(string staticJson, out string result)
        {
            // 初始缓冲区长度（可根据实际情况调整）
            int length = 512;
            StringBuilder bf = new StringBuilder(length);
            var res = CV_Ali_calcSingle(IntPtr.Zero, staticJson, bf, ref length);

            // 如果返回长度不足错误，扩容后重新调用
            if (res == CV_AliResType.ERR_LENGTH)
            {
                bf = new StringBuilder(length);
                res = CV_Ali_calcSingle(IntPtr.Zero, staticJson, bf, ref length);
            }

            result = bf.ToString();
            return res;
        }
    }

    public class AOIService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AOIService));
        // 标记当前是否已经完成测试（完成后切换Die一律批量加载）
        private bool _testCompletedForCurrentDie;
        private DieViewModel _currentDieVM;
        public CVCamImagerViewModel CustomImageVM { get; private set; }
        public CVSpectrumViewModel CustomIVLVM { get; private set; }
        public ChipMappingControlViewModel CustomMappingVM { get; private set; }
        public AOIService(MainViewModel mainVM, IFlowService flowService) : base(mainVM, flowService)
        {
            this.CustomImageVM = mainVM.CustomImageVM;
            this.CustomIVLVM = mainVM.CustomIVLVM;
            this.CustomMappingVM = mainVM.CustomMappingVM;
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




        #region 1
        //public override void AutoExportData(DieViewModel dieViewModel)
        //{
        //    string serialNumber = dieViewModel.SerialNumber ?? "Unknown";
        //    logger.InfoFormat("serialNumber => {0}", serialNumber);
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        CustomIVLVM.LoadSpectrumData(dieViewModel.SerialNumber);
        //    });

        //    //// 实现自动导出数据逻辑
        //    var Measurements = CustomIVLVM.Measurements;
        //    var Wavelengths = CustomIVLVM.Wavelengths;
        //    //if (Measurements == null || !Measurements.Any() || Wavelengths == null || Wavelengths.Length == 0)
        //    //{
        //    //    logger.Info("No valid IVL data available for export");
        //    //    return;
        //    //}

        //    // 读取全局配置的IVL导出路径
        //    string ivlRootPath = ConfigManager.Config.ExportPathSettings?.IvlExportPath ?? @"D:\Project\IVL";

        //    // 确保目录存在
        //    if (!Directory.Exists(ivlRootPath))
        //    {
        //        Directory.CreateDirectory(ivlRootPath);
        //        logger.Info($"Create IVL export directory：{ivlRootPath}");
        //    }
        //    // 构造文件名（包含SerialNumber+时间戳）
        //    string fileName = $"IVL_Data_{serialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        //    string fullExportPath = Path.Combine(ivlRootPath, fileName);
        //    // 执行导出
        //    CustomIVLVM.ExportToCsv(fullExportPath, Measurements, Wavelengths);
        //    logger.Info($"IVL data exported to：{fullExportPath}");
        //    #region aoi数据导出
        //    string aoiRootPath = ConfigManager.Config.ExportPathSettings?.AoiExportPath ?? @"D:\Project\AOI";

        //    // 确保目录存在
        //    if (!Directory.Exists(aoiRootPath))
        //    {
        //        Directory.CreateDirectory(aoiRootPath);
        //        logger.Info($"Create AOI export directory：{aoiRootPath}");
        //    }

        //    // 构造文件名（包含SerialNumber+时间戳）- 与AOI-3.csv格式类似
        //    string fileName1 = $"AOI_Data_{serialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        //    string fullExportPath1 = Path.Combine(aoiRootPath, fileName1);

        //    // 执行导出，格式与AOI-3.csv一致
        //    ExportAOICSV(fullExportPath1, Measurements, Wavelengths, dieViewModel);

        //    logger.Info($"AOI CSV data exported to：{fullExportPath1}");
        //    #endregion
        //}
        /// <summary>
        /// 导出AOI格式的CSV文件，与AOI-3.csv格式完全一致
        /// </summary>
        //private void ExportAOICSV(string filePath, ObservableCollection<SpectrumMeasurement> measurements, float[] wavelengths, DieViewModel dieViewModel)
        //{
        //    try
        //    {
        //        //if (measurements == null || !measurements.Any())
        //        //{
        //        //    logger.Warn("No measurement data to export");
        //        //    return;
        //        //}

        //        var csv = new StringBuilder();

        //        // ========== 1. 构建表头 - 与AOI-3.csv完全一致 ==========
        //        var headers = new List<string>
        //        {
        //            "No", "Die_x", "Die_y", "LightOnStatus", "RegisterPixels", "Final Class",
        //            "Pixel Logic", "AOI GradeLevel", "Defect Density(%)", "Black Pattern",
        //            "Uniformity", "Luminance(nit)", "Voltage(v)", "Current(mA)", "Temperature(℃)",
        //            "Measurement Time", "Pin Pressure", "TouchDown Counts", "Probing Card SN",
        //            "Lv(cd/m2)", "IP", "Excitation Purity(%)", "BlueLight", "cx", "cy",
        //            "u'", "v'", "CCT(K)", "Dominant Wavelength(nm)", "Saturation(%)",
        //            "Peak Wavelength(nm)", "FWHM", "Temperature(℃)"
        //        };

        //        // 添加波长表头 (380-780nm，每1nm一列)
        //        for (int wl = 380; wl <= 780; wl++)
        //        {
        //            headers.Add(wl.ToString());
        //        }

        //        csv.AppendLine(string.Join(",", headers));


        //        // ========== 2. 填充数据行 ==========
        //        int rowIndex = 1;
        //        foreach (var measurement in measurements)
        //        {
        //            var row = new List<string>();

        //            // 基本字段
        //            row.Add(rowIndex.ToString()); // No
        //            row.Add(dieViewModel.MapX.ToString()); // Die_x (使用当前Die的列)
        //            row.Add(dieViewModel.MapY.ToString()); // Die_y (使用当前Die的行)
        //            row.Add("OK"); // LightOnStatus (默认值)
        //            row.Add("OK"); // RegisterPixels (默认值)  
        //            row.Add("na"); // Final Class (根据测量结果判断)
        //            row.Add("OK"); // Pixel Logic (默认值)
        //            row.Add(string.IsNullOrEmpty(dieViewModel.AOIGradeLevel) ? "na" : dieViewModel.AOIGradeLevel); // AOI GradeLevel
        //            row.Add("2"); // Defect Density(%) (默认值)
        //            row.Add(string.IsNullOrEmpty(dieViewModel.AOIGradeLevel) ? "na" : dieViewModel.AOIGradeLevel); // Black Pattern
        //            row.Add("0"); // Uniformity measurement.Uniformity.ToString("F2")
        //            row.Add(measurement.Luminance.ToString("F0")); // Luminance(nit)
        //            row.Add(measurement.Voltage.ToString("F2")); // Voltage(v)
        //            row.Add(measurement.Current.ToString("F2")); // Current(mA)
        //            row.Add(DateTime.Now.ToString("yyyy/MM/dd")); // Measurement Time
        //            row.Add("na"); // Pin Pressure
        //            row.Add("na"); // TouchDown Counts
        //            row.Add("na"); // Probing Card SN
        //            row.Add(measurement.Luminance.ToString("F2")); // Lv(cd/m2)
        //            row.Add(measurement.IP); // IP
        //            row.Add(measurement.fPur != 0 ? (measurement.fPur * 100).ToString("F2") : "0"); // Excitation Purity(%)
        //            row.Add(measurement.Blue.ToString("F2")); // BlueLight
        //            row.Add(measurement.CIE_x.ToString("F6")); // cx
        //            row.Add(measurement.CIE_y.ToString("F6")); // cy
        //            row.Add(measurement.CIE_u.ToString("F6")); // u'
        //            row.Add(measurement.CIE_v.ToString("F6")); // v'
        //            row.Add(measurement.CCT.ToString("F1")); // CCT(K)
        //            row.Add(measurement.PeakWavelength.ToString("F2")); // Dominant Wavelength(nm)
        //            row.Add(measurement.fPur.ToString("F6")); // Saturation(%)
        //            row.Add(measurement.PeakWavelength.ToString("F1")); // Peak Wavelength(nm)
        //            row.Add(measurement.FHW.ToString("F1")); // FWHM
        //            row.Add($"{ChipMappingControlVM?.Temperatures:F1}"); // Temperature(℃)

        //            // ========== 3. 添加光谱数据 (380-780nm) ==========
        //            // 创建波长到强度值的映射字典
        //            Dictionary<int, double> spectralMap = new Dictionary<int, double>();

        //            if (measurement.Intensities != null && wavelengths != null)
        //            {
        //                // 将强度值映射到对应的波长
        //                for (int i = 0; i < Math.Min(wavelengths.Length, measurement.Intensities.Length); i++)
        //                {
        //                    int wl = (int)Math.Round(wavelengths[i]);
        //                    if (wl >= 380 && wl <= 780)
        //                    {
        //                        spectralMap[wl] = measurement.Intensities[i];
        //                    }
        //                }
        //            }

        //            // 填充380-780nm的光谱数据，每1nm一个值
        //            for (int wl = 380; wl <= 780; wl++)
        //            {
        //                if (spectralMap.ContainsKey(wl))
        //                {
        //                    double value = spectralMap[wl];
        //                    // 使用科学计数法格式化，与AOI-3.csv一致
        //                    row.Add(FormatScientific(value));
        //                }
        //                else
        //                {
        //                    row.Add("0");
        //                }
        //            }

        //            csv.AppendLine(string.Join(",", row));
        //            rowIndex++;
        //        }

        //        // 写入文件
        //        File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        //        logger.Info($"Successfully exported {measurements.Count} rows of AOI data to {filePath}");
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error($"Failed to export AOI CSV: {ex.Message}", ex);
        //    }
        //}
        #endregion

        #region 2
        public override void AutoExportData(DieViewModel dieViewModel)
        {
            // 检查Die状态，只有OK状态才导出数据吧
            if (dieViewModel.Status != ChipStatus.OK)
            {
                logger.Info($"Die {dieViewModel.SerialNumber} status is {dieViewModel.Status}, skip export");
                return;
            }
            string serialNumber = dieViewModel.SerialNumber ?? "Unknown";
            logger.InfoFormat("serialNumber => {0}", serialNumber);

            // ========== 核心修改：导出前先清空原有光谱数据 ==========
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (CustomIVLVM != null)
                {
                    // 清空Measurements和Wavelengths，确保数据隔离
                    CustomIVLVM.ClearResult();
                    // 重新加载当前die的光谱数据（仅加载当前die）
                    CustomIVLVM.LoadSpectrumData(dieViewModel.SerialNumber);
                }
            });

            // 尝试加载光谱数据，但不强制要求
            ObservableCollection<SpectrumMeasurement> measurements = null;
            float[] wavelengths = null;

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (CustomIVLVM != null)
                    {
                        measurements = CustomIVLVM.Measurements;
                        wavelengths = CustomIVLVM.Wavelengths;
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to load spectrum data, continuing with basic AOI data: {ex.Message}");
                // 继续执行，使用空的光谱数据
            }

            // 读取全局配置的IVL导出路径
            string ivlRootPath = ConfigManager.Config.ExportPathSettings?.AoiExportPath ?? @"D:\Project\AOI";
            if (!Directory.Exists(ivlRootPath))
            {
                Directory.CreateDirectory(ivlRootPath);
                logger.Info($"Create IVL export directory：{ivlRootPath}");
            }

            // 导出IVL数据（如果有）
            if (measurements != null && measurements.Any() && wavelengths != null && wavelengths.Length > 0)
            {
                string ivlFileName = $"IVL_Data_{serialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string ivlFullExportPath = Path.Combine(ivlRootPath, ivlFileName);
                CustomIVLVM.ExportToCsv(ivlFullExportPath, measurements, wavelengths);
                logger.Info($"IVL data exported to：{ivlFullExportPath}");
            }

            #region aoi数据导出
            string aoiRootPath = ConfigManager.Config.ExportPathSettings?.AoiExportPath ?? @"D:\Project\AOI";
            if (!Directory.Exists(aoiRootPath))
            {
                Directory.CreateDirectory(aoiRootPath);
                logger.Info($"Create AOI export directory：{aoiRootPath}");
            }

            string aoiFileName = $"AOI_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string aoiFullExportPath = Path.Combine(aoiRootPath, aoiFileName);

            // 判断文件是否已存在（决定是否写入表头）
            bool fileExists = File.Exists(aoiFullExportPath);

            // 执行导出，追加数据到现有文件或创建新文件
            ExportAOICSV(aoiFullExportPath, measurements, wavelengths, dieViewModel, !fileExists);
            logger.Info($"AOI CSV data exported to：{aoiFullExportPath}");
            #endregion
        }
        /// <summary>
        /// 追加AOI数据到CSV文件（而不是覆盖）
        /// </summary>
        private void ExportAOICSV(string filePath, ObservableCollection<SpectrumMeasurement> measurements,
            float[] wavelengths, DieViewModel dieViewModel, bool writeHeader)
        {
            try
            {
                var csv = new StringBuilder();

                // ========== 1. 如果需要写入表头 ==========
                if (writeHeader)
                {
                    var headers = new List<string>
                    {
                        "No", "Die_x", "Die_y", "LightOnStatus", "RegisterPixels", "Final Class",
                        "Pixel Logic", "AOI GradeLevel", "Defect Density(%)", "Black Pattern",
                        "Uniformity", "Luminance(nit)", "A_Voltage/V", "A_Current/mA","B_Voltage/V", "B_Current/mA",
                        "Measurement Time", "Pin Pressure", "TouchDown Counts", "Probing Card SN",
                        "Lv(cd/m2)", "IP", "Excitation Purity(%)", "BlueLight", "cx", "cy",
                        "u'", "v'", "CCT(K)", "Dominant Wavelength(nm)", "Saturation(%)",
                        "Peak Wavelength(nm)", "FWHM", "Temperature(℃)"
                    };

                    // 添加波长表头 (380-780nm，每1nm一列)
                    for (int wl = 380; wl <= 780; wl++)
                    {
                        headers.Add(wl.ToString());
                    }

                    csv.AppendLine(string.Join(",", headers));
                }

                // ========== 2. 判断是否有光谱数据 ==========
                bool hasSpectrumData = measurements != null && measurements.Any() && wavelengths != null && wavelengths.Length > 0;

                if (hasSpectrumData)
                {
                    logger.Info($"Appending AOI data with spectrum data, {measurements.Count} measurements");

                    // 获取当前文件中的最大行号
                    int startRowIndex = GetNextRowNumber(filePath);

                    // 有光谱数据：为每个测量点生成一行
                    int rowIndex = startRowIndex;
                    foreach (var measurement in measurements)
                    {
                        var row = GenerateDataRowWithSpectrum(measurement, dieViewModel, rowIndex++, wavelengths);
                        csv.AppendLine(string.Join(",", row));
                    }
                }
                else
                {
                    logger.Info("Appending AOI data without spectrum data");

                    // 获取当前文件中的最大行号
                    int startRowIndex = GetNextRowNumber(filePath);

                    // 无光谱数据：只生成一行基本数据
                    var row = GenerateDataRowWithoutSpectrum(dieViewModel, startRowIndex);
                    csv.AppendLine(string.Join(",", row));
                }

                // 追加写入文件（使用追加模式）
                File.AppendAllText(filePath, csv.ToString(), Encoding.UTF8);

                string dataType = hasSpectrumData ? "with spectrum" : "without spectrum";
                logger.Info($"Successfully appended AOI data {dataType} to {filePath}");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to append AOI CSV: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// 获取CSV文件中下一行的行号
        /// </summary>
        private int GetNextRowNumber(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return 1;
                }

                // 读取所有行
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length <= 1) // 只有表头或空文件
                {
                    return 1;
                }

                // 获取最后一行的第一列（No列）的值
                var lastLine = lines[lines.Length - 1];
                if (string.IsNullOrEmpty(lastLine))
                {
                    return 1;
                }

                var columns = lastLine.Split(',');
                if (columns.Length > 0 && int.TryParse(columns[0], out int lastRowNumber))
                {
                    return lastRowNumber + 1;
                }

                return lines.Length; // 如果解析失败，返回当前行数作为起始
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to get next row number: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// 导出AOI格式的CSV文件，兼容有无光谱数据的情况
        /// </summary>
        //private void ExportAOICSV(string filePath, ObservableCollection<SpectrumMeasurement> measurements,
        //    float[] wavelengths, DieViewModel dieViewModel)
        //{
        //    try
        //    {
        //        var csv = new StringBuilder();

        //        // ========== 1. 构建表头 - 与AOI-3.csv完全一致 ==========
        //        var headers = new List<string>
        //        {
        //            "No", "Die_x", "Die_y", "LightOnStatus", "RegisterPixels", "Final Class",
        //            "Pixel Logic", "AOI GradeLevel", "Defect Density(%)", "Black Pattern",
        //            "Uniformity", "Luminance(nit)", "Voltage(v)", "Current(mA)",
        //            "Measurement Time", "Pin Pressure", "TouchDown Counts", "Probing Card SN",
        //            "Lv(cd/m2)", "IP", "Excitation Purity(%)", "BlueLight", "cx", "cy",
        //            "u'", "v'", "CCT(K)", "Dominant Wavelength(nm)", "Saturation(%)",
        //            "Peak Wavelength(nm)", "FWHM", "Temperature(℃)"
        //        };

        //        // 添加波长表头 (380-780nm，每1nm一列)
        //        for (int wl = 380; wl <= 780; wl++)
        //        {
        //            headers.Add(wl.ToString());
        //        }

        //        csv.AppendLine(string.Join(",", headers));

        //        // ========== 2. 判断是否有光谱数据 ==========
        //        bool hasSpectrumData = measurements != null && measurements.Any() && wavelengths != null && wavelengths.Length > 0;

        //        if (hasSpectrumData)
        //        {
        //            logger.Info($"Exporting AOI data with spectrum data, {measurements.Count} measurements");
        //            // 有光谱数据：为每个测量点生成一行
        //            int rowIndex = 1;
        //            foreach (var measurement in measurements)
        //            {
        //                var row = GenerateDataRowWithSpectrum(measurement, dieViewModel, rowIndex++, wavelengths);
        //                csv.AppendLine(string.Join(",", row));
        //            }
        //        }
        //        else
        //        {
        //            logger.Info("Exporting AOI data without spectrum data");
        //            // 无光谱数据：只生成一行基本数据
        //            var row = GenerateDataRowWithoutSpectrum(dieViewModel, 1);
        //            csv.AppendLine(string.Join(",", row));
        //        }

        //        // 写入文件
        //        File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

        //        string dataType = hasSpectrumData ? "with spectrum" : "without spectrum";
        //        logger.Info($"Successfully exported AOI data {dataType} to {filePath}");
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error($"Failed to export AOI CSV: {ex.Message}", ex);
        //    }
        //}
        /// <summary>
        /// 生成包含光谱数据的行
        /// </summary>
        private List<string> GenerateDataRowWithSpectrum(SpectrumMeasurement measurement, DieViewModel dieViewModel, int rowIndex, float[] wavelengths)
        {
            var row = new List<string>();
            // 基础字段（严格32个，与表头顺序完全一致）
            row.Add(rowIndex.ToString()); // 1. No
            row.Add(dieViewModel.MapX.ToString()); // 2. Die_x
            row.Add(dieViewModel.MapY.ToString()); // 3. Die_y
            row.Add("OK"); // 4. LightOnStatus
            row.Add("OK"); // 5. RegisterPixels
            row.Add(dieViewModel.FinalClass ?? "na"); // 6. Final Class（修复：从dieViewModel获取，而非硬编码）
            row.Add("OK"); // 7. Pixel Logic
            row.Add(string.IsNullOrEmpty(dieViewModel.AOIGradeLevel) ? "na" : dieViewModel.AOIGradeLevel); // 8. AOI GradeLevel
            row.Add("2"); // 9. Defect Density(%)
            row.Add(string.IsNullOrEmpty(dieViewModel.BlackPattern) ? "na" : dieViewModel.BlackPattern); // 10. Black Pattern
            row.Add("na"); // 11. Uniformity
            row.Add(measurement.Luminance.ToString("F0")); // 12. Luminance(nit)
            row.Add(measurement.Voltage.ToString("F2")); // 13. Voltage(v)
            row.Add(measurement.Current.ToString("F2")); // 14. Current(mA)

            List<VScgdMeasureResultSmu> lists = MysqlControler.GetInstance().Sql.Select<VScgdMeasureResultSmu>().Where(a => a.BatchId == dieViewModel.Id).ToList();

            string b_v = "Na";
            string b_i = "Na";

            if (lists.Count == 2)
            {
                foreach (var item in lists)
                {
                    if (item.Channel == 1)
                    {
                        b_v = item.VResult?.ToString();
                        b_i = item.IResult?.ToString();
                    }
                }
            }
            row.Add(b_v);
            row.Add(b_i);


            row.Add(DateTime.Now.ToString("yyyy/MM/dd")); // 15. Measurement Time

            string pinPressure = dieViewModel.Pressure ?? "0,0,0,0"; // 16. Pin Pressure - 关键修改：用引号括起来
                                                                     // 如果值包含逗号，需要用引号括起来
            if (pinPressure.Contains(","))
            {
                row.Add($"\"{pinPressure}\"");
            }
            else
            {
                row.Add(pinPressure);
            }
            row.Add(dieViewModel.TouchDownCounts.ToString() ?? "0"); // 17. TouchDown Counts（空值处理）
            row.Add(dieViewModel.ProbingCardSN ?? "0"); // 18. Probing Card SN（空值处理）
            row.Add(measurement.Luminance.ToString("F2")); // 19. Lv(cd/m2)（修复：与无光谱数据格式统一）

            // 修复：处理可能包含逗号的字段，移除逗号并格式化
            row.Add(measurement.IP?.Replace(",", "") ?? "na"); // 20. IP（移除逗号）

            #region 兴奋纯度
            double purityValue = 0;
            // 从measurement获取CIE色坐标
            if (measurement.CIE_x > 0 && measurement.CIE_y > 0)
            {
                // 调用dll计算兴奋纯度
                purityValue = CalculateExcitationPurity(measurement.CIE_x, measurement.CIE_y);
                logger.Debug($"CIE({measurement.CIE_x}, {measurement.CIE_y}) => Purity: {purityValue}");
            }
            // 转为百分比（*100）并格式化，保留2位小数
            string purityPercent = purityValue > 0 ? (purityValue * 100).ToString("F2") : "0";
            row.Add(purityPercent); // 21. Excitation Purity(%) 兴奋纯度
            #endregion
            // row.Add(measurement.fPur != 0 ? (measurement.fPur * 100).ToString("F2") : "0"); // 21. Excitation Purity(%)
            row.Add(measurement.Blue.ToString("F2").Replace(",", "")); // 22. BlueLight（移除逗号+固定格式）
            row.Add(measurement.CIE_x.ToString("F6")); // 23. cx
            row.Add(measurement.CIE_y.ToString("F6")); // 24. cy
            row.Add(measurement.CIE_u.ToString("F6")); // 25. u'
            row.Add(measurement.CIE_v.ToString("F6")); // 26. v'
            row.Add(measurement.CCT.ToString("F1")); // 27. CCT(K)
            row.Add(measurement.PeakWavelength.ToString("F2")); // 28. Dominant Wavelength(nm)
            row.Add(measurement.fPur.ToString("F6")); // 29. Saturation(%)
            row.Add(measurement.PeakIntensity.ToString("F1")); // 30. Peak PeakIntensity(nm)
            row.Add(measurement.FHW.ToString("F1")); // 31. FWHM
            row.Add($"{CustomMappingVM?.Temperatures:F1}"); // 32. Temperature(℃)

            // 添加光谱数据（401个字段，与表头一致）
            AddSpectralData(row, measurement, wavelengths);

            return row;
        }
        /// <summary>
        /// 生成不包含光谱数据的行（只有基本字段）
        /// </summary>
        private List<string> GenerateDataRowWithoutSpectrum(DieViewModel dieViewModel, int rowIndex)
        {
            var row = new List<string>();

            // 基本字段 - 使用DieViewModel中的可用数据或默认值
            row.Add(rowIndex.ToString()); // No
            row.Add(dieViewModel.MapX.ToString()); // Die_x
            row.Add(dieViewModel.MapY.ToString()); // Die_y
            row.Add("OK"); // LightOnStatus
            row.Add("OK"); // RegisterPixels
            row.Add(dieViewModel.FinalClass ?? "na"); // Final Class
            row.Add("OK"); // Pixel Logic
            row.Add(string.IsNullOrEmpty(dieViewModel.AOIGradeLevel) ? "na" : dieViewModel.AOIGradeLevel); // AOI GradeLevel
            row.Add("na"); // Defect Density(%) - 默认值
            row.Add(string.IsNullOrEmpty(dieViewModel.BlackPattern) ? "na" : dieViewModel.BlackPattern); // Black Pattern
            row.Add("0.55"); // Uniformity - 默认值
            row.Add("na"); // Luminance(nit) - 默认值
            row.Add("na"); // Voltage(v)
            row.Add("na"); // Current(mA)
            row.Add(DateTime.Now.ToString("yyyy/MM/dd")); // Measurement Time
            string pinPressure = dieViewModel.Pressure ?? "0,0,0,0"; // 16. Pin Pressure - 关键修改：用引号括起来
                                                                     // 如果值包含逗号，需要用引号括起来
            if (pinPressure.Contains(","))
            {
                row.Add($"\"{pinPressure}\"");
            }
            else
            {
                row.Add(pinPressure);
            }
            row.Add(dieViewModel.TouchDownCounts.ToString() ?? "0"); // 17. TouchDown Counts（空值处理）
            row.Add(dieViewModel.ProbingCardSN ?? "0"); // 18. Probing Card SN（空值处理）
            row.Add("na"); // Lv(cd/m2) - 默认值
            row.Add("na"); // IP - 默认值
            row.Add("99"); // Excitation Purity(%) - 默认值 兴奋纯度
            row.Add("na"); // BlueLight - 默认值
            row.Add("0"); // cx - 默认值
            row.Add("0"); // cy - 默认值
            row.Add("0"); // u' - 默认值
            row.Add("0"); // v' - 默认值
            row.Add("na"); // CCT(K) - 默认值
            row.Add("na"); // Dominant Wavelength(nm) - 默认值
            row.Add("na"); // Saturation(%) - 默认值
            row.Add("na"); // Peak Wavelength(nm) - 默认值
            row.Add("na"); // FWHM - 默认值
            row.Add($"{CustomMappingVM?.Temperatures:F1}"); // Temperature(℃)

            // 添加空的光谱数据 (380-780nm 全部为0)
            for (int wl = 380; wl <= 780; wl++)
            {
                row.Add("0");
            }

            return row;
        }
        /// <summary>
        /// 添加光谱数据到行
        /// </summary>
        private void AddSpectralData(List<string> row, SpectrumMeasurement measurement, float[] wavelengths)
        {
            // 创建波长到强度值的映射字典
            Dictionary<int, double> spectralMap = new Dictionary<int, double>();

            if (measurement.Intensities != null && wavelengths != null)
            {
                // 将强度值映射到对应的波长
                for (int i = 0; i < Math.Min(wavelengths.Length, measurement.Intensities.Length); i++)
                {
                    int wl = (int)Math.Round(wavelengths[i]);
                    if (wl >= 380 && wl <= 780)
                    {
                        spectralMap[wl] = measurement.Intensities[i];
                    }
                }
            }

            // 填充380-780nm的光谱数据，每1nm一个值
            for (int wl = 380; wl <= 780; wl++)
            {
                if (spectralMap.ContainsKey(wl))
                {
                    double value = spectralMap[wl];
                    row.Add(FormatScientific(value));
                }
                else
                {
                    row.Add("0");
                }
            }
        }
        #endregion
        private string FormatScientific(double value)
        {
            if (Math.Abs(value) < 1e-6)
            {
                return value.ToString("E2", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                return value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
            }
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

        #region 调用CV_algorithm.dll计算光学兴奋纯度
        /// <summary>
        /// 调用CV_algorithm.dll计算光学兴奋纯度
        /// </summary>
        /// <param name="cieX">CIE色坐标x</param>
        /// <param name="cieY">CIE色坐标y</param>
        /// <returns>兴奋纯度（原始值，需*100转为百分比）</returns>
        private double CalculateExcitationPurity(double cieX, double cieY)
        {
            double excitationPurity = 0;

            try
            {
                // 记录输入参数，便于调试
                logger.Debug($"Calculating excitation purity for CIE_x={cieX}, CIE_y={cieY}");

                // 1. 构建输入JSON参数（严格按照接口文档）
                var inputParams = new
                {
                    type = 0,
                    Optics = new
                    {
                        cie_x = Math.Round(cieX, 6),  // 限制小数位数，避免精度问题
                        cie_y = Math.Round(cieY, 6)
                    }
                };

                string inputJson = JsonConvert.SerializeObject(inputParams);
                logger.Debug($"Input JSON: {inputJson}");

                // 2. 使用封装后的方法调用（核心修改）
                string resultJson;
                CVAlgorithmNative.CV_AliResType result = CVAlgorithmNative.CV_Ali_calcSingle(inputJson, out resultJson);

                // 3. 处理调用结果
                if (result == CVAlgorithmNative.CV_AliResType.SUCCESS)
                {
                    logger.Debug($"Result JSON: {resultJson}");

                    // 按照接口文档的格式解析JSON
                    var purityResult = JsonConvert.DeserializeObject<ExcitationPurityResult>(resultJson);

                    if (purityResult?.result?.ExcitationPurity != null)
                    {
                        excitationPurity = purityResult.result.ExcitationPurity.Value;
                        logger.Info($"计算兴奋纯度成功：{excitationPurity}（原始值）= {excitationPurity * 100:F2}%");
                    }
                    else
                    {
                        logger.Warn($"解析兴奋纯度结果失败：JSON格式不匹配。原始JSON：{resultJson}");

                        // 尝试直接解析为数值（作为备选方案）
                        if (double.TryParse(resultJson, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double directValue))
                        {
                            excitationPurity = directValue;
                            logger.Info($"使用直接数值解析成功：{excitationPurity}");
                        }
                    }
                }
                else
                {
                    logger.Error($"调用CV_algorithm.dll失败，错误码：{result}");

                    // 根据错误码提供更具体的错误信息
                    switch (result)
                    {
                        case CVAlgorithmNative.CV_AliResType.FAILED:
                            logger.Error("失败");
                            break;
                        case CVAlgorithmNative.CV_AliResType.PART_SUCCESS:
                            logger.Error($"部分成功（如计算不同类型的畸变）");
                            break;
                        case CVAlgorithmNative.CV_AliResType.ERR_LENGTH:
                            logger.Error("接收的内存长度不够");
                            break;
                        case CVAlgorithmNative.CV_AliResType.ERR_FILE:
                            logger.Error("结果存文件失败，扩容后仍失败");
                            break;
                        case CVAlgorithmNative.CV_AliResType.ERR_JSON:
                            logger.Error("JSON格式异常");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"计算兴奋纯度异常：{ex.Message}", ex);
            }

            return excitationPurity;
        }

        // 定义结果解析的DTO
        private class ExcitationPurityResult
        {
            public PurityResultDetail result { get; set; }
        }

        private class PurityResultDetail
        {
            [JsonProperty("ExcitationPurity")]
            public double? ExcitationPurity { get; set; }
        }
        #endregion

    }
}