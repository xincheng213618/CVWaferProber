using CVDB.Services.Image;
using CVWaferProber.Core.Events;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;

namespace CVWaferProber.Services
{
    public class VAMService : BaseSerivce
    {
        public VAMService(RCRestService rcService) : base(rcService, CVWPEventAggregatorInstance.Instance)
        {
        }

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.FAILED;
        }
        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            //string cieFileName = "D:\\work\\img\\test_ND0.cvcie";
            //EventAggregator?.Publish(new VAMFlowCompletedEvent(cieFileName));
            if (string.IsNullOrEmpty(dieViewModel.SerialNumber)) return ChipStatus.FAILED;

            var results = ImageResultService.LoadResultByBatchCode(dieViewModel.SerialNumber);
            if (results != null && results.Count == 1)
            {
                var result = results[0];
                if (result.ResultCode.HasValue && result.ResultCode.Value == 0)
                {
                    string cieFileName = result.FileUrl;
                    cieFileName = "D:\\work\\img\\test_ND0.cvcie";
                    EventAggregator?.Publish(new VAMFlowCompletedEvent(cieFileName));
                }
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

        public override Task StartTesting(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool isEnd = true)
        {
            //
            EventAggregator?.Publish(new VAMFlowStartingEvent());

            dieViewModel.ChangeStatus(ChipStatus.VAM_TESTING);
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel, isEnd);
            return task;
        }
    }
}
