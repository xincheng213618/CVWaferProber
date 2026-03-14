using ChipMapping.Models;
using ChipMapping.Models.HZCC;
using ChipMapping.ViewModels;
using ColorVision.Core.Entities;
using ColorVision.UI;
using CVDB.Services.Buz;
using CVWaferProber.Components;
using CVWaferProber.Core;
using CVWaferProber.Core.Config;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.Utils;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Models;
using CVWaferProber.Services;
using CVWaferProber.Views;
using FreeSql;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WaferComm.StateMachine;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using CheckBox = System.Windows.Controls.CheckBox;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace CVWaferProber.ViewModels
{

    public class MappingDataViewModelConfig : ViewModelBase, IConfig
    {
        public static MappingDataViewModelConfig Instance => ConfigService.Instance.GetRequiredService<MappingDataViewModelConfig>();

        public int SelectedIndex { get => _SelectedIndex; set { _SelectedIndex = value; OnPropertyChanged(); } }
        private int _SelectedIndex = 0;

    }



    public class MappingDataViewModel : ViewModelBase
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(MappingDataViewModel));

        // 新增：标记自动测试的第一个Die
        public bool _isFirstDieInAutoTest = true;
        private static MappingDataViewModel _instance;
        private static readonly object _locker = new();
        public static MappingDataViewModel GetInstance()
        {
            lock (_locker)
            {
                _instance ??= new MappingDataViewModel();
                return _instance;
            }
        }

        public event EventHandler<WPFlowViewModel> ActivateCorrespondingPanel;
        public ChipMappingControlViewModel CustomMappingVM { get; private set; }

        #region 命令 ICommand
        public ICommand LoadMappingFileCommand { get; }
        public ICommand ClearMappingCommand { get; }
        public ICommand FlowLoadCommand { get; }
        public ICommand OpenMappingFileCommand { get; }
        public ICommand RefreshStatusCommand { get; }
        public ICommand SaveTestResultCommand { get; }
        public ICommand LoadTestResultCommand { get; }
        public ICommand ResetStatusCommand { get; }
        public ICommand StartManTestCommand { get; }
        public ICommand InvertSelectAOICommand { get; }
        public ICommand InvertSelectIVLCommand { get; }
        public ICommand InvertSelectEQECommand { get; }
        public ICommand InvertSelectVAMCommand { get; }
        public ICommand SearchCommand { get; }

        // 自动保存防抖器和锁
        private System.Timers.Timer? _autoSaveDebounceTimer;
        private readonly object _autoSaveLock = new object();
        public ObservableCollection<DieViewModel> TestResults { get; } = new ObservableCollection<DieViewModel>();
        //public RangeEnabledObservableCollection<FlowViewModel> FlowItems { get; } = new RangeEnabledObservableCollection<FlowViewModel>();
        public ObservableCollection<WPFlowViewModel> WPFlows { get; } = new ObservableCollection<WPFlowViewModel>();

        public MappingDataViewModelConfig Config => MappingDataViewModelConfig.Instance;

        private WPFlowViewModel? _selectedWPFlow;
        public WPFlowViewModel? SelectedWPFlow
        {
            get => _selectedWPFlow;
            set
            {
                if (_selectedWPFlow != value)
                {
                    SetProperty(ref _selectedWPFlow, value);
                    ActivateCorrespondingPanel?.Invoke(this, _selectedWPFlow);
                }
            }
        }

        private object? _selectedItem;
        public object? SelectedItem
        {
            get => _selectedItem;
            set
            {
                SetProperty(ref _selectedItem, value);
                ManScrollToItem(SelectedItem);
                OnSelectedChanged(value);
            }
        }

        private string _MappingCsvFilePath;
        public string MappingCsvFilePath
        {
            get => _MappingCsvFilePath;
            set => SetProperty(ref _MappingCsvFilePath, value);
        }

        private bool? _selectAllAOI = false;
        private bool _isUpdatingFromHeader_AOI = false;
        public bool? SelectAllAOI
        {
            get => _selectAllAOI;
            set
            {
                if (!Equals(_selectAllAOI, value))
                {
                    // 标记开始更新，避免循环
                    bool oldUpdatingState = _isUpdatingFromHeader_AOI;
                    _isUpdatingFromHeader_AOI = true;

                    _selectAllAOI = value;
                    OnPropertyChanged();

                    // 当用户点击全选复选框时，更新所有项的选中状态
                    if (!oldUpdatingState && value.HasValue)
                    {
                        bool newState = value.Value;
                        foreach (var item in TestResults)
                        {
                            if (item.IsAOIEnabled != newState)
                                item.IsAOIEnabled = newState;
                        }

                        // 刷新DataGrid
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _dataGrid?.Items.Refresh();
                        });

                        // 更新ChipMapping中的选中数量
                        UpdateChipMappingSelectedCount();
                    }

                    // 更新全选状态（基于实际选中项）
                    UpdateSelectAllAOIState();

                    // 标记更新结束
                    _isUpdatingFromHeader_AOI = false;
                }
            }
        }


        private bool? _selectAllIVL = false;
        private bool _isUpdatingFromHeader_IVL = false;
        public bool? SelectAllIVL
        {
            get => _selectAllIVL;
            set
            {
                if (!Equals(_selectAllIVL, value))
                {
                    // 标记开始更新，避免循环
                    bool oldUpdatingState = _isUpdatingFromHeader_IVL;
                    _isUpdatingFromHeader_IVL = true;

                    _selectAllIVL = value;
                    OnPropertyChanged();

                    // 当用户点击全选复选框时，更新所有项的选中状态
                    if (!oldUpdatingState && value.HasValue)
                    {
                        bool newState = value.Value;
                        foreach (var item in TestResults)
                        {
                            if (item.IsIVLEnabled != newState)
                                item.IsIVLEnabled = newState;
                        }

                        // 刷新DataGrid
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _dataGrid?.Items.Refresh();
                        });

                        // 更新ChipMapping中的选中数量
                        UpdateChipMappingSelectedCount();
                    }

                    // 更新全选状态（基于实际选中项）
                    UpdateSelectAllIVLState();

                    // 标记更新结束
                    _isUpdatingFromHeader_IVL = false;
                }
            }
        }



        private bool? _selectAllEQE = false;
        private bool _isUpdatingFromHeader_EQE = false;
        public bool? SelectAllEQE
        {
            get => _selectAllEQE;
            set
            {
                if (!Equals(_selectAllEQE, value))
                {
                    // 标记开始更新，避免循环
                    bool oldUpdatingState = _isUpdatingFromHeader_EQE;
                    _isUpdatingFromHeader_EQE = true;

                    _selectAllEQE = value;
                    OnPropertyChanged();

                    // 当用户点击全选复选框时，更新所有项的选中状态
                    if (!oldUpdatingState && value.HasValue)
                    {
                        bool newState = value.Value;
                        foreach (var item in TestResults)
                        {
                            if (item.IsEQEEnabled != newState)
                                item.IsEQEEnabled = newState;
                        }

                        // 刷新DataGrid
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _dataGrid?.Items.Refresh();
                        });

                        // 更新ChipMapping中的选中数量
                        UpdateChipMappingSelectedCount();
                    }

                    // 更新全选状态（基于实际选中项）
                    UpdateSelectAllEQEState();

                    // 标记更新结束
                    _isUpdatingFromHeader_EQE = false;
                }
            }
        }
  

        private bool? _selectAllVAM = false;
        private bool _isUpdatingFromHeader_VAM = false;
        public bool? SelectAllVAM
        {
            get => _selectAllVAM;
            set
            {

                if (!Equals(_selectAllVAM, value))
                {
                    // 标记开始更新，避免循环
                    bool oldUpdatingState = _isUpdatingFromHeader_VAM;
                    _isUpdatingFromHeader_VAM = true;

                    _selectAllVAM = value;
                    OnPropertyChanged();

                    // 当用户点击全选复选框时，更新所有项的选中状态
                    if (!oldUpdatingState && value.HasValue)
                    {
                        bool newState = value.Value;
                        foreach (var item in TestResults)
                        {
                            if (item.IsVAMEnabled != newState)
                                item.IsVAMEnabled = newState;
                        }

                        // 刷新DataGrid
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                _dataGrid?.Items.Refresh();

                            }
                            catch (Exception ex)
                            {

                            }

                        });

                        // 更新ChipMapping中的选中数量
                        UpdateChipMappingSelectedCount();
                    }

                    // 更新全选状态（基于实际选中项）
                    UpdateSelectAllVAMState();

                    // 标记更新结束
                    _isUpdatingFromHeader_VAM = false;
                }
            }
        }
       

        public bool IsColorEnabled { get; set; }


        private string _Timestamp;
        public string Timestamp
        {
            get => _Timestamp;
            set => SetProperty(ref _Timestamp, value);
        }
        private bool _isAutoSN;
        public bool IsAutoSN
        {
            get => _isAutoSN;
            set => SetProperty(ref _isAutoSN, value);
        }
        private bool _isProcessing = false;
        public bool IsNotProcessing => !_isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }
        private bool _isIVLCameraEnabled;
        public bool IsIVLCameraEnabled
        {
            get => _isIVLCameraEnabled;
            set => SetProperty(ref _isIVLCameraEnabled, value);
        }
        private bool selfClick = true;

        public  DataGrid? _dataGrid { get; set; }

        private string _yieldInfo = "0/0 (0.00%)";
        public string YieldInfo
        {
            get => _yieldInfo;
            set
            {
                if (_yieldInfo != value)
                {
                    _yieldInfo = value;
                    OnPropertyChanged();
                    CustomMappingVM.YieldInfo = value;
                }
            }
        }

        private const int StaticColumnCount = 13;
        private readonly List<ColumnConfig> _staticColumnConfigs = new List<ColumnConfig>
        {
            new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("GridHeader.No"), ColumnBindingPath = "Id", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
            new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.Row"), ColumnBindingPath = "MapY", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
            new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.Col"), ColumnBindingPath = "MapX", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
            new ColumnConfig { ColumnHeader = "AOI Enabled", ColumnBindingPath = "IsAOIEnabled", ColumnType = ColumnType.CheckBox, IsOptional = false, ColumnKey = ColumnKey.AOI },
            new ColumnConfig { ColumnHeader = "IVL Enabled", ColumnBindingPath = "IsIVLEnabled", ColumnType = ColumnType.CheckBox, IsOptional = false, ColumnKey = ColumnKey.IVL },
            new ColumnConfig { ColumnHeader = "EQE Enabled", ColumnBindingPath = "IsEQEEnabled", ColumnType = ColumnType.CheckBox, IsOptional = false, ColumnKey = ColumnKey.EQE },
            new ColumnConfig { ColumnHeader = "VAM Enabled", ColumnBindingPath = "IsVAMEnabled", ColumnType = ColumnType.CheckBox, IsOptional = false, ColumnKey = ColumnKey.VAM },
            new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.SerialNumber"), ColumnBindingPath = "SerialNumber", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
            new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.TestStatus"), ColumnBindingPath = "DisplayStatus", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
            new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.Uniformity"), ColumnBindingPath = "DataValue", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
            new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.StartTestTime"), ColumnBindingPath = "StartTestTime", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
            new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.EndTestTime"), ColumnBindingPath = "EndTestTime", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
            new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.TotalTime"), ColumnBindingPath = "TotalTime", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
        };
        private List<ColumnConfig> _dynamicColumnConfigs = new List<ColumnConfig>();
        public ObservableCollection<ColumnConfig> SelectedDynamicColumns { get; set; }

        private string _searchSN;
        public string SearchSN { get => _searchSN; set => SetProperty(ref _searchSN, value); }
        private List<MainViewModel.TestItem>? _testQueue;
        private ObservableCollection<DieViewModel> _filteredTestResults;
        public ObservableCollection<DieViewModel> FilteredTestResults { get => _filteredTestResults; set => SetProperty(ref _filteredTestResults, value); }
        #endregion

        #region 进度条核心属性（精简版，适配单Die独立进度）
        #region 新增：手动测试标记属性

        private bool _isManualTesting;
        /// <summary>
        /// 是否为手动测试（控制总进度条显示/隐藏）
        /// </summary>
        public bool IsManualTesting
        {
            get => _isManualTesting;
            set => SetProperty(ref _isManualTesting, value);
        }
        #endregion

        // 单个Die测试进度（0-100）- 绑定到界面单Die进度条
        private double _singleDieTestProgress;
        public double SingleDieTestProgress
        {
            get => _singleDieTestProgress;
            set
            {
                if (SetProperty(ref _singleDieTestProgress, Math.Max(0, Math.Min(100, value))))
                {
                    UpdateTotalProgress(); // 单Die进度更新同步总进度
                    OnPropertyChanged();
                    ProgressText = $"{SingleDieTestProgress:F1}%";
                }
            }
        }

        // 总测试进度（0-100）- 绑定到界面总进度条
        private double _totalTestProgress;
        public double TotalTestProgress
        {
            get => _totalTestProgress;
            set => SetProperty(ref _totalTestProgress, Math.Max(0, Math.Min(100, value)));
        }

        // 总测试Die数
        private int _totalTestCount;
        public int TotalTestCount
        {
            get => _totalTestCount;
            set => SetProperty(ref _totalTestCount, value);
        }

        // 已完成测试Die数
        private int _completedTestCount;
        public int CompletedTestCount
        {
            get => _completedTestCount;
            set
            {
                if (SetProperty(ref _completedTestCount, value))
                {
                    UpdateTotalProgress(); // 完成数更新同步总进度
                    OnPropertyChanged(nameof(ProgressTextAll));

                }
            }
        }

        // 当前测试Die的行列信息（如：X1/Y2）
        private string _currentDieInfo = string.Empty;
        public string CurrentDieInfo
        {
            get => _currentDieInfo;
            set => SetProperty(ref _currentDieInfo, value);
        }
        private string _ProgressText;
        // 进度文本（绑定到界面，显示详细进度信息）
        public string ProgressText
        {
            get
            {
                return _ProgressText;
            }
            set
            {
                _ProgressText = value;
                OnPropertyChanged();
            }
        }
        public string ProgressTextAll
        {
            get
            {
                if (TotalTestCount == 0) return "0/0";
                return $"{CompletedTestCount}/{TotalTestCount}";
            }
        }
        #endregion

        #region 构造函数（保留原有+初始化进度属性）
        public DateTime _currentDieStartTime;
        public int _currentDiePredictSeconds = 60; // 默认60秒


        public System.Timers.Timer _progressUpdateTimer;
        public MappingDataViewModel()
        {
            _selectedItem = null;
            //_selectedFlow = null;
            _dataGrid = null;
            _isIVLCameraEnabled = false;
            _isAutoSN = true;
            CustomMappingVM = new ChipMappingControlViewModel();

            _Timestamp = string.Empty;
            _MappingCsvFilePath = string.Empty;
            // 初始化进度属性
            SingleDieTestProgress = 0;
            TotalTestProgress = 0;
            TotalTestCount = 0;
            CompletedTestCount = 0;
            CurrentDieInfo = string.Empty;
            // 初始化进度更新定时器
            _progressUpdateTimer = new System.Timers.Timer(1000); // 1秒更新一次
            _progressUpdateTimer.Elapsed += OnProgressUpdateTimerElapsed;
            _progressUpdateTimer.AutoReset = true;
          
            // 初始化命令
            SearchCommand = new RelayCommand(ExecuteSearch);
            ClearMappingCommand = new RelayCommand(_ => ClearMapping());
            FlowLoadCommand = new RelayCommand(_ => LoadBuzWPFlows());
            LoadMappingFileCommand = new RelayCommand(_ => LoadMappingFileAsync());
            RefreshStatusCommand = new RelayCommand(RefreshStatus);
            OpenMappingFileCommand = new RelayCommand(OpenMappingFile);
            StartManTestCommand = new RelayCommand(StartManTest);
            SaveTestResultCommand = new RelayCommand(SaveTestResult);
            LoadTestResultCommand = new RelayCommand(LoadTestResult);
            ResetStatusCommand = new RelayCommand(ResetStatus);
            InvertSelectAOICommand = new RelayCommand(ExecuteInvertSelectAOI);
            InvertSelectIVLCommand = new RelayCommand(ExecuteInvertSelectIVL);
            InvertSelectEQECommand = new RelayCommand(ExecuteInvertSelectEQE);
            InvertSelectVAMCommand = new RelayCommand(ExecuteInvertSelectVAM);
           
            // 初始化集合事件
            TestResults.CollectionChanged += AOIItems_CollectionChanged;
            TestResults.CollectionChanged += IVLItems_CollectionChanged;
            TestResults.CollectionChanged += EQEItems_CollectionChanged;
            TestResults.CollectionChanged += VAMItems_CollectionChanged;
            SubscribeItems_AOI(TestResults);
            SubscribeItems_IVL(TestResults);
            SubscribeItems_EQE(TestResults);
            SubscribeItems_VAM(TestResults);

            // 新增：订阅每个 Die 的 PropertyChanged，用于触发自动保存（防抖）
            SubscribeToSaveEvents();

            ProberClientService.Instance.InitializeMapVM(this);
            InitColumnConfigs();
            InitAutoSave();

        }
        #endregion

        #region 进度条核心方法（适配新逻辑，精简且精准）
        /// <summary>
        /// 初始化自动测试进度 - 自动测试开始时调用
        /// </summary>
        /// <param name="selectedDice">选中的测试Die列表</param>
        public void InitializeAutoTestProgress(List<DieViewModel> selectedDice)
        {
            if (selectedDice == null || selectedDice.Count == 0)
            {
                ResetProgressBars();
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                TotalTestCount = selectedDice.Count;
                CompletedTestCount = 0;
                SingleDieTestProgress = 0;
                TotalTestProgress = 0;
                CurrentDieInfo = string.Empty;

                logger.InfoFormat("{0} Dies to test", TotalTestCount);
                OnPropertyChanged(nameof(ProgressTextAll));
            });
        }

        /// <summary>
        /// 开始测试单个Die - 手动/自动测试单个Die时调用
        /// </summary>
        /// <param name="die">当前测试的Die</param>
        public void StartSingleDieTest(DieViewModel die)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 如果当前Die就是正在测试的Die，且进度不是0，不重复初始化
                if (CurrentDieInfo == $"{die.MapX}/{die.MapY}" && SingleDieTestProgress > 0)
                    return;

                CurrentDieInfo = $"{die.MapX}/{die.MapY}";
                SingleDieTestProgress = 0; // 强制重置为0

                _currentDieStartTime = DateTime.Now;

                // 获取预测时间
                _currentDiePredictSeconds = TestTimePredictService.GetPredictTestSeconds();

                // 启动进度更新定时器
                _progressUpdateTimer.Start();
                // 强制更新UI
                OnPropertyChanged(nameof(CurrentDieInfo));
                OnPropertyChanged(nameof(SingleDieTestProgress));
                OnPropertyChanged(nameof(ProgressText));

                logger.DebugFormat("Start testing Die [{0}/{1}], reset single Die progress to 0%", die.MapX, die.MapY);
            });
        }
        /// <summary>
        /// 进度更新定时器事件
        /// </summary>
        private void OnProgressUpdateTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // 3. 核心业务逻辑
            double newProgress = 0;
            bool shouldStopTimer = false;

            if (SingleDieTestProgress >= 100 || TotalTestCount == 0)
            {
                shouldStopTimer = true;
            }
            else
            {
                var elapsed = (DateTime.Now - _currentDieStartTime).TotalSeconds;
                newProgress = Math.Min(99, (elapsed / _currentDiePredictSeconds) * 100);

                if (newProgress > SingleDieTestProgress + 10)
                {
                    newProgress = SingleDieTestProgress + 10;
                }

                if (newProgress < SingleDieTestProgress + 0.5 && SingleDieTestProgress < 99)
                {
                    newProgress = SingleDieTestProgress + 0.5;
                }

                newProgress = Math.Min(99, newProgress);
            }

            if (shouldStopTimer)
            {
                _progressUpdateTimer?.Stop();
                return;
            }


            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (SingleDieTestProgress < 100 && TotalTestCount > 0)
                {
                    SingleDieTestProgress = newProgress;
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        /// <summary>
        /// 更新单个Die进度 - 由DieViewModel定时器触发
        /// </summary>
        /// <param name="progress">单Die进度（0-100）</param>
        /// <param name="stageInfo">阶段信息（用于日志）</param>
        public void UpdateSingleDieProgress(double progress, string stageInfo = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 只接受从主定时器来的进度更新
                SingleDieTestProgress = Math.Min(99, progress); // 限制在99%以内

                OnPropertyChanged(nameof(SingleDieTestProgress));
                OnPropertyChanged(nameof(ProgressText));

                if (!string.IsNullOrEmpty(stageInfo) && logger.IsDebugEnabled)
                    logger.DebugFormat("Die[{0}]Progress Update：{1} - {2:F1}%", CurrentDieInfo, stageInfo, progress);
            });
        }

        /// <summary>
        /// 完成单个Die测试 - 测试完成后调用
        /// </summary>
        public void CompleteSingleDieTest()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 停止进度更新定时器
                _progressUpdateTimer.Stop();

                // 增加边界检查，防止计数溢出
                if (CompletedTestCount < TotalTestCount)
                {
                    CompletedTestCount++;

                    // 拉满当前Die进度到100%
                    SingleDieTestProgress = 100;

                    // 触发属性变更通知
                    OnPropertyChanged(nameof(CompletedTestCount));
                    OnPropertyChanged(nameof(SingleDieTestProgress));
                    OnPropertyChanged(nameof(ProgressText));
                    OnPropertyChanged(nameof(ProgressTextAll));

                    UpdateTotalProgress();

                    logger.DebugFormat("Single Die test completed; cumulative completion: {0}/{1}", CompletedTestCount, TotalTestCount);
                    // ========== 新增：单个Die测试完成时显式保存 ==========
                    DebouncedSaveLastSession(500); // 500ms防抖，避免高频调用
                }
            });
        }

        /// <summary>
        /// 计算总进度 - 核心公式：总进度=已完成/总数量 + 当前Die进度/总数量
        /// </summary>
        public void UpdateTotalProgress()
        {
            if (TotalTestCount <= 0)
            {
                TotalTestProgress = 0;
                return;
            }
            // 修复核心：当前Die未完成时，已完成数不包含当前Die
            int actualCompletedCount = CompletedTestCount;
            // 如果还有未完成的Die，当前Die不计入已完成数
            if (actualCompletedCount < TotalTestCount)
            {
                actualCompletedCount = CompletedTestCount; // 保持原有已完成数
            }

            // 已完成Die的基础进度
            double completedProgress = (double)CompletedTestCount / TotalTestCount * 100;
            // 当前Die的进度贡献（未完成时才计算）
            double currentDieContribution = CompletedTestCount < TotalTestCount
                ? SingleDieTestProgress / 100 * (1.0 / TotalTestCount) * 100
                : 0;

            TotalTestProgress = Math.Min(100, completedProgress + currentDieContribution);

            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(TotalTestProgress));
                OnPropertyChanged(nameof(ProgressTextAll));
            });
        }

        /// <summary>
        /// 重置所有进度条 - 测试停止/重置/完成时调用
        /// </summary>
        public void ResetProgressBars()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 停止定时器但不销毁
                if (_progressUpdateTimer != null)
                {
                    _progressUpdateTimer.Stop();
                }

                SingleDieTestProgress = 0;
                TotalTestProgress = 0;
                TotalTestCount = 0;
                CompletedTestCount = 0;
                CurrentDieInfo = string.Empty;

                // 同步重置手动测试标记
                IsManualTesting = false;

                // 触发所有进度相关属性变更
                OnPropertyChanged(nameof(SingleDieTestProgress));
                OnPropertyChanged(nameof(TotalTestProgress));
                OnPropertyChanged(nameof(TotalTestCount));
                OnPropertyChanged(nameof(CompletedTestCount));
                OnPropertyChanged(nameof(ProgressText));
                OnPropertyChanged(nameof(ProgressTextAll));
                OnPropertyChanged(nameof(CurrentDieInfo));
            });
        }
        #endregion

        #region 原有业务方法（修改StartManFlow/StartAutoFlow，集成新进度逻辑）
        private void StartManTest(object? obj)
        {
            // 新增：检查是否选择了Die
            if (SelectedItem == null || !(SelectedItem is DieViewModel))
            {
                // 弹出提示框
                MessageBox.Show(
                    (string)Application.Current.FindResource("PleaseSelectDieToTest") ?? "请选择需要测试的Die",
                    (string)Application.Current.FindResource("Prompt") ?? "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            StartManFlow();
        }
        private void StartManFlow()
        {
            if (_selectedWPFlow != null && SelectedItem is DieViewModel die)
            {
                // 1. 手动测试初始化：标记IsManualTesting=true（隐藏总进度条）
                IsManualTesting = true;
                // 1. 手动测试进度初始化
                TotalTestCount = 1;
                CompletedTestCount = 0;
                SingleDieTestProgress = 0;
                TotalTestProgress = 0;
                CurrentDieInfo = $"{die.MapX}/{die.MapY}";

                EnableBtnGUI(false);
                ManTestingReady(die);
                // 2. 启动单Die进度跟踪
                StartSingleDieTest(die);
                Task task = MainService.Instance.StartDieTestingAsync(_selectedWPFlow, die);
                ActivateCorrespondingPanel?.Invoke(this, _selectedWPFlow);
            }
        }

        public void StartAutoFlow()
        {
            // 新增：初始化第一个Die标记
            _isFirstDieInAutoTest = true;
            // 新增：检查是否选择了Die
            if (SelectedItem == null || !(SelectedItem is DieViewModel))
            {
                // 弹出提示框
                MessageBox.Show(
                    (string)Application.Current.FindResource("PleaseSelectDieToTest") ?? "请选择需要测试的Die",
                    (string)Application.Current.FindResource("Prompt") ?? "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            if (SelectedWPFlow == null)
            {
                logger.Error("Test flow (Flow) not selected");
                return;
            }

            _testQueue = GetSelectedTestItems();

            int SelectedFlowRunCout = 0;
            foreach (var item in _testQueue)
            {
                if (item.TestType == SelectedWPFlow.FlowType.ToString())
                {
                    SelectedFlowRunCout++;
                }
            }
            if (SelectedFlowRunCout == 0)
            {
                MessageBox.Show(SelectedWPFlow.FlowType.ToString() + Environment.NewLine + "Plese select die for testing first!", (string)Application.Current.FindResource("Prompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            //清除當前測試狀態
            foreach (var item in _testQueue)
            {
                item.Die.ResetUI();
                item.Die.chipViewModel?.SetStatus(ChipStatus.WAITING);
            }

            var selectedDice = GetSelectedDieTestItems();
            // 1. 自动测试进度初始化
            InitializeAutoTestProgress(selectedDice);
            TestingReady(_testQueue);
            EnableBtnGUI(false);
            // 2. 启动自动测试
            _progressUpdateTimer.Start();
            WaferProberData.SelectedWPFlow = _selectedWPFlow;



            MainService.Instance.StartAutoTesting(_selectedWPFlow, selectedDice);
        }

        private void ManTestingReady(DieViewModel die)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fff");
            die.TestingReady(ProberStateStatus.Instance.CurrentWaferId, timestamp);
            if (_isAutoSN) Timestamp = timestamp;
        }

        private void TestingReady(List<MainViewModel.TestItem> testItems)
        {
            CustomMappingVM.DisabledInput = IsProcessing = true;
            string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fff");
            if (_isAutoSN) Timestamp = timestamp;
            foreach (var itemT in testItems)
            {
                itemT.Die.TestingReady(SanitizeFileName(ProberStateStatus.Instance.CurrentWaferId), timestamp);
            }
        }
        public static string SanitizeFileName(string fileName)
        {
            // 定义非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_'); // 将非法字符替换为下划线或其他合法字符
            }
            return fileName;
        }



        public void StopAutoFlow()
        {
            if (_testQueue == null) return;

            foreach (var item in _testQueue)
            {
                item.Die.UnSelected();
                item.Die.StopTestProgressTimer(); // 停止未完成Die的进度定时器
            }

            MainService.Instance.StopAutoTesting();
            ResetProgressBars(); // 重置进度条
            CalculateYieldBySerialNumber();
            _ = SaveLastSessionIfNeededAsync();
            // 新增：重置第一个Die标记
            _isFirstDieInAutoTest = true;
        }
        #endregion

        #region 原有其他方法（保留，集合事件/列配置/良率计算/CSV导出/持久化等）
        private void InitAutoSave()
        {
            if (Application.Current != null)
            {
                Application.Current.Exit -= Application_Exit;
                Application.Current.Exit += Application_Exit;
            }

            try
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await TestResultPersistenceService.CleanupOldSessionsAsync(10); // 改为异步版本
                        await LoadFromPersistenceAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Failed to load last session", ex);
                    }
                }), DispatcherPriority.Background);
            }
            catch { }
        }

        private void Application_Exit(object? sender, ExitEventArgs e)
        {
            try
            {
                var saveTask = Task.Run(async () =>
                {
                    try { await SaveLastSessionAsync().ConfigureAwait(false); }
                    catch (Exception ex) { logger.Warn("Failed to save session on exit", ex); }
                    try { SaveTestResultsToDefaultFile(); }
                    catch (Exception ex) { logger.Warn("Failed to auto-export CSV on exit", ex); }
                });
                if (!saveTask.Wait(TimeSpan.FromSeconds(2))) logger.Warn("Save timed out on exit");
            }
            catch (Exception ex) { logger.Error("Exception occurred on exit", ex); }
        }

        private void OnSelectedChanged(object? value)
        {
            if (value is DieViewModel die && selfClick)
            {
                DebounceTimer.AddOrResetTimer("UpdateSelectedChipCount", 30, () =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CustomMappingVM?.UpdateSelectedChipCount();
                    });
                });
                MainService.Instance.ResultDisplay(die);
                CalculateYieldBySerialNumber();
            }
            else
            {
                selfClick = true;
            }
        }

        private void OnChipDieSelected(object? sender, ChipViewModel chip)
        {
            if (chip == null || TestResults.Count == 0) { SelectedItem = null; return; }
            var targetDie = TestResults.FirstOrDefault(die => die.Id == chip.Id);
            if (targetDie != null) { selfClick = false; SelectedItem = targetDie; ManScrollToItem(targetDie); }
        }

        public void LoadBuzWPFlows()
        {
            List<TScgdBuzProductDetail> flows = WaferProberDBService.LoadBuzFlows();
            WPFlows.Clear();
            if (flows != null && flows.Count > 0)
            {
                foreach (var flow in flows) WPFlows.Add(new WPFlowViewModel(flow));
                if (WPFlows.Count > 0)
                {
                    if (Config.SelectedIndex >=0 && Config.SelectedIndex < WPFlows.Count)
                    {
                        SelectedWPFlow = WPFlows[Config.SelectedIndex];
                    }
                    else
                    {
                        SelectedWPFlow = WPFlows[0];

                    }

                }
            }
        }

        private void OpenMappingFile(object? obj)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = (string)Application.Current.FindResource("CSVFile") };
            if (openFileDialog.ShowDialog() == true)
            {
                MappingCsvFilePath = openFileDialog.FileName;
                LoadMappingFileFromCsv();
                MainService.Instance.Maintenance();
            }
        }

        private void LoadMappingFileAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = "result.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try { ResultService.LoadFromCSV(openFileDialog.FileName, TestResults); _dataGrid?.Items.Refresh(); MainService.Instance.Maintenance(); logger.InfoFormat("Result loaded successfully：{0}", openFileDialog.FileName); }
                catch (Exception ex) { logger.Error(ex); }
            }
        }

        private void RefreshStatus(object? obj) => _dataGrid?.Items.Refresh();


        public void InitializeServive(MainService main)
        {
            mainService = main;
            mainService.ChipSelected += OnChipDieSelected;
            mainService.TestingCompleted += OnTestingCompleted;
            mainService.PreAutoTestingNextDie += OnAutoTestingNextDie;
        }

        private void OnTestingCompleted(object? sender, TestCompletedEventArgs e)
        {
            EnableBtnGUI(true);
            CalculateYieldBySerialNumber();
            AutoExportSummaryResult();
            SingleDieTestProgress = 100; // 拉满当前Die进度
            UpdateTotalProgress();
            OnPropertyChanged(nameof(ProgressText));

            // 关键：测试完成后重置IsManualTesting=false
            if (!e.IsAuto) IsManualTesting = false;
            if (e.IsAuto && CompletedTestCount >= TotalTestCount) ResetProgressBars(); // 自动测试全部完成，重置进度条
            _ = SaveLastSessionIfNeededAsync();
        }

        private void OnAutoTestingNextDie(object? sender, (DieViewModel? preDie, DieViewModel nextDie) e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.preDie != null) e.preDie.UnSelected();
                SelectedItem = e.nextDie;
                // 新增：仅IVL测试且是第一个Die时，切换到总览图
                if (_isFirstDieInAutoTest && SelectedWPFlow?.FlowType.ToString().Contains("IVL") == true)
                {
                    // 调用DockMainWindow的切换方法（总览图）
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainVM = (MainViewModel)Application.Current.MainWindow.DataContext;
                        mainVM.ActivateSpectralInnerTabAction?.Invoke();
                    });
                    // 标记为非第一个Die，后续不再切换
                    _isFirstDieInAutoTest = false;
                }
                StartSingleDieTest(e.nextDie); // 下一个Die开始测试，重置单Die进度
            });
        }

        public void EnableBtnGUI(bool enabled)
        {
            CustomMappingVM.DisabledInput = IsProcessing = !enabled;
            OnPropertyChanged(nameof(IsNotProcessing));
        }

        private void ClearMapping()
        {
            // 1. 停止并释放进度条定时器
            if (_progressUpdateTimer != null)
            {
                _progressUpdateTimer.Elapsed -= OnProgressUpdateTimerElapsed;
                _progressUpdateTimer.Stop();
                _progressUpdateTimer.Dispose();
                _progressUpdateTimer = null;
            }
            // 新增：重置选中数量
            if (CustomMappingVM != null)
            {
                CustomMappingVM.DieTotal = 0;
                CustomMappingVM.OnPropertyChanged(nameof(CustomMappingVM.DieTotal));
            }
            // 2. 停止并释放自动保存防抖定时器
            lock (_autoSaveLock)
            {
                if (_autoSaveDebounceTimer != null)
                {
                    _autoSaveDebounceTimer.Stop();
                    _autoSaveDebounceTimer.Dispose();
                    _autoSaveDebounceTimer = null;
                }
            }

            // 3. 释放所有Die的资源+解绑事件
            lock (TestResults)
            {
                foreach (var die in TestResults)
                {
                    die.Dispose();
                    // 解绑自动保存的PropertyChanged事件
                    die.PropertyChanged -= Die_PropertyChangedForSave;
                }
                // 清空集合前先解绑集合变更事件
                TestResults.CollectionChanged -= AOIItems_CollectionChanged;
                TestResults.CollectionChanged -= IVLItems_CollectionChanged;
                TestResults.CollectionChanged -= EQEItems_CollectionChanged;
                TestResults.CollectionChanged -= VAMItems_CollectionChanged;

                TestResults.Clear();
            }

            // 4. 重置进度条和状态
            ResetProgressBars();

            // 5. 重新绑定集合变更事件（供下次使用）
            TestResults.CollectionChanged += AOIItems_CollectionChanged;
            TestResults.CollectionChanged += IVLItems_CollectionChanged;
            TestResults.CollectionChanged += EQEItems_CollectionChanged;
            TestResults.CollectionChanged += VAMItems_CollectionChanged;

            // 6. 清理映射VM
            CustomMappingVM?.Cleanup();
            CustomMappingVM.Chips.Clear();
        }

        private List<DieViewModel> GetSelectedDieTestItems()
        {
            var testQueue = new List<DieViewModel>();
            var fType = _selectedWPFlow?.FlowType;
            foreach (var die in TestResults)
            {
                if (die.IsAOIEnabled && fType == CVWaferProberFlowType.AOI) testQueue.Add(die);
                if (die.IsIVLEnabled && (fType == CVWaferProberFlowType.IVL || fType == CVWaferProberFlowType.IVL_SP || fType == CVWaferProberFlowType.IVL_Camera)) testQueue.Add(die);
                if (die.IsEQEEnabled && fType == CVWaferProberFlowType.EQE) testQueue.Add(die);
                if (die.IsVAMEnabled && fType == CVWaferProberFlowType.VAM) testQueue.Add(die);
            }
            return SortEfficiently(testQueue);
        }

        public List<DieViewModel> SortEfficiently(List<DieViewModel> testQueue)
        {
            var list = testQueue.OrderBy(d => d.MapY).ThenBy(d => d.MapX).ToList();
            var result = new List<DieViewModel>();
            int? currentY = int.MinValue;
            List<DieViewModel> currentGroup = new List<DieViewModel>();
            int groupIndex = 0;
            foreach (var item in list)
            {
                if (item.MapY != currentY)
                {
                    if (groupIndex % 2 == 1) currentGroup.Reverse();
                    result.AddRange(currentGroup);
                    currentGroup = new List<DieViewModel>();
                    currentY = item.MapY;
                    groupIndex++;
                }
                currentGroup.Add(item);
            }
            if (groupIndex % 2 == 1) currentGroup.Reverse();
            result.AddRange(currentGroup);
            return result;
        }

        public void OpenSummaryConfig()
        {
            var configWindow = new SummaryConfigWindow(_dynamicColumnConfigs);
            if (configWindow.ShowDialog() == true)
            {
                SelectedDynamicColumns.Clear();
                foreach (var config in configWindow.SelectedColumns)
                {
                    SelectedDynamicColumns.Add(config);
                    var targetConfig = _dynamicColumnConfigs.First(c => c.ColumnBindingPath == config.ColumnBindingPath);
                    targetConfig.IsSelected = config.IsSelected;
                }
                UpdateDataGridColumns();
            }
        }

        #region 全选/反选事件（保留）
        private void AOIItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                SubscribeItems_AOI(e.NewItems.Cast<DieViewModel>());
            if (e.OldItems != null)
                UnsubscribeItems_AOI(e.OldItems.Cast<DieViewModel>());
            UpdateSelectAllAOIState();
        }

        private void SubscribeItems_AOI(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items) item.PropertyChanged += AOI_Item_PropertyChanged;
        }

        private void UnsubscribeItems_AOI(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items) item.PropertyChanged -= AOI_Item_PropertyChanged;
        }

        private void AOI_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsAOIEnabled) && !_isUpdatingFromHeader_AOI)
            {
                UpdateSelectAllAOIState();
                // 新增：当单个Die的AOI启用状态改变时，更新Total
                UpdateChipMappingSelectedCount();
            }
        }

        private void IVLItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) SubscribeItems_IVL(e.NewItems.Cast<DieViewModel>());
            if (e.OldItems != null) UnsubscribeItems_IVL(e.OldItems.Cast<DieViewModel>());
            UpdateSelectAllIVLState();
        }

        private void SubscribeItems_IVL(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
                item.PropertyChanged += IVL_Item_PropertyChanged;
        }

        private void UnsubscribeItems_IVL(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
                item.PropertyChanged -= IVL_Item_PropertyChanged;
        }

        private void IVL_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsIVLEnabled) && !_isUpdatingFromHeader_IVL)
            {
                UpdateSelectAllIVLState();
                // 新增：当单个Die的IVL启用状态改变时，更新Total
                UpdateChipMappingSelectedCount();
            }
        }

        private void EQEItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                SubscribeItems_EQE(e.NewItems.Cast<DieViewModel>());
            if (e.OldItems != null)
                UnsubscribeItems_EQE(e.OldItems.Cast<DieViewModel>());
            UpdateSelectAllEQEState();
        }

        private void SubscribeItems_EQE(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items) item.PropertyChanged += EQE_Item_PropertyChanged;
        }

        private void UnsubscribeItems_EQE(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items) item.PropertyChanged -= EQE_Item_PropertyChanged;
        }

        private void EQE_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsEQEEnabled) && !_isUpdatingFromHeader_EQE)
            {
                UpdateSelectAllEQEState();
                // 新增：当单个Die的EQE启用状态改变时，更新Total
                UpdateChipMappingSelectedCount();
            }
        }

        private void VAMItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                SubscribeItems_VAM(e.NewItems.Cast<DieViewModel>());
            if (e.OldItems != null)
                UnsubscribeItems_VAM(e.OldItems.Cast<DieViewModel>());
            UpdateSelectAllVAMState();
        }

        private void SubscribeItems_VAM(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
                item.PropertyChanged += VAM_Item_PropertyChanged;
        }

        private void UnsubscribeItems_VAM(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
                item.PropertyChanged -= VAM_Item_PropertyChanged;
        }

        private void VAM_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsVAMEnabled) && !_isUpdatingFromHeader_VAM)
            {
                UpdateSelectAllVAMState();
                // 新增：当单个Die的VAM启用状态改变时，更新Total
                UpdateChipMappingSelectedCount();
            }
        }

        private void UpdateSelectAllAOIState()
        {
            if (TestResults.Count == 0)
            {
                SelectAllAOI = false;
                return;
            }
            int selectedCount = TestResults.Count(item => item.IsAOIEnabled);
            SelectAllAOI = selectedCount switch
            {
                0 => false,
                var c when c == TestResults.Count => true,
                _ => null
            };
        }

        private void UpdateSelectAllIVLState()
        {
            if (TestResults.Count == 0)
            {
                SelectAllIVL = false;
                return;
            }
            int selectedCount = TestResults.Count(item => item.IsIVLEnabled);
            SelectAllIVL = selectedCount switch
            {
                0 => false,
                var c when c == TestResults.Count => true,
                _ => null
            };
        }

        private void UpdateSelectAllEQEState()
        {
            if (TestResults.Count == 0)
            {
                SelectAllEQE = false;
                return;
            }
            int selectedCount = TestResults.Count(item => item.IsEQEEnabled);
            SelectAllEQE = selectedCount switch
            {
                0 => false,
                var c when c == TestResults.Count => true,
                _ => null
            };
        }

        private void UpdateSelectAllVAMState()
        {
            if (TestResults.Count == 0)
            {
                SelectAllVAM = false;
                return;
            }
            int selectedCount = TestResults.Count(item => item.IsVAMEnabled);
            SelectAllVAM = selectedCount switch
            {
                0 => false,
                var c when c == TestResults.Count => true,
                _ => null
            };
        }

        private async void ExecuteInvertSelectAOI(object obj)
        {
            try
            {
                _isBatchUpdating = true; 
                _isUpdatingFromHeader_AOI = true;
                Mouse.OverrideCursor = Cursors.Wait;

                // 批量设置
                foreach (var item in TestResults)
                {
                    // 只有当状态需要改变时才设置，减少不必要的事件触发
                    item.IsAOIEnabled = !item.IsAOIEnabled;
                }

                // 延迟刷新，让UI有机会处理
                await Task.Delay(10);

                // 一次性更新
                _dataGrid?.Items.Refresh();
                UpdateSelectAllAOIState();
                UpdateChipMappingSelectedCount();
            }
            finally
            {
                _isUpdatingFromHeader_AOI = false;
                Mouse.OverrideCursor = null;
            }
           
        }

        private async void ExecuteInvertSelectIVL(object obj)
        {
            try
            {
                _isUpdatingFromHeader_IVL = true;
                Mouse.OverrideCursor = Cursors.Wait;

                // 计算目标状态
                bool targetState = !TestResults.FirstOrDefault()?.IsIVLEnabled ?? false;

                // 批量设置
                foreach (var item in TestResults)
                {
                    
                    item.IsIVLEnabled = !item.IsIVLEnabled;
                }

                // 延迟刷新，让UI有机会处理
                await Task.Delay(10);

                // 一次性更新
                _dataGrid?.Items.Refresh();
                UpdateSelectAllIVLState();
                UpdateChipMappingSelectedCount();
            }
            finally
            {
                _isUpdatingFromHeader_IVL = false;
                Mouse.OverrideCursor = null;
            }
        }

        private async void ExecuteInvertSelectEQE(object obj)
        {
            try
            {
                _isUpdatingFromHeader_EQE = true;
                Mouse.OverrideCursor = Cursors.Wait;

                // 计算目标状态
                bool targetState = !TestResults.FirstOrDefault()?.IsEQEEnabled ?? false;

                // 批量设置
                foreach (var item in TestResults)
                {

                    item.IsEQEEnabled = !item.IsEQEEnabled;
                }

                // 延迟刷新，让UI有机会处理
                await Task.Delay(10);

                // 一次性更新
                _dataGrid?.Items.Refresh();
                UpdateSelectAllEQEState();
                UpdateChipMappingSelectedCount();
            }
            finally
            {
                _isUpdatingFromHeader_EQE = false;
                Mouse.OverrideCursor = null;
            }
        }

        private async void ExecuteInvertSelectVAM(object obj)
        {
            try
            {
                _isUpdatingFromHeader_VAM = true;
                Mouse.OverrideCursor = Cursors.Wait;

                // 计算目标状态
                bool targetState = !TestResults.FirstOrDefault()?.IsVAMEnabled ?? false;

                // 批量设置
                foreach (var item in TestResults)
                {

                    item.IsVAMEnabled = !item.IsVAMEnabled;
                }

                // 延迟刷新，让UI有机会处理
                await Task.Delay(10);

                // 一次性更新
                _dataGrid?.Items.Refresh();
                UpdateSelectAllVAMState();
                UpdateChipMappingSelectedCount();
            }
            finally
            {
                _isUpdatingFromHeader_VAM = false;
                Mouse.OverrideCursor = null;
            }
        }
        #endregion

        // 新增：更新ChipMapping中的选中数量
        private void UpdateChipMappingSelectedCount()
        {
            if (CustomMappingVM == null) return;

            // 获取当前选中的Die ID列表（只要任一测试启用，就视为选中）
            var selectedIds = TestResults.Where(d => d.IsAOIEnabled || d.IsIVLEnabled || d.IsEQEEnabled || d.IsVAMEnabled)
                                         .Select(d => (uint)d.Id)
                                         .ToList();

            // 更新ChipMapping中的选中状态和数量
            CustomMappingVM.SetSelectedChips(selectedIds);
        }


        public void LoadMappingFile(string mappingFile)
        {
            MappingCsvFilePath = mappingFile;
            Task.Factory.StartNew(() => LoadMappingFileFromCsvAsync());
        }

        private void LoadMappingFileFromCsvAsync() => Application.Current.Dispatcher.Invoke(LoadMappingFileFromCsv);

        private void LoadMappingFileFromCsv()
        {
            List<CVMappingData> mappingData = null;
            if (!File.Exists(MappingCsvFilePath))
            {
                logger.WarnFormat("File does not exist：{0}", MappingCsvFilePath); return;
            }
            bool bR = CsvMappingDataTool.LoadMappingCsv(MappingCsvFilePath, ref mappingData);
            if (bR && mappingData != null && mappingData.Count > 0)
            {
                CustomMappingVM.RefreshFromMap(mappingData);
                // --- 关键：先 Dispose 旧的 TestResults 项，解绑定时器/事件，防止旧计时器继续触发 ---
                lock (TestResults)
                {
                    foreach (var oldDie in TestResults.ToList())
                    {
                        try
                        {
                            oldDie.Dispose();
                        }
                        catch (Exception ex)
                        {
                            logger.Warn("Dispose old DieViewModel failed", ex);
                        }
                    }
                    TestResults.Clear();
                }
                ObservableCollection<DieViewModel> _TestResults = new ObservableCollection<DieViewModel>();
                foreach (var map in CustomMappingVM.Chips)
                {
                    DieViewModel dieViewModel = new DieViewModel(map);
                    dieViewModel.Temperature = CustomMappingVM.Temperatures.ToString("F1");
                    dieViewModel.Pressure = CustomMappingVM.Pressure;
                    dieViewModel.ProbingCardSN = CustomMappingVM.SN;
                    dieViewModel.TouchDownCounts = CustomMappingVM.TDCount; 
                    _TestResults.Add(dieViewModel);
                }
                var sorted = _TestResults.OrderBy(x => x.MapY).ToList();
                //TestResults.Clear();
                foreach (var item in sorted) if (item.Status != ChipStatus.SKIP) TestResults.Add(item);
            }
            BuildSNIndex();
            if (string.IsNullOrWhiteSpace(SearchSN))
                FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);
            else ExecuteSearch();
            CalculateYieldBySerialNumber();
        }

        private Dictionary<string, List<DieViewModel>> _snIndex = new Dictionary<string, List<DieViewModel>>(StringComparer.OrdinalIgnoreCase);
        private void BuildSNIndex()
        {
            _snIndex.Clear();
            foreach (var die in TestResults)
            {
                if (string.IsNullOrEmpty(die.SerialNumber)) continue;
                if (!_snIndex.ContainsKey(die.SerialNumber)) _snIndex[die.SerialNumber] = new List<DieViewModel>();
                _snIndex[die.SerialNumber].Add(die);
            }
        }

        private void ExecuteSearch(object parameter = null)
        {
            if (string.IsNullOrWhiteSpace(SearchSN)) FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);
            else
            {
                var searchKey = SearchSN.Trim();
                if (_snIndex.TryGetValue(searchKey, out var results)) FilteredTestResults = new ObservableCollection<DieViewModel>(results);
                else
                {
                    var query = TestResults.Where(die => !string.IsNullOrEmpty(die.SerialNumber) && die.SerialNumber.Contains(searchKey, StringComparison.OrdinalIgnoreCase));
                    FilteredTestResults = new ObservableCollection<DieViewModel>(query);
                }
            }
        }

        private void ManScrollToItem(object? toItem)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_dataGrid != null && toItem != null) { _dataGrid.ScrollIntoView(toItem); _dataGrid.UpdateLayout(); }
            });
        }

        private void InitColumnConfigs()
        {
            _dynamicColumnConfigs = new List<ColumnConfig>
            {
                new ColumnConfig { ColumnHeader = "LightOnStatus", ColumnBindingPath = "LightOnStatus", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "RegisterPixels", ColumnBindingPath = "RegisterPixels", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "Final Class", ColumnBindingPath = "FinalClass", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "AOI GradeLevel", ColumnBindingPath = "AOIGradeLevel", IsSelected = true, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "Black Pattern", ColumnBindingPath = "BlackPattern", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "Luminance(nit)", ColumnBindingPath = "Luminance", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "Voltage(v)", ColumnBindingPath = "Voltage", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "Current(mA)", ColumnBindingPath = "Current", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "Dominant Wavelength", ColumnBindingPath = "DominantWavelength", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "Temperature(℃)", ColumnBindingPath = "Temperature", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "Pixel Logic", ColumnBindingPath = "PixelLogic", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "Pin Pressure", ColumnBindingPath = "Pressure", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "TouchDown Counts", ColumnBindingPath = "TouchDownCounts", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "Probing Card SN", ColumnBindingPath = "ProbingCardSN", IsSelected = false, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "AxisX", ColumnBindingPath = "MotionAxisX", IsSelected = true, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "AxisY", ColumnBindingPath = "MotionAxisY", IsSelected = true, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
                new ColumnConfig { ColumnHeader = "AxisZ", ColumnBindingPath = "MotionAxisZ", IsSelected = true, IsOptional = true, ColumnType = ColumnType.Text, ColumnKey = ColumnKey.Other },
            };
            SelectedDynamicColumns = new ObservableCollection<ColumnConfig>(_dynamicColumnConfigs.Where(c => c.IsSelected));
        }

        public void UpdateDataGridColumns()
        {
            if (_dataGrid == null) return;
            while (_dataGrid.Columns.Count > StaticColumnCount) _dataGrid.Columns.RemoveAt(StaticColumnCount);
            foreach (var config in SelectedDynamicColumns)
            {
                if (config.ColumnType == ColumnType.CheckBox)
                {
                    var templateColumn = new DataGridTemplateColumn { Header = config.ColumnHeader };
                    var checkBoxFactory = new FrameworkElementFactory(typeof(CheckBox));
                    checkBoxFactory.SetBinding(CheckBox.IsCheckedProperty, new Binding(config.ColumnBindingPath) { Mode = BindingMode.OneWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                    checkBoxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
                    checkBoxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                    templateColumn.CellTemplate = new DataTemplate { VisualTree = checkBoxFactory };
                    _dataGrid.Columns.Add(templateColumn);
                }
                else
                {
                    _dataGrid.Columns.Add(new DataGridTextColumn
                    {
                        Header = config.ColumnHeader,
                        Binding = new Binding(config.ColumnBindingPath) { Mode = BindingMode.OneWay, TargetNullValue = "", StringFormat = config.ColumnBindingPath.Contains("Time") ? "yyyy-MM-dd HH:mm:ss" : null }
                    });
                }
            }
        }

        public void CalculateYieldBySerialNumber()
        {
            try
            {
                if (TestResults == null || !TestResults.Any()) { YieldInfo = "0/0 (0.00%)"; return; }
                var testedDices = TestResults.Where(d => !string.IsNullOrWhiteSpace(d.SerialNumber)).ToList();
                if (!testedDices.Any()) { YieldInfo = "0/0 (0.00%)"; return; }
                int successCount = testedDices.Count(d => d.Status == ChipStatus.OK || d.Status == ChipStatus.IVL_COMPLETED || d.Status == ChipStatus.EQE_COMPLETED);
                double yieldRate = (double)successCount / testedDices.Count * 100;
                YieldInfo = $"{successCount}/{testedDices.Count} ({yieldRate:F2}%)";
            }
            catch (Exception ex)
            {
                logger.Error("Yield calculation exception", ex);
                YieldInfo = (string)Application.Current.FindResource("CalculationException");
            }
        }

        private void AutoExportSummaryResult()
        {
            try
            {
                if (TestResults == null || !TestResults.Any())
                {
                    logger.Info("No test results, skip Summary export");
                    return;
                }
                string exportRootPath = ConfigManager.Config.ExportPathSettings?.SummaryExportPath;//?? @"D:\Project\IVL"
                if (!Directory.Exists(exportRootPath)) Directory.CreateDirectory(exportRootPath);
                var savePath = Path.Combine(exportRootPath, $"Summary_Result_{DateTime.Now:yyyyMMddHHmmss}.csv");
                var allSelectedColumns = new List<ColumnConfig>();
                allSelectedColumns.AddRange(_staticColumnConfigs);
                allSelectedColumns.AddRange(SelectedDynamicColumns);
                using (var writer = new StreamWriter(savePath, false, System.Text.Encoding.UTF8))
                {
                    var headers = allSelectedColumns.Select(c => c.ColumnHeader).ToList();
                    writer.WriteLine(string.Join(",", headers));
                    foreach (var die in TestResults)
                    {
                        var rowData = new List<string>();
                        foreach (var config in allSelectedColumns)
                        {
                            var prop = die.GetType().GetProperty(config.ColumnBindingPath);
                            if (prop == null) { rowData.Add(""); continue; }
                            var value = prop.GetValue(die);
                            if (value == null || value == DBNull.Value) rowData.Add("");
                            else if (value is bool boolValue) rowData.Add(boolValue ? "Y" : "N");
                            else if (value is DateTime dateTimeValue) rowData.Add(dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss"));
                            else rowData.Add(value.ToString());
                        }
                        writer.WriteLine(string.Join(",", rowData.Select(d => d.Contains(",") ? $"\"{d}\"" : d)));
                    }
                }
                logger.InfoFormat("Summary results automatically exported：{0}", savePath);
            }
            catch (Exception ex) { logger.Error("Failed to export Summary results", ex); }
        }

        private void ResetStatus(object? obj)
        {
            foreach (var die in TestResults) die.ResetStatus();
            CalculateYieldBySerialNumber();
            ResetProgressBars();
            DebouncedSaveLastSession(300);
        }

        private void SaveTestResult(object? obj)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = string.Format("{0}_{1}_result.csv", ProberStateStatus.Instance.CurrentWaferId, _Timestamp),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                try { ResultService.SaveToCSV(saveFileDialog.FileName, TestResults); logger.InfoFormat("Results saved successfully：{0}", saveFileDialog.FileName); }
                catch (Exception ex) { logger.Error(ex); }
            }
        }

        private void LoadTestResult(object? obj)
        {
            if (obj is string path && !string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    ResultService.LoadFromCSV(path, TestResults);
                    _dataGrid?.Items.Refresh();
                    logger.InfoFormat("Result loaded successfully：{0}", path);
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
                return;
            }
            if (obj == null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await LoadFromPersistenceAsync();
                        // 确保在UI线程中刷新
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 强制刷新DataGrid
                            _dataGrid?.Items.Refresh();


                            BuildSNIndex();
                            CalculateYieldBySerialNumber();

                            logger.Info("Successfully loaded the results of the last session from the persistent storage");
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Failed to load from persistence", ex);
                    }
                });
                return;
            }
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = "result.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    ResultService.LoadFromCSV(openFileDialog.FileName, TestResults);
                    _dataGrid?.Items.Refresh();
                    logger.InfoFormat("Result loaded successfully：{0}", openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }

        public async Task LoadFromPersistenceAsync()
        {
            try
            {
                var dtos = await TestResultPersistenceService.LoadAsync();
                if (dtos == null || dtos.Count == 0) return;


                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.HasShutdownStarted) return;
                // 异步执行UI更新，超时3秒
                var uiTask = dispatcher.InvokeAsync(() =>
                {
                    lock (TestResults) // 加锁避免并发修改
                    {
                        ApplyDtosToTestResults(dtos);
                    }
                    _dataGrid?.Items.Refresh();
                    BuildSNIndex();
                    CalculateYieldBySerialNumber();

                    try
                    {
                        DieViewModel? toSelect = null;
                        var dtoWithSN = dtos.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.SerialNumber));
                        if (dtoWithSN != null)
                            toSelect = TestResults.FirstOrDefault(t =>
                                !string.IsNullOrWhiteSpace(t.SerialNumber) &&
                                t.SerialNumber.Equals(dtoWithSN.SerialNumber, StringComparison.OrdinalIgnoreCase));
                        if (toSelect == null && TestResults.Count > 0) toSelect = TestResults[0];
                        if (toSelect != null)
                        {
                            selfClick = false;
                            SelectedItem = toSelect;
                            ManScrollToItem(toSelect);
                            logger.InfoFormat("Auto-selected after loading：Id={0}, SN={1}", toSelect.Id, toSelect.SerialNumber);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Failed to auto-select after loading", ex);
                    }
                });
                var completedTask = await Task.WhenAny(uiTask.Task, Task.Delay(3000));
                if (completedTask == uiTask.Task)
                {
                    if (uiTask.Task.IsFaulted)
                    {
                        logger.Error("UI update failed", uiTask.Task.Exception);
                    }
                }
                else
                {
                    logger.Warn("LoadFromPersistence UI update timed out");
                }
            }
            catch (Exception ex)
            {
                logger.Error("Failed to load from persistence", ex);
            }
        }
        private void ApplyDtosToTestResults(List<TestResultDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return;

            // 创建一个列表记录所有被更新的Die，最后统一刷新
            var updatedDies = new List<DieViewModel>();

            lock (TestResults)
            {
                foreach (var dto in dtos)
                {
                    try
                    {
                        DieViewModel? die = null;

                        // 优先按ID匹配 → 其次按SN匹配 → 最后按行列匹配
                        if (dto.Id != 0)
                            die = TestResults.FirstOrDefault(d => d.Id == dto.Id);
                        if (die == null && !string.IsNullOrEmpty(dto.SerialNumber))
                            die = TestResults.FirstOrDefault(d =>
                                !string.IsNullOrEmpty(d.SerialNumber) &&
                                d.SerialNumber.Trim().Equals(dto.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (die == null && dto.MapX.HasValue && dto.MapY.HasValue)
                            die = TestResults.FirstOrDefault(d => d.MapX == dto.MapX && d.MapY == dto.MapY);

                        if (die == null) continue;

                        // 1. 恢复基础选中状态
                        die.IsAOIEnabled = dto.IsAOIEnabled;
                        die.IsIVLEnabled = dto.IsIVLEnabled;
                        die.IsEQEEnabled = dto.IsEQEEnabled;
                        die.IsVAMEnabled = dto.IsVAMEnabled;
                        die.SerialNumber = dto.SerialNumber;

                        // 2. 【核心修复】优先使用保存的ChipStatus枚举值恢复状态
                        ChipStatus statusToRestore = ChipStatus.WAITING;
                        bool statusRestored = false;

                        // 优先从ChipStatus恢复
                        if (!string.IsNullOrWhiteSpace(dto.ChipStatus))
                        {
                            if (Enum.TryParse<ChipStatus>(dto.ChipStatus.Trim(), true, out var parsedStatus))
                            {
                                statusToRestore = parsedStatus;
                                statusRestored = true;
                                //logger.Debug($"Recover status from ChipStatus: {dto.ChipStatus} -> {parsedStatus}");
                            }
                        }

                        // 降级方案：如果ChipStatus不存在或解析失败，尝试从DisplayStatus解析
                        if (!statusRestored && !string.IsNullOrWhiteSpace(dto.DisplayStatus))
                        {
                            string raw = dto.DisplayStatus.Trim().Trim('"').Trim();

                            // 尝试直接解析枚举
                            if (Enum.TryParse<ChipStatus>(raw, true, out var enumStatus))
                            {
                                statusToRestore = enumStatus;
                                statusRestored = true;
                                //logger.Debug($"Parse the enumeration directly from DisplayStatus: {raw} -> {enumStatus}");
                            }
                            else
                            {
                                // 尝试通过本地化工具解析
                                try
                                {
                                    statusToRestore = ChipStatusTool.GetStatusFromDisplay(raw, die.IsChinese);
                                    statusRestored = true;
                                    logger.Debug($"Localized parsing from Display Status (Chinese): {raw} -> {statusToRestore}");
                                }
                                catch
                                {
                                    try
                                    {
                                        statusToRestore = ChipStatusTool.GetStatusFromDisplay(raw, !die.IsChinese);
                                        statusRestored = true;
                                        logger.Debug($"Localized parsing from Display Status (in English): {raw} -> {statusToRestore}");
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.Warn($"Unable to parse status string: {raw}", ex);
                                    }
                                }
                            }
                        }

                        // 3. 应用恢复的状态
                        if (statusRestored)
                        {
                            // 使用ChangeStatusOnly更新状态
                            die.ChangeStatusOnly(statusToRestore);

                            // 【重要】手动触发所有相关属性的更新
                            die.OnPropertyChanged(nameof(DieViewModel.Status));
                            die.OnPropertyChanged(nameof(DieViewModel.DisplayStatus));
                        }

                        // 4. 恢复其他测试数据
                        if (die.chipViewModel?.ChipData != null &&
                            !string.IsNullOrEmpty(dto.DataValue) &&
                            double.TryParse(dto.DataValue, out double dataValue))
                        {
                            die.chipViewModel.ChipData.DataValue = dataValue;
                            die.RefreshDataValue();
                        }

                        die.StartTestTime = dto.StartTestTime;
                        die.EndTestTime = dto.EndTestTime;
                        die.TotalTime = dto.TotalTime;
                        die.AOIGradeLevel = dto.AOIGradeLevel;
                        die.BlackPattern = dto.BlackPattern;

                        // 记录被更新的Die
                        updatedDies.Add(die);
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Recovery of Die data failed: {ex.Message}");
                    }
                }
            }

            // 5. 【关键】在UI线程中强制刷新所有被更新的Die
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var die in updatedDies)
                {
                    // 再次触发所有属性的更新
                    die.OnPropertyChanged(nameof(DieViewModel.Status));
                    die.OnPropertyChanged(nameof(DieViewModel.DisplayStatus));
                    die.OnPropertyChanged(nameof(DieViewModel.StartTestTime));
                    die.OnPropertyChanged(nameof(DieViewModel.EndTestTime));
                    die.OnPropertyChanged(nameof(DieViewModel.TotalTime));
                    die.OnPropertyChanged(nameof(DieViewModel.DataValue));
                }

                // 刷新整个DataGrid
                _dataGrid?.Items.Refresh();

                // 重新构建索引和计算良率
                BuildSNIndex();
                CalculateYieldBySerialNumber();
            });
        }
        private void SaveTestResultsToDefaultFile()
        {
            try
            {
                if (TestResults == null || !TestResults.Any()) { logger.Info("No test results, skip auto-save"); return; }
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CVWaferProber", "AutoSaves");
                Directory.CreateDirectory(folder);
                string fileName = $"TestResults_Auto_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullPath = Path.Combine(folder, fileName);
                ResultService.SaveToCSV(fullPath, TestResults);
                logger.InfoFormat("Auto-saving test results to：{0}", fullPath);
            }
            catch (Exception ex) { logger.Error("Failed to auto-save test results", ex); }
        }
        public async Task SaveLastSessionAsync()
        {
            await SaveLastSessionIfNeededAsync();
        }
        // 新增：检查并保存（供防抖或直接调用）
        private async Task SaveLastSessionIfNeededAsync()
        {
            try
            {
                // 空值保护：先检查TestResults是否为null
                if (TestResults == null || !TestResults.Any())
                {
                    logger.Info("TestResults is empty, skip saving");
                    return;
                }

                // 判断是否有有意义的结果
                bool hasMeaningful = TestResults.Any(r =>
                    (r.Status != null && r.Status != ChipStatus.WAITING) ||
                    !string.IsNullOrWhiteSpace(r.SerialNumber) ||
                    r.StartTestTime.HasValue ||
                    r.EndTestTime.HasValue);

                if (!hasMeaningful)
                {
                    logger.Info("No meaningful test results detected, skip saving last session to persistence.");
                    return;
                }

                // 转换为DTO（添加空值过滤）
                var dtos = TestResults.Select(r => TestResultDto.FromObject(r))
                                      .Where(x => x != null) // 过滤null的DTO
                                      .ToList();

                if (dtos.Count == 0)
                {
                    logger.Info("No valid DTOs to save");
                    return;
                }

                // 【修改2】调用改造后的SaveAsync（忽略返回值，保持兼容）
                await TestResultPersistenceService.SaveAsync(dtos);
                logger.Info("Successfully saved last session results to persistence");
            }
            catch (Exception ex)
            {
                logger.Error("Failed to save the last session", ex);
            }
        }

        public void SelectItemById(uint id)
        {
            var itemToSelect = TestResults.FirstOrDefault(item => item.Id == id);
            if (itemToSelect != null) { selfClick = false; SelectedItem = itemToSelect; }
        }


        private List<MainViewModel.TestItem> GetSelectedTestItems()
        {
            var testQueue = new List<MainViewModel.TestItem>();
            foreach (var die in TestResults)
            {
                if (die.IsAOIEnabled) testQueue.Add(new MainViewModel.TestItem { Die = die, TestType = "AOI" });
                if (die.IsIVLEnabled) testQueue.Add(new MainViewModel.TestItem { Die = die, TestType = "IVL" });
                if (die.IsEQEEnabled) testQueue.Add(new MainViewModel.TestItem { Die = die, TestType = "EQE" });
                if (die.IsVAMEnabled) testQueue.Add(new MainViewModel.TestItem { Die = die, TestType = "VAM" });
            }
            return testQueue;
        }

        private void ToFlowSel(CVWaferProberFlowType type)
        {
            foreach (var item in WPFlows) { if (item.FlowType == type) { SelectedWPFlow = item; break; } }
        }

        public void ToIntegratingSphere() => ToFlowSel(CVWaferProberFlowType.EQE);
        public void ToAuxCamera() => ToFlowSel(CVWaferProberFlowType.VAM);
        public void ToMainCamera() => ToFlowSel(CVWaferProberFlowType.AOI);

        public void OnMoveTo(ChipViewModel? dieVM)
        {
            DieViewModel? toSelect = TestResults.First(t => t.MapX == dieVM?.Column && t.MapY == dieVM?.Row);
            if (toSelect != null) OnMoveTo(toSelect);
        }

        private string FindResource(string resourceKey) => Application.Current.FindResource(resourceKey).ToString();

        public void OnMoveTo(DieViewModel selectedItem)
        {
            if (IsProcessing) { logger.Warn("Testing in progress, operation forbidden"); return; }
            var result = MessageDialog.Show($"{FindResource("Btn.MoveToMsg")} {selectedItem.MapAxisToString()}", FindResource("Prompt"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) ProberClientService.Instance?.MoveToAsync(selectedItem);
        }

        public MainService mainService { get; private set; }
        #endregion

        #region 新增：订阅 TestResults 中 Die 的变更以触发自动保存（防抖）
        private void SubscribeToSaveEvents()
        {
            // 订阅已有项
            foreach (var die in TestResults)
            {
                die.PropertyChanged += Die_PropertyChangedForSave;
            }

            // 订阅集合变更，新增/移除时绑定或解绑
            TestResults.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (DieViewModel die in e.NewItems) die.PropertyChanged += Die_PropertyChangedForSave;
                }
                if (e.OldItems != null)
                {
                    foreach (DieViewModel die in e.OldItems) die.PropertyChanged -= Die_PropertyChangedForSave;
                }
            };
        }
        private bool _isBatchUpdating = false;
        private void Die_PropertyChangedForSave(object? sender, PropertyChangedEventArgs e)
        {
            if (sender == null) return;
            if (e.PropertyName == nameof(DieViewModel.IsAOIEnabled) || e.PropertyName == nameof(DieViewModel.IsIVLEnabled) || e.PropertyName == nameof(DieViewModel.IsEQEEnabled) || e.PropertyName == nameof(DieViewModel.IsVAMEnabled))
            {
                DebounceTimer.AddOrResetTimer("UpdateChipMappingSelectedCount", 50, () => 
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!_isBatchUpdating)
                        {
                            UpdateChipMappingSelectedCount();
                        }
                    });
                }); 
              
            }
            else if (e.PropertyName == nameof(DieViewModel.Status) ||
                     e.PropertyName == nameof(DieViewModel.EndTestTime) ||
                     e.PropertyName == nameof(DieViewModel.StartTestTime) ||
                     e.PropertyName == nameof(DieViewModel.SerialNumber) ||
                     e.PropertyName == nameof(DieViewModel.DataValue))
            {

                DebounceTimer.AddOrResetTimer("UpdateChipMappingSelectedCount", 50, () =>
                {
                    DebouncedSaveLastSession();
                });

            }
        }


        // 防抖：在最后一次变更后等待一段时间再保存，避免频繁IO
        private async Task DebouncedSaveLastSession(int debounceMs = 100)
        {
            DebounceTimer.AddOrResetTimer("DebouncedSaveLastSession",debounceMs,() => SaveLastSessionIfNeededAsync());
        }
        #endregion
       
    }
}
