using ChipMapping.ViewModels;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Utils;
using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl.ViewModels;
using Microsoft.VisualBasic.Logging;
using System.Diagnostics.Metrics;
using System.IO;
using System.Text;
using System.Windows;
using Application = System.Windows.Application;

namespace CVWaferProber.Services
{

    public class IVLService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(IVLService));
        // 缓存当前测试的DieViewModel（供定时器回调使用）
        private DieViewModel _currentDieVM;
        private ChipMappingControlViewModel _chipMappingControlViewModel;
    
        //
        // 存储当前测试的光谱数据（供生成CSV使用）
        private SpectrumMeasurement _currentSpectrumData;
        public bool IsIVLCameraEnabled { get; set; }
        public CVSpectrumViewModel CustomIVLVM { get; private set; }
        public IVLService(CVSpectrumViewModel customIVLVM, ChipMappingControlViewModel chipMappingControlViewModel, RCRestService rcService) : base(rcService)
        {
            this.CustomIVLVM = customIVLVM;
            this._chipMappingControlViewModel = chipMappingControlViewModel;
            // 初始化导出文件夹（确保目录存在）
            AutoExportHelper.InitFolders();
        }
        public IVLService(string proberId, ChipMappingControlViewModel chipMappingControlViewModel, RCRestService rcService) : this(new CVSpectrumViewModel(), chipMappingControlViewModel, rcService)
        {
            this.ProberId = proberId;
        }
        
        public override Task StartTesting(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool isEnd = true)
        {
            dieViewModel.ChangeStatus(ChipStatus.IVL_TESTING);
            CustomIVLVM.ClearResult();


            IsIVLCameraEnabled = _selectedWPFlow.FlowType == CVWaferProberFlowType.IVL_Camera;
            dieViewModel.IsIVLCameraEnabled = IsIVLCameraEnabled;
            if (IsIVLCameraEnabled) CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.IVLCamera;
            else CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.Spectrum;
            // 缓存当前DieViewModel（定时器回调中需要用到）
            _currentDieVM = dieViewModel;
            //System.Timers.Timer timer = new System.Timers.Timer(1000);
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel, isEnd);
            // 初始化并启动定时器（1秒调用一次IVLResultDisplay）
            System.Timers.Timer refreshTimer = new System.Timers.Timer(350)
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
                        ResultDisplay(_currentDieVM);
                        // 新增：加载光谱数据（供后续生成CSV）
                       // _currentSpectrumData = CustomIVLVM.GetSpectrumData(dieViewModel.SerialNumber);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Timer failed to refresh the chart", ex);//: "定时器刷新图表失败"
                    }
                });
            };

            // 测试流程结束后，停止定时器（避免内存泄漏）
            task.ContinueWith(t =>
            {
                refreshTimer.Enabled = false;
                refreshTimer.Dispose();
                logger.Debug( "Test process completed, stop the refresh timer");// : "测试流程结束，停止刷新定时器"
            }, TaskScheduler.FromCurrentSynchronizationContext());

            return task;
        }
        public override void ResultDisplay(DieViewModel dieViewModel)
        {
            if (string.IsNullOrEmpty(dieViewModel.SerialNumber))
            {
                CustomIVLVM?.ClearResult();
                //EventAggregator?.Publish(new EQEResultGUIClearEvent());
                return;
            }
            else
            {
                FlowResultDisplay(dieViewModel);
            }
            //CustomIVLVM.ClearResult();
            //CustomIVLVM.LoadData(dieViewModel.SerialNumber, dieViewModel.IsIVLCameraEnabled);
        } 

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.FAILED;
        }

        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            //IVLResultDisplay(dieViewModel);
            CustomIVLVM.ClearResult();
            CustomIVLVM.LoadData(dieViewModel.SerialNumber, dieViewModel.IsIVLCameraEnabled);

            // 【关键修改1】只生成CSV内容，不直接导出文件
            //string csvContent = GenerateCsvContent(dieViewModel, "IVL");
            // 【关键修改2】构建Summary数据（对接AutoExportHelper的TestSummaryData）
            //AutoExportHelper.TestSummaryData summaryData = BuildIVLSummaryData(dieViewModel);
           
            return ChipStatus.IVL_COMPLETED;
        }
        /// <summary>
        /// 【核心新增】生成IVL专属CSV内容（仅拼接字符串，不写文件）
        /// 对接AutoExportHelper的导出规范
        /// </summary>
        /// <param name="die">测试芯片</param>
        /// <param name="category">固定为"IVL"</param>
        /// <param name="temperature">温度数据</param>
        /// <returns>拼接好的CSV字符串</returns>
        //public string GenerateCsvContent(DieViewModel die, string category)
        //{
        //    try
        //    {
        //        if (category != "IVL")
        //        {
        //            throw new ArgumentException((string)Application.Current.FindResource("IVLServiceonlysupportsCSV"));
        //        }

        //        // 1. IVL CSV表头（与截图/业务匹配）
        //        string ivlHeader = "Voltage/V,Current/mA,Lv(cd/m2),IP,BlueLight,cx,cy,u',v',CCT(K),Dominant Wavelength(nm),Saturation(%),Peak Wavelength(nm),FWHM,Temperature(℃)";

        //        // 2. 构建CSV数据行（从光谱数据/DieViewModel中读取）
        //        StringBuilder csvRows = new StringBuilder();
        //        csvRows.AppendLine(ivlHeader);

        //        // 如果有多个数据行，遍历_spectrumDataList；这里以单行为例
        //        if (_currentSpectrumData != null)
        //        {
        //            string row = $"{_currentSpectrumData.Voltage:F5}," +
        //                         $"{_currentSpectrumData.Current:F4}," +
        //                         $"{_currentSpectrumData.Luminance:F1}," +
        //                         $"{_currentSpectrumData.IP}," +
        //                         $"{_currentSpectrumData.Blue:F2}," +
        //                         $"{_currentSpectrumData.CIE_x:F6}," +
        //                         $"{_currentSpectrumData.CIE_y:F6}," +
        //                         $"{_currentSpectrumData.CIE_u:F6}," +
        //                         $"{_currentSpectrumData.CIE_v:F6}," +
        //                         $"{_currentSpectrumData.CCT:F1}," +
        //                         $"{_currentSpectrumData.PeakWavelength:F2}," +
        //                         $"{_currentSpectrumData.fPur:F4}," +
        //                         $"{_currentSpectrumData.PeakWavelength:F2}," +
        //                         $"{_currentSpectrumData.FHW:F1}," +
        //                         $"{_chipMappingControlViewModel.Temperatures:F1}";
        //            csvRows.AppendLine(row);
        //        }
        //        else
        //        {
        //            logger.Warn( "IVL data is empty，SN：{die.SerialNumber}");//" : $"IVL数据为空
        //            csvRows.AppendLine(",,,,,,,,,,,,,,"); // 空行兜底
        //        }

        //        return csvRows.ToString().TrimEnd();
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error( "Failed to generate IVL.CSV content", ex);// : "生成IVL.CSV内容失败"
        //        throw;
        //    }
        //}

        /// <summary>
        /// 【核心新增】构建IVL的Summary数据（对接AutoExportHelper的TestSummaryData）
        /// </summary>
        //private AutoExportHelper.TestSummaryData BuildIVLSummaryData(DieViewModel die)
        //{
        //    return new AutoExportHelper.TestSummaryData
        //    {
        //        IVSelected = "Y",          // IVL测试选中，标记为Y
        //        IVLSelected = "Y",         // 截图中重复的IV Selected，同步为Y
        //        AOISelected = "N",         // 未选AOI，标记为N
        //        VAMSelected = "N",         // 未选VAM，标记为N
        //        EQESelected = "N",         // 未选EQE，标记为N
        //        /*   假数据   */
        //        LightOnStatus =/* die.LightOnStatus ??*/  "na",
        //        Register =/* die.Register ??*/ "na",
        //        Pixels = /*die.Pixels ?? */"na",
        //        /*            */
        //        AOIGradeLevel = "na",
        //        BlackPatterns = "na",
        //        Temperature = _chipMappingControlViewModel.Temperatures.ToString() ?? "na",
        //        PixelLogic = "na",
        //        MeasurePin = "na",
        //        Pressure = _chipMappingControlViewModel.Pressure ?? "na",
        //        TouchDownCounts = _chipMappingControlViewModel.TDCount != 0? _chipMappingControlViewModel.TDCount: 0,
        //        ProbingCardID = _chipMappingControlViewModel.SN ?? "na",
        //    };
        //}

        /// <summary>
        /// 重写基类EndTesting（确保流程结束时停止定时器）
        /// </summary>
        protected override void DoEndTesting()
        {

            base.DoEndTesting(); // 调用基类触发TestingCompleted事件
            
        }

        // 仅暴露“生成CSV内容”的方法（不执行文件写入，只返回内容）
        //public string GenerateCsvContent(DieViewModel die)
        //{
        //    // 原有CSV内容生成逻辑（只拼接字符串，不写文件）
        //    string ivlHeader = "Voltage/V,Current/mA,Lv(cd/m2),IP,BlueLight,cx,cy,u',v',CCT(K),Dominant Wavelength(nm),Saturation(%),Peak Wavelength(nm),FWHM,Temperature(℃)";
        //    var dataRows = BuildIVLCsvDataRows(die);
        //    return AutoExportHelper.GenerateCsvWithWavelengths(ivlHeader, dataRows, die.Wavelengths, die.Intensities);
        //}
        /// <summary>
        /// 构建IVL CSV的数据行
        /// </summary>
        private List<string> BuildIVLCsvDataRows(DieViewModel die)
        {
            var dataRows = new List<string>();
            // 从光谱数据中获取IVL相关数据（假设你已缓存了_currentSpectrumData）
            if (_currentSpectrumData != null)
            {
                string row = $"{_currentSpectrumData.Voltage:F5}," +
                                 $"{_currentSpectrumData.Current:F4}," +
                                 $"{_currentSpectrumData.Luminance:F1}," +
                                 $"{_currentSpectrumData.IP}," +
                                 $"{_currentSpectrumData.Blue:F2}," +
                                 $"{_currentSpectrumData.CIE_x:F6}," +
                                 $"{_currentSpectrumData.CIE_y:F6}," +
                                 $"{_currentSpectrumData.CIE_u:F6}," +
                                 $"{_currentSpectrumData.CIE_v:F6}," +
                                 $"{_currentSpectrumData.CCT:F1}," +
                                 $"{_currentSpectrumData.PeakWavelength:F2}," +
                                 $"{_currentSpectrumData.fPur:F4}," +
                                 $"{_currentSpectrumData.PeakWavelength:F2}," +
                                 $"{_currentSpectrumData.FHW:F1}," +
                                 $"{_chipMappingControlViewModel.Temperatures:F1}";
                dataRows.Add(row);
            }
            return dataRows;
        }
        WPFlowViewModel wpfFlowViewModel { get; set; }

        public override void AutoExportData()
        {
            if (wpfFlowViewModel == null) return;
            if (wpfFlowViewModel.FlowType == CVWaferProberFlowType.IV)
            {
                var IVMeasurements = CustomIVLVM.IVMeasurements;
                // 1. 固定导出根路径
                string ivRootPath = @"D:\Project\IV";

                // 2. 确保目标目录存在（不存在则自动创建，避免路径不存在异常）
                if (!Directory.Exists(ivRootPath))
                {
                    Directory.CreateDirectory(ivRootPath);
                    logger.Info($"创建IV导出目录：{ivRootPath}");
                }
                
                // 3. 构造文件名（包含时间戳，避免文件重名覆盖）
                string fileName = $"IVL_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullExportPath = Path.Combine(ivRootPath, fileName);
                CustomIVLVM.ExportToCsv(IVMeasurements, fullExportPath, 1);
            }
            else 
            {
                var Measurements = CustomIVLVM.Measurements;
                var Wavelengths = CustomIVLVM.Wavelengths;
                // 1. 固定导出根路径
                string ivlRootPath = @"D:\Project\IVL";

                // 2. 确保目标目录存在（不存在则自动创建，避免路径不存在异常）
                if (!Directory.Exists(ivlRootPath))
                {
                    Directory.CreateDirectory(ivlRootPath);
                    logger.Info($"创建IVL导出目录：{ivlRootPath}");
                }

                // 3. 构造文件名（包含时间戳，避免文件重名覆盖）
                string fileName = $"IVL_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullExportPath = Path.Combine(ivlRootPath, fileName);
                CustomIVLVM.ExportToCsv(fullExportPath, Measurements, Wavelengths);
            }
           
        }
    }
}
