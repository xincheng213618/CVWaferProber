using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl.ViewModels;
using System.Windows;

namespace CVWaferProber.Services
{
    public class EQEService : BaseSerivce
    {
        // 完全复刻IVL的日志命名规则
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(EQEService));

        // 缓存当前测试的DieViewModel（与IVL的_currentDieVM一致）
        private DieViewModel _currentDieVM;

        // EQE无需相机启用标记，移除IsEQECameraEnabled

        // EQE专属VM（对应IVL的CustomIVLVM）
        private CVEQEViewModel CustomEQEVM { get; set; }

        // 构造函数：完全复刻IVL，仅替换VM名称
        public EQEService(CVEQEViewModel customEQEVM, RCRestService rcService) : base(rcService)
        {
            this.CustomEQEVM = customEQEVM;
        }

        // 核心测试启动方法：移除所有Camera相关逻辑，保留IVL核心流程
        public void StartTestingEQE(string timestamp, DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow)
        {
            // 完全复用IVL的SN构建逻辑
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;

            // 标记EQE测试中（替换IVL的状态枚举）
            dieViewModel.ChangeStatus(ChipStatus.EQE_TESTING);
            // 清空EQE结果
            CustomEQEVM.ClearResult();

            // EQE固定切换到Spectrum Tab
            //CustomEQEVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.Spectrum;

            // 缓存当前DieVM
            _currentDieVM = dieViewModel;

            // 启动测试异步任务
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel);

            // 初始化刷新定时器
            //System.Timers.Timer refreshTimer = new System.Timers.Timer(350)
            //{
            //    AutoReset = true,
            //    Enabled = true
            //};

            //// 定时器回调：复刻IVL的UI线程调用逻辑，仅替换EQEResultDisplay
            //refreshTimer.Elapsed += (sender, e) =>
            //{
            //    Application.Current.Dispatcher.Invoke(() =>
            //    {
            //        try
            //        {
            //            EQEResultDisplay(_currentDieVM);
            //        }
            //        catch (Exception ex)
            //        {
            //            logger.Warn("定时器刷新EQE图表失败", ex);
            //        }
            //    });
            //};

            //// 测试结束停止定时器
            //task.ContinueWith(t =>
            //{
            //    refreshTimer.Enabled = false;
            //    refreshTimer.Dispose();
            //    logger.Debug("EQE测试流程结束，停止刷新定时器");
            //}, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // EQE结果展示方法：移除Camera参数，仅保留SerialNumber
        public void EQEResultDisplay(DieViewModel dieViewModel)
        {
            CustomEQEVM.ClearResult();
            // EQE仅需SerialNumber加载数据
            CustomEQEVM.LoadEQEData(dieViewModel.SerialNumber);
        }

        // 重写基类方法
        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.FAILED; // 与IVL保持一致
        }

        // 核心流程结果展示：移除Camera相关参数，替换为EQE逻辑
        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            EQEResultDisplay(dieViewModel);
            CustomEQEVM.ClearResult();
            CustomEQEVM.LoadEQEData(dieViewModel.SerialNumber);
            return ChipStatus.EQE_COMPLETED; // 替换为EQE完成状态
        }

        // 重写结束测试方法：与IVL完全一致
        protected override void EndTesting()
        {
            base.EndTesting(); // 调用基类触发TestingCompleted事件
        }
    }
}
