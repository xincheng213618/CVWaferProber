using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Models;
using CVWaferProber.Utils;
using CVWaferProber.ViewModels;
using WaferComm.Core;

namespace CVWaferProber.Services
{
    public abstract class BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseSerivce));

        protected IFlowService _flowService;
        protected MainViewModel mainVM;
        protected readonly IEventAggregator? EventAggregator;

        public string ProberId { get; set; }

        public event EventHandler<TestCompletedEventArgs>? TestingCompleted;
        public event EventHandler<DieViewModel>? AutoTestingNextCompleted;
     
        // 保留原方法为私有，避免子类直接调用      
        public BaseSerivce(MainViewModel mainVM, IFlowService flowService, IEventAggregator? eventAggregator = null)
        {
            this.mainVM = mainVM;
            this._flowService = flowService;
            this.ProberId = string.Empty;
            this.EventAggregator = eventAggregator;
            this.TestingCompleted = null;
            this.AutoTestingNextCompleted = null;
        }

        public async Task StartTestingAsync(string timestamp, DieViewModel dieViewModel, WPFlowViewModel selectedWPFlow, bool hasNext, bool isAuto)
        {
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;
            await StartTestingAsync(dieViewModel, selectedWPFlow, hasNext, isAuto);
        }
        public abstract Task StartTestingAsync(DieViewModel dieViewModel, WPFlowViewModel selectedWPFlow, bool hasNext, bool isAuto);
        protected async Task RunFlowAsync(WPFlowViewModel selectedWPFlow, DieViewModel dieViewModel, bool hasNext, bool isAuto)
        {
            try
            {
                var response = await _flowService.FlowRunAndWaitResponseAsync(-1, selectedWPFlow.Name,
                    dieViewModel.SerialNumber, TimeSpan.FromSeconds(selectedWPFlow.Timeout));

                if (response != null)
                {

                    //var flowResult = await PollFlowResultWithRxAsync(dieViewModel.SerialNumber,
                    //    new CancellationTokenSource(TimeSpan.FromSeconds(_selectedWPFlow.Timeout)).Token);

                    //if (flowResult != null && flowResult.IsSuccess)
                    if (response.IsSuccess())
                    {
                        ChipStatus status = await FlowResultDisplayAsync(dieViewModel);
                        dieViewModel.ChangeStatus(status, true);

                    }
                    else
                    {
                        ChipStatus status = GetResultStatus(dieViewModel.SerialNumber);
                        dieViewModel.ChangeStatus(status, true);
                    }
                }
                else
                {
                    logger.Error($"Procedure {selectedWPFlow.Name} startup failed");
                    dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                }
            }
            catch (TaskCanceledException ex)
            {
                logger.Warn($"Procedure execution timed out: {ex.Message}");
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
            }
            catch (Exception ex)
            {
                logger.Error($"Procedure execution failed: {ex.Message}", ex);
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                throw;
            }
            finally
            {
                if (logger.IsInfoEnabled)
                    logger.InfoFormat("Die test completed: {0}/{1} => {2}",
                        dieViewModel.MapAxisToString(), dieViewModel.Status.ToString(), dieViewModel.SerialNumber);

                if (hasNext)
                    DoAutoTestingNextCompleted(dieViewModel);
                else
                    DoEndTesting(dieViewModel, isAuto);
            }
        }
     

        /// <summary>
        /// 更新进度
        /// </summary>

        public void DoAutoTestingNextCompleted(DieViewModel dieViewModel)
        {
            AutoTestingNextCompleted?.Invoke(this, dieViewModel);
            if (IsAutoExportData)
            {
                AutoExportData(dieViewModel);
            }
        }

        protected abstract ChipStatus GetResultStatus(string serialNumber);
        //protected abstract ChipStatus FlowResultDisplay(DieViewModel dieViewModel);
        protected abstract Task<ChipStatus> FlowResultDisplayAsync(DieViewModel dieViewModel);
        protected string BuildFlowSN(DieViewModel dieViewModel, string timestamp)
        {
            return SNBuilder.Build(ProberId, timestamp, dieViewModel);
        }
        public bool IsAutoExportData = true;
        protected virtual void DoEndTesting(DieViewModel dieViewModel, bool isAuto)
        {
            TestingCompleted?.Invoke(this, new TestCompletedEventArgs(dieViewModel, isAuto));
            if (IsAutoExportData)
            {
                AutoExportData(dieViewModel);
            }


        }
        public abstract void AutoExportData(DieViewModel dieViewModel);
        public abstract void ResultDisplay(DieViewModel dieViewModel);
    }
}
