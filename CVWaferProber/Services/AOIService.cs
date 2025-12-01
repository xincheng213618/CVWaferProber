using CVCommCore;
using CVDB.Services.Algorithm;
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
            AOIResultDisplay(dieViewModel);
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
            var results = AlgResultService.LoadAlgResultByBatchCodeAndType(serialNumber, (int)CVResultType.Algorithm_OLED_AOI_ALL);
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
        private void AOIResultDisplay(DieViewModel dieViewModel)
        {
            CustomImageVM?.ClearImageResult();
            CustomImageVM?.LoadImageResult(dieViewModel.chipViewModel.ChipData, dieViewModel.SerialNumber);
        }
    }
}
