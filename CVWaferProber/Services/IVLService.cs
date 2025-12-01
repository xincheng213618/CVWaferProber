using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Utils;
using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl.ViewModels;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace CVWaferProber.Services
{

    public class IVLService: ViewModelBase
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(IVLService));

        public string ProberId { get; set; }
        public bool IsIVLCameraEnabled { get; set; }
        //
        private RCRestService rcService;
        private CVSpectrumViewModel CustomIVLVM { get; set; }
        // 用于UI线程更新的上下文 + 定时器取消令牌
        private readonly SynchronizationContext _uiSyncContext;
        private IDisposable _realTimePollingDisposable;

        public event EventHandler TestingCompleted;
        public IVLService(CVSpectrumViewModel customIVLVM, RCRestService rcModel)
        {
            this.CustomIVLVM = customIVLVM;
            this.ProberId = string.Empty;
            this.rcService = rcModel;
            // 捕获UI线程的同步上下文（确保后续更新UI安全）
            _uiSyncContext = SynchronizationContext.Current;
        }

        public void StartTestingIVL(string timestamp,DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow)
        {
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;
            dieViewModel.ChangeStatus(ChipStatus.IVL_TESTING);
            CustomIVLVM.ClearResult();
            IsIVLCameraEnabled = _selectedWPFlow.FlowType == CVWaferProberFlowType.IVL_Camera;
            dieViewModel.IsIVLCameraEnabled = IsIVLCameraEnabled;
            if (IsIVLCameraEnabled) CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.IVLCamera;
            else CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.Spectrum;
            //Task.Factory.StartNew(() => RunIVLFlowAsync(_selectedFlow.Id, sn));
            Task task = RunIVLFlowAsync(_selectedWPFlow.Name, dieViewModel);
            // 启动流程 + 实时轮询数据
            Task.Run(() => RunIVLFlowWithRealTimePollingAsync(_selectedWPFlow.Name, dieViewModel));
        }

        // 新增：带实时轮询的流程执行方法
        private async Task RunIVLFlowWithRealTimePollingAsync(string fname, DieViewModel dieViewModel)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            try
            {
                // 1. 启动实时轮询（每500ms更新一次数据）
                StartRealTimeDataPolling(dieViewModel.SerialNumber, cts.Token);

                // 2. 执行IVL流程
                var flowResult = await AsyncRunIVLFlow(fname, dieViewModel.SerialNumber);
                if (flowResult.IsSuccess)
                {
                    // 流程结束后最终更新一次数据
                    UpdateDataOnUI(dieViewModel);
                    dieViewModel.ChangeStatus(ChipStatus.IVL_COMPLETED, true);
                }
                else
                {
                    dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                }
            }
            catch (OperationCanceledException)
            {
                logger.Debug("IVL Flow execution was cancelled.");
            }
            catch (Exception ex)
            {
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                logger.Debug($"IVL Flow execution failed: {ex.Message}");
                throw;
            }
            finally
            {
                // 停止实时轮询
                _realTimePollingDisposable?.Dispose();
                cts.Cancel();
                EndTesting();
            }
        }

        // 新增：启动实时数据轮询（Rx定时器）
        private void StartRealTimeDataPolling(string sn, CancellationToken cancellationToken)
        {
            // 每500ms轮询一次数据，并更新到UI
            _realTimePollingDisposable = Observable.Interval(TimeSpan.FromMilliseconds(500))
                .TakeUntil(_ => cancellationToken.IsCancellationRequested) // 流程结束后停止
                .ObserveOn(_uiSyncContext) // 切换到UI线程更新
                .Subscribe(
                    _ => UpdateDataOnUI(sn), // 轮询并更新数据
                    ex => logger.Error("Real-time polling error", ex)
                );
        }

        private void UpdateDataOnUI(string sn)
        {
            try
            {
                // 从数据库获取最新数据（复用原LoadData逻辑）
                CustomIVLVM.LoadData(sn, IsIVLCameraEnabled);
                // 触发图表刷新（如果ViewModel未自动刷新）
                CustomIVLVM.NotifyPropertyChanged(nameof(CustomIVLVM.Measurements));
                CustomIVLVM.NotifyPropertyChanged(nameof(CustomIVLVM.OverviewSpectralPlotModel));
            }
            catch (Exception ex)
            {
                logger.Warn("Failed to update real-time data", ex);
            }
        }
        private void UpdateDataOnUI(DieViewModel dieViewModel)
        {
            UpdateDataOnUI(dieViewModel.SerialNumber);
        }

        private string BuildFlowSN(DieViewModel dieViewModel, string timestamp)
        {
            if (string.IsNullOrEmpty(ProberId)) return string.Format("{1}[{3},{4}]", ProberId, timestamp, Snowflake.Instance.NextSeqId(), dieViewModel.MapY, dieViewModel.MapX);
            else return string.Format("{0}_{1}[{3},{4}]", ProberId, timestamp, Snowflake.Instance.NextSeqId(), dieViewModel.MapY, dieViewModel.MapX);
        }

        private async Task RunIVLFlowAsync(string fname, DieViewModel dieViewModel)
        {
            try
            {
                Task<RespDataBaseFlowResultDTO> resp = AsyncRunIVLFlow(fname, dieViewModel.SerialNumber);
                await resp;
                if (resp.Result.IsSuccess)
                {
                    IVLResultDisplay(dieViewModel);
                    dieViewModel.ChangeStatus(ChipStatus.IVL_COMPLETED, true);
                }
                else
                {
                    dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                }
            }
            catch (OperationCanceledException)
            {
                // 处理取消操作
                if (logger.IsDebugEnabled) logger.Debug("IVL Flow execution was cancelled.");
            }
            catch (Exception ex)
            {
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                // 处理其他异常
                if (logger.IsDebugEnabled) logger.Debug($"IVL Flow execution failed: {ex.Message}");
                throw;
            }
            finally
            {
                EndTesting();
            }
        }
        private void EndTesting()
        {
            TestingCompleted?.Invoke(this, EventArgs.Empty);
        }


        private async Task<RespDataBaseFlowResultDTO> AsyncRunIVLFlow(string fname, string sn)
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // 启动流程
            rcService.RcRunFlowByName(fname, sn);

            // 异步轮询结果，避免阻塞UI线程
            return await PollFlowResultWithRxAsync(sn, cancellationToken);
        }
        private async Task<RespDataBaseFlowResultDTO> PollFlowResultWithRxAsync(string sn, CancellationToken cancellationToken)
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

        private void IVLResultDisplay(DieViewModel dieViewModel)
        {
            CustomIVLVM.ClearResult();
            CustomIVLVM.LoadData(dieViewModel.SerialNumber, dieViewModel.IsIVLCameraEnabled);
        }
    }
}
