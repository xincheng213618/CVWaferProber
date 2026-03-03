using ChipMapping.ViewModels;
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Text;
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
        public ChipMappingControlViewModel ChipMappingControlVM { get; private set; }
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
            string serialNumber = dieViewModel.SerialNumber ?? "Unknown";
            logger.InfoFormat("serialNumber => {0}", serialNumber);

            // 尝试加载光谱数据，但不强制要求
            ObservableCollection<SpectrumMeasurement> measurements = null;
            float[] wavelengths = null;

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (CustomIVLVM != null)
                    {
                        CustomIVLVM.LoadSpectrumData(dieViewModel.SerialNumber);
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
            string ivlRootPath = ConfigManager.Config.ExportPathSettings?.IvlExportPath ?? @"D:\Project\IVL";
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

            string aoiFileName = $"AOI_Data_{serialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string aoiFullExportPath = Path.Combine(aoiRootPath, aoiFileName);

            // 执行导出，兼容有无光谱数据的情况
            ExportAOICSV(aoiFullExportPath, measurements, wavelengths, dieViewModel);
            logger.Info($"AOI CSV data exported to：{aoiFullExportPath}");
            #endregion
        }
        /// <summary>
        /// 导出AOI格式的CSV文件，兼容有无光谱数据的情况
        /// </summary>
        private void ExportAOICSV(string filePath, ObservableCollection<SpectrumMeasurement> measurements,
            float[] wavelengths, DieViewModel dieViewModel)
        {
            try
            {
                var csv = new StringBuilder();

                // ========== 1. 构建表头 - 与AOI-3.csv完全一致 ==========
                var headers = new List<string>
                {
                    "No", "Die_x", "Die_y", "LightOnStatus", "RegisterPixels", "Final Class",
                    "Pixel Logic", "AOI GradeLevel", "Defect Density(%)", "Black Pattern",
                    "Uniformity", "Luminance(nit)", "Voltage(v)", "Current(mA)", "Temperature(℃)",
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

                // ========== 2. 判断是否有光谱数据 ==========
                bool hasSpectrumData = measurements != null && measurements.Any() && wavelengths != null && wavelengths.Length > 0;

                if (hasSpectrumData)
                {
                    logger.Info($"Exporting AOI data with spectrum data, {measurements.Count} measurements");
                    // 有光谱数据：为每个测量点生成一行
                    int rowIndex = 1;
                    foreach (var measurement in measurements)
                    {
                        var row = GenerateDataRowWithSpectrum(measurement, dieViewModel, rowIndex++, wavelengths);
                        csv.AppendLine(string.Join(",", row));
                    }
                }
                else
                {
                    logger.Info("Exporting AOI data without spectrum data");
                    // 无光谱数据：只生成一行基本数据
                    var row = GenerateDataRowWithoutSpectrum(dieViewModel, 1);
                    csv.AppendLine(string.Join(",", row));
                }

                // 写入文件
                File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

                string dataType = hasSpectrumData ? "with spectrum" : "without spectrum";
                logger.Info($"Successfully exported AOI data {dataType} to {filePath}");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to export AOI CSV: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// 生成包含光谱数据的行
        /// </summary>
        private List<string> GenerateDataRowWithSpectrum(SpectrumMeasurement measurement,
            DieViewModel dieViewModel, int rowIndex, float[] wavelengths)
        {
            var row = new List<string>();

            // 基本字段
            row.Add(rowIndex.ToString()); // No
            row.Add(dieViewModel.MapX.ToString()); // Die_x
            row.Add(dieViewModel.MapY.ToString()); // Die_y
            row.Add("OK"); // LightOnStatus
            row.Add("OK"); // RegisterPixels (默认值)
            row.Add("0"); // Final Class
            row.Add("OK"); // Pixel Logic (默认值)
            row.Add(string.IsNullOrEmpty(dieViewModel.AOIGradeLevel) ? "na" : dieViewModel.AOIGradeLevel); // AOI GradeLevel
            row.Add("2"); // Defect Density(%) (默认值)
            row.Add(string.IsNullOrEmpty(dieViewModel.AOIGradeLevel) ? "na" : dieViewModel.AOIGradeLevel); // Black Pattern
            row.Add("0"); // Uniformity
            row.Add(measurement.Luminance.ToString("F0")); // Luminance(nit)
            row.Add(measurement.Voltage.ToString("F2")); // Voltage(v)
            row.Add(measurement.Current.ToString("F2")); // Current(mA)
            row.Add(DateTime.Now.ToString("yyyy/MM/dd")); // Measurement Time
            row.Add("na"); // Pin Pressure
            row.Add("na"); // TouchDown Counts
            row.Add("na"); // Probing Card SN
            row.Add(measurement.Luminance.ToString("F2")); // Lv(cd/m2)
            row.Add(measurement.IP); // IP
            row.Add(measurement.fPur != 0 ? (measurement.fPur * 100).ToString("F2") : "0"); // Excitation Purity(%)
            row.Add(measurement.Blue.ToString("F2")); // BlueLight
            row.Add(measurement.CIE_x.ToString("F6")); // cx
            row.Add(measurement.CIE_y.ToString("F6")); // cy
            row.Add(measurement.CIE_u.ToString("F6")); // u'
            row.Add(measurement.CIE_v.ToString("F6")); // v'
            row.Add(measurement.CCT.ToString("F1")); // CCT(K)
            row.Add(measurement.PeakWavelength.ToString("F2")); // Dominant Wavelength(nm)
            row.Add(measurement.fPur.ToString("F6")); // Saturation(%)
            row.Add(measurement.PeakWavelength.ToString("F1")); // Peak Wavelength(nm)
            row.Add(measurement.FHW.ToString("F1")); // FWHM
            row.Add($"{ChipMappingControlVM?.Temperatures:F1}"); // Temperature(℃)

            // 添加光谱数据
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
            row.Add("na"); // Uniformity - 默认值
            row.Add("na"); // Luminance(nit) - 默认值
            row.Add("na"); // Voltage(v)
            row.Add("na"); // Current(mA)
            row.Add(DateTime.Now.ToString("yyyy/MM/dd")); // Measurement Time
            row.Add("na"); // Pin Pressure
            row.Add("na"); // TouchDown Counts
            row.Add("na"); // Probing Card SN
            row.Add("na"); // Lv(cd/m2) - 默认值
            row.Add("na"); // IP - 默认值
            row.Add("99"); // Excitation Purity(%) - 默认值 兴奋纯度
            row.Add("na"); // BlueLight - 默认值
            row.Add("na"); // cx - 默认值
            row.Add("na"); // cy - 默认值
            row.Add("na"); // u' - 默认值
            row.Add("na"); // v' - 默认值
            row.Add("na"); // CCT(K) - 默认值
            row.Add("na"); // Dominant Wavelength(nm) - 默认值
            row.Add("na"); // Saturation(%) - 默认值
            row.Add("na"); // Peak Wavelength(nm) - 默认值
            row.Add("na"); // FWHM - 默认值
            row.Add($"{ChipMappingControlVM?.Temperatures:F1}"); // Temperature(℃)

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
    }
}