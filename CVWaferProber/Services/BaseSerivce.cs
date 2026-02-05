using ChipMapping.ViewModels;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.Models;
using CVWaferProber.Utils;
using CVWaferProber.ViewModels;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using WaferComm.Core;
using System.Windows;
using Application = System.Windows.Application;

namespace CVWaferProber.Services
{
    public abstract class BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseSerivce));

        protected RCRestService rcService;
        protected readonly IEventAggregator? EventAggregator;

        public string ProberId { get; set; }

        public event EventHandler<TestCompletedEventArgs> TestingCompleted;
        public event EventHandler<DieViewModel> AutoTestingNextCompleted;
        //public event EventHandler<DieViewModel> AutoTestingPaused;
     
        // 保留原方法为私有，避免子类直接调用
      
        public BaseSerivce(RCRestService rcService, IEventAggregator? eventAggregator = null)
        {
            this.rcService = rcService;
            this.ProberId = string.Empty;
            this.EventAggregator = eventAggregator;
        }

        public async Task StartTestingAsync(string timestamp, DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool hasNext, bool isAuto)
        {
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;
            await StartTestingAsync(dieViewModel, _selectedWPFlow, hasNext, isAuto);
        }
        public abstract Task StartTestingAsync(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool hasNext, bool isAuto);
        protected async Task RunFlowAsync(WPFlowViewModel _selectedWPFlow, DieViewModel dieViewModel, bool hasNext, bool isAuto)
        {
            try
            {
                // 阶段1：准备阶段 (0-20%)
                //UpdateProgress(dieViewModel, 10);

                // 阶段2：发送测试请求 (20-40%)
                var resp = rcService.RcRunFlowByName(_selectedWPFlow.Name, dieViewModel.SerialNumber);
               // UpdateProgress(dieViewModel, 30);

                if (resp)
                {
                    // 阶段3：等待测试执行 (40-80%)
                    //UpdateProgress(dieViewModel, 40);

                    var flowResult = await PollFlowResultWithRxAsync(dieViewModel.SerialNumber,
                        new CancellationTokenSource(TimeSpan.FromSeconds(_selectedWPFlow.Timeout)).Token);

                   // UpdateProgress(dieViewModel, 80);

                    // 阶段4：处理结果 (80-100%)
                    if (flowResult != null && flowResult.IsSuccess)
                    {
                        ChipStatus status = await FlowResultDisplay(dieViewModel);
                        dieViewModel.ChangeStatus(status, true);
                        //UpdateProgress(dieViewModel, 95);
                    }
                    else
                    {
                        ChipStatus status = GetResultStatus(dieViewModel.SerialNumber);
                        dieViewModel.ChangeStatus(status, true);
                        //UpdateProgress(dieViewModel, 95);
                    }

                   // UpdateProgress(dieViewModel, 100, "测试完成");
                }
                else
                {
                    logger.Error($"流程 {_selectedWPFlow.Name} 启动失败");
                    dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                    //UpdateProgress(dieViewModel, 100, "流程启动失败");
                }
            }
            catch (TaskCanceledException ex)
            {
                logger.Warn($"流程执行超时: {ex.Message}");
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                //UpdateProgress(dieViewModel, 100, "测试超时");
            }
            catch (Exception ex)
            {
                logger.Error($"流程执行失败: {ex.Message}", ex);
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                //UpdateProgress(dieViewModel, 100, $"执行失败: {ex.Message}");
                throw;
            }
            finally
            {
                if (logger.IsInfoEnabled)
                    logger.InfoFormat("Die测试结束: {0}/{1} => {2}",
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
        }

        protected abstract ChipStatus GetResultStatus(string serialNumber);
        //protected abstract ChipStatus FlowResultDisplay(DieViewModel dieViewModel);
        protected abstract Task<ChipStatus> FlowResultDisplay(DieViewModel dieViewModel);
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
        protected virtual void DoEndTesting(DieViewModel dieViewModel, bool isAuto)
        {
            TestingCompleted?.Invoke(this, new TestCompletedEventArgs(dieViewModel, isAuto));
            if (IsAutoExportData)
            {
                AutoExportData();
            }


        }
        public abstract void AutoExportData();
        public abstract void ResultDisplay(DieViewModel dieViewModel);
    }
}
