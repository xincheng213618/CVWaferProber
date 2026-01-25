using CVDB.Services.Spectrum;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl.ViewModels;
using System.IO;

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
        public CVEQEViewModel CustomEQEVM { get; private set; }

        // 新增：全局配置对象（核心修改点1）
        private readonly GlobalConfigModel _globalConfig;

        // 构造函数：完全复刻IVL，仅替换VM名称
        public EQEService(CVEQEViewModel customEQEVM, RCRestService rcService) : base(rcService)
        {
            this.CustomEQEVM = customEQEVM;
        }
        public EQEService(RCRestService rcService) : this(new CVEQEViewModel(),rcService)
        {
        }

        // 核心测试启动方法：移除所有Camera相关逻辑，保留IVL核心流程
        public override Task StartTesting(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool hasNext)
        {
            // 标记EQE测试中（替换IVL的状态枚举）
            dieViewModel.ChangeStatus(ChipStatus.EQE_TESTING);
            // 清空EQE结果
            CustomEQEVM.ClearAllDisplays();

            // EQE固定切换到Spectrum Tab
            //CustomEQEVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.Spectrum;

            // 缓存当前DieVM
            _currentDieVM = dieViewModel;
           
            // 启动测试异步任务
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel, hasNext);

            return task;
        }
        // EQE结果展示方法：移除Camera参数，仅保留SerialNumber
        public override void ResultDisplay(DieViewModel dieViewModel)
        {
            if (string.IsNullOrEmpty(dieViewModel.SerialNumber))
            {
                CustomEQEVM.ClearResult();
                //EventAggregator?.Publish(new EQEResultGUIClearEvent());
                return;
            }
            else
            {
                FlowResultDisplay(dieViewModel);
            }
            //CustomEQEVM.ClearResult();
            //// EQE仅需SerialNumber加载数据
            //CustomEQEVM.LoadEQEData(dieViewModel.SerialNumber);
        }

        // 重写基类方法
        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.FAILED; // 与IVL保持一致
        }

        // 核心流程结果展示：移除Camera相关参数，替换为EQE逻辑
        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            var results = SpectrumResultService.LoadEQEResultByBatchCode(dieViewModel.SerialNumber);
            CustomEQEVM.ClearResult();
            CustomEQEVM.LoadEQEData(results);
            //EventAggregator?.Publish(new EQEFlowCompletedEvent(results));
            //EQEResultDisplay(dieViewModel);
            //CustomEQEVM.ClearResult();
            //CustomEQEVM.LoadEQEData(dieViewModel.SerialNumber);
            // 测试完成后自动触发导出
           
            return ChipStatus.EQE_COMPLETED; // 替换为EQE完成状态
        }

        // 重写结束测试方法：与IVL完全一致
        protected override void DoEndTesting()
        {
            base.DoEndTesting(); // 调用基类触发TestingCompleted事件
            
        }
        public override void AutoExportData()
        {
            try
            {
                // 1. 从CustomEQEVM获取必要数据（ViewModel中的公共属性）
                var measurements = CustomEQEVM.Measurements;
                var wavelengths = CustomEQEVM.Wavelengths;

                if (measurements == null || !measurements.Any() || wavelengths == null || wavelengths.Length == 0)
                {
                    logger.Warn("No valid EQE data available for export！");
                    return;
                }

                // 2. 读取全局配置的EQE导出路径（核心修改点2）
                // 优先级：配置路径 > 默认路径（保证降级兼容）
                string basePath = _globalConfig?.EqeExportPath ?? @"D:\Project\EQE";

                // 3. 确保目录存在（自动创建，无需用户手动操作）
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                    logger.Info($"Create EQE export directory：{basePath}");
                }

                // 4. 构造文件名（包含SerialNumber+时间戳，更贴合业务场景）
                string serialNumber = _currentDieVM?.SerialNumber ?? "Unknown";
                string fileName = $"EQE_Data_{serialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullPath = Path.Combine(basePath, fileName);

                // 5. 调用CVEQEViewModel的公共ExportToCsv方法完成核心导出
                CustomEQEVM.ExportToCsv(fullPath, measurements, wavelengths);

                logger.Info($"EQE data has been automatically exported to：{fullPath}");
            }
            catch (Exception ex)
            {
                logger.Error("EQE automatic export failed", ex);
            }
        }
        //try
        //{
        //    // 从CustomEQEVM获取必要数据（ViewModel中的公共属性）
        //    var measurements = CustomEQEVM.Measurements;
        //    var wavelengths = CustomEQEVM.Wavelengths;

        //    if (!measurements.Any() || wavelengths == null || wavelengths.Length == 0)
        //    {
        //        logger.Info("No valid EQE data available for export！");
        //        return;
        //    }

        //    // 构造导出路径：D:\Project\EQE（保持原有路径逻辑）
        //    string basePath = @"D:\Projects\EQE";

        //    // 确保目录存在（保留原有目录创建逻辑）
        //    if (!Directory.Exists(basePath))
        //    {
        //        Directory.CreateDirectory(basePath);
        //        logger.Info($"Create EQE export directory：{basePath}");
        //    }

        //    // 构造文件名（包含时间戳避免重复，保持原有命名规则）
        //    string fileName = $"EQE_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        //    string fullPath = Path.Combine(basePath, fileName);

        //    // 调用CVEQEViewModel的公共ExportToCsv方法完成核心导出
        //    CustomEQEVM.ExportToCsv(fullPath, measurements, wavelengths);

        //    logger.Info($"EQE data has been automatically exported to：{fullPath}");
        //}
        //catch (Exception ex)
        //{
        //    logger.Error("EQE automatic export failed", ex);
        //}
    }
       
      
}
