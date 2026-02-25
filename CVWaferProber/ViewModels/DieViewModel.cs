using ChipMapping.ViewModels;
using CVRepositoryLib.Services.Measure;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Services;
using CVWaferProber.Utils;
using System.Timers;
using System.Windows;
using WaferComm.StateMachine;
using Application = System.Windows.Application;
using Timer = System.Timers.Timer;

namespace CVWaferProber.ViewModels
{
    public class DieViewModel : ViewModelBase, IDisposable
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AOIService));
        #region 新增：单Die进度核心属性+定时器（独立实例，避免全局冲突）
        // 进度更新定时器（1秒执行一次）
        private Timer _testProgressTimer;
        // 预测测试时长（秒）- 从TestTimePredictService获取
        private int _predictTestSeconds;
        // 测试已运行时长（秒）- 用于计算进度
        private int _testElapsedSeconds;
        // 进度更新锁 - 避免多线程同时更新进度
        private readonly object _progressLock = new object();
        // 是否正在测试 - 标记定时器是否需要运行
        private bool _isTesting;
        // 定时器是否已释放
        private bool _disposed = false;
        #endregion

        #region 原有核心属性（保留+少量修改）
        public uint? Id => chipViewModel?.Id;
        public int? ScreenX => (int?)chipViewModel?.Position.X;
        public int? ScreenY => (int?)chipViewModel?.Position.Y;
        public int? MapX => chipViewModel?.Column;
        public int? MapY => chipViewModel?.Row;

        private string? _serialNumber;
        public string? SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (_serialNumber != value)
                {
                    _serialNumber = value;
                    OnPropertyChanged(nameof(SerialNumber));
                }
            }
        }

        private int _currentTestStep; // 0:未开始, 1:移动完成, 2:初始化完成, 3:测试中, 4:完成
        public int CurrentTestStep
        {
            get => _currentTestStep;
            set => SetProperty(ref _currentTestStep, value);
        }

        public bool IsIVLCameraEnabled { get; set; }
        public bool IsChinese { get; set; }

        // AOI/IVL/EQE/VAM复选框属性（保留原有逻辑）
        private bool _isAOIEnabled;
        public bool IsAOIEnabled
        {
            get => _isAOIEnabled;
            set
            {
                if (Status != ChipStatus.SKIP) _isAOIEnabled = value;
                else _isAOIEnabled = false;
                OnPropertyChanged();
            }
        }

        private bool _isIVLEnabled;
        public bool IsIVLEnabled
        {
            get => _isIVLEnabled;
            set { _isIVLEnabled = value; OnPropertyChanged(); }
        }

        private bool _isEQEEnabled;
        public bool IsEQEEnabled
        {
            get => _isEQEEnabled;
            set { _isEQEEnabled = value; OnPropertyChanged(); }
        }

        private bool _isVAMEnabled;
        public bool IsVAMEnabled
        {
            get => _isVAMEnabled;
            set { _isVAMEnabled = value; OnPropertyChanged(); }
        }

        public ChipStatus? Status => chipViewModel?.Status;
        public string? DisplayStatus => Status.HasValue ? ChipStatusTool.GetStatusDisplay(Status.Value, IsChinese) : "Unknown";

        public DateTime? EndTestTime { get; set; }
        public DateTime? StartTestTime { get; set; }
        public MotionStatus MStatus { get; set; }
        public string? TotalTime { get; set; }
        public decimal MotionAxisX { get; set; }
        public decimal MotionAxisY { get; set; }
        public decimal MotionAxisZ { get; set; }
        public ChipViewModel? chipViewModel { get; set; }
        public string? DataValue => string.Format("{0:F4}", chipViewModel?.DataValue);

        // 动态属性（保留原有）
        private string _aoiGradeLevel = "na";
        public string AOIGradeLevel { get => _aoiGradeLevel; set => SetProperty(ref _aoiGradeLevel, value); }

        private string _lightOnStatus = "na";
        public string LightOnStatus { get => _lightOnStatus; set => SetProperty(ref _lightOnStatus, value); }

        private string _registerPixels = "na";
        public string RegisterPixels { get => _registerPixels; set => SetProperty(ref _registerPixels, value); }

        private string _finalClass = "na";
        public string FinalClass { get => _finalClass; set => SetProperty(ref _finalClass, value); }

        private string _blackPattern = "na";
        public string BlackPattern { get => _blackPattern; set => SetProperty(ref _blackPattern, value); }

        private string _temperature = "na";
        public string Temperature { get => _temperature; set => SetProperty(ref _temperature, value); }

        private string _pixelLogic = "na";
        public string PixelLogic { get => _pixelLogic; set => SetProperty(ref _pixelLogic, value); }

        private string _pressure = "na";
        public string Pressure { get => _pressure; set => SetProperty(ref _pressure, value); }

        private int _touchDownCounts = 0;
        public int TouchDownCounts { get => _touchDownCounts; set { _touchDownCounts = value; OnPropertyChanged(); } }

        private string _probingCardSN = "na";
        public string ProbingCardSN { get => _probingCardSN; set => SetProperty(ref _probingCardSN, value); }
        #endregion

        #region 构造函数（保留原有+初始化定时器标识）
        public DieViewModel(ChipViewModel die)
        {
            chipViewModel = die;
            IsIVLCameraEnabled = false;
            IsChinese = GetCurrentLanguage() == "Chinese";
            _testProgressTimer = null;
            _testElapsedSeconds = 0;
            _isTesting = false;
        }
        #endregion

        #region 新增：进度定时器核心方法
        /// <summary>
        /// 初始化测试进度定时器 - 测试开始时调用
        /// </summary>
        public void InitTestProgressTimer()
        {
            // 先停止原有定时器（防止重复启动）
            StopTestProgressTimer();

            lock (_progressLock)
            {
                // 从预测服务获取时长，首次默认240秒
                _predictTestSeconds = TestTimePredictService.GetPredictTestSeconds();
                _testElapsedSeconds = 0;
                _isTesting = true;

                // 初始化定时器：1秒执行一次，自动循环
                _testProgressTimer = new Timer(1000) { AutoReset = true };
                _testProgressTimer.Elapsed += OnProgressTimerElapsed;
                _testProgressTimer.Start();
            }

            if (logger.IsDebugEnabled)
                logger.DebugFormat($"Die[{0}/{1}] progress timer initialized, estimated time: {2}s", MapX, MapY, _predictTestSeconds);
        }

        /// <summary>
        /// 定时器触发事件 - 计算并更新单Die进度
        /// </summary>
        private void OnProgressTimerElapsed(object sender, ElapsedEventArgs e) 
        {
            // 1. 先提取需要的变量，缩小锁的范围
            double currentProgress = 0;
            int testElapsedSeconds = 0;
            bool shouldUpdate = false;

            lock (_progressLock)
            {
                if (!_isTesting || _disposed) return;

                _testElapsedSeconds++;
                testElapsedSeconds = _testElapsedSeconds;
                // 恢复你的进度计算方法
                // currentProgress = CalculateTestProgress();
                shouldUpdate = true;
            }

            // 2. 在锁外安全地更新UI，避免死锁
            if (shouldUpdate && Application.Current != null && !Application.Current.Dispatcher.HasShutdownStarted)
            {
                // 使用InvokeAsync，避免阻塞调用线程
                var uiTask = Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 再次检查状态，防止在任务排队期间状态发生变化
                    if (!_disposed)
                    {
                        MainViewModel.Instance?.DataMappingVM?.UpdateSingleDieProgress(
                            currentProgress,
                            $"Running for {testElapsedSeconds}s / Estimated {_predictTestSeconds}s"
                        );
                    }
                });

                // 3. 如果应用正在关闭，等待任务完成或取消
                if (Application.Current.Dispatcher.HasShutdownStarted)
                {
                    try
                    {
                        uiTask.Wait(TimeSpan.FromMilliseconds(100));
                    }
                    catch (TaskCanceledException)
                    {
                        // 预期内的取消，静默处理
                    }
                }
            }
        }

        /// <summary>
        /// 核心：按时间预测计算进度（0~99%，仅完成时到100%）
        /// 规则：90%前匀速，90%后降速，避免提前满进度
        /// </summary>
        //private double CalculateTestProgress()
        //{
        //    if (_predictTestSeconds <= 0 || !_isTesting) return 0;

        //    double progress;
        //    // 阶段1：0~90% 匀速推进（按预测时长计算）
        //    double ninetyPercentSeconds = _predictTestSeconds * 0.9;
        //    if (_testElapsedSeconds <= ninetyPercentSeconds)
        //    {
        //        progress = (_testElapsedSeconds / (double)_predictTestSeconds) * 90;
        //    }
        //    // 阶段2：90%后 降速推进（剩余10%分配到剩余时间的2倍）
        //    else
        //    {
        //        double extraSeconds = _testElapsedSeconds - ninetyPercentSeconds;
        //        double extraTotalSeconds = _predictTestSeconds * 0.1 * 2; // 剩余时间放大2倍，降速
        //        progress = 90 + (extraSeconds / extraTotalSeconds) * 10;
        //    }

        //    // 强制限制在0~99%，仅测试完成时手动拉满100%
        //    return Math.Max(0, Math.Min(99, progress));
        //}

        /// <summary>
        /// 测试完成时调用 - 停止定时器并拉满进度到100%
        /// </summary>
        public void CompleteTestProgress()
        {
            lock (_progressLock)
            {
                _isTesting = false;
                StopTestProgressTimer();
            }

            if (logger.IsDebugEnabled)
                logger.DebugFormat("Die[{0}/{1}] tested, stopping progress timer", MapX, MapY);
        }

        /// <summary>
        /// 停止进度定时器 - 异常/取消/重置时调用
        /// </summary>
        public void StopTestProgressTimer()
        {
            lock (_progressLock)
            {
                _isTesting = false;
                _disposed = true;
            }

            if (_testProgressTimer != null)
            {
                _testProgressTimer.Stop();
                _testProgressTimer.Elapsed -= OnProgressTimerElapsed;
                _testProgressTimer.Dispose();
                _testProgressTimer = null;
            }
        }
        #endregion

        #region 原有方法（修改ChangeStatus/ResetStatus，集成进度逻辑）
        public void RefreshDataValue()
        {
            OnPropertyChanged(nameof(DataValue));
        }

        public static string GetCurrentLanguage()
        {
            var app = Application.Current;
            if (app?.Resources?.MergedDictionaries?.FirstOrDefault() is ResourceDictionary resourceDict)
            {
                var source = resourceDict.Source?.ToString();
                if (source != null)
                {
                    if (source.Contains("Chinese.xaml")) return "Chinese";
                    else if (source.Contains("English.xaml")) return "English";
                }
            }
            return "Unknown";
        }

        /// <summary>
        /// 修改状态 - 集成进度逻辑：测试中启动时间，完成/非测试中停止定时器+拉满进度
        /// </summary>
        public void ChangeStatus(ChipStatus status, bool updateTime = false)
        {
            if (updateTime) EndTestTime = DateTime.Now;

            // 测试开始：记录开始时间
            if (status == ChipStatus.TESTING || status == ChipStatus.IVL_TESTING|| status == ChipStatus.EQE_TESTING|| status == ChipStatus.VAM_TESTING)
            {
                StartTestTime = DateTime.Now;
                CurrentTestStep = 3; // 标记为测试中
            }
            // 测试完成/非测试状态：计算总耗时，停止定时器并拉满进度
            else
            {
                if (StartTestTime.HasValue && EndTestTime.HasValue)
                {
                    var span = EndTestTime - StartTestTime;
                    TotalTime = span?.TotalSeconds.ToString("F1") + "S";
                    // 同步更新TScgdMeasureBatch的TotalTime（可选，根据业务需要）
                    if (!string.IsNullOrEmpty(SerialNumber))
                    {
                        var batchId = MeasureBatchService.GetBatchId(SerialNumber);
                        if (batchId > 0)
                        {
                            var batch = MeasureBatchService.GetByCode(SerialNumber);
                            if (batch != null)
                            {
                                batch.TotalTime = (int)span?.TotalSeconds;
                                MeasureBatchService.Update(batch);
                            }
                        }
                    }
                }
                CurrentTestStep = 4; // 标记为完成
                //CompleteTestProgress(); // 核心：拉满进度到100%
            }

            chipViewModel?.SetStatus(status);
            chipViewModel.IsSelected = true;
            FirePropertyChanged();
        }

        public void ChangeStatusOnly(ChipStatus status)
        {
            chipViewModel?.SetStatus(status);
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(DisplayStatus));
            FirePropertyChanged();
        }

        /// <summary>
        /// 测试准备 - 集成进度逻辑：初始化定时器
        /// </summary>
        public void TestingReady(string proberId, string timestamp)
        {
            EndTestTime = null;
            SerialNumber = SNBuilder.Build(proberId, timestamp, this);
            StartTestTime = null;
            TotalTime = null;
            chipViewModel?.SetStatus(ChipStatus.WAITING);
            CurrentTestStep = 0; // 重置测试步骤
            FirePropertyChanged();

            // 核心：初始化进度定时器，准备测试
            InitTestProgressTimer();
        }

        public void UnSelected()
        {
            chipViewModel.IsSelected = false;
        }

        /// <summary>
        /// 重置状态 - 集成进度逻辑：停止定时器，重置进度相关参数
        /// </summary>
        public void ResetStatus()
        {
            UnSelected();
            chipViewModel?.SetStatus(ChipStatus.WAITING);
            StartTestTime = null;
            EndTestTime = null;
            TotalTime = null;
            SerialNumber = null;
            CurrentTestStep = 0;
            // 重置动态属性
            AOIGradeLevel = "na";
            LightOnStatus = "na";
            RegisterPixels = "na";
            FinalClass = "na";
            BlackPattern = "na";
            Temperature = "na";
            PixelLogic = "na";
            Pressure = "0,0,0,0";
            TouchDownCounts = 0;
            ProbingCardSN = "na";

            if (chipViewModel?.ChipData != null) chipViewModel.ChipData.DataValue = null;
            FirePropertyChanged();

            // 核心：停止进度定时器，重置进度
            StopTestProgressTimer();
        }

        private void FirePropertyChanged()
        {
            OnPropertyChanged(nameof(DisplayStatus));
            OnPropertyChanged(nameof(StartTestTime));
            OnPropertyChanged(nameof(EndTestTime));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(SerialNumber));
            OnPropertyChanged(nameof(TotalTime));
            OnPropertyChanged(nameof(DataValue));
            OnPropertyChanged(nameof(AOIGradeLevel));
            OnPropertyChanged(nameof(LightOnStatus));
            OnPropertyChanged(nameof(RegisterPixels));
            OnPropertyChanged(nameof(FinalClass));
            OnPropertyChanged(nameof(BlackPattern));
            OnPropertyChanged(nameof(Temperature));
            OnPropertyChanged(nameof(PixelLogic));
            OnPropertyChanged(nameof(Pressure));
            OnPropertyChanged(nameof(TouchDownCounts));
            OnPropertyChanged(nameof(ProbingCardSN));
        }

        public (string x, string y) ToMapAxis()
        {
            string x = string.Format("{0}{1:D3}", MapX >= 0 ? "+" : "", MapX);
            string y = string.Format("{0}{1:D3}", MapY >= 0 ? "+" : "", MapY);
            return (x, y);
        }

        public string MapAxisToString()
        {
            return string.Format("Y{0}{1:D3}X{2}{3:D3}", MapY >= 0 ? "+" : "", MapY, MapX >= 0 ? "+" : "", MapX);
        }

        public void UpdateMotionAxis(ProberMotionAxisStatus axis)
        {
            MotionAxisX = axis.CurrentAxisX;
            MotionAxisY = axis.CurrentAxisY;
            MotionAxisZ = axis.CurrentAxisZ;
            OnPropertyChanged(nameof(MotionAxisX));
            OnPropertyChanged(nameof(MotionAxisY));
            OnPropertyChanged(nameof(MotionAxisZ));
        }

        public bool IsCompleted => Status == ChipStatus.OK ||
              Status == ChipStatus.VAM_COMPLETED ||
              Status == ChipStatus.IVL_COMPLETED ||
              Status == ChipStatus.EQE_COMPLETED;
        public bool IsNG => !IsCompleted;
        #endregion

        #region 新增：IDisposable实现 - 保障定时器资源释放
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 手动释放托管资源
                StopTestProgressTimer();
            }

            // 释放非托管资源（此处无）
            _disposed = true;
        }

        // 析构函数 - 防止未显式Dispose时定时器泄漏
        ~DieViewModel()
        {
            Dispose(false);
        }
        #endregion
        private bool _isAOITestCompleted;
        /// <summary>
        /// 标记该Die的AOI测试是否完成（图片是否已全部加载过）
        /// </summary>
        public bool IsAOITestCompleted
        {
            get => _isAOITestCompleted;
            set => SetProperty(ref _isAOITestCompleted, value);
        }

        /// <summary>
        /// 恢复测试进度（暂停后继续测试时调用）
        /// </summary>
        public void ResumeTestProgress()
        {
            lock (_progressLock)
            {
                if (_disposed) return;

                _isTesting = true;
                // 如果定时器已销毁，重新初始化
                if (_testProgressTimer == null)
                {
                    InitTestProgressTimer();
                }
                else if (!_testProgressTimer.Enabled)
                {
                    _testProgressTimer.Start();
                    logger.DebugFormat($"Die[{MapX}/{MapY}] progress timer resumed");
                }
            }
        }
    }
   
}
