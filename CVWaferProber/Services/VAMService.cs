using CVAVMControl;
using CVDB.Services.Image;
using CVWaferProber.Core.Events;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using Newtonsoft.Json;

namespace CVWaferProber.Services
{
    public class VAMService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(VAMService));
        CVVAMAnalyzer cVVAMAnalyzer = new CVVAMAnalyzer();
        public VAMService(RCRestService rcService) : base(rcService, CVWPEventAggregatorInstance.Instance)
        {
        }
        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.FAILED;
        }
        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            if (string.IsNullOrEmpty(dieViewModel.SerialNumber)) return ChipStatus.FAILED;

            var results = ImageResultService.LoadCIEResultByBatchCode(dieViewModel.SerialNumber);
            if (results != null && results.Count == 1)
            {
                var result = results[0];
                if (result.ResultCode.HasValue && result.ResultCode.Value == 0)
                {
                    string cieFileName = result.FileUrl;
                    logger.InfoFormat("VAM result cie => {0}", cieFileName);
                    EventAggregator?.Publish(new VAMFlowCompletedEvent(cieFileName));

                    // 2.延迟1秒后发布自动导出事件（确保文件加载完成）
                    Task.Delay(1000).ContinueWith(t =>
                    {
                        EventAggregator?.Publish(new VAMAutoExportCsvEvent
                        {
                            CvcieFilePath = cieFileName
                        });
                    });
                }
                else
                {
                    if (logger.IsErrorEnabled) logger.ErrorFormat("VAM result is failed => {0}", JsonConvert.SerializeObject(result));
                }
            }
            else
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("VAM result is empty or count > 1 => {0}", results != null ? results.Count : 0);
            }
            return ChipStatus.VAM_COMPLETED;
        }

        public override void ResultDisplay(DieViewModel dieViewModel)
        {
            if (!string.IsNullOrEmpty(dieViewModel.SerialNumber))
            {
                FlowResultDisplay(dieViewModel);
            }
            else
            {
                EventAggregator?.Publish(new VAMResultGUIClearEvent());
            }
        }

        public override async Task StartTestingAsync(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool hasNext, bool tranStatus = true)
        {
            //
            EventAggregator?.Publish(new VAMFlowStartingEvent());

            dieViewModel.ChangeStatus(ChipStatus.VAM_TESTING);
            await RunFlowAsync(_selectedWPFlow, dieViewModel, hasNext, tranStatus);
        }
      
        public override void AutoExportData()
        {
            cVVAMAnalyzer.BtnExportClick();
        }
     
    }
}
