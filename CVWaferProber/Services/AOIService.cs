using CVCommCore;
using CVCommCore.CVImage;
using CVDB.Services.Algorithm;
using CVWaferProber.Core.Events;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using CVWPFCamImageCtrl;
using Newtonsoft.Json;

namespace CVWaferProber.Services
{
    public class AOIService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AOIService));

        public CVCamImagerViewModel CustomImageVM { get; set; }

        public AOIService(CVCamImagerViewModel customImageVM ,RCRestService rcService) : base(rcService)
        {
            this.CustomImageVM = customImageVM;
        }

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return GetDieResultStatus(serialNumber);
        }

        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            //AOIResultDisplay(dieViewModel);
            //EventAggregator?.Publish(new EQEFlowCompletedEvent(results));
            LoadImageResult(dieViewModel.chipViewModel!.ChipData, dieViewModel.SerialNumber!);
            return ChipStatus.OK;
        }
        public void StartTestingAOI(string timestamp, DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool isEnd = true)
        {
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;
            dieViewModel.ChangeStatus(ChipStatus.TESTING);
            CustomImageVM?.ClearImageResult();
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel, isEnd);
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
        public void AOIResultDisplay(DieViewModel dieViewModel)
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

        private void AddResultImage(int id,string imgFile)
        {
            ImageItem loc = new ImageItem(id);
            loc.FileName = System.IO.Path.GetFileName(imgFile);
            loc.ImagePath = imgFile;
            CustomImageVM?.AddImage(loc);
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
                    AddResultImage(id++, result.ImgFile);
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
    }
}
