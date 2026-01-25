using CVCommCore;
using CVCommCore.CVImage;
using CVDB.Services.Algorithm;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using CVWPFCamImageCtrl;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;

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
        public AOIService(RCRestService rcService) : this(new CVCamImagerViewModel(),rcService)
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
            //AOIResultDisplay(dieViewModel);
            //EventAggregator?.Publish(new EQEFlowCompletedEvent(results));
            LoadImageResult(dieViewModel.chipViewModel!.ChipData, dieViewModel.SerialNumber!);
            return ChipStatus.OK;
        }
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
                FlowResultDisplay(dieViewModel);
            }
            //CustomImageVM?.ClearImageResult();
            //LoadImageResult(dieViewModel.chipViewModel!.ChipData, dieViewModel.SerialNumber!);
        }

        private void AddResultImage(int id, string imgFile, bool isRawImage = false)
        {
            if (!System.IO.File.Exists(imgFile)) return;

            // 检查是否是 po.dat 文件
            string fileName = Path.GetFileNameWithoutExtension(imgFile).ToLower();
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
            string? resultImageFile = null;
            DateTime? TestTime = null;
            string? ImageDisplayBrightnessUniformity = null;
            var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
            if (results == null || results.Count == 0) return;
            List<POIMarker> POIMarkers = new List<POIMarker>();
            int id = 1;
            foreach (var result in results)
            {
                AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;
                if (result.ImgFileType >= 42 && result.ImgFileType <= 45)
                {
                    resultImageFile = result.ImgFile;
                    TestTime = result.CreateDate;
                }
                else if (resultType == AlgorithmResultType.OLED_CombineQuaterImages)
                {
                    AddResultImage(id++, result.ImgResult);
                }
                else if (resultType == AlgorithmResultType.OLED_RebuildPixelsMem)
                {
                    var details = AlgResultService.GetPOIDetailResultFileByPid(result.Id);
                    if (details != null && details.Count == 1)
                    {
                        if (System.IO.File.Exists(details[0].FileUrl))
                        {
                            AddResultImage(id++, details[0].FileUrl);
                        }
                    }
                }
                else if (resultType == AlgorithmResultType.POI_Y)
                {
                    var details = AlgResultService.GetPOIDetailResult(result.Id);
                    foreach (var poi in details)
                    {
                        if (poi.PoiType == 0) POIMarkers.Add(new CircleMarker() { Label = poi.PoiName, X = (double)poi.PoiX, Y = (double)poi.PoiY, Width = (double)poi.PoiWidth, Height = (double)poi.PoiHeight, Fill = null });
                        else if (poi.PoiType == 1) POIMarkers.Add(new RectangleMarker() { Label = poi.PoiName, X = (double)poi.PoiX, Y = (double)poi.PoiY, Width = (double)poi.PoiWidth, Height = (double)poi.PoiHeight, Fill = null });
                    }
                }
                else if (resultType == AlgorithmResultType.PoiAnalysis)
                {
                    var details = AlgResultService.GetCommDetailResult(result.Id);
                    if (details != null && details.Count == 1)
                    {
                        DetailResult_CommFile_V2 detailResult_Comm = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(details[0].Result);
                        if (System.IO.File.Exists(detailResult_Comm.ResultFileName))
                        {
                            //ImageItem imageResultViewModel = new ImageItem(id++);

                            PoiAnalysis poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(System.IO.File.ReadAllText(detailResult_Comm.ResultFileName));
                            chipData.DataValue = poiAnalysis.result.Value;
                            ImageDisplayBrightnessUniformity = string.Format("[{0},{1}]={2:F4}", chipData.Row, chipData.Column, chipData.DataValue);
                            //
                            //imageResultViewModel.FileName = System.IO.Path.GetFileName(resultImageFile);
                            //imageResultViewModel.ImagePath = resultImageFile;
                            //imageResultViewModel.SerialNumber = serialNumber;
                            //imageResultViewModel.TestTime = TestTime;
                            //imageResultViewModel.ResultType = "数据提取";
                            // 在UI线程更新集合
                            //CustomImageVM?.AddImage(imageResultViewModel);

                            if (!string.IsNullOrEmpty(resultImageFile)) AddResultImage(id++, resultImageFile);
                        }
                    }
                }
                //定位
                else if (resultType == AlgorithmResultType.OLED_FindDotsArrayOutFile)
                {
                    //ImageItem loc = new ImageItem(id++);
                    //loc.FileName = System.IO.Path.GetFileName(result.ImgFile);
                    //loc.ImagePath = result.ImgFile;
                    ////loc.ResultType = "定位";
                    ////loc.SerialNumber = serialNumber;
                    ////loc.TestTime = result.CreateDate;
                    //// 在UI线程更新集合
                    //CustomImageVM?.AddImage(loc);
                   // AddResultImage(id++, result.ImgFile);
                    bool isRawImage = Path.GetExtension(result.ImgFile).ToLower() == ".cvraw";
                    AddResultImage(id++, result.ImgFile, isRawImage);

                }
            }

            if (!string.IsNullOrEmpty(resultImageFile) && !string.IsNullOrEmpty(ImageDisplayBrightnessUniformity))
            {
                OpenCvSharp.Mat? image = null;
                if (!CVImageFileUtil.LoadImgFile(resultImageFile, ref image)) return;
                image = OpenCvMatTools.ConvertImageTo8UC3(image);
                CustomImageVM?.UpdatePOIImage(image, POIMarkers, ImageDisplayBrightnessUniformity);             
            }
        }

        public override void AutoExportData()
        {
            throw new NotImplementedException();
        }
    }
}
