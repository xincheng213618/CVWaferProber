using ChipMapping.ViewModels;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.Utils;
using CVWaferProber.ViewModels;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using WaferComm.Core;

namespace CVWaferProber.Services
{
    public abstract class BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseSerivce));

        protected RCRestService rcService;
        protected readonly IEventAggregator? EventAggregator;

        public string ProberId { get; set; }

        public event EventHandler TestingCompleted;
        public event EventHandler<DieViewModel> AutoTestingNextCompleted;

        public BaseSerivce(RCRestService rcService, IEventAggregator? eventAggregator = null)
        {
            this.rcService = rcService;
            this.ProberId = string.Empty;
            this.EventAggregator = eventAggregator;
        }

        public Task StartTesting(string timestamp, DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool hasNext)
        {
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;
            return StartTesting(dieViewModel, _selectedWPFlow, hasNext);
        }
        public abstract Task StartTesting(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool hasNext);
        protected async Task RunFlowAsync(WPFlowViewModel _selectedWPFlow, DieViewModel dieViewModel, bool hasNext)
        {
            try
            {
                // 优化：直接await异步方法，避免先赋值再await+访问Result的冗余写法
                var resp = await AsyncRunFlow(_selectedWPFlow.Name, dieViewModel.SerialNumber, _selectedWPFlow.Timeout);

                if (resp != null && resp.IsSuccess)
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
            // 优化1：优先捕获TaskCanceledException（超时/取消场景）
            catch (TaskCanceledException ex)
            {
                logger.Debug($"Flow execution was cancelled (timeout/cancel signal): {ex.Message}");
                // 超时/取消时标记为失败，也可根据业务定义专属状态（如ChipStatus.CANCELLED）
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
            }
            // 优化2：保留OperationCanceledException作为兜底
            catch (OperationCanceledException ex)
            {
                logger.Debug($"Flow operation was cancelled: {ex.Message}");
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
            }
            catch (InvalidOperationException ex)
            {
                logger.Error($"Flow execution failed (invalid operation): {ex.Message}", ex);
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                // 业务异常可选择不抛出，避免上层崩溃
                // throw; 
            }
            catch (Exception ex)
            {
                logger.Error($"Flow execution failed (unknown error): {ex.Message}", ex);
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                // 未知异常按需抛出，便于上层排查
                throw;
            }
            finally
            {
                if (hasNext) DoAutoTestingNextCompleted(dieViewModel);
                else DoEndTesting();
            }
        }

        private void DoAutoTestingNextCompleted(DieViewModel dieViewModel)
        {
            AutoTestingNextCompleted?.Invoke(this, dieViewModel);
        }

        protected abstract ChipStatus GetResultStatus(string serialNumber);
        protected abstract ChipStatus FlowResultDisplay(DieViewModel dieViewModel);

        protected async Task<RespDataBaseFlowResultDTO?> AsyncRunFlow(string fname, string sn, int timeout)
        {
            // 优化3：使用using包裹CancellationTokenSource，确保资源释放
            using var cancellationTokenSource = timeout > 0
                ? new CancellationTokenSource(TimeSpan.FromSeconds(timeout))
                : new CancellationTokenSource();

            var cancellationToken = cancellationTokenSource.Token;

            // 启动流程
            var resp = rcService.RcRunFlowByName(fname, sn);
            if (resp)
            {
                // 异步轮询结果，避免阻塞UI线程
                return await PollFlowResultWithRxAsync(sn, cancellationToken);
            }
            else
            {
                return await Task.FromResult<RespDataBaseFlowResultDTO?>(null);
            }
        }

        protected async Task<RespDataBaseFlowResultDTO> PollFlowResultWithRxAsync(string sn, CancellationToken cancellationToken)
        {
            // 优化4：添加TakeWhile+超时兜底，避免无限轮询；同时优化异常提示
            return await Observable.Interval(TimeSpan.FromSeconds(1))
                 // 取消时立即终止轮询
                 .TakeUntil(_ => cancellationToken.IsCancellationRequested)
                 .Select(_ =>
                 {
                     // 轮询中检测取消信号，提前终止
                     cancellationToken.ThrowIfCancellationRequested();
                     return rcService.RcGetFlowResult_AOI(sn);
                 })
                 // 过滤null结果，只处理有效响应
                 .Where(resp => resp != null)
                 // 终止条件：流程完成 或 接口调用失败
                 .FirstAsync(resp => (resp.IsSuccess && resp.Data.IsFinished) || !resp.IsSuccess)
                 .Select(resp =>
                 {
                     // 接口返回失败时抛出业务异常
                     if (!resp.IsSuccess)
                         throw new InvalidOperationException($"Flow execution failed: {resp.Message} (SN: {sn})");
                     return resp.Data;
                 })
                 // 绑定取消令牌，超时/取消时抛出TaskCanceledException
                 .ToTask(cancellationToken);
        }

        protected string BuildFlowSN(DieViewModel dieViewModel, string timestamp)
        {
            return SNBuilder.Build(ProberId, timestamp, dieViewModel);
        }
        public bool IsAutoExportData = true;
        protected virtual void DoEndTesting()
        {
            TestingCompleted?.Invoke(this, EventArgs.Empty);
            if (IsAutoExportData)
            {
                AutoExportData();
            }

           
        }
        public abstract void AutoExportData();
        public abstract void ResultDisplay(DieViewModel dieViewModel);
    }
} 
