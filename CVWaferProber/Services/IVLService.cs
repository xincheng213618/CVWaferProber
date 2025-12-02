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
        /* private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(IVLService));

         public bool IsIVLCameraEnabled { get; set; }
         //
         private CVSpectrumViewModel CustomIVLVM { get; set; }

         public IVLService(CVSpectrumViewModel customIVLVM, RCRestService rcService) : base(rcService)
         {
             this.CustomIVLVM = customIVLVM;


         }

         public void StartTestingIVL(string timestamp,DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow)
         {
             string sn = BuildFlowSN(dieViewModel, timestamp);
             dieViewModel.SerialNumber = sn;

             dieViewModel.ChangeStatus(ChipStatus.IVL_TESTING);
             CustomIVLVM.ClearResult();

             // 重置逐点索引（确保每次测试从第一个点开始）


             IsIVLCameraEnabled = _selectedWPFlow.FlowType == CVWaferProberFlowType.IVL_Camera;
             dieViewModel.IsIVLCameraEnabled = IsIVLCameraEnabled;
             if (IsIVLCameraEnabled) CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.IVLCamera;
             else CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.Spectrum;


             Task task = RunFlowAsync(_selectedWPFlow, dieViewModel);
             //_flowTimerDisposable?.Dispose(); // 释放定时器资源
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
         }*/

        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(IVLService));

        public bool IsIVLCameraEnabled { get; set; }
        private CVSpectrumViewModel CustomIVLVM { get; set; }

        // 新增：逐点显示核心字段
        private IDisposable _pointTimer; // 定时器句柄
        private int _currentPointIndex = 0; // 当前显示点索引
        private string _currentSN; // 当前测试序列号
        private List<VScgdMeasureResultSpectrometer> _allMeasureData; // 缓存所有测量数据
        private readonly SynchronizationContext _uiSyncContext; // UI线程上下文

        public IVLService(CVSpectrumViewModel customIVLVM, RCRestService rcService) : base(rcService)
        {
            this.CustomIVLVM = customIVLVM;
            _uiSyncContext = SynchronizationContext.Current ?? new DispatcherSynchronizationContext(Application.Current.Dispatcher);
            _allMeasureData = new List<VScgdMeasureResultSpectrometer>();
        }

        public void StartTestingIVL(string timestamp, DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow)
        {
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;
            _currentSN = sn; // 保存当前SN

            dieViewModel.ChangeStatus(ChipStatus.IVL_TESTING);
            CustomIVLVM.ClearResult();

            // 初始化逐点状态
            _currentPointIndex = 0;
            _allMeasureData.Clear();

            // 设置Tab类型
            IsIVLCameraEnabled = _selectedWPFlow.FlowType == CVWaferProberFlowType.IVL_Camera;
            dieViewModel.IsIVLCameraEnabled = IsIVLCameraEnabled;
            CustomIVLVM.SelectedTab = IsIVLCameraEnabled
                ? CVWPFSpectrometerCtrl.Models.TabType.IVLCamera
                : CVWPFSpectrometerCtrl.Models.TabType.Spectrum;

            // 启动逐点显示定时器（1000ms/点，可调整速度）
            StartPointTimer(dieViewModel);

            // 启动测试流程
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel);
        }

        /// <summary>
        /// 启动逐点显示定时器
        /// </summary>
        private void StartPointTimer(DieViewModel dieViewModel)
        {
            // 先停止已有定时器
            StopPointTimer();

            // 定时器配置：每500ms执行一次，切换到UI线程
            _pointTimer = Observable.Interval(TimeSpan.FromMilliseconds(1000))
                .ObserveOn(_uiSyncContext)
                .Subscribe(
                    _ => ShowNextPoint(dieViewModel), // 显示下一个点
                    ex => logger.Error("逐点显示定时器异常", ex),
                    () => logger.Debug("逐点显示定时器已停止")
                );
        }

        /// <summary>
        /// 显示下一个数据点（核心逻辑）
        /// </summary>
        private void ShowNextPoint(DieViewModel dieViewModel)
        {
            try
            {
                if (_allMeasureData.Count == 0)
                {
                    // 数据库查询放后台线程，避免阻塞UI
                    Task.Run(() =>
                    {
                        var data = SpectrumResultService.LoadResultByBatchCode(CustomIVLVM.DeviceCode, _currentSN)
                            .OrderBy(m => m.CreateDate)
                            .ToList();
                        // 切换回UI线程更新缓存
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _allMeasureData = data;
                            logger.Debug($"SN[{_currentSN}]共加载{_allMeasureData.Count}个测量点");
                        });
                    });
                    return;
                }

                // 确保所有ViewModel数据更新在UI线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_currentPointIndex >= _allMeasureData.Count)
                    {
                        StopPointTimer();
                        return;
                    }
                    var currentData = _allMeasureData[_currentPointIndex];
                    var spectrumPoint = ConvertToSpectrumMeasurement(currentData, _currentPointIndex + 1);
                    CustomIVLVM.Measurements.Add(spectrumPoint);
                    SyncIVILVLData(currentData, _currentPointIndex + 1);

                    if (_currentPointIndex == 0)
                    {
                        CustomIVLVM.SelectedMeasurement = spectrumPoint;
                    }
                    CustomIVLVM.InitializeOverviewSeries();
                    _currentPointIndex++;
                    CustomIVLVM.RefreshAllPlots();
                });
            }
            catch (Exception ex)
            {
                logger.Warn($"显示第{_currentPointIndex + 1}个点失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 转换数据库实体到ViewModel的SpectrumMeasurement
        /// </summary>
        private SpectrumMeasurement ConvertToSpectrumMeasurement(VScgdMeasureResultSpectrometer dbData, int no)
        {
            // 从数据库数据反序列化强度数组（与ViewModel.LoadSpectrumData逻辑一致）
            //float[] intensities = JsonConvert.DeserializeObject<float[]>(dbData.FPL);
            float[] intensities = JsonConvert.DeserializeObject<float[]>(dbData.FPL) ?? Array.Empty<float>();
            var wavelengths = CustomIVLVM.Wavelengths ?? Enumerable.Range(380, 401).Select(i => (float)i).ToArray();
            return new SpectrumMeasurement(no)
            {
                Timestamp = dbData.CreateDate,
                Meas_Id = dbData.BatchCode,
                Voltage = (float)dbData.VResult,
                Current = (float)dbData.IResult,
                Luminance = (float)dbData.FPh / 1,
                IP = Math.Round((decimal)(dbData.FIp / 65535 * 100), 2).ToString() + "%",
                Blue = CalculateBlueRatio(intensities), // 计算蓝光占比（复用原有逻辑）
                CIE_x = (float)dbData.Fx,
                CIE_y = (float)dbData.Fy,
                CIE_u = (float)dbData.Fu,
                CIE_v = (float)dbData.Fv,
                CCT = (float)dbData.FCCT,
                PeakWavelength = (float)dbData.FLd,
                fPur = (float)dbData.FPur,
                PeakIntensity = (float)dbData.FLp,
                FHW = (float)dbData.FHW,
                Intensities = intensities,
                Wavelengths = wavelengths, // 复用ViewModel的波长数组
                fPlambda = (float)dbData.FPlambda
            };
        }

        /// <summary>
        /// 计算蓝光占比（复用ViewModel中的逻辑）
        /// </summary>
        private float CalculateBlueRatio(float[] intensities)
        {
            if (intensities == null || intensities.Length < 1200)
                return 0;

            double sum1 = 0, sum2 = 0;
            // 蓝光范围：380~420nm（对应索引350~750，步长10）
            for (int i = 350; i <= 750; i += 10)
                sum1 += intensities[i];
            // 全光谱范围：380~780nm（对应索引200~1200，步长10）
            for (int i = 200; i <= 1200; i += 10)
                sum2 += intensities[i];

            return sum2 == 0 ? 0 : (float)Math.Round(sum1 / sum2 * 100, 2);
        }

        /// <summary>
        /// 同步更新IV/IL/VL数据（适配多图表逐点显示）
        /// </summary>
        private void SyncIVILVLData(VScgdMeasureResultSpectrometer dbData, int no)
        {
            // IV数据（电流-电压）
            CustomIVLVM.IVMeasurements.Add(new IVMeasurement(no)
            {
                
                Timestamp = dbData.CreateDate,
                Current = (float)dbData.IResult,
                Voltage = (float)dbData.VResult
            });

            // IL数据（电流-亮度）
            CustomIVLVM.ILMeasurements.Add(new ILMeasurement(no)
            {
                Timestamp = dbData.CreateDate,
                Current = (float)dbData.IResult,
                Luminance = (float)dbData.FPh / 1
            });

            // VL数据（电压-亮度）
            CustomIVLVM.VLMeasurements.Add(new VLMeasurement(no)
            {
                
                Timestamp = dbData.CreateDate,
                Voltage = (float)dbData.VResult,
                Luminance = (float)dbData.FPh / 1
            });
        }

        /// <summary>
        /// 停止逐点显示定时器
        /// </summary>
        private void StopPointTimer()
        {
            _pointTimer?.Dispose();
            _pointTimer = null;
            _currentSN = null;
            _currentPointIndex = 0;
            _allMeasureData.Clear();
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
            StopPointTimer(); // 测试完成后停止定时器
            IVLResultDisplay(dieViewModel); // 加载全部数据用于最终显示
            return ChipStatus.IVL_COMPLETED;
        }

        /// <summary>
        /// 重写基类EndTesting，确保流程结束时停止定时器
        /// </summary>
        protected override void EndTesting()
        {
            StopPointTimer();
            base.EndTesting();
        }
    }
}
