//using CVWaferProber.Core.Models.Enums;
//using CVWaferProber.Core.Restful.DTO;
//using CVWaferProber.Utils;
//using CVWaferProber.ViewModels;
//using System.Reactive.Linq;
//using System.Reactive.Threading.Tasks;

//namespace CVWaferProber.Services
//{
//    public abstract class BaseSerivce
//    {
//        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseSerivce));

//        protected RCRestService rcService;

//        public string ProberId { get; set; }


//        public event EventHandler TestingCompleted;

//        public BaseSerivce(RCRestService rcService)
//        {
//            this.rcService = rcService;
//            this.ProberId = string.Empty;
//        }
//        protected async Task RunFlowAsync(WPFlowViewModel _selectedWPFlow, DieViewModel dieViewModel, bool isEnd = true)
//        {
//            try
//            {
//                Task<RespDataBaseFlowResultDTO> resp = AsyncRunFlow(_selectedWPFlow.Name, dieViewModel.SerialNumber, _selectedWPFlow.Timeout);
//                await resp;
//                if (resp.Result.IsSuccess)
//                {
//                    ChipStatus status = FlowResultDisplay(dieViewModel);
//                    dieViewModel.ChangeStatus(status, true);
//                }
//                else
//                {
//                    ChipStatus status = GetResultStatus(dieViewModel.SerialNumber);
//                    dieViewModel.ChangeStatus(status, true);
//                }
//            }
//            catch (OperationCanceledException)
//            {
//                // 处理取消操作
//                if (logger.IsDebugEnabled) logger.Debug("Flow execution was cancelled.");
//            }
//            catch (Exception ex)
//            {
//                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
//                // 处理其他异常
//                if (logger.IsDebugEnabled) logger.Debug($"Flow execution failed: {ex.Message}");
//                throw;
//            }
//            finally
//            {

//                if (isEnd) EndTesting();
//            }
//        }

//        protected abstract ChipStatus GetResultStatus(string serialNumber);
//        protected abstract ChipStatus FlowResultDisplay(DieViewModel dieViewModel);

//        protected async Task<RespDataBaseFlowResultDTO> AsyncRunFlow(string fname, string sn, int timeout)
//        {
//            CancellationTokenSource cancellationTokenSource;
//            if (timeout > 0) cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
//            else cancellationTokenSource = new CancellationTokenSource();
//            var cancellationToken = cancellationTokenSource.Token;

//            // 启动流程
//            rcService.RcRunFlowByName(fname, sn);

//            // 异步轮询结果，避免阻塞UI线程
//            return await PollFlowResultWithRxAsync(sn, cancellationToken);
//        }
//        protected async Task<RespDataBaseFlowResultDTO> PollFlowResultWithRxAsync(string sn, CancellationToken cancellationToken)
//        {
//            return await Observable.Interval(TimeSpan.FromSeconds(1))
//                 .Select(_ => rcService.RcGetFlowResult_AOI(sn))
//                 .Where(resp => resp != null)
//                 .FirstAsync(resp => (resp.IsSuccess && resp.Data.IsFinished) || !resp.IsSuccess)
//                 .Select(resp =>
//                 {
//                     if (!resp.IsSuccess) throw new InvalidOperationException($"Flow execution failed: {resp.Message}");
//                     return resp.Data;
//                 })
//                 .ToTask(cancellationToken);
//        }

//        protected string BuildFlowSN(DieViewModel dieViewModel, string timestamp)
//        {
//            if (string.IsNullOrEmpty(ProberId)) return string.Format("{1}[{3},{4}]", ProberId, timestamp, Snowflake.Instance.NextSeqId(), dieViewModel.MapY, dieViewModel.MapX);
//            else return string.Format("{0}_{1}[{3},{4}]", ProberId, timestamp, Snowflake.Instance.NextSeqId(), dieViewModel.MapY, dieViewModel.MapX);
//        }
//        protected virtual void EndTesting()
//        {
//            TestingCompleted?.Invoke(this, EventArgs.Empty);
//        }

//    }
//}
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.Restful.DTO;
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

        public BaseSerivce(RCRestService rcService, IEventAggregator? eventAggregator = null)
        {
            this.rcService = rcService;
            this.ProberId = string.Empty;
            this.EventAggregator = eventAggregator;
        }

        public void StartTesting(string timestamp, DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool isEnd = true)
        {
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;
            StartTesting(dieViewModel, _selectedWPFlow, isEnd);
        }
        public abstract void StartTesting(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool isEnd = true);
        protected async Task RunFlowAsync(WPFlowViewModel _selectedWPFlow, DieViewModel dieViewModel, bool isEnd = true)
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
                if (isEnd) EndTesting();
            }
        }

        protected abstract ChipStatus GetResultStatus(string serialNumber);
        protected abstract ChipStatus FlowResultDisplay(DieViewModel dieViewModel);

        protected async Task<RespDataBaseFlowResultDTO> AsyncRunFlow(string fname, string sn, int timeout)
        {
            // 优化3：使用using包裹CancellationTokenSource，确保资源释放
            using var cancellationTokenSource = timeout > 0
                ? new CancellationTokenSource(TimeSpan.FromSeconds(timeout))
                : new CancellationTokenSource();

            var cancellationToken = cancellationTokenSource.Token;

            // 启动流程
            rcService.RcRunFlowByName(fname, sn);

            // 异步轮询结果，避免阻塞UI线程
            return await PollFlowResultWithRxAsync(sn, cancellationToken);
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
            // 修复：原格式化字符串的占位符索引错误（使用了3/4但参数只有0-4）
            if (string.IsNullOrEmpty(ProberId))
                return string.Format("{1}[{2},{3}]", ProberId, timestamp, dieViewModel.MapY, dieViewModel.MapX);
            else
                return string.Format("{0}_{1}[{2},{3}]", ProberId, timestamp, dieViewModel.MapY, dieViewModel.MapX);
        }

        protected virtual void EndTesting()
        {
            TestingCompleted?.Invoke(this, EventArgs.Empty);
        }

        public abstract void ResultDisplay(DieViewModel dieViewModel);
    }
} 
