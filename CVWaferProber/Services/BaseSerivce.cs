using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.Utils;
using CVWaferProber.ViewModels;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace CVWaferProber.Services
{
    public abstract class BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseSerivce));

        protected RCRestService rcService;
        private int _overTimefRestapi = 30; //S

        public string ProberId { get; set; }


        public event EventHandler TestingCompleted;

        public BaseSerivce(RCRestService rcService)
        {
            this.rcService = rcService;
            this.ProberId = string.Empty;
        }
        protected async Task RunFlowAsync(WPFlowViewModel _selectedWPFlow, DieViewModel dieViewModel, bool isEnd = true)
        {
            try
            {
                Task<RespDataBaseFlowResultDTO> resp = AsyncRunFlow(_selectedWPFlow.Name, dieViewModel.SerialNumber);
                await resp;
                if (resp.Result.IsSuccess)
                {
                    ChipStatus status = FlowResultDisplay(dieViewModel);
                    dieViewModel.ChangeStatus(status, true);
                }
                else
                {
                    ChipStatus status = GetResultStatus(dieViewModel.SerialNumber);
                    dieViewModel.ChangeStatus(status, true);
                }
            }
            catch (OperationCanceledException)
            {
                // 处理取消操作
                if (logger.IsDebugEnabled) logger.Debug("Flow execution was cancelled.");
            }
            catch (Exception ex)
            {
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                // 处理其他异常
                if (logger.IsDebugEnabled) logger.Debug($"Flow execution failed: {ex.Message}");
                throw;
            }
            finally
            {
                if (isEnd) EndTesting();
            }
        }

        protected abstract ChipStatus GetResultStatus(string serialNumber);
        protected abstract ChipStatus FlowResultDisplay(DieViewModel dieViewModel);

        protected async Task<RespDataBaseFlowResultDTO> AsyncRunFlow(string fname, string sn)
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(_overTimefRestapi));
            var cancellationToken = cancellationTokenSource.Token;

            // 启动流程
            rcService.RcRunFlowByName(fname, sn);

            // 异步轮询结果，避免阻塞UI线程
            return await PollFlowResultWithRxAsync(sn, cancellationToken);
        }
        protected async Task<RespDataBaseFlowResultDTO> PollFlowResultWithRxAsync(string sn, CancellationToken cancellationToken)
        {
            return await Observable.Interval(TimeSpan.FromSeconds(1))
                 .Select(_ => rcService.RcGetFlowResult_AOI(sn))
                 .Where(resp => resp != null)
                 .FirstAsync(resp => (resp.IsSuccess && resp.Data.IsFinished) || !resp.IsSuccess)
                 .Select(resp =>
                 {
                     if (!resp.IsSuccess) throw new InvalidOperationException($"Flow execution failed: {resp.Message}");
                     return resp.Data;
                 })
                 .ToTask(cancellationToken);
        }

        protected string BuildFlowSN(DieViewModel dieViewModel, string timestamp)
        {
            if (string.IsNullOrEmpty(ProberId)) return string.Format("{1}[{3},{4}]", ProberId, timestamp, Snowflake.Instance.NextSeqId(), dieViewModel.MapY, dieViewModel.MapX);
            else return string.Format("{0}_{1}[{3},{4}]", ProberId, timestamp, Snowflake.Instance.NextSeqId(), dieViewModel.MapY, dieViewModel.MapX);
        }
        protected void EndTesting()
        {
            TestingCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}
