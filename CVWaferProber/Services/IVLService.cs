using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl.ViewModels;
using System.Reactive.Linq;

namespace CVWaferProber.Services
{

    public class IVLService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(IVLService));
        // 类内新增字段
        private IDisposable _flowTimerDisposable; // 管理定时器生命周期
        private readonly SynchronizationContext _uiSyncContext; // 确保UI线程安全
        public bool IsIVLCameraEnabled { get; set; }
        //
        private CVSpectrumViewModel CustomIVLVM { get; set; }

        public IVLService(CVSpectrumViewModel customIVLVM, RCRestService rcService) : base(rcService)
        {
            this.CustomIVLVM = customIVLVM;
            _uiSyncContext = SynchronizationContext.Current; // 捕获UI线程上下文
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
            StartFlowTimer(sn, dieViewModel);
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel);
            _flowTimerDisposable?.Dispose(); // 释放定时器资源
        }

        private void StartFlowTimer(string sn, DieViewModel dieViewModel)
        {
            // 每500ms执行一次操作（可调整间隔）
            _flowTimerDisposable = Observable.Interval(TimeSpan.FromMilliseconds(500))
                .ObserveOn(_uiSyncContext) // 切换到UI线程，避免跨线程异常
                .Subscribe(
                    _ => ExecuteTimerTask(sn, dieViewModel), // 定时器触发的任务
                    ex => logger.Error("定时器执行异常", ex), // 异常处理
                    () => logger.Debug("定时器已停止") // 定时器终止回调
                );
        }

        private void ExecuteTimerTask(string sn, DieViewModel dieViewModel)
        {
            try
            {
                // 示例：调用数据更新方法（复用之前的实时更新逻辑）
                CustomIVLVM.LoadData(sn, dieViewModel.IsIVLCameraEnabled);
                CustomIVLVM.NotifyPropertyChanged(nameof(CustomIVLVM.Measurements)); // 触发UI刷新
            }
            catch (Exception ex)
            {
                logger.Warn($"定时器任务执行失败：{ex.Message}", ex);
            }
        }

        public void IVLResultDisplay(DieViewModel dieViewModel)
        {
            CustomIVLVM.ClearResult();
            CustomIVLVM.LoadData(dieViewModel.SerialNumber, dieViewModel.IsIVLCameraEnabled);
        }

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.FAILED;
        }

        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            IVLResultDisplay(dieViewModel);
            return ChipStatus.IVL_COMPLETED;
        }
    }
}
