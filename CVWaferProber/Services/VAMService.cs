using CVAVMControl;
using CVWaferProber.Core.Events;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using Newtonsoft.Json;

namespace CVWaferProber.Services
{
    public class VAMService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(VAMService));

        public VAMService(RCRestService rcService) : base(rcService, CVWPEventAggregatorInstance.Instance)
        {
        }

        public CVVAMAnalyzer? VamAnalyzer { get; set; }

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.OK;
        }
        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            if (string.IsNullOrEmpty(dieViewModel.SerialNumber)) return ChipStatus.FAILED;

            var results = ImageResultService.LoadResultByBatchCode(dieViewModel.SerialNumber);
            if (results != null && results.Count == 1)
            {
                var result = results[0];
                if (result.ResultCode.HasValue && result.ResultCode.Value == 0)
                {
                    string cieFileName = result.FileUrl;
                    //TODO test
                    //cieFileName = "F:\\img\\晶圆台\\VAM\\test_ND0.cvcie";
                    logger.InfoFormat("VAM result cie => {0}", cieFileName);
                    EventAggregator?.Publish(new VAMFlowCompletedEvent(cieFileName));
                }
                else
                {
                    if (logger.IsErrorEnabled) logger.ErrorFormat("VAM result is failed => {0}", JsonConvert.SerializeObject(result));
                }
            }
            else
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("VAM result is enpty");
            }
            return ChipStatus.VAM_COMPLETED;
        }

        private string GetFlowResult(DieViewModel dieViewModel)
        {
            return string.Empty;
        }

        public void StartTestingVAM(string timestamp, DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow)
        {
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel);
        }
    }
}
