using ColorVision.Core.Entities;
using CVDB.Services.Spectrum;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl.ViewModels;
using Newtonsoft.Json;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Threading;

namespace CVWaferProber.Services
{

    public class IVLService : BaseSerivce
    {

        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(IVLService));
        // 新增：缓存当前测试的DieViewModel（供定时器回调使用）
        private DieViewModel _currentDieVM;

        public bool IsIVLCameraEnabled { get; set; }
        //
        private CVSpectrumViewModel CustomIVLVM { get; set; }

        public IVLService(CVSpectrumViewModel customIVLVM, RCRestService rcService) : base(rcService)
        {
            this.CustomIVLVM = customIVLVM;

        }

        public void StartTestingIVL(string timestamp, DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow)
        {
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;

            dieViewModel.ChangeStatus(ChipStatus.IVL_TESTING);
            CustomIVLVM.ClearResult();


            IsIVLCameraEnabled = _selectedWPFlow.FlowType == CVWaferProberFlowType.IVL_Camera;
            dieViewModel.IsIVLCameraEnabled = IsIVLCameraEnabled;
            if (IsIVLCameraEnabled) CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.IVLCamera;
            else CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.Spectrum;
            // 缓存当前DieViewModel（定时器回调中需要用到）
            _currentDieVM = dieViewModel;
            //System.Timers.Timer timer = new System.Timers.Timer(1000);
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel);
            // 初始化并启动定时器（1秒调用一次IVLResultDisplay）
            System.Timers.Timer refreshTimer = new System.Timers.Timer(300)
            {
                AutoReset = true, // 自动重复触发（循环调用）
                Enabled = true    // 启动定时器
            };

            // 绑定定时器回调：每次触发都调用IVLResultDisplay
            refreshTimer.Elapsed += (sender, e) =>
            {
                // 关键：切换到UI线程执行（避免跨线程操作异常）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // 循环调用刷新方法（每次都会加载最新数据）
                        IVLResultDisplay(_currentDieVM);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("定时器刷新图表失败", ex);
                    }
                });
            };

            // 测试流程结束后，停止定时器（避免内存泄漏）
            task.ContinueWith(t =>
            {
                refreshTimer.Enabled = false;
                refreshTimer.Dispose();
                logger.Debug("测试流程结束，停止刷新定时器");
            }, TaskScheduler.FromCurrentSynchronizationContext());

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
            CustomIVLVM.ClearResult();
            CustomIVLVM.LoadData(dieViewModel.SerialNumber, dieViewModel.IsIVLCameraEnabled);
            return ChipStatus.IVL_COMPLETED;
        }
        /// <summary>
        /// 重写基类EndTesting（确保流程结束时停止定时器）
        /// </summary>
        protected void EndTesting()
        {

            base.EndTesting(); // 调用基类触发TestingCompleted事件
        }
    }
}
