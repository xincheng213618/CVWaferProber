using CVAVMControl;
using CVDB.Services.Image;
using CVWaferProber.Core.Events;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using Newtonsoft.Json;
using System.Windows.Threading; // WPF用这个，WinForm替换为 System.Windows.Forms

namespace CVWaferProber.Services
{
    public class VAMService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(VAMService));
        private readonly CVVAMAnalyzer _cVVAMAnalyzer;
        // 保存UI主线程的同步上下文，用于后台线程切回UI线程（核心）
        private readonly SynchronizationContext _uiSyncContext;
        // WPF专属：若用WinForm，注释这个，保留上面的SynchronizationContext即可
        private readonly Dispatcher _uiDispatcher;

        public VAMService(MainViewModel mainVM, IFlowService flowService, CVVAMAnalyzer cVVAMAnalyzer)
            : base(mainVM, flowService, CVWPEventAggregatorInstance.Instance)
        {
            _cVVAMAnalyzer = cVVAMAnalyzer ?? throw new ArgumentNullException(nameof(cVVAMAnalyzer));
            // 初始化：在构造函数（主线程执行）中获取UI同步上下文
            _uiSyncContext = SynchronizationContext.Current;
            // WPF专属：获取UI主线程的Dispatcher
            _uiDispatcher = Dispatcher.CurrentDispatcher;
        }

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.FAILED;
        }

        // 核心修复：异步方法全程await，耗时操作后台执行，UI事件切回主线程
        protected override async Task<ChipStatus> FlowResultDisplayAsync(DieViewModel dieViewModel)
        {
            if (string.IsNullOrEmpty(dieViewModel.SerialNumber))
            {
                logger.Warn("VAM test Die serial number is empty, return failed directly");
                return ChipStatus.FAILED;
            }

            try
            {
                // 1. 耗时IO操作：丢到后台线程，避免阻塞UI
                var results = await Task.Run(() =>
                    ImageResultService.LoadCIEResultByBatchCode(dieViewModel.SerialNumber));

                if (results != null && results.Count == 1)
                {
                    var result = results[0];
                    if (result.ResultCode.HasValue && result.ResultCode.Value == 0)
                    {
                        string cieFileName = result.FileUrl;
                        logger.InfoFormat("VAM result cie => {0}", cieFileName);

                        // 2. 发布UI事件：切回UI主线程执行（核心修复，解决跨线程）
                        await RunOnUiThreadAsync(() =>
                            EventAggregator?.Publish(new VAMFlowCompletedEvent(cieFileName)));

                        // 3. 延迟1秒导出：异步延迟（不阻塞），导出事件仍切回UI线程
                        await Task.Delay(1000); // 替换ContinueWith，用await更安全
                        await RunOnUiThreadAsync(() =>
                            EventAggregator?.Publish(new VAMAutoExportCsvEvent
                            {
                                CvcieFilePath = cieFileName
                            }));

                        return ChipStatus.VAM_COMPLETED;
                    }
                    else
                    {
                        logger.ErrorFormat("VAM result is failed => {0}", JsonConvert.SerializeObject(result));
                    }
                }
                else
                {
                    logger.ErrorFormat("VAM result is empty or count > 1 => {0}",
                        results != null ? results.Count : 0);
                }
            }
            catch (Exception ex)
            {
                // 全局异常捕获：避免后台线程异常导致线程卡死，同时切回UI线程提示
                logger.Error("VAM FlowResultDisplay execution exception", ex);
                await RunOnUiThreadAsync(() =>
                    EventAggregator?.Publish(new VAMResultFailedEvent(ex.Message))); // 可新增失败事件，UI层提示
            }

            return ChipStatus.FAILED;
        }

        // 修复：异步方法加await，避免“火并忘”，确保线程有序
        public override async void ResultDisplay(DieViewModel dieViewModel)
        {
            if (!string.IsNullOrEmpty(dieViewModel.SerialNumber))
            {
                // 等待异步方法完成，避免线程混乱
                await FlowResultDisplayAsync(dieViewModel);
            }
            else
            {
                // 清空UI事件：直接切回UI线程
                RunOnUiThread(() => EventAggregator?.Publish(new VAMResultGUIClearEvent()));
            }
        }

        public override async Task StartTestingAsync(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool hasNext, bool tranStatus = true)
        {
            // 测试开始事件：UI操作，切回主线程
            RunOnUiThread(() =>
            {
                EventAggregator?.Publish(new VAMFlowStartingEvent());
                dieViewModel.ChangeStatus(ChipStatus.VAM_TESTING); // 状态更新是UI操作，必须主线程
            });

            // 核心：若RunFlowAsync是同步耗时方法，包裹成Task.Run异步执行（关键！）
            // 若基类RunFlowAsync已实现真正异步，直接await即可
            await Task.Run(() => RunFlowAsync(_selectedWPFlow, dieViewModel, hasNext, tranStatus));
        }

        public override void AutoExportData()
        {
            // 导出操作可能涉及UI，切回主线程执行
            RunOnUiThread(() => _cVVAMAnalyzer.BtnExportClick());
        }

        #region 核心工具方法：后台线程切回UI线程（WPF/WinForm通用）
        /// <summary>
        /// 同步执行：后台线程切回UI线程执行同步方法
        /// </summary>
        /// <param name="action">UI线程要执行的操作（发布事件、更新界面等）</param>
        private void RunOnUiThread(Action action)
        {
            if (action == null) return;

            // WPF优先用Dispatcher，WinForm用SynchronizationContext

            if (_uiDispatcher.CheckAccess())
            {
                action.Invoke(); // 已经是UI线程，直接执行
            }
            else
            {
                _uiDispatcher.Invoke(action); // 切回UI线程执行
            }
        }

        /// <summary>
        /// 异步执行：后台线程切回UI线程执行异步方法（适配await）
        /// </summary>
        /// <param name="action">UI线程要执行的异步操作</param>
        private async Task RunOnUiThreadAsync(Action action)
        {
            if (action == null) return;
            if (_uiDispatcher.CheckAccess())
            {
                action.Invoke();
            }
            else
            {
                // WPF异步切回UI线程，不阻塞后台线程
                await _uiDispatcher.InvokeAsync(action);
            }

        }
        #endregion
    }
}