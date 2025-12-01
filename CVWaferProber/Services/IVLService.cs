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
        // private IDisposable _flowTimerDisposable; // 管理定时器生命周期
        // private readonly SynchronizationContext _uiSyncContext; // 确保UI线程安全
        // 定时器相关字段（管理生命周期+UI线程安全）
        private IDisposable _realTimePollingDisposable; // 定时器订阅句柄
        private readonly SynchronizationContext _uiSyncContext; // UI线程上下文
        private string _currentSN; // 记录当前测试序列号（避免定时器参数丢失）
        
        public bool IsIVLCameraEnabled { get; set; }
        //
        private CVSpectrumViewModel CustomIVLVM { get; set; }

        public IVLService(CVSpectrumViewModel customIVLVM, RCRestService rcService) : base(rcService)
        {
            this.CustomIVLVM = customIVLVM;
            // _uiSyncContext = SynchronizationContext.Current; // 捕获UI线程上下文
            _uiSyncContext = SynchronizationContext.Current; // 捕获UI线程上下文
        }

        public void StartTestingIVL(string timestamp,DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow)
        {
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;
            _currentSN = sn; // 保存当前SN，供定时器使用

            dieViewModel.ChangeStatus(ChipStatus.IVL_TESTING);
            CustomIVLVM.ClearResult();

            IsIVLCameraEnabled = _selectedWPFlow.FlowType == CVWaferProberFlowType.IVL_Camera;
            dieViewModel.IsIVLCameraEnabled = IsIVLCameraEnabled;
            if (IsIVLCameraEnabled) CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.IVLCamera;
            else CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.Spectrum;
            //Task.Factory.StartNew(() => RunIVLFlowAsync(_selectedFlow.Id, sn));
            // StartFlowTimer(sn, dieViewModel);
            // 1. 启动实时数据轮询定时器（每500ms更新一次）
            StartRealTimePolling(dieViewModel);
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel);
            //_flowTimerDisposable?.Dispose(); // 释放定时器资源
        }

        private void StartRealTimePolling(DieViewModel dieViewModel)
        {
            // 先停止已有定时器（避免重复启动导致多次更新）
            StopRealTimePolling();

            // 定时器配置：每500ms执行一次，切换到UI线程，异常捕获
            _realTimePollingDisposable = Observable.Interval(TimeSpan.FromMilliseconds(500))
                .ObserveOn(_uiSyncContext) // 关键：切换到UI线程，避免跨线程操作控件
                .Subscribe(
                    _ => UpdateRealTimeData(dieViewModel), // 定时器触发：更新数据
                    ex => logger.Error("实时数据轮询异常", ex), // 捕获轮询异常
                    () => logger.Debug("实时数据轮询已停止") // 轮询正常终止回调
                );
        }

        private void UpdateRealTimeData(DieViewModel dieViewModel)
        {
            try
            {
                // 空序列号校验（避免无效查询）
                if (string.IsNullOrWhiteSpace(_currentSN))
                {
                    StopRealTimePolling();
                    return;
                }

                // 核心：调用ViewModel加载最新数据（复用原有LoadData逻辑，自动查询数据库）
                CustomIVLVM.LoadData(_currentSN, dieViewModel.IsIVLCameraEnabled);

                // 触发UI刷新（需CVSpectrumViewModel实现公共通知方法）
                CustomIVLVM.NotifyPropertyChanged(nameof(CustomIVLVM.Measurements));
                CustomIVLVM.NotifyPropertyChanged(nameof(CustomIVLVM.OverviewSpectralPlotModel));
                CustomIVLVM.NotifyPropertyChanged(nameof(CustomIVLVM.IVLCameraImageSrc));
                CustomIVLVM.NotifyPropertyChanged(nameof(CustomIVLVM.SpectralGridItems)); // 若有DataGrid，补充刷新
            }
            catch (Exception ex)
            {
                // 忽略单次更新失败，不影响后续轮询（避免测试中断）
                logger.Warn($"实时数据更新失败（不影响测试）：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 停止实时轮询（释放资源）
        /// </summary>
        private void StopRealTimePolling()
        {
            _realTimePollingDisposable?.Dispose(); // 释放定时器订阅
            _realTimePollingDisposable = null;
            _currentSN = null; // 清空当前SN
        }

        //private void StartFlowTimer(string sn, DieViewModel dieViewModel)
        //{
        //    // 每500ms执行一次操作（可调整间隔）
        //    _flowTimerDisposable = Observable.Interval(TimeSpan.FromMilliseconds(500))
        //        .ObserveOn(_uiSyncContext) // 切换到UI线程，避免跨线程异常
        //        .Subscribe(
        //            _ => ExecuteTimerTask(sn, dieViewModel), // 定时器触发的任务
        //            ex => logger.Error("定时器执行异常", ex), // 异常处理
        //            () => logger.Debug("定时器已停止") // 定时器终止回调
        //        );
        //}

        //private void ExecuteTimerTask(string sn, DieViewModel dieViewModel)
        //{
        //    try
        //    {
        //        // 示例：调用数据更新方法（复用之前的实时更新逻辑）
        //        CustomIVLVM.LoadData(sn, dieViewModel.IsIVLCameraEnabled);
        //        CustomIVLVM.NotifyPropertyChanged(nameof(CustomIVLVM.Measurements)); // 触发UI刷新
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Warn($"定时器任务执行失败：{ex.Message}", ex);
        //    }
        //}

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
            CustomIVLVM.ClearResult();
            CustomIVLVM.LoadData(dieViewModel.SerialNumber, dieViewModel.IsIVLCameraEnabled);
            return ChipStatus.IVL_COMPLETED;
        }
        /// <summary>
        /// 重写基类EndTesting（确保流程结束时停止定时器）
        /// </summary>
        protected override void EndTesting()
        {
            StopRealTimePolling(); // 流程结束，强制停止定时器
            base.EndTesting(); // 调用基类触发TestingCompleted事件
        }
    }
}
