using ChipMapping.Models;
using ChipMapping.ViewModels;
using ColorVision.Core.Entities;
using CVDB.Services.Buz;
using CVWaferProber.Components;
using CVWaferProber.Core;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Models;
using CVWaferProber.Services;
using CVWaferProber.Views;
using FreeSql;
using log4net;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using static CVWaferProber.ViewModels.MainViewModel;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace CVWaferProber.ViewModels
{
    public class MappingDataViewModel : ViewModelBase
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(MappingDataViewModel));

        #region 原有核心属性+命令（保留，无修改）
        public event EventHandler<WPFlowViewModel> ActivateCorrespondingPanel;
        public ChipMappingControlViewModel? CustomMappingVM { get; set; }
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

        public ObservableCollection<DieViewModel> TestResults { get; } = new ObservableCollection<DieViewModel>();
        public RangeEnabledObservableCollection<FlowViewModel> FlowItems { get; } = new RangeEnabledObservableCollection<FlowViewModel>();
        public ObservableCollection<WPFlowViewModel> WPFlows { get; } = new ObservableCollection<WPFlowViewModel>();

        private FlowViewModel? _selectedFlow;
        public FlowViewModel? SelectedFlow { get => _selectedFlow; set { if (_selectedFlow != value) SetProperty(ref _selectedFlow, value); } }

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
        public string MappingCsvFilePath { get => _MappingCsvFilePath; set => SetProperty(ref _MappingCsvFilePath, value); }

        private bool? _selectAllAOI = false;
        public bool? SelectAllAOI { get => _selectAllAOI; set { if (!Equals(_selectAllAOI, value)) { _selectAllAOI = value; OnPropertyChanged(); UpdateSelectAllAOIState(); } } }
        private bool _isUpdatingFromHeader_AOI;

        private bool? _selectAllIVL = false;
        public bool? SelectAllIVL { get => _selectAllIVL; set { if (!Equals(_selectAllIVL, value)) { _selectAllIVL = value; OnPropertyChanged(); UpdateSelectAllIVLState(); } } }
        private bool _isUpdatingFromHeader_IVL;

        private bool? _selectAllEQE = false;
        public bool? SelectAllEQE { get => _selectAllEQE; set { if (!Equals(_selectAllEQE, value)) { _selectAllEQE = value; OnPropertyChanged(); UpdateSelectAllEQEState(); } } }
        private bool _isUpdatingFromHeader_EQE;

        private bool? _selectAllVAM = false;
        public bool? SelectAllVAM { get => _selectAllVAM; set { if (!Equals(_selectAllVAM, value)) { _selectAllVAM = value; OnPropertyChanged(); UpdateSelectAllVAMState(); } } }
        private bool _isUpdatingFromHeader_VAM;

        public bool IsColorEnabled { get; set; }
        public string ProberId { get; set; }
        private string _Timestamp;
        public string Timestamp { get => _Timestamp; set => SetProperty(ref _Timestamp, value); }
        private bool _isAutoSN;
        public bool IsAutoSN { get => _isAutoSN; set => SetProperty(ref _isAutoSN, value); }
        private bool _isProcessing = false;
        public bool IsNotProcessing => !_isProcessing;
        public bool IsProcessing { get => _isProcessing; set => SetProperty(ref _isProcessing, value); }
        private bool _isIVLCameraEnabled;
        public bool IsIVLCameraEnabled { get => _isIVLCameraEnabled; set => SetProperty(ref _isIVLCameraEnabled, value); }
        private bool selfClick = true;
        private DataGrid? _dataGrid;

        private string _yieldInfo = "0/0 (0.00%)";
        public string YieldInfo { get => _yieldInfo; set { if (_yieldInfo != value) { _yieldInfo = value; OnPropertyChanged(); CustomMappingVM.YieldInfo = value; } } }

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
        private List<TestItem> _testQueue;
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
                    OnPropertyChanged(nameof(ProgressText));
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
                    OnPropertyChanged(nameof(ProgressText));
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

        // 进度文本（绑定到界面，显示详细进度信息）
        public string ProgressText
        {
            get
            {
                if (TotalTestCount == 0) return "未开始";
                return $"{SingleDieTestProgress:F1}%";
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
        public MappingDataViewModel()
        {
            _selectedItem = null;
            _selectedFlow = null;
            _dataGrid = null;
            _isIVLCameraEnabled = false;
            _isAutoSN = true;

            // 初始化进度属性
            SingleDieTestProgress = 0;
            TotalTestProgress = 0;
            TotalTestCount = 0;
            CompletedTestCount = 0;
            CurrentDieInfo = string.Empty;

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

            ProberId = "CVProber01";
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

                logger.InfoFormat("自动测试进度初始化：共{0}个Die待测试", TotalTestCount);
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
                CurrentDieInfo = $"{die.MapX}/{die.MapY}"; // 显示当前Die行列
                SingleDieTestProgress = 0; // 重置单Die进度

                logger.DebugFormat("开始测试Die[{0}/{1}]，单Die进度重置为0%", die.MapX, die.MapY);
            });
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
                SingleDieTestProgress = progress;
                if (!string.IsNullOrEmpty(stageInfo) && logger.IsDebugEnabled)
                    logger.DebugFormat("Die[{0}]进度更新：{1} - {2:F1}%", CurrentDieInfo, stageInfo, progress);
            });
        }

        /// <summary>
        /// 完成单个Die测试 - 测试完成后调用
        /// </summary>
        public void CompleteSingleDieTest()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CompletedTestCount++;
                UpdateTotalProgress();
                OnPropertyChanged(nameof(ProgressText));

                logger.DebugFormat("Die测试完成，累计完成{0}/{1}个", CompletedTestCount, TotalTestCount);
            });
        }

        /// <summary>
        /// 计算总进度 - 核心公式：总进度=已完成/总数量 + 当前Die进度/总数量
        /// </summary>
        private void UpdateTotalProgress()
        {
            if (TotalTestCount <= 0)
            {
                TotalTestProgress = 0;
                return;
            }

            // 已完成Die的基础进度
            double completedProgress = (double)CompletedTestCount / TotalTestCount * 100;
            // 当前Die的进度贡献（未完成时才计算）
            double currentDieContribution = CompletedTestCount < TotalTestCount
                ? SingleDieTestProgress / 100 * (1.0 / TotalTestCount) * 100
                : 0;

            TotalTestProgress = Math.Min(100, completedProgress + currentDieContribution);
        }

        /// <summary>
        /// 重置所有进度条 - 测试停止/重置/完成时调用
        /// </summary>
        public void ResetProgressBars()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SingleDieTestProgress = 0;
                TotalTestProgress = 0;
                TotalTestCount = 0;
                CompletedTestCount = 0;
                CurrentDieInfo = string.Empty;

                // 同步重置手动测试标记
                IsManualTesting = false;
            });
        }
        #endregion

        #region 原有业务方法（修改StartManFlow/StartAutoFlow，集成新进度逻辑）
        private void StartManTest(object? obj)
        {
            StartManFlow();
        }
        private void StartManFlow()
        {
            if (SelectedWPFlow != null && SelectedItem is DieViewModel die)
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
                MainService.Instance.DoDieFlowExec(_selectedWPFlow, die, false, false);
                ActivateCorrespondingPanel?.Invoke(this, _selectedWPFlow);
            }
        }

        public void StartAutoFlow()
        {
            if (SelectedWPFlow == null)
            {
                logger.Error("未选择测试流程（Flow）");
                return;
            }

            _testQueue = GetSelectedTestItems();
            if (_testQueue.Count == 0)
            {
                MessageBox.Show((string)Application.Current.FindResource("Nodata"), (string)Application.Current.FindResource("Prompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedDice = GetSelectedDieTestItems();
            // 1. 自动测试进度初始化
            InitializeAutoTestProgress(selectedDice);
            TestingReady(_testQueue);
            EnableBtnGUI(false);
            // 2. 启动自动测试
            MainService.Instance.StartAutoTesting(_selectedWPFlow, selectedDice);
        }

        private void ManTestingReady(DieViewModel die)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fff");
            die.TestingReady(ProberId, timestamp);
            if (_isAutoSN) Timestamp = timestamp;
        }

        private void TestingReady(List<TestItem> testItems)
        {
            CustomMappingVM.DisabledInput = IsProcessing = true;
            string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fff");
            if (_isAutoSN) Timestamp = timestamp;
            foreach (var itemT in testItems)
            {
                itemT.Die.TestingReady(ProberId, timestamp);
            }
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
        }
        #endregion

        #region 原有其他方法（保留，无修改：集合事件/列配置/良率计算/CSV导出/持久化等）
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
                        await LoadFromPersistenceAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.Error("加载上次会话失败", ex);
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
                    catch (Exception ex) { logger.Warn("退出时保存会话失败", ex); }
                    try { SaveTestResultsToDefaultFile(); }
                    catch (Exception ex) { logger.Warn("退出时自动导出CSV失败", ex); }
                });
                if (!saveTask.Wait(TimeSpan.FromSeconds(5))) logger.Warn("退出时保存超时");
            }
            catch (Exception ex) { logger.Error("退出时处理异常", ex); }
        }

        private void OnSelectedChanged(object? value)
        {
            if (IsNotProcessing && value is DieViewModel die && selfClick)
            {
                CustomMappingVM?.SetSelectedChip((uint)die.Id);
                DieResultDisplay(die);
            }
            else selfClick = true;
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
                if (WPFlows.Count > 0) SelectedWPFlow = WPFlows[0];
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
                try { ResultService.LoadFromCSV(openFileDialog.FileName, TestResults); _dataGrid?.Items.Refresh(); MainService.Instance.Maintenance(); logger.InfoFormat("加载结果成功：{0}", openFileDialog.FileName); }
                catch (Exception ex) { logger.Error(ex); }
            }
        }

        private void RefreshStatus(object? obj) => _dataGrid?.Items.Refresh();

        private void DieResultDisplay(DieViewModel dieViewModel)
        {
            MainService.Instance.ResultDisplay(dieViewModel);
            CalculateYieldBySerialNumber();
        }

        public void InitializeServive(MainService main)
        {
            mainService = main;
            mainService.ChipSelected += OnChipDieSelected;
            mainService.TestingCompleted += OnTestingCompleted;
            mainService.PreAutoTestingNextDie += OnAutoTestingNextDie;
        }

        private void OnTestingCompleted(object? sender, TestCompletedEventArgs e)
        {
            DoEndTesting(e.IsAuto);
        }

        private void OnAutoTestingNextDie(object? sender, (DieViewModel? preDie, DieViewModel nextDie) e)
        {
            Application.Current.Dispatcher.Invoke(() => { MoveNextSel(e); });
        }

        private void MoveNextSel((DieViewModel? preDie, DieViewModel nextDie) e)
        {
            if (e.preDie != null) e.preDie.UnSelected();
            SelectedItem = e.nextDie;
            StartSingleDieTest(e.nextDie); // 下一个Die开始测试，重置单Die进度
        }

        private void DoEndTesting(bool isAuto)
        {
            CompletedTestCount = Math.Min(CompletedTestCount + 1, TotalTestCount);
            EnableBtnGUI(true);
            CalculateYieldBySerialNumber();
            AutoExportSummaryResult();
            SingleDieTestProgress = 100; // 拉满当前Die进度
            UpdateTotalProgress();
            OnPropertyChanged(nameof(ProgressText));

            // 关键：测试完成后重置IsManualTesting=false
            if (!isAuto) IsManualTesting = false;
            if (isAuto && CompletedTestCount >= TotalTestCount) ResetProgressBars(); // 自动测试全部完成，重置进度条
        }

        public void EnableBtnGUI(bool enabled)
        {
            CustomMappingVM.DisabledInput = IsProcessing = !enabled;
            OnPropertyChanged(nameof(IsNotProcessing));
        }

        private void ClearMapping()
        {
            // 清理时释放所有Die的定时器资源
            foreach (var die in TestResults) die.Dispose();
            CustomMappingVM?.Cleanup();
            CustomMappingVM.Chips.Clear();
            TestResults.Clear();
            ResetProgressBars();
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

        public List<DieViewModel> GetSortedResultsLinq(List<DieViewModel> testQueue)
        {
            return testQueue.GroupBy(d => d.MapY).OrderBy(g => g.Key)
                .SelectMany((g, index) => index % 2 == 0 ? g.OrderBy(d => d.MapX) : g.OrderByDescending(d => d.MapX))
                .ToList();
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
            if (e.NewItems != null) SubscribeItems_AOI(e.NewItems.Cast<DieViewModel>());
            if (e.OldItems != null) UnsubscribeItems_AOI(e.OldItems.Cast<DieViewModel>());
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
            if (e.PropertyName == nameof(DieViewModel.IsAOIEnabled) && !_isUpdatingFromHeader_AOI) UpdateSelectAllAOIState();
        }

        private void IVLItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) SubscribeItems_IVL(e.NewItems.Cast<DieViewModel>());
            if (e.OldItems != null) UnsubscribeItems_IVL(e.OldItems.Cast<DieViewModel>());
            UpdateSelectAllIVLState();
        }

        private void SubscribeItems_IVL(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items) item.PropertyChanged += IVL_Item_PropertyChanged;
        }

        private void UnsubscribeItems_IVL(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items) item.PropertyChanged -= IVL_Item_PropertyChanged;
        }

        private void IVL_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsIVLEnabled) && !_isUpdatingFromHeader_IVL) UpdateSelectAllIVLState();
        }

        private void EQEItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) SubscribeItems_EQE(e.NewItems.Cast<DieViewModel>());
            if (e.OldItems != null) UnsubscribeItems_EQE(e.OldItems.Cast<DieViewModel>());
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
            if (e.PropertyName == nameof(DieViewModel.IsEQEEnabled) && !_isUpdatingFromHeader_EQE) UpdateSelectAllEQEState();
        }

        private void VAMItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) SubscribeItems_VAM(e.NewItems.Cast<DieViewModel>());
            if (e.OldItems != null) UnsubscribeItems_VAM(e.OldItems.Cast<DieViewModel>());
            UpdateSelectAllVAMState();
        }

        private void SubscribeItems_VAM(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items) item.PropertyChanged += VAM_Item_PropertyChanged;
        }

        private void UnsubscribeItems_VAM(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items) item.PropertyChanged -= VAM_Item_PropertyChanged;
        }

        private void VAM_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsVAMEnabled) && !_isUpdatingFromHeader_VAM) UpdateSelectAllVAMState();
        }

        private void UpdateSelectAllAOIState()
        {
            if (TestResults.Count == 0) { SelectAllAOI = false; return; }
            int selectedCount = TestResults.Count(item => item.IsAOIEnabled);
            SelectAllAOI = selectedCount switch { 0 => false, var c when c == TestResults.Count => true, _ => null };
        }

        private void UpdateSelectAllIVLState()
        {
            if (TestResults.Count == 0) { SelectAllIVL = false; return; }
            int selectedCount = TestResults.Count(item => item.IsIVLEnabled);
            SelectAllIVL = selectedCount switch { 0 => false, var c when c == TestResults.Count => true, _ => null };
        }

        private void UpdateSelectAllEQEState()
        {
            if (TestResults.Count == 0) { SelectAllEQE = false; return; }
            int selectedCount = TestResults.Count(item => item.IsEQEEnabled);
            SelectAllEQE = selectedCount switch { 0 => false, var c when c == TestResults.Count => true, _ => null };
        }

        private void UpdateSelectAllVAMState()
        {
            if (TestResults.Count == 0) { SelectAllVAM = false; return; }
            int selectedCount = TestResults.Count(item => item.IsVAMEnabled);
            SelectAllVAM = selectedCount switch { 0 => false, var c when c == TestResults.Count => true, _ => null };
        }

        private void ExecuteInvertSelectAOI(object obj)
        {
            foreach (var item in TestResults) item.IsAOIEnabled = !item.IsAOIEnabled;
            _dataGrid?.Items.Refresh();
            UpdateSelectAllAOIState();
        }

        private void ExecuteInvertSelectIVL(object obj)
        {
            foreach (var item in TestResults) item.IsIVLEnabled = !item.IsIVLEnabled;
            _dataGrid?.Items.Refresh();
            UpdateSelectAllIVLState();
        }

        private void ExecuteInvertSelectEQE(object obj)
        {
            foreach (var item in TestResults) item.IsEQEEnabled = !item.IsEQEEnabled;
            _dataGrid?.Items.Refresh();
            UpdateSelectAllEQEState();
        }

        private void ExecuteInvertSelectVAM(object obj)
        {
            foreach (var item in TestResults) item.IsVAMEnabled = !item.IsVAMEnabled;
            _dataGrid?.Items.Refresh();
            UpdateSelectAllVAMState();
        }
        #endregion

        public void SetDataGrid(DataGrid dataGrid)
        {
            _dataGrid = dataGrid;
            UpdateDataGridColumns();
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
            if (!File.Exists(MappingCsvFilePath)) { logger.WarnFormat("文件不存在：{0}", MappingCsvFilePath); return; }
            bool bR = CsvMappingDataTool.LoadMappingCsv(MappingCsvFilePath, ref mappingData);
            if (bR && mappingData != null && mappingData.Count > 0)
            {
                CustomMappingVM.RefreshFromMap(mappingData);
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
                TestResults.Clear();
                foreach (var item in sorted) if (item.Status != ChipStatus.SKIP) TestResults.Add(item);
            }
            BuildSNIndex();
            if (string.IsNullOrWhiteSpace(SearchSN)) FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);
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
            catch (Exception ex) { logger.Error("良率计算异常", ex); YieldInfo = (string)Application.Current.FindResource("CalculationException"); }
        }

        private void AutoExportSummaryResult()
        {
            try
            {
                if (TestResults == null || !TestResults.Any()) { logger.Info("无测试结果，跳过Summary导出"); return; }
                string exportRootPath = @"D:\Project";
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
                logger.InfoFormat("Summary结果已自动导出：{0}", savePath);
            }
            catch (Exception ex) { logger.Error("Summary结果导出失败", ex); }
        }

        public void LoadFlow(List<RespDataFlowTempDTO>? flows)
        {
            FlowItems.Clear();
            SelectedFlow = null;
            if (flows != null) { foreach (var flow in flows) FlowItems.Add(new FlowViewModel(flow)); if (FlowItems.Count > 0) SelectedFlow = FlowItems[FlowItems.Count - 1]; }
        }

        private void ResetStatus(object? obj)
        {
            foreach (var die in TestResults) die.ResetStatus();
            CalculateYieldBySerialNumber();
            ResetProgressBars();
        }

        private void SaveTestResult(object? obj)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = string.Format("{0}_{1}_result.csv", ProberId, _Timestamp),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                try { ResultService.SaveToCSV(saveFileDialog.FileName, TestResults); logger.InfoFormat("保存结果成功：{0}", saveFileDialog.FileName); }
                catch (Exception ex) { logger.Error(ex); }
            }
        }

        private void LoadTestResult(object? obj)
        {
            if (obj is string path && !string.IsNullOrWhiteSpace(path))
            {
                try { ResultService.LoadFromCSV(path, TestResults); _dataGrid?.Items.Refresh(); logger.InfoFormat("加载结果成功：{0}", path); }
                catch (Exception ex) { logger.Error(ex); }
                return;
            }
            if (obj == null)
            {
                Task.Run(async () =>
                {
                    try { await LoadFromPersistenceAsync(); Application.Current.Dispatcher.Invoke(() => { _dataGrid?.Items.Refresh(); logger.Info("从持久化加载上次会话结果成功"); }); }
                    catch (Exception ex) { logger.Error("从持久化加载失败", ex); }
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
                try { ResultService.LoadFromCSV(openFileDialog.FileName, TestResults); _dataGrid?.Items.Refresh(); logger.InfoFormat("加载结果成功：{0}", openFileDialog.FileName); }
                catch (Exception ex) { logger.Error(ex); }
            }
        }

        public async Task LoadFromPersistenceAsync()
        {
            try
            {
                var dtos = await TestResultPersistenceService.LoadAsync();
                if (dtos == null || dtos.Count == 0) return;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ApplyDtosToTestResults(dtos);
                    _dataGrid?.Items.Refresh();
                    BuildSNIndex();
                    CalculateYieldBySerialNumber();
                    try
                    {
                        DieViewModel? toSelect = null;
                        var dtoWithSN = dtos.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.SerialNumber));
                        if (dtoWithSN != null) toSelect = TestResults.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.SerialNumber) && t.SerialNumber.Equals(dtoWithSN.SerialNumber, StringComparison.OrdinalIgnoreCase));
                        if (toSelect == null && TestResults.Count > 0) toSelect = TestResults[0];
                        if (toSelect != null) { selfClick = false; SelectedItem = toSelect; ManScrollToItem(toSelect); logger.InfoFormat("加载后自动选中：Id={0}, SN={1}", toSelect.Id, toSelect.SerialNumber); }
                    }
                    catch (Exception ex) { logger.Warn("加载后自动选中失败", ex); }
                });
            }
            catch (Exception ex) { logger.Error("从持久化加载失败", ex); }
        }

        private void ApplyDtosToTestResults(List<TestResultDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return;
            foreach (var dto in dtos)
            {
                try
                {
                    DieViewModel? die = null;
                    if (dto.Id != 0) die = TestResults.FirstOrDefault(d => d.Id == dto.Id);
                    if (die == null && !string.IsNullOrEmpty(dto.SerialNumber)) die = TestResults.FirstOrDefault(d => !string.IsNullOrEmpty(d.SerialNumber) && d.SerialNumber.Trim().Equals(dto.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (die == null && dto.MapX.HasValue && dto.MapY.HasValue) die = TestResults.FirstOrDefault(d => d.MapX == dto.MapX && d.MapY == dto.MapY);
                    if (die == null) continue;
                    die.IsAOIEnabled = dto.IsAOIEnabled;
                    die.IsIVLEnabled = dto.IsIVLEnabled;
                    die.IsEQEEnabled = dto.IsEQEEnabled;
                    die.IsVAMEnabled = dto.IsVAMEnabled;
                    die.SerialNumber = dto.SerialNumber;
                    if (!string.IsNullOrWhiteSpace(dto.DisplayStatus))
                    {
                        string raw = dto.DisplayStatus.Trim().Trim('"').Trim();
                        if (Enum.TryParse<ChipStatus>(raw, true, out var enumStatus)) die.ChangeStatusOnly(enumStatus);
                        else
                        {
                            try { die.ChangeStatusOnly(ChipStatusTool.GetStatusFromDisplay(raw, die.IsChinese)); }
                            catch { try { die.ChangeStatusOnly(ChipStatusTool.GetStatusFromDisplay(raw, !die.IsChinese)); } catch { logger.WarnFormat("解析DisplayStatus失败：{0}", dto.DisplayStatus); } }
                        }
                    }
                    if (die.chipViewModel?.ChipData != null && !string.IsNullOrEmpty(dto.DataValue) && double.TryParse(dto.DataValue, out double dataValue))
                    {
                        die.chipViewModel.ChipData.DataValue = dataValue;
                        die.RefreshDataValue();
                    }
                    die.StartTestTime = dto.StartTestTime;
                    die.EndTestTime = dto.EndTestTime;
                    die.TotalTime = dto.TotalTime;
                    die.AOIGradeLevel = dto.AOIGradeLevel;
                    die.BlackPattern = dto.BlackPattern;
                }
                catch (Exception ex) { logger.Warn("应用DTO到TestResults失败", ex); }
            }
        }

        private void SaveTestResultsToDefaultFile()
        {
            try
            {
                if (TestResults == null || !TestResults.Any()) { logger.Info("无测试结果，跳过自动保存"); return; }
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CVWaferProber", "AutoSaves");
                Directory.CreateDirectory(folder);
                string fileName = $"TestResults_Auto_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullPath = Path.Combine(folder, fileName);
                ResultService.SaveToCSV(fullPath, TestResults);
                logger.InfoFormat("自动保存测试结果到：{0}", fullPath);
            }
            catch (Exception ex) { logger.Error("自动保存测试结果失败", ex); }
        }

        public async Task SaveLastSessionAsync()
        {
            try
            {
                var dtos = TestResults.Select(r => TestResultDto.FromObject(r)).Where(x => x != null).ToList();
                await TestResultPersistenceService.SaveAsync(dtos);
                logger.Info("保存上次会话结果到持久化成功");
            }
            catch (Exception ex) { logger.Error("保存上次会话失败", ex); }
        }

        public void SelectItemById(uint id)
        {
            var itemToSelect = TestResults.FirstOrDefault(item => item.Id == id);
            if (itemToSelect != null) { selfClick = false; SelectedItem = itemToSelect; }
        }

        private List<TestItem> GetSelectedTestItems()
        {
            var testQueue = new List<TestItem>();
            foreach (var die in TestResults)
            {
                if (die.IsAOIEnabled) testQueue.Add(new TestItem { Die = die, TestType = "AOI" });
                if (die.IsIVLEnabled) testQueue.Add(new TestItem { Die = die, TestType = "IVL" });
                if (die.IsEQEEnabled) testQueue.Add(new TestItem { Die = die, TestType = "EQE" });
                if (die.IsVAMEnabled) testQueue.Add(new TestItem { Die = die, TestType = "VAM" });
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
            DieViewModel? toSelect = TestResults.First(t => t.MapX == dieVM.Column && t.MapY == dieVM.Row);
            if (toSelect != null) OnMoveTo(toSelect);
        }

        private string FindResource(string resourceKey) => Application.Current.FindResource(resourceKey).ToString();

        public void OnMoveTo(DieViewModel selectedItem)
        {
            if (IsProcessing) { logger.Warn("测试中，禁止操作"); return; }
            var result = MessageDialog.Show($"{FindResource("Btn.MoveToMsg")} {selectedItem.MapAxisToString()}", FindResource("Prompt"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) ProberClientService.Instance?.MoveToAsync(selectedItem);
        }

        public MainService mainService { get; private set; }
        #endregion
    }
    //public class MappingDataViewModel : ViewModelBase
    //{
    //    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MappingDataViewModel));

    //    public event EventHandler<WPFlowViewModel> ActivateCorrespondingPanel;
    //    public ChipMappingControlViewModel? CustomMappingVM { get; set; }
    //    public ICommand LoadMappingFileCommand { get; }
    //    public ICommand ClearMappingCommand { get; }
    //    public ICommand FlowLoadCommand { get; }
    //    public ICommand OpenMappingFileCommand { get; }
    //    public ICommand RefreshStatusCommand { get; }
    //    public ICommand SaveTestResultCommand { get; }
    //    public ICommand LoadTestResultCommand { get; }
    //    public ICommand ResetStatusCommand { get; }
    //    public ICommand StartManTestCommand { get; }


    //    #region AOI列的全选/反选命令
    //    // ========== 1. AOI列的全选/反选命令 ==========
    //    public ICommand InvertSelectAOICommand { get; }
    //    public ICommand InvertSelectIVLCommand { get; }
    //    public ICommand InvertSelectEQECommand { get; }
    //    public ICommand InvertSelectVAMCommand { get; }
    //    #endregion AOI列的全选/反选命令

    //    public ObservableCollection<DieViewModel> TestResults { get; } = new ObservableCollection<DieViewModel>();
    //    public RangeEnabledObservableCollection<FlowViewModel> FlowItems { get; } = new RangeEnabledObservableCollection<FlowViewModel>();
    //    public ObservableCollection<WPFlowViewModel> WPFlows { get; } = new ObservableCollection<WPFlowViewModel>();

    //    private FlowViewModel? _selectedFlow;
    //    public FlowViewModel? SelectedFlow
    //    {
    //        get => _selectedFlow;
    //        set
    //        {
    //            if (_selectedFlow != value)
    //            {
    //                SetProperty(ref _selectedFlow, value);
    //            }
    //        }
    //    }

    //    private WPFlowViewModel? _selectedWPFlow;
    //    public WPFlowViewModel? SelectedWPFlow
    //    {
    //        get => _selectedWPFlow;
    //        set
    //        {
    //            if (_selectedWPFlow != value)
    //            {
    //                SetProperty(ref _selectedWPFlow, value);
    //                ActivateCorrespondingPanel?.Invoke(this, _selectedWPFlow);
    //            }
    //        }
    //    }
    //    private object? _selectedItem;
    //    public object? SelectedItem
    //    {
    //        get => _selectedItem;
    //        set
    //        {
    //            SetProperty(ref _selectedItem, value);
    //            ManScrollToItem(SelectedItem);
    //            OnSelectedChanged(value);
    //        }
    //    }

    //    public string _MappingCsvFilePath;
    //    public string MappingCsvFilePath
    //    {
    //        get => _MappingCsvFilePath;
    //        set
    //        {
    //            SetProperty(ref _MappingCsvFilePath, value);
    //        }
    //    }
    //    #region 三态全选属性
    //    // AOI三态全选
    //    private bool? _selectAllAOI = false;
    //    public bool? SelectAllAOI
    //    {
    //        get => _selectAllAOI;
    //        set
    //        {
    //            if (!Equals(_selectAllAOI, value))
    //            {
    //                _selectAllAOI = value;
    //                OnPropertyChanged(nameof(SelectAllAOI));

    //                if (value.HasValue && TestResults != null)
    //                {
    //                    _isUpdatingFromHeader_AOI = true;
    //                    try
    //                    {
    //                        foreach (var item in TestResults)
    //                        {
    //                            item.IsAOIEnabled = value.Value;
    //                        }
    //                        // 强制刷新DataGrid
    //                        //_dataGrid?.Items.Refresh();
    //                    }
    //                    finally
    //                    {
    //                        _isUpdatingFromHeader_AOI = false;
    //                    }
    //                }
    //            }
    //        }
    //    }
    //    private bool _isUpdatingFromHeader_AOI;

    //    // IVL三态全选
    //    private bool? _selectAllIVL = false;
    //    public bool? SelectAllIVL
    //    {
    //        get => _selectAllIVL;
    //        set
    //        {
    //            if (!Equals(_selectAllIVL, value))
    //            {
    //                _selectAllIVL = value;
    //                OnPropertyChanged(nameof(SelectAllIVL));

    //                if (value.HasValue && TestResults != null)
    //                {
    //                    _isUpdatingFromHeader_IVL = true;
    //                    try
    //                    {
    //                        foreach (var item in TestResults)
    //                        {
    //                            item.IsIVLEnabled = value.Value;
    //                        }
    //                        //_dataGrid?.Items.Refresh();
    //                    }
    //                    finally
    //                    {
    //                        _isUpdatingFromHeader_IVL = false;
    //                    }
    //                }
    //            }
    //        }
    //    }
    //    private bool _isUpdatingFromHeader_IVL;

    //    // EQE三态全选
    //    private bool? _selectAllEQE = false;
    //    public bool? SelectAllEQE
    //    {
    //        get => _selectAllEQE;
    //        set
    //        {
    //            if (!Equals(_selectAllEQE, value))
    //            {
    //                _selectAllEQE = value;
    //                OnPropertyChanged(nameof(SelectAllEQE));

    //                if (value.HasValue && TestResults != null)
    //                {
    //                    _isUpdatingFromHeader_EQE = true;
    //                    try
    //                    {
    //                        foreach (var item in TestResults)
    //                        {
    //                            item.IsEQEEnabled = value.Value;
    //                        }
    //                        //_dataGrid?.Items.Refresh();
    //                    }
    //                    finally
    //                    {
    //                        _isUpdatingFromHeader_EQE = false;
    //                    }
    //                }
    //            }
    //        }
    //    }
    //    private bool _isUpdatingFromHeader_EQE;

    //    // VAM三态全选
    //    private bool? _selectAllVAM = false;
    //    public bool? SelectAllVAM
    //    {
    //        get => _selectAllVAM;
    //        set
    //        {
    //            if (!Equals(_selectAllVAM, value))
    //            {
    //                _selectAllVAM = value;
    //                OnPropertyChanged(nameof(SelectAllVAM));

    //                if (value.HasValue && TestResults != null)
    //                {
    //                    _isUpdatingFromHeader_VAM = true;
    //                    try
    //                    {
    //                        foreach (var item in TestResults)
    //                        {
    //                            item.IsVAMEnabled = value.Value;
    //                        }
    //                        //_dataGrid?.Items.Refresh();
    //                    }
    //                    finally
    //                    {
    //                        _isUpdatingFromHeader_VAM = false;
    //                    }
    //                }
    //            }
    //        }
    //    }
    //    private bool _isUpdatingFromHeader_VAM;
    //    #endregion

    //    public bool IsColorEnabled { get; set; }

    //    public string ProberId { get; set; }

    //    private string _Timestamp;
    //    public string Timestamp
    //    {
    //        get => _Timestamp;
    //        set
    //        {
    //            SetProperty(ref _Timestamp, value);
    //        }
    //    }

    //    private bool _isAutoSN;
    //    public bool IsAutoSN
    //    {
    //        get => _isAutoSN;
    //        set
    //        {
    //            SetProperty(ref _isAutoSN, value);
    //        }
    //    }

    //    private bool _isProcessing = false;
    //    public bool IsNotProcessing => !_isProcessing;
    //    public bool IsProcessing
    //    {
    //        get => _isProcessing;
    //        set
    //        {
    //            SetProperty(ref _isProcessing, value);
    //        }
    //    }

    //    private bool _isIVLCameraEnabled;
    //    public bool IsIVLCameraEnabled
    //    {
    //        get => _isIVLCameraEnabled;
    //        set
    //        {
    //            SetProperty(ref _isIVLCameraEnabled, value);
    //        }
    //    }
    //    /// <summary>
    //    /// false 外部控件关联触发
    //    /// </summary>
    //    private bool selfClick = true;

    //    private DataGrid? _dataGrid; // 引用DataGrid（静态列+动态列）

    //    #region 良率计算
    //    private string _yieldInfo = "0/0 (0.00%)";
    //    public string YieldInfo
    //    {
    //        get => _yieldInfo;
    //        set
    //        {
    //            if (_yieldInfo != value)
    //            {
    //                _yieldInfo = value;
    //                OnPropertyChanged(nameof(YieldInfo));
    //                CustomMappingVM.YieldInfo = value;
    //            }
    //        }
    //    }
    //    #endregion

    //    #region 静态列+动态列配置
    //    /// <summary>
    //    /// 静态列数量（前13列）
    //    /// </summary>
    //    private const int StaticColumnCount = 13;

    //    /// <summary>
    //    /// 静态列配置（前13列，不可选）
    //    /// </summary>
    //    private readonly List<ColumnConfig> _staticColumnConfigs = new List<ColumnConfig>
    //    {
    //        new ColumnConfig { ColumnHeader =(string)Application.Current.FindResource("GridHeader.No"), ColumnBindingPath = "Id", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
    //        new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.Row"), ColumnBindingPath = "MapY", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
    //        new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.Col"), ColumnBindingPath = "MapX", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
    //        new ColumnConfig { ColumnHeader = "AOI Enabled", ColumnBindingPath = "IsAOIEnabled", ColumnType = ColumnType.CheckBox, IsOptional = false, ColumnKey = ColumnKey.AOI },
    //        new ColumnConfig { ColumnHeader = "IVL Enabled", ColumnBindingPath = "IsIVLEnabled", ColumnType = ColumnType.CheckBox, IsOptional = false, ColumnKey = ColumnKey.IVL },
    //        new ColumnConfig { ColumnHeader = "EQE Enabled", ColumnBindingPath = "IsEQEEnabled", ColumnType = ColumnType.CheckBox, IsOptional = false, ColumnKey = ColumnKey.EQE },
    //        new ColumnConfig { ColumnHeader = "VAM Enabled", ColumnBindingPath = "IsVAMEnabled", ColumnType = ColumnType.CheckBox, IsOptional = false, ColumnKey = ColumnKey.VAM },
    //        new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.SerialNumber"), ColumnBindingPath = "SerialNumber", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
    //        new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.TestStatus"), ColumnBindingPath = "DisplayStatus", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
    //        new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.Uniformity"), ColumnBindingPath = "DataValue", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
    //        new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.StartTestTime"), ColumnBindingPath = "StartTestTime", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
    //        new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.EndTestTime"), ColumnBindingPath = "EndTestTime", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
    //        new ColumnConfig { ColumnHeader = (string)Application.Current.FindResource("Maping.GridHeader.TotalTime"), ColumnBindingPath = "TotalTime", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },

    //    };

    //    /// <summary>
    //    /// 动态可选列配置（第14列及以后）
    //    /// </summary>
    //    private List<ColumnConfig> _dynamicColumnConfigs = new List<ColumnConfig>();

    //    /// <summary>
    //    /// 当前选中的动态列配置
    //    /// </summary>
    //    public ObservableCollection<ColumnConfig> SelectedDynamicColumns { get; set; }

    //    #endregion

    //    //private string timestamp;
    //    //public string Timestamp
    //    //{
    //    //    get => timestamp;
    //    //    set
    //    //    {
    //    //        timestamp = value;
    //    //        OnPropertyChanged(); // 通知 UI 属性变更
    //    //    }
    //    //}
    //    private string serialNumber;
    //    public string SerialNumber
    //    {
    //        get => serialNumber;
    //        set
    //        {
    //            serialNumber = value;
    //            OnPropertyChanged(); // 通知 UI 属性变更
    //        }
    //    }
    //    #region SN查找功能
    //    // SN查找输入属性
    //    private string _searchSN;
    //    public string SearchSN
    //    {
    //        get => _searchSN;
    //        set => SetProperty(ref _searchSN, value);
    //    }
    //    private List<TestItem> _testQueue;

    //    // 筛选后的测试结果集合（绑定到DataGrid）
    //    private ObservableCollection<DieViewModel> _filteredTestResults;
    //    public ObservableCollection<DieViewModel> FilteredTestResults
    //    {
    //        get => _filteredTestResults;
    //        set => SetProperty(ref _filteredTestResults, value);
    //    }

    //    // 查找命令
    //    public ICommand SearchCommand { get; }
    //    #endregion
    //    public MappingDataViewModel()
    //    {
    //        _selectedItem = null;
    //        _selectedFlow = null;
    //        _dataGrid = null;
    //        _isIVLCameraEnabled = false;
    //        _isAutoSN = true;
    //        // 初始化进度条
    //        SingleDieTestProgress = 0;
    //        TotalTestProgress = 0;
    //        TotalTestCount = 0;
    //        CompletedTestCount = 0;
    //        CurrentDieIndex = 0;

    //        SearchCommand = new RelayCommand(OnSearch);

    //        ClearMappingCommand = new RelayCommand(_ => ClearMapping());
    //        FlowLoadCommand = new RelayCommand(_ => LoadBuzWPFlows());
    //        LoadMappingFileCommand = new RelayCommand(_ => LoadMappingFileAsync());
    //        RefreshStatusCommand = new RelayCommand(RefreshStatus);
    //        OpenMappingFileCommand = new RelayCommand(OpenMappingFile);

    //        StartManTestCommand = new RelayCommand(StartManTest);

    //        SearchCommand = new RelayCommand(ExecuteSearch);
    //        SaveTestResultCommand = new RelayCommand(SaveTestResult);
    //        LoadTestResultCommand = new RelayCommand(LoadTestResult);
    //        ResetStatusCommand = new RelayCommand(ResetStatus);

    //        // 反选命令初始化
    //        InvertSelectAOICommand = new RelayCommand(ExecuteInvertSelectAOI);
    //        InvertSelectIVLCommand = new RelayCommand(ExecuteInvertSelectIVL);
    //        InvertSelectEQECommand = new RelayCommand(ExecuteInvertSelectEQE);
    //        InvertSelectVAMCommand = new RelayCommand(ExecuteInvertSelectVAM);

    //        // 初始化数据源
    //        TestResults = new ObservableCollection<DieViewModel>();
    //        TestResults.CollectionChanged += AOIItems_CollectionChanged;
    //        TestResults.CollectionChanged += IVLItems_CollectionChanged;
    //        TestResults.CollectionChanged += EQEItems_CollectionChanged;
    //        TestResults.CollectionChanged += VAMItems_CollectionChanged;

    //        SubscribeItems_AOI(TestResults);
    //        SubscribeItems_IVL(TestResults);
    //        SubscribeItems_EQE(TestResults);
    //        SubscribeItems_VAM(TestResults);
    //        //
    //        ProberId = "CVProber01";

    //        ProberClientService.Instance.InitializeMapVM(this);
    //        // 初始化列配置
    //        InitColumnConfigs();

    //        InitAutoSave();
    //    }
    //    // 新增：应用退出事件处理（同步调用以确保在进程退出前执行保存）
    //    private void Application_Exit(object? sender, ExitEventArgs e)
    //    {
    //        try
    //        {
    //            // 在线程池运行保存任务并等待有限时间，避免 UI 线程死锁
    //            var saveTask = Task.Run(async () =>
    //            {
    //                try
    //                {
    //                    await SaveLastSessionAsync().ConfigureAwait(false);
    //                }
    //                catch (Exception ex)
    //                {
    //                    if (logger.IsWarnEnabled) logger.Warn("SaveLastSessionAsync failed on Exit", ex);
    //                }

    //                try
    //                {
    //                    // CSV 导出也放到线程池内执行
    //                    SaveTestResultsToDefaultFile();
    //                }
    //                catch (Exception ex)
    //                {
    //                    if (logger.IsWarnEnabled) logger.Warn("Auto CSV save failed on Exit", ex);
    //                }
    //            });

    //            // 等待保存任务完成，但不要无限期阻塞，5 秒超时可避免长时间卡死
    //            if (!saveTask.Wait(TimeSpan.FromSeconds(5)))
    //            {
    //                if (logger.IsWarnEnabled) logger.Warn("Saving last session timed out on Exit.");
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            // 确保不抛出异常阻塞退出流程
    //            if (logger.IsErrorEnabled) logger.Error("Application_Exit unexpected error", ex);
    //        }
    //    }

    //    private void InitAutoSave()
    //    {
    //        if (Application.Current != null)
    //        {
    //            Application.Current.Exit -= Application_Exit; // 先解绑保险
    //            Application.Current.Exit += Application_Exit;
    //        }

    //        // 自动加载上次会话数据（在 UI 初始化后异步执行）
    //        try
    //        {
    //            Application.Current?.Dispatcher?.BeginInvoke(new Action(async () =>
    //            {
    //                try
    //                {
    //                    await LoadFromPersistenceAsync();
    //                }
    //                catch (Exception ex)
    //                {
    //                    if (logger.IsErrorEnabled) logger.Error("Load previous session failed", ex);
    //                }
    //            }), DispatcherPriority.Background);
    //        }
    //        catch { }

    //        // 程序退出时自动保存当前会话
    //        try
    //        {
    //            if (Application.Current != null)
    //            {
    //                Application.Current.Exit += async (s, e) =>
    //                {
    //                    try
    //                    {
    //                        await SaveLastSessionAsync();
    //                    }
    //                    catch { }
    //                };
    //            }
    //        }
    //        catch { }
    //    }

    //    private void OnSelectedChanged(object? value)
    //    {
    //        if (IsNotProcessing)
    //        {
    //            // 选中项变化时的逻辑
    //            if (value != null && value is DieViewModel die)
    //            {
    //                if (selfClick)
    //                {
    //                    CustomMappingVM?.SetSelectedChip((uint)die.Id);
    //                    DieResultDisplay(die);
    //                }
    //                else selfClick = true;
    //            }
    //        }
    //    }
    //    private void OnChipDieSelected(object? sender, ChipViewModel chip)
    //    {
    //        if (chip == null || TestResults.Count == 0)
    //        {
    //            SelectedItem = null;
    //            return;
    //        }

    //        var targetDie = TestResults.FirstOrDefault(die => die.Id == chip.Id);
    //        if (targetDie != null)
    //        {
    //            selfClick = false;
    //            SelectedItem = targetDie;
    //            ManScrollToItem(targetDie);
    //        }
    //    }
    //    private void OnSearch(object? obj)
    //    {

    //    }
    //    public void LoadBuzWPFlows()
    //    {
    //        List<TScgdBuzProductDetail> flows = WaferProberDBService.LoadBuzFlows();
    //        WPFlows.Clear();
    //        if (flows != null && flows.Count > 0)
    //        {
    //            foreach (var flow in flows)
    //            {
    //                WPFlows.Add(new WPFlowViewModel(flow));
    //            }
    //            if (WPFlows.Count > 0) SelectedWPFlow = WPFlows[0];
    //        }
    //        else
    //        {

    //        }
    //    }
    //    private void OpenMappingFile(object? obj)
    //    {
    //        OpenFileDialog openFileDialog = new OpenFileDialog();
    //        openFileDialog.Filter = (string)System.Windows.Application.Current.FindResource("CSVFile");//CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"

    //        if (openFileDialog.ShowDialog() ==  true)
    //        {
    //            MappingCsvFilePath = openFileDialog.FileName;
    //            LoadMappingFileFromCsv();
    //            mainService.Maintenance();
    //        }
    //    }
    //    private void LoadMappingFileAsync()
    //    {
    //        var openFileDialog = new OpenFileDialog
    //        {
    //            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
    //            DefaultExt = ".csv",
    //            FileName = "result.csv",
    //            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
    //        };
    //        if (openFileDialog.ShowDialog() == true)
    //        {
    //            try
    //            {
    //                ResultService.LoadFromCSV(openFileDialog.FileName, TestResults);
    //                _dataGrid?.Items.Refresh();
    //                mainService.Maintenance();
    //                if (logger.IsInfoEnabled) logger.InfoFormat("Load result ok => {0}", openFileDialog.FileName);
    //            }
    //            catch (Exception ex)
    //            {
    //                if (logger.IsErrorEnabled) logger.Error(ex);
    //            }
    //        }
    //        //try
    //        //{
    //        //    // 支持传入文件路径：优先使用 parameter，其次使用现有 MappingCsvFilePath
    //        //    if (obj is string path && !string.IsNullOrWhiteSpace(path))
    //        //    {
    //        //        MappingCsvFilePath = path;
    //        //    }

    //        //    // 如果存在有效的 Mapping CSV，则按原逻辑加载映射
    //        //    if (!string.IsNullOrWhiteSpace(MappingCsvFilePath) && File.Exists(MappingCsvFilePath))
    //        //    {
    //        //        // 调用已有的加载逻辑
    //        //        LoadMappingFileFromCsv();

    //        //        // 若需要通知其它模块，可通过 EventAggregator 发布事件（可选）
    //        //        // EventAggregator?.Publish(new MappingReloadedEvent(MappingCsvFilePath));

    //        //        if (logger.IsInfoEnabled) logger.InfoFormat("Mapping file reloaded => {0}", MappingCsvFilePath);
    //        //        return;
    //        //    }

    //        //    // 如果没有可用的 Mapping CSV，则尝试从持久化（上次会话）加载测试结果（实现“重载/记忆”功能）
    //        //    if (string.IsNullOrWhiteSpace(MappingCsvFilePath) || !File.Exists(MappingCsvFilePath))
    //        //    {
    //        //        if (logger.IsWarnEnabled) logger.WarnFormat("Mapping file not found => {0}", MappingCsvFilePath);

    //        //        // 弹窗改为提供选择：用户可以选择加载持久化数据或选取文件
    //        //        var result = MessageBox.Show(
    //        //            (string)Application.Current.FindResource("Maping.FileNotExist") + "\n\n" +
    //        //            "是否从上次会话重载测试数据？（是 = 重载上次数据；否 = 选择文件）",
    //        //            (string)Application.Current.FindResource("Prompt"),
    //        //            MessageBoxButton.YesNoCancel,
    //        //            MessageBoxImage.Question);

    //        //        // Yes -> 从持久化加载
    //        //        if (result == MessageBoxResult.Yes)
    //        //        {
    //        //            try
    //        //            {
    //        //                await LoadFromPersistenceAsync();

    //        //                Application.Current?.Dispatcher?.Invoke(() =>
    //        //                {
    //        //                    _dataGrid?.Items.Refresh();
    //        //                    if (logger.IsInfoEnabled) logger.Info("Loaded last session test results from persistence (LoadMappingFile).");
    //        //                });
    //        //            }
    //        //            catch (Exception ex)
    //        //            {
    //        //                if (logger.IsErrorEnabled) logger.Error("Failed to load persisted test results", ex);
    //        //                MessageBox.Show((string)Application.Current.FindResource("Maping.ReloadFailed") + $": {ex.Message}", (string)Application.Current.FindResource("State.Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    //        //            }

    //        //            return;
    //        //        }
    //        //        // No -> 打开文件对话框让用户选择 Mapping CSV（保留原行为）
    //        //        if (result == MessageBoxResult.No)
    //        //        {
    //        //            var openFileDialog = new Microsoft.Win32.OpenFileDialog
    //        //            {
    //        //                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
    //        //                DefaultExt = ".csv",
    //        //                Title = (string)Application.Current.FindResource("Maping.OpenMappingFile")
    //        //            };
    //        //            if (openFileDialog.ShowDialog() == true)
    //        //            {
    //        //                MappingCsvFilePath = openFileDialog.FileName;
    //        //                if (!string.IsNullOrWhiteSpace(MappingCsvFilePath) && File.Exists(MappingCsvFilePath))
    //        //                {
    //        //                    LoadMappingFileFromCsv();
    //        //                    if (logger.IsInfoEnabled) logger.InfoFormat("Mapping file loaded => {0}", MappingCsvFilePath);
    //        //                }
    //        //                else
    //        //                {
    //        //                    MessageBox.Show((string)Application.Current.FindResource("Maping.FileNotExist"), (string)Application.Current.FindResource("Prompt"), MessageBoxButton.OK, MessageBoxImage.Warning);
    //        //                }
    //        //            }

    //        //            return;
    //        //        }

    //        //        // Cancel -> 直接返回
    //        //        return;
    //        //    }
    //        //}
    //        //catch (Exception ex)
    //        //{
    //        //    if (logger.IsErrorEnabled) logger.Error("Failed to reload mapping file or load persistence", ex);
    //        //    MessageBox.Show($"{(string)Application.Current.FindResource("Maping.ReloadFailed")}: {ex.Message}", (string)Application.Current.FindResource("State.Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    //        //}
    //    }
    //    private void RefreshStatus(object? obj)
    //    {
    //        _dataGrid?.Items.Refresh();
    //    }

    //    private void DieResultDisplay(DieViewModel dieViewModel)
    //    {
    //        //if (string.IsNullOrEmpty(dieViewModel.SerialNumber))
    //        //{
    //        //    CustomIVLVM.ClearResult();
    //        //    CustomImageVM?.ClearImageResult();
    //        //}
    //        // if (dieViewModel.Status == ChipStatus.IVL_TESTING || dieViewModel.Status == ChipStatus.IVL_COMPLETED)
    //        // {
    //        //ivlService.IVLResultDisplay(dieViewModel);
    //        //
    //        // else
    //        // {
    //        //aoiService.AOIResultDisplay(dieViewModel);
    //        // }
    //        //eqeService.EQEResultDisplay(dieViewModel);
    //        //vamService.VAMResultDisplay(dieViewModel);
    //        MainService.Instance.ResultDisplay(dieViewModel);
    //        // 新增：计算良率
    //        CalculateYieldBySerialNumber();
    //    }
    //    public MainService mainService { get; private set; }
    //    public void InitializeServive(MainService main)
    //    {
    //        mainService = main;
    //        mainService.ChipSelected += OnChipDieSelected;

    //        mainService.TestingCompleted += OnTestingCompleted;
    //        mainService.PreAutoTestingNextDie += OnAutoTestingNextDie;


    //    }

    //    private void StartManTest(object? obj)
    //    {
    //        StartManFlow();
    //    }


    //    private void OnTestingCompleted(object? sender, TestCompletedEventArgs e)
    //    {
    //        DoEndTesting(e.IsAuto);
    //    }
    //    private void OnAutoTestingNextDie(object? sender, (DieViewModel? preDie, DieViewModel nextDie) e)
    //    {
    //        Application.Current.Dispatcher.Invoke(() => { MoveNextSel(e); });
    //    }

    //    private void MoveNextSel((DieViewModel? preDie, DieViewModel nextDie) e)
    //    {
    //        if (e.preDie != null) e.preDie.UnSelected();
    //        SelectedItem = e.nextDie;
    //    }
    //    private void DoEndTesting(bool isAuto)
    //    {
    //        CompletedTestCount = 1;
    //        EnableBtnGUI(true);
    //        CalculateYieldBySerialNumber();
    //        AutoExportSummaryResult();
    //        // 补充：单Die进度拉满100%，总进度自动同步（由UpdateTotalProgress逻辑触发）
    //        SingleDieTestProgress = 100;
    //        OnPropertyChanged(nameof(ProgressText));

    //    }
    //    public void EnableBtnGUI(bool enabled)
    //    {
    //        CustomMappingVM.DisabledInput = IsProcessing = !enabled;

    //        OnPropertyChanged(nameof(IsNotProcessing));

    //        //ToolsVM?.FireUI();
    //    }
    //    private void ClearMapping()
    //    {
    //        CustomMappingVM?.Cleanup();
    //        CustomMappingVM.Chips.Clear();
    //        TestResults.Clear();
    //    }
    //    public void StopAutoFlow()
    //    {
    //        if (_testQueue == null) return;

    //        foreach (var item in _testQueue)
    //        {
    //            item.Die.UnSelected();

    //        }
    //        //EnableBtn(true);

    //        mainService.StopAutoTesting();

    //        CalculateYieldBySerialNumber();


    //    }
    //    public void StartAutoFlow()
    //    {
    //        if (SelectedWPFlow == null)
    //        {
    //            logger.Error("No test process (Flow) selected");
    //            return;
    //        }

    //        _testQueue = GetSelectedTestItems();
    //        if (_testQueue.Count == 0)
    //        {
    //            System.Windows.MessageBox.Show($"{(string)Application.Current.FindResource("Nodata")}", $"{(string)Application.Current.FindResource("Prompt")}", MessageBoxButton.OK, MessageBoxImage.Information);
    //            return;
    //        }


    //        TestingReady(_testQueue);
    //        EnableBtnGUI(false);
    //        mainService.StartAutoTesting(_selectedWPFlow, GetSelectedDieTestItems());
    //    }
    //    public void StartManFlow()
    //    {
    //        if (SelectedWPFlow != null && SelectedItem is DieViewModel die)
    //        {

    //            // 1. 手动测试进度条初始化（关键：初始化所有进度参数）
    //            TotalTestCount = 1;
    //            CompletedTestCount = 0;
    //            CurrentDieIndex = 1; // 手动测试只有1个Die，索引直接设为1
    //            CurrentDieInfo = $"{die.MapX}/{die.MapY}"; // 显示当前Die的行列
    //            SingleDieTestProgress = 0; // 重置单Die进度
    //            TotalTestProgress = 0; // 重置总进度

    //            EnableBtnGUI(false);
    //            ManTestingReady(die);
    //            // 2. 启动单Die进度跟踪（关键：通知ViewModel开始跟踪当前Die进度）
    //            StartSingleDieTest(die);
    //            mainService.DoDieFlowExec(_selectedWPFlow, die, false, false);
    //            ActivateCorrespondingPanel?.Invoke(this, _selectedWPFlow);
    //        }
    //    }
    //    private List<DieViewModel> GetSelectedDieTestItems()
    //    {
    //        var testQueue = new List<DieViewModel>();
    //        var fType = _selectedWPFlow?.FlowType;
    //        foreach (var die in TestResults)
    //        {
    //            if (die.IsAOIEnabled && fType == CVWaferProberFlowType.AOI)
    //            {
    //                testQueue.Add(die);
    //            }
    //            if (die.IsIVLEnabled && (fType == CVWaferProberFlowType.IVL ||
    //                fType == CVWaferProberFlowType.IVL_SP ||
    //                fType == CVWaferProberFlowType.IVL_Camera))
    //            {
    //                testQueue.Add(die);
    //            }
    //            if (die.IsEQEEnabled && fType == CVWaferProberFlowType.EQE)
    //            {
    //                testQueue.Add(die);
    //            }
    //            if (die.IsVAMEnabled && fType == CVWaferProberFlowType.VAM)
    //            {
    //                testQueue.Add(die);
    //            }
    //        }

    //        Stopwatch sw = Stopwatch.StartNew();
    //        var resultList = SortEfficiently(testQueue);
    //        sw.Stop();
    //        Stopwatch sw2 = Stopwatch.StartNew();
    //        var resultList1 = GetSortedResultsLinq(testQueue);
    //        sw2.Stop();
    //        logger.InfoFormat("SortEfficiently={0},GetSortedResultsLinq={1}", sw.Elapsed.ToString(), sw2.Elapsed.ToString());
    //        return resultList;
    //    }
    //    // ========== 静态列+动态列配置初始化 ==========
    //    private void InitColumnConfigs()
    //    {
    //        // 初始化动态可选列（第14列及以后）
    //        _dynamicColumnConfigs = new List<ColumnConfig>
    //        {
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "LightOnStatus",
    //                ColumnBindingPath = "LightOnStatus",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "RegisterPixels",
    //                ColumnBindingPath = "RegisterPixels",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "Final Class",
    //                ColumnBindingPath = "FinalClass",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "AOI GradeLevel",
    //                ColumnBindingPath = "AOIGradeLevel",
    //                IsSelected = true,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "Black Pattern",
    //                ColumnBindingPath = "BlackPattern",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "Luminance(nit)",
    //                ColumnBindingPath = "Luminance",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "Voltage(v)",
    //                ColumnBindingPath = "Voltage",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "Current(mA)",
    //                ColumnBindingPath = "Current",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "Dominant Wavelength",
    //                ColumnBindingPath = "DominantWavelength",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "Temperature(℃)",
    //                ColumnBindingPath = "Temperature",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "Pixel Logic",
    //                ColumnBindingPath = "PixelLogic",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "Pin Pressure",
    //                ColumnBindingPath = "Pressure",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "TouchDown Counts",
    //                ColumnBindingPath = "TouchDownCounts",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "Probing Card SN",
    //                ColumnBindingPath = "ProbingCardSN",
    //                IsSelected = false,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "AxisX",
    //                ColumnBindingPath = "MotionAxisX",
    //                IsSelected = true,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "AxisY",
    //                ColumnBindingPath = "MotionAxisY",
    //                IsSelected = true,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //            new ColumnConfig
    //            {
    //                ColumnHeader = "AxisZ",
    //                ColumnBindingPath = "MotionAxisZ",
    //                IsSelected = true,
    //                IsOptional = true,
    //                ColumnType = ColumnType.Text,
    //                ColumnKey = ColumnKey.Other
    //            },
    //        };

    //        // 初始化选中的动态列（默认不选）
    //        SelectedDynamicColumns = new ObservableCollection<ColumnConfig>(
    //            _dynamicColumnConfigs.Where(c => c.IsSelected));
    //    }
    //    public List<DieViewModel> GetSortedResultsLinq(List<DieViewModel> testQueue)
    //    {
    //        return new List<DieViewModel>(
    //            testQueue
    //                .GroupBy(d => d.MapY)
    //                .OrderBy(g => g.Key)
    //                .SelectMany((g, index) =>
    //                    index % 2 == 0
    //                        ? g.OrderBy(d => d.MapX)
    //                        : g.OrderByDescending(d => d.MapX))
    //                .ToList()
    //        );
    //    }
    //    public List<DieViewModel> SortEfficiently(List<DieViewModel> testQueue)
    //    {
    //        // 先按 Y 和 X 排序
    //        var list = testQueue.OrderBy(d => d.MapY).ThenBy(d => d.MapX).ToList();
    //        var result = new List<DieViewModel>();

    //        int? currentY = int.MinValue;
    //        List<DieViewModel> currentGroup = new List<DieViewModel>();
    //        int groupIndex = 0;

    //        foreach (var item in list)
    //        {
    //            if (item.MapY != currentY)
    //            {
    //                // 处理上一个组
    //                if (groupIndex % 2 == 1)
    //                {
    //                    currentGroup.Reverse(); // 奇数组反向
    //                }

    //                result.AddRange(currentGroup);
    //                currentGroup = new List<DieViewModel>();
    //                currentY = item.MapY;
    //                groupIndex++;
    //            }

    //            currentGroup.Add(item);
    //        }

    //        // 处理最后一个组
    //        if (groupIndex % 2 == 1)
    //        {
    //            currentGroup.Reverse();
    //        }
    //        result.AddRange(currentGroup);

    //        return result;
    //    }
    //    public void OpenSummaryConfig()
    //    {
    //        var configWindow = new SummaryConfigWindow(_dynamicColumnConfigs);
    //        if (configWindow.ShowDialog() == true)
    //        {
    //            // 更新选中的动态列
    //            SelectedDynamicColumns.Clear();
    //            foreach (var config in configWindow.SelectedColumns)
    //            {
    //                SelectedDynamicColumns.Add(config);
    //                // 同步更新配置状态
    //                var targetConfig = _dynamicColumnConfigs.First(c => c.ColumnBindingPath == config.ColumnBindingPath);
    //                targetConfig.IsSelected = config.IsSelected;
    //            }
    //            // 刷新动态列
    //            UpdateDataGridColumns();
    //        }
    //    }
    //    private void ManTestingReady(DieViewModel die)
    //    {
    //        string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fff");
    //        die.TestingReady(ProberId, timestamp);
    //        if (_isAutoSN) Timestamp = timestamp;
    //    }

    //    private void TestingReady(List<TestItem> testItems)
    //    {
    //        CustomMappingVM.DisabledInput = IsProcessing = true;

    //        string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fff");
    //        if (_isAutoSN) Timestamp = timestamp;
    //        foreach (var itemT in testItems)
    //        {
    //            itemT.Die.TestingReady(ProberId, timestamp);
    //        }
    //    }

    //    #region 反选方法（添加刷新） 
    //    private void ExecuteInvertSelectAOI(object obj)
    //    {
    //        foreach (var item in TestResults)
    //        {
    //            item.IsAOIEnabled = !item.IsAOIEnabled;
    //        }
    //        _dataGrid?.Items.Refresh();
    //        UpdateSelectAllAOIState();
    //    }

    //    private void ExecuteInvertSelectIVL(object obj)
    //    {
    //        foreach (var item in TestResults)
    //        {
    //            item.IsIVLEnabled = !item.IsIVLEnabled;
    //        }
    //        _dataGrid?.Items.Refresh();
    //        UpdateSelectAllIVLState();
    //    }

    //    private void ExecuteInvertSelectEQE(object obj)
    //    {
    //        foreach (var item in TestResults)
    //        {
    //            item.IsEQEEnabled = !item.IsEQEEnabled;
    //        }
    //        _dataGrid?.Items.Refresh();
    //        UpdateSelectAllEQEState();
    //    }

    //    private void ExecuteInvertSelectVAM(object obj)
    //    {
    //        foreach (var item in TestResults)
    //        {
    //            item.IsVAMEnabled = !item.IsVAMEnabled;
    //        }
    //        _dataGrid?.Items.Refresh();
    //        UpdateSelectAllVAMState();
    //    }

    //    #endregion

    //    #region AOI 全选/部分选中事件
    //    private void AOIItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    //    {
    //        if (e.NewItems != null)
    //        {
    //            SubscribeItems_AOI(e.NewItems.Cast<DieViewModel>());
    //        }

    //        if (e.OldItems != null)
    //        {
    //            UnsubscribeItems_AOI(e.OldItems.Cast<DieViewModel>());
    //        }

    //        UpdateSelectAllAOIState();
    //    }

    //    private void SubscribeItems_AOI(IEnumerable<DieViewModel> items)
    //    {
    //        foreach (var item in items)
    //        {
    //            item.PropertyChanged += AOI_Item_PropertyChanged;
    //        }
    //    }

    //    private void UnsubscribeItems_AOI(IEnumerable<DieViewModel> items)
    //    {
    //        foreach (var item in items)
    //        {
    //            item.PropertyChanged -= AOI_Item_PropertyChanged;
    //        }
    //    }

    //    private void AOI_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    //    {
    //        if (e.PropertyName == nameof(DieViewModel.IsAOIEnabled) && !_isUpdatingFromHeader_AOI)
    //        {
    //            UpdateSelectAllAOIState();
    //        }
    //    }
    //    #endregion

    //    #region IVL 全选/部分选中事件
    //    private void IVLItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    //    {
    //        if (e.NewItems != null)
    //        {
    //            SubscribeItems_IVL(e.NewItems.Cast<DieViewModel>());
    //        }

    //        if (e.OldItems != null)
    //        {
    //            UnsubscribeItems_IVL(e.OldItems.Cast<DieViewModel>());
    //        }

    //        UpdateSelectAllIVLState();
    //    }

    //    private void SubscribeItems_IVL(IEnumerable<DieViewModel> items)
    //    {
    //        foreach (var item in items)
    //        {
    //            item.PropertyChanged += IVL_Item_PropertyChanged;
    //        }
    //    }

    //    private void UnsubscribeItems_IVL(IEnumerable<DieViewModel> items)
    //    {
    //        foreach (var item in items)
    //        {
    //            item.PropertyChanged -= IVL_Item_PropertyChanged;
    //        }
    //    }

    //    private void IVL_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    //    {
    //        if (e.PropertyName == nameof(DieViewModel.IsIVLEnabled) && !_isUpdatingFromHeader_IVL)
    //        {
    //            UpdateSelectAllIVLState();
    //        }
    //    }
    //    #endregion

    //    #region EQE 全选/部分选中事件
    //    private void EQEItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    //    {
    //        if (e.NewItems != null)
    //        {
    //            SubscribeItems_EQE(e.NewItems.Cast<DieViewModel>());
    //        }

    //        if (e.OldItems != null)
    //        {
    //            UnsubscribeItems_EQE(e.OldItems.Cast<DieViewModel>());
    //        }

    //        UpdateSelectAllEQEState();
    //    }

    //    private void SubscribeItems_EQE(IEnumerable<DieViewModel> items)
    //    {
    //        foreach (var item in items)
    //        {
    //            item.PropertyChanged += EQE_Item_PropertyChanged;
    //        }
    //    }

    //    private void UnsubscribeItems_EQE(IEnumerable<DieViewModel> items)
    //    {
    //        foreach (var item in items)
    //        {
    //            item.PropertyChanged -= EQE_Item_PropertyChanged;
    //        }
    //    }

    //    private void EQE_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    //    {
    //        if (e.PropertyName == nameof(DieViewModel.IsEQEEnabled) && !_isUpdatingFromHeader_EQE)
    //        {
    //            UpdateSelectAllEQEState();
    //        }
    //    }
    //    #endregion

    //    #region VAM 全选/部分选中事件
    //    private void VAMItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    //    {
    //        if (e.NewItems != null)
    //        {
    //            SubscribeItems_VAM(e.NewItems.Cast<DieViewModel>());
    //        }

    //        if (e.OldItems != null)
    //        {
    //            UnsubscribeItems_VAM(e.OldItems.Cast<DieViewModel>());
    //        }

    //        UpdateSelectAllVAMState();
    //    }

    //    private void SubscribeItems_VAM(IEnumerable<DieViewModel> items)
    //    {
    //        foreach (var item in items)
    //        {
    //            item.PropertyChanged += VAM_Item_PropertyChanged;
    //        }
    //    }

    //    private void UnsubscribeItems_VAM(IEnumerable<DieViewModel> items)
    //    {
    //        foreach (var item in items)
    //        {
    //            item.PropertyChanged -= VAM_Item_PropertyChanged;
    //        }
    //    }

    //    private void VAM_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    //    {
    //        if (e.PropertyName == nameof(DieViewModel.IsVAMEnabled) && !_isUpdatingFromHeader_VAM)
    //        {
    //            UpdateSelectAllVAMState();
    //        }
    //    }

    //    #endregion

    //    #region 三态全选状态更新
    //    private void UpdateSelectAllAOIState()
    //    {
    //        if (TestResults == null || TestResults.Count == 0)
    //        {
    //            SelectAllAOI = false;
    //            return;
    //        }

    //        int selectedCount = TestResults.Count(item => item.IsAOIEnabled);
    //        int totalCount = TestResults.Count;

    //        bool? newState = selectedCount switch
    //        {
    //            0 => false,
    //            var c when c == totalCount => true,
    //            _ => null
    //        };

    //        if (!Equals(SelectAllAOI, newState))
    //        {
    //            SelectAllAOI = newState;
    //            OnPropertyChanged(nameof(SelectAllAOI));
    //        }
    //    }

    //    private void UpdateSelectAllIVLState()
    //    {
    //        if (TestResults == null || TestResults.Count == 0)
    //        {
    //            SelectAllIVL = false;
    //            return;
    //        }

    //        int selectedCount = TestResults.Count(item => item.IsIVLEnabled);
    //        int totalCount = TestResults.Count;

    //        bool? newState = selectedCount switch
    //        {
    //            0 => false,
    //            var c when c == totalCount => true,
    //            _ => null
    //        };

    //        if (!Equals(SelectAllIVL, newState))
    //        {
    //            SelectAllIVL = newState;
    //            OnPropertyChanged(nameof(SelectAllIVL));
    //        }
    //    }

    //    private void UpdateSelectAllEQEState()
    //    {
    //        if (TestResults == null || TestResults.Count == 0)
    //        {
    //            SelectAllEQE = false;
    //            return;
    //        }

    //        int selectedCount = TestResults.Count(item => item.IsEQEEnabled);
    //        int totalCount = TestResults.Count;

    //        bool? newState = selectedCount switch
    //        {
    //            0 => false,
    //            var c when c == totalCount => true,
    //            _ => null
    //        };

    //        if (!Equals(SelectAllEQE, newState))
    //        {
    //            SelectAllEQE = newState;
    //            OnPropertyChanged(nameof(SelectAllEQE));
    //        }
    //    }

    //    private void UpdateSelectAllVAMState()
    //    {
    //        if (TestResults == null || TestResults.Count == 0)
    //        {
    //            SelectAllVAM = false;
    //            return;
    //        }

    //        int selectedCount = TestResults.Count(item => item.IsVAMEnabled);
    //        int totalCount = TestResults.Count;

    //        bool? newState = selectedCount switch
    //        {
    //            0 => false,
    //            var c when c == totalCount => true,
    //            _ => null
    //        };

    //        if (!Equals(SelectAllVAM, newState))
    //        {
    //            SelectAllVAM = newState;
    //            OnPropertyChanged(nameof(SelectAllVAM));
    //        }
    //    }

    //    #endregion


    //    public void SetDataGrid(DataGrid dataGrid)
    //    {
    //        _dataGrid = dataGrid;
    //        // 初始化动态列
    //        UpdateDataGridColumns();
    //    }

    //    public void LoadMappingFile(string mappingFile)
    //    {
    //        MappingCsvFilePath = mappingFile;
    //        Task.Factory.StartNew(() => LoadMappingFileFromCsvAsync());
    //    }

    //    private void LoadMappingFileFromCsvAsync()
    //    {
    //        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
    //        {
    //            LoadMappingFileFromCsv();
    //        });
    //    }

    //    private void LoadMappingFileFromCsv()
    //    {
    //        List<CVMappingData> mappingData = null;
    //        if (!System.IO.File.Exists(MappingCsvFilePath))
    //        {
    //            if (logger.IsWarnEnabled) logger.WarnFormat("File not exist => {0}", MappingCsvFilePath);
    //            return;
    //        }
    //        bool bR = CsvMappingDataTool.LoadMappingCsv(MappingCsvFilePath, ref mappingData);
    //        if (bR && mappingData != null && mappingData.Count > 0)
    //        {
    //            CustomMappingVM.RefreshFromMap(mappingData);
    //            ObservableCollection<DieViewModel> _TestResults = new ObservableCollection<DieViewModel>();
    //            foreach (var map in CustomMappingVM.Chips)
    //            {
    //                DieViewModel dieViewModel = new DieViewModel(map);
    //                // 从CustomMappingVM获取值并赋值给DieViewModel
    //                dieViewModel.Temperature = CustomMappingVM.Temperatures.ToString("F1"); // double转string（保留1位小数）
    //                dieViewModel.Pressure = CustomMappingVM.Pressure;                      // 直接赋值
    //                dieViewModel.ProbingCardSN = CustomMappingVM.SN;                       // SN对应ProbingCardSN
    //                dieViewModel.TouchDownCounts = CustomMappingVM.TDCount;
    //                _TestResults.Add(dieViewModel);
    //            }
    //            var sorted = _TestResults.OrderBy(x => x.MapY).ToList();
    //            TestResults.Clear();
    //            foreach (var item in sorted)
    //            {
    //                if (item.Status != ChipStatus.SKIP)
    //                {
    //                    TestResults.Add(item);
    //                }
    //            }
    //        }
    //        BuildSNIndex();
    //        if (string.IsNullOrWhiteSpace(SearchSN))
    //        {
    //            FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);

    //        }
    //        else
    //        {
    //            ExecuteSearch();
    //        }

    //        CalculateYieldBySerialNumber();
    //    }

    //    // SN索引字典
    //    private Dictionary<string, List<DieViewModel>> _snIndex = new Dictionary<string, List<DieViewModel>>(StringComparer.OrdinalIgnoreCase);

    //    private void BuildSNIndex()
    //    {
    //        _snIndex.Clear();
    //        foreach (var die in TestResults)
    //        {
    //            if (string.IsNullOrEmpty(die.SerialNumber)) continue;
    //            if (!_snIndex.ContainsKey(die.SerialNumber))
    //            {
    //                _snIndex[die.SerialNumber] = new List<DieViewModel>();
    //            }
    //            _snIndex[die.SerialNumber].Add(die);
    //        }
    //    }

    //    private void ExecuteSearch(object parameter = null)
    //    {
    //        if (string.IsNullOrWhiteSpace(SearchSN))
    //        {
    //            FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);
    //        }
    //        else
    //        {
    //            var searchKey = SearchSN.Trim();
    //            if (_snIndex.TryGetValue(searchKey, out var results))
    //            {
    //                FilteredTestResults = new ObservableCollection<DieViewModel>(results);
    //            }
    //            else
    //            {
    //                var query = TestResults.Where(die =>
    //                    !string.IsNullOrEmpty(die.SerialNumber) &&
    //                    die.SerialNumber.Contains(searchKey, StringComparison.OrdinalIgnoreCase));
    //                FilteredTestResults = new ObservableCollection<DieViewModel>(query);
    //            }
    //        }
    //    }
    //    private void ManScrollToItem(object? toItem)
    //    {
    //        System.Windows.Application.Current.Dispatcher.Invoke(() =>
    //        {
    //            if (_dataGrid != null && toItem != null)
    //            {
    //                _dataGrid.ScrollIntoView(toItem);
    //                _dataGrid.UpdateLayout();
    //            }
    //        });
    //    }
    //    // ========== 动态列更新（仅处理第14列及以后） ==========
    //    public void UpdateDataGridColumns()
    //    {
    //        if (_dataGrid == null) return;

    //        // 步骤1：保留前13列静态列，删除所有动态列
    //        while (_dataGrid.Columns.Count > StaticColumnCount)
    //        {
    //            _dataGrid.Columns.RemoveAt(StaticColumnCount);
    //        }

    //        // 步骤2：添加选中的动态列
    //        foreach (var config in SelectedDynamicColumns)
    //        {
    //            if (config.ColumnType == ColumnType.CheckBox)
    //            {
    //                var templateColumn = new DataGridTemplateColumn
    //                {
    //                    Header = config.ColumnHeader
    //                };
    //                var checkBoxFactory = new FrameworkElementFactory(typeof(CheckBox));
    //                checkBoxFactory.SetBinding(CheckBox.IsCheckedProperty, new Binding(config.ColumnBindingPath)
    //                {
    //                    Mode = BindingMode.OneWay,
    //                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
    //                });
    //                checkBoxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
    //                checkBoxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
    //                templateColumn.CellTemplate = new DataTemplate { VisualTree = checkBoxFactory };
    //                _dataGrid.Columns.Add(templateColumn);
    //            }
    //            else
    //            {
    //                _dataGrid.Columns.Add(new DataGridTextColumn
    //                {
    //                    Header = config.ColumnHeader,
    //                    Binding = new Binding(config.ColumnBindingPath)
    //                    {
    //                        Mode = BindingMode.OneWay,
    //                        TargetNullValue = "",
    //                        StringFormat = config.ColumnBindingPath.Contains("Time") ? "yyyy-MM-dd HH:mm:ss" : null
    //                    }
    //                });
    //            }
    //        }
    //    }

    //    #region 良率计算
    //    public void CalculateYieldBySerialNumber()
    //    {
    //        try
    //        {
    //            if (TestResults == null || !TestResults.Any())
    //            {
    //                YieldInfo = "0/0 (0.00%)";
    //                return;
    //            }

    //            var testedDices = TestResults.Where(d => !string.IsNullOrWhiteSpace(d.SerialNumber)).ToList();
    //            if (!testedDices.Any())
    //            {
    //                YieldInfo = "0/0 (0.00%)";
    //                return;
    //            }

    //            int successCount = testedDices.Count(d =>
    //                d.Status == ChipStatus.OK ||
    //                d.Status == ChipStatus.IVL_COMPLETED ||
    //                d.Status == ChipStatus.EQE_COMPLETED);

    //            double yieldRate = (double)successCount / testedDices.Count * 100;
    //            YieldInfo = $"{successCount}/{testedDices.Count} ({yieldRate:F2}%)";
    //        }
    //        catch (Exception ex)
    //        {
    //            logger.Error("Yield Calculation Exception", ex);
    //            YieldInfo = $"{(string)Application.Current.FindResource("CalculationException")}";
    //        }
    //    }
    //    #endregion

    //    #region 自动导出Summary（适配静态+动态列）

    //    private void AutoExportSummaryResult()
    //    {
    //        try
    //        {
    //            if (TestResults == null || !TestResults.Any())
    //            {
    //                logger.Info("No test results, skipping Summary export");
    //                return;
    //            }

    //            //固定导出根路径为 D:/Project
    //            string exportRootPath = @"D:\Project";

    //            // 自动创建目录（如果不存在）
    //            if (!Directory.Exists(exportRootPath))
    //            {
    //                Directory.CreateDirectory(exportRootPath);
    //                logger.Info($"Export directory has been created automatically：{exportRootPath}");//已自动创建导出目录
    //            }

    //            // 拼接最终保存路径（目录 + 带时间戳的文件名）
    //            var savePath = Path.Combine(
    //                exportRootPath,
    //                $"Summary_Result_{DateTime.Now:yyyyMMddHHmmss}.csv");

    //            // 合并静态列+选中的动态列
    //            var allSelectedColumns = new List<ColumnConfig>();
    //            allSelectedColumns.AddRange(_staticColumnConfigs);
    //            allSelectedColumns.AddRange(SelectedDynamicColumns);

    //            // 写入CSV
    //            using (var writer = new StreamWriter(savePath, false, System.Text.Encoding.UTF8))
    //            {
    //                // 表头
    //                var headers = allSelectedColumns.Select(c => c.ColumnHeader).ToList();
    //                writer.WriteLine(string.Join(",", headers));

    //                // 数据行
    //                foreach (var die in TestResults)
    //                {
    //                    var rowData = new List<string>();
    //                    foreach (var config in allSelectedColumns)
    //                    {
    //                        var prop = die.GetType().GetProperty(config.ColumnBindingPath);
    //                        if (prop == null)
    //                        {
    //                            rowData.Add("");
    //                            continue;
    //                        }

    //                        var value = prop.GetValue(die);
    //                        if (value == null || value == DBNull.Value)
    //                        {
    //                            rowData.Add("");
    //                        }
    //                        else if (value is bool boolValue)
    //                        {
    //                            rowData.Add(boolValue ? "Y" : "N");
    //                        }
    //                        else if (value is DateTime dateTimeValue)
    //                        {
    //                            rowData.Add(dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss"));
    //                        }
    //                        else
    //                        {
    //                            rowData.Add(value.ToString());
    //                        }
    //                    }
    //                    // 处理包含逗号的字段，添加双引号包裹
    //                    writer.WriteLine(string.Join(",", rowData.Select(d => d.Contains(",") ? $"\"{d}\"" : d)));
    //                }
    //            }

    //            logger.Info($"The summary results have been automatically exported：{savePath}");//" : "Summary结果已自动导出")}
    //            //Application.Current.Dispatcher.Invoke(() =>
    //            //{
    //            //    MessageBox.Show($"{(IsEnglishMode ? "The summary results have been exported to" : "Summary结果已导出至")}：{savePath}", IsEnglishMode? "Export successful" : "导出成功",
    //            //        MessageBoxButton.OK, MessageBoxImage.Information);
    //            //});
    //        }
    //        catch (Exception ex)
    //        {
    //            logger.Error("Failed to export Summary results", ex);//" : "Summary结果导出失败"
    //            //Application.Current.Dispatcher.Invoke(() =>
    //            //{
    //            //    MessageBox.Show($"导出失败：{ex.Message}", "错误",
    //            //        MessageBoxButton.OK, MessageBoxImage.Error);
    //            //});
    //        }
    //    }
    //    #endregion

    //    public void LoadFlow(List<RespDataFlowTempDTO>? flows)
    //    {
    //        //var flows = rcService.RcLoadFlows();
    //        FlowItems.Clear();
    //        SelectedFlow = null;
    //        if (flows != null)
    //        {
    //            foreach (var flow in flows)
    //            {
    //                FlowItems.Add(new FlowViewModel(flow));
    //            }
    //            if (FlowItems.Count > 0) SelectedFlow = FlowItems[FlowItems.Count - 1];
    //        }
    //    }
    //    private void ResetStatus(object? obj)
    //    {
    //        foreach (var die in TestResults)
    //        {
    //            die.ResetStatus();
    //        }
    //        CalculateYieldBySerialNumber();
    //    }

    //    private void SaveTestResult(object? obj)
    //    {
    //        var saveFileDialog = new SaveFileDialog
    //        {
    //            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
    //            DefaultExt = ".csv",
    //            FileName = string.Format("{0}_{1}_result.csv", ProberId, _Timestamp),
    //            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
    //        };

    //        if (saveFileDialog.ShowDialog() == true)
    //        {
    //            try
    //            {
    //                ResultService.SaveToCSV(saveFileDialog.FileName, TestResults);
    //                if (logger.IsInfoEnabled) logger.InfoFormat("Save result ok => {0}", saveFileDialog.FileName);
    //            }
    //            catch (Exception ex)
    //            {
    //                if (logger.IsErrorEnabled) logger.Error(ex);
    //            }
    //        }
    //    }

    //    private void LoadTestResult(object? obj)
    //    {
    //        // 支持三种用法：
    //        // 1) obj is string path -> 打开对话框或直接加载该CSV（保持原逻辑）
    //        // 2) obj == null -> 从上次会话持久化文件加载（用户点击“重载”且未选择文件）
    //        // 3) 保持原有打开文件对话框行为
    //        if (obj is string path && !string.IsNullOrWhiteSpace(path))
    //        {
    //            // 通过指定路径加载 CSV
    //            try
    //            {
    //                ResultService.LoadFromCSV(path, TestResults);
    //                _dataGrid?.Items.Refresh();
    //                if (logger.IsInfoEnabled) logger.InfoFormat("Load result ok => {0}", path);
    //            }
    //            catch (Exception ex)
    //            {
    //                if (logger.IsErrorEnabled) logger.Error(ex);
    //            }
    //            return;
    //        }

    //        if (obj == null)
    //        {
    //            // 从持久化位置加载上次数据
    //            Task.Run(async () =>
    //            {
    //                try
    //                {
    //                    await LoadFromPersistenceAsync();
    //                    Application.Current?.Dispatcher?.Invoke(() =>
    //                    {
    //                        _dataGrid?.Items.Refresh();
    //                        if (logger.IsInfoEnabled) logger.Info("Loaded last session test results from persistence.");
    //                    });
    //                }
    //                catch (Exception ex)
    //                {
    //                    if (logger.IsErrorEnabled) logger.Error("Failed to load persisted test results", ex);
    //                }
    //            });
    //            return;
    //        }

    //        // 旧行为：打开对话框让用户选择文件
    //        var openFileDialog = new OpenFileDialog
    //        {
    //            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
    //            DefaultExt = ".csv",
    //            FileName = "result.csv",
    //            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
    //        };
    //        if (openFileDialog.ShowDialog() == true)
    //        {
    //            try
    //            {
    //                ResultService.LoadFromCSV(openFileDialog.FileName, TestResults);
    //                _dataGrid?.Items.Refresh();
    //                if (logger.IsInfoEnabled) logger.InfoFormat("Load result ok => {0}", openFileDialog.FileName);
    //            }
    //            catch (Exception ex)
    //            {
    //                if (logger.IsErrorEnabled) logger.Error(ex);
    //            }
    //        }
    //    }
    //    // 新增：从持久化文件加载 DTO 并应用到 TestResults（不覆盖 Mapping 布局，只覆盖属性/结果）
    //    public async Task LoadFromPersistenceAsync()
    //    {
    //        try
    //        {
    //            var dtos = await TestResultPersistenceService.LoadAsync();
    //            if (dtos == null || dtos.Count == 0) return;

    //            Application.Current?.Dispatcher?.Invoke(() =>
    //            {
    //                ApplyDtosToTestResults(dtos);
    //                _dataGrid?.Items.Refresh();
    //                BuildSNIndex();
    //                CalculateYieldBySerialNumber();

    //                // 自动选中：优先选中 DTO 中第一个有 SerialNumber 且在 TestResults 中存在的记录，
    //                // 若没有则选中 TestResults 的第一项（保证 UI 有选中项）
    //                try
    //                {
    //                    DieViewModel? toSelect = null;

    //                    var dtoWithSN = dtos.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.SerialNumber));
    //                    if (dtoWithSN != null)
    //                    {
    //                        toSelect = TestResults.FirstOrDefault(t =>
    //                            !string.IsNullOrWhiteSpace(t.SerialNumber) &&
    //                            t.SerialNumber.Equals(dtoWithSN.SerialNumber, System.StringComparison.OrdinalIgnoreCase));
    //                    }

    //                    if (toSelect == null && TestResults.Count > 0)
    //                    {
    //                        toSelect = TestResults[0];
    //                    }

    //                    if (toSelect != null)
    //                    {
    //                        selfClick = false; // 防止触发外部关联选中逻辑误判
    //                        SelectedItem = toSelect;
    //                        ManScrollToItem(toSelect);
    //                        if (logger.IsInfoEnabled) logger.InfoFormat("Auto-selected item after loading persistence => Id={0}, SN={1}", toSelect.Id, toSelect.SerialNumber);
    //                    }
    //                }
    //                catch (Exception ex)
    //                {
    //                    if (logger.IsWarnEnabled) logger.Warn("Auto-select after LoadFromPersistenceAsync failed", ex);
    //                }
    //            });
    //        }
    //        catch (Exception ex)
    //        {
    //            if (logger.IsErrorEnabled) logger.Error("LoadFromPersistenceAsync failed", ex);
    //        }
    //    }

    //    private void ApplyDtosToTestResults(List<TestResultDto> dtos)
    //    {
    //        if (dtos == null || dtos.Count == 0) return;
    //        foreach (var dto in dtos)
    //        {
    //            try
    //            {
    //                DieViewModel? die = null;
    //                if (dto.Id != 0)
    //                    die = TestResults.FirstOrDefault(d => d.Id == dto.Id);
    //                if (die == null && !string.IsNullOrEmpty(dto.SerialNumber))
    //                {
    //                    var sn = dto.SerialNumber.Trim();
    //                    die = TestResults.FirstOrDefault(d => !string.IsNullOrEmpty(d.SerialNumber) &&
    //                        d.SerialNumber.Trim().Equals(sn, System.StringComparison.OrdinalIgnoreCase));
    //                }
    //                if (die == null && dto.MapX.HasValue && dto.MapY.HasValue)
    //                    die = TestResults.FirstOrDefault(d => d.MapX == dto.MapX && d.MapY == dto.MapY);

    //                if (die == null) continue;

    //                // 只覆盖测试结果相关字段，保留 mapping 中的其它信息
    //                die.IsAOIEnabled = dto.IsAOIEnabled;
    //                die.IsIVLEnabled = dto.IsIVLEnabled;
    //                die.IsEQEEnabled = dto.IsEQEEnabled;
    //                die.IsVAMEnabled = dto.IsVAMEnabled;
    //                die.SerialNumber = dto.SerialNumber;

    //                // 解析 DisplayStatus：增强容错
    //                if (!string.IsNullOrWhiteSpace(dto.DisplayStatus))
    //                {
    //                    string raw = dto.DisplayStatus.Trim().Trim('"').Trim();
    //                    bool applied = false;

    //                    // 1) 尝试按枚举名解析（例如 "OK", "WAITING"）
    //                    if (System.Enum.TryParse<ChipStatus>(raw, true, out var enumStatus))
    //                    {
    //                        die.ChangeStatusOnly(enumStatus);
    //                        applied = true;
    //                    }

    //                    if (!applied)
    //                    {
    //                        // 2) 先使用当前语言尝试解析
    //                        try
    //                        {
    //                            var targetStatus = ChipStatusTool.GetStatusFromDisplay(raw, die.IsChinese);
    //                            // 有些实现可能返回默认值 - 只在明显不同的时候应用
    //                            die.ChangeStatusOnly(targetStatus);
    //                            applied = true;
    //                        }
    //                        catch
    //                        {
    //                            applied = false;
    //                        }
    //                    }

    //                    if (!applied)
    //                    {
    //                        // 3) 再尝试使用另一种语言解析作为回退
    //                        try
    //                        {
    //                            var targetStatus2 = ChipStatusTool.GetStatusFromDisplay(raw, !die.IsChinese);
    //                            die.ChangeStatusOnly(targetStatus2);
    //                            applied = true;
    //                        }
    //                        catch { applied = false; }
    //                    }

    //                    // 如果仍未成功，则忽略（保持原状态），并记录日志
    //                    if (!applied && logger.IsWarnEnabled)
    //                    {
    //                        logger.WarnFormat("Failed to parse DisplayStatus '{0}' for Die Id={1}, SN={2}", dto.DisplayStatus, die.Id, die.SerialNumber);
    //                    }
    //                }

    //                // 修复：更新DataValue（修改底层ChipData）
    //                if (die.chipViewModel?.ChipData != null && !string.IsNullOrEmpty(dto.DataValue))
    //                {
    //                    if (double.TryParse(dto.DataValue, out double dataValue))
    //                    {
    //                        die.chipViewModel.ChipData.DataValue = dataValue;
    //                        die.RefreshDataValue();
    //                    }
    //                }

    //                die.StartTestTime = dto.StartTestTime;
    //                die.EndTestTime = dto.EndTestTime;
    //                die.TotalTime = dto.TotalTime;

    //                die.AOIGradeLevel = dto.AOIGradeLevel;
    //                //die.LightOnStatus = dto.LightOnStatus;
    //                //die.RegisterPixels = dto.RegisterPixels;
    //                //die.FinalClass = dto.FinalClass;
    //                die.BlackPattern = dto.BlackPattern;
    //                //die.Temperature = dto.Temperature;
    //                //die.PixelLogic = dto.PixelLogic;
    //                //die.Pressure = dto.Pressure;
    //                //die.TouchDownCounts = dto.TouchDownCounts;
    //                //die.ProbingCardSN = dto.ProbingCardSN;

    //            }
    //            catch (Exception ex)
    //            {
    //                if (logger.IsWarnEnabled) logger.Warn("Apply DTO to TestResults failed for one item", ex);
    //            }
    //        }
    //    }

    //    // 新增：把 TestResults 自动保存为 CSV 的默认位置（Documents\CVWaferProber\AutoSaves）
    //    private void SaveTestResultsToDefaultFile()
    //    {
    //        try
    //        {
    //            if (TestResults == null || !TestResults.Any())
    //            {
    //                if (logger.IsInfoEnabled) logger.Info("No test results to auto-save on exit.");
    //                return;
    //            }

    //            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CVWaferProber", "AutoSaves");
    //            Directory.CreateDirectory(folder);
    //            string fileName = $"TestResults_Auto_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
    //            string fullPath = Path.Combine(folder, fileName);

    //            // 使用已有 ResultService 写 CSV（此方法会同步写文件）
    //            ResultService.SaveToCSV(fullPath, TestResults);

    //            if (logger.IsInfoEnabled) logger.InfoFormat("Auto-saved test results to => {0}", fullPath);
    //        }
    //        catch (Exception ex)
    //        {
    //            if (logger.IsErrorEnabled) logger.Error("SaveTestResultsToDefaultFile failed", ex);
    //        }
    //    }
    //    // 在程序退出时保存当前 TestResults 到持久化文件
    //    public async Task SaveLastSessionAsync()
    //    {
    //        try
    //        {
    //            var dtos = TestResults.Select(r => TestResultDto.FromObject(r)).Where(x => x != null).ToList();
    //            await TestResultPersistenceService.SaveAsync(dtos);
    //            if (logger.IsInfoEnabled) logger.Info("Saved last session test results to persistence.");
    //        }
    //        catch (Exception ex)
    //        {
    //            if (logger.IsErrorEnabled) logger.Error("SaveLastSessionAsync failed", ex);
    //        }
    //    }


    //    public void SelectItemById(uint id)
    //    {
    //        var itemToSelect = TestResults.FirstOrDefault(item => item.Id == id);
    //        if (itemToSelect != null)
    //        {
    //            selfClick = false;
    //            SelectedItem = itemToSelect;
    //        }
    //    }

    //    private List<TestItem> GetSelectedTestItems()
    //    {
    //        var testQueue = new List<TestItem>();

    //        foreach (var die in TestResults)
    //        {
    //            if (die.IsAOIEnabled)
    //            {
    //                testQueue.Add(new TestItem { Die = die, TestType = "AOI" });
    //            }
    //            if (die.IsIVLEnabled)
    //            {
    //                testQueue.Add(new TestItem { Die = die, TestType = "IVL" });
    //            }
    //            if (die.IsEQEEnabled)
    //            {
    //                testQueue.Add(new TestItem { Die = die, TestType = "EQE" });
    //            }
    //            if (die.IsVAMEnabled)
    //            {
    //                testQueue.Add(new TestItem { Die = die, TestType = "VAM" });
    //            }

    //        }

    //        return testQueue;
    //    }

    //    private void ToFlowSel(CVWaferProberFlowType type)
    //    {
    //        foreach (var item in WPFlows)
    //        {
    //            if (item.FlowType == type)
    //            {
    //                SelectedWPFlow = item;
    //                break;
    //            }
    //        }
    //    }
    //    public void ToIntegratingSphere()
    //    {
    //        ToFlowSel(CVWaferProberFlowType.EQE);
    //    }

    //    public void ToAuxCamera()
    //    {
    //        ToFlowSel(CVWaferProberFlowType.VAM);
    //    }   

    //    public void ToMainCamera()
    //    {
    //        ToFlowSel(CVWaferProberFlowType.AOI);
    //    }

    //    public void OnMoveTo(ChipViewModel? dieVM)
    //    {
    //        DieViewModel? toSelect = TestResults.First(t => t.MapX == dieVM.Column && t.MapY == dieVM.Row);

    //        if (toSelect != null)
    //        {
    //            OnMoveTo(toSelect);
    //        }
    //    }

    //    private string FindResource(string resourceKey)
    //    {
    //        return Application.Current.FindResource(resourceKey).ToString();
    //    }

    //    public void OnMoveTo(DieViewModel selectedItem)
    //    {
    //        if (IsProcessing) 
    //        {
    //            logger.Warn("Testing in Progress - Do Not Operate");
    //            return; 
    //        }
    //        var result = MessageDialog.Show(
    //               $"{FindResource("Btn.MoveToMsg")} {selectedItem.MapAxisToString()}",
    //               $"{FindResource("Prompt")}", // 使用默认标题
    //               MessageBoxButton.YesNo,
    //               MessageBoxImage.Question);
    //        //var result = MessageBox.Show($"{FindResource("Btn.MoveToMsg")} {selectedItem.MapAxisToString()}", $"{FindResource("Prompt")}", MessageBoxButton.YesNo);
    //        if (result == MessageBoxResult.Yes)
    //        {
    //            ProberClientService.Instance?.MoveToAsync(selectedItem);
    //        }
    //    }


    //    #region 进度条相关属性（增强版）

    //    // 单个Die测试进度（0-100）
    //    private double _singleDieTestProgress;
    //    public double SingleDieTestProgress
    //    {
    //        get => _singleDieTestProgress;
    //        set
    //        {
    //            if (SetProperty(ref _singleDieTestProgress, Math.Max(0, Math.Min(100, value))))
    //            {
    //                // 更新总进度
    //                UpdateTotalProgress();
    //                // 更新进度文本
    //                OnPropertyChanged(nameof(ProgressText));
    //            }
    //        }
    //    }

    //    // 总进度（0-100）
    //    private double _totalTestProgress;
    //    public double TotalTestProgress
    //    {
    //        get => _totalTestProgress;
    //        set
    //        {
    //            if (SetProperty(ref _totalTestProgress, Math.Max(0, Math.Min(100, value))))
    //            {
    //                OnPropertyChanged(nameof(ProgressText));
    //            }
    //        }
    //    }

    //    // 总测试数量
    //    private int _totalTestCount;
    //    public int TotalTestCount
    //    {
    //        get => _totalTestCount;
    //        set => SetProperty(ref _totalTestCount, value);
    //    }

    //    // 当前已完成测试数量
    //    private int _completedTestCount;
    //    public int CompletedTestCount
    //    {
    //        get => _completedTestCount;
    //        set
    //        {
    //            if (SetProperty(ref _completedTestCount, value))
    //            {
    //                UpdateTotalProgress();
    //                OnPropertyChanged(nameof(ProgressText));
    //            }
    //        }
    //    }

    //    // 当前正在测试的Die索引（从1开始）
    //    private int _currentDieIndex;
    //    public int CurrentDieIndex
    //    {
    //        get => _currentDieIndex;
    //        set => SetProperty(ref _currentDieIndex, value);
    //    }

    //    // 当前测试的Die信息
    //    private string _currentDieInfo = string.Empty;
    //    public string CurrentDieInfo
    //    {
    //        get => _currentDieInfo;
    //        set => SetProperty(ref _currentDieInfo, value);
    //    }

    //    // 进度文本显示
    //    public string ProgressText
    //    {
    //        get
    //        {
    //            return $"测试进度: {CompletedTestCount}/{TotalTestCount} 完成 (总进度: {TotalTestProgress:F1}%) | 当前Die: {CurrentDieInfo} (进度: {SingleDieTestProgress:F1}%)";
    //        }
    //    }

    //    #endregion
    //    #region 进度条更新方法（增强版）

    //    /// <summary>
    //    /// 开始自动测试时初始化进度
    //    /// </summary>
    //    /// <param name="selectedDice">选中的Die列表</param>
    //    public void InitializeAutoTestProgress(List<DieViewModel> selectedDice)
    //    {
    //        if (selectedDice == null || selectedDice.Count == 0)
    //        {
    //            ResetProgressBars();
    //            return;
    //        }

    //        Application.Current?.Dispatcher?.Invoke(() =>
    //        {
    //            TotalTestCount = selectedDice.Count;
    //            CompletedTestCount = 0;
    //            CurrentDieIndex = 0;

    //            SingleDieTestProgress = 0;
    //            TotalTestProgress = 0;

    //            if (logger.IsInfoEnabled)
    //                logger.InfoFormat($"进度条初始化: 总共 {TotalTestCount} 个Die需要测试");
    //        });
    //    }

    //    /// <summary>
    //    /// 开始测试单个Die
    //    /// </summary>
    //    public void StartSingleDieTest(DieViewModel die)
    //    {
    //        Application.Current?.Dispatcher?.Invoke(() =>
    //        {
    //            CurrentDieIndex = CompletedTestCount + 1;
    //            //CurrentDieInfo = $"{die.MapAxisToString()}";
    //            CurrentDieInfo = $"{die.MapX}/{die.MapY}"; 
    //            SingleDieTestProgress = 0; // 重置单个Die进度

    //            if (logger.IsDebugEnabled)
    //                logger.DebugFormat($"开始测试第 {CurrentDieIndex}/{TotalTestCount} 个Die: {die.MapAxisToString()}");
    //        });
    //    }

    //    /// <summary>
    //    /// 更新单个Die测试进度
    //    /// </summary>
    //    /// <param name="progress">进度百分比（0-100）</param>
    //    /// <param name="stage">当前测试阶段（用于日志）</param>
    //    public void UpdateSingleDieProgress(double progress, string stage = "")
    //    {
    //        Application.Current?.Dispatcher?.Invoke(() =>
    //        {
    //            progress = Math.Max(0, Math.Min(100, progress));
    //            SingleDieTestProgress = progress;

    //            if (!string.IsNullOrEmpty(stage) && logger.IsDebugEnabled)
    //                logger.DebugFormat($"单个Die进度更新: {stage} - {progress:F1}%");
    //        });
    //    }

    //    /// <summary>
    //    /// 更新单个Die进度（根据阶段权重）
    //    /// </summary>
    //    /// <param name="stage">当前阶段</param>
    //    /// <param name="stageProgress">阶段内进度（0-1）</param>
    //    //public void UpdateSingleDieProgressByStage(TestStage stage, double stageProgress)
    //    //{
    //    //    double baseProgress = GetBaseProgressForStage(stage);
    //    //    double stageWeight = GetWeightForStage(stage);
    //    //    double progress = baseProgress + (stageProgress * stageWeight);

    //    //    UpdateSingleDieProgress(progress, stage.ToString());
    //    //}

    //    /// <summary>
    //    /// 完成单个Die测试
    //    /// </summary>
    //    public void CompleteSingleDieTest()
    //    {
    //        Application.Current?.Dispatcher?.Invoke(() =>
    //        {
    //            CompletedTestCount++;
    //            UpdateTotalProgress();
    //            OnPropertyChanged(nameof(ProgressText));
    //        }); 
    //        //Application.Current?.Dispatcher?.Invoke(() =>
    //        //{
    //        //    SingleDieTestProgress = 100;
    //        //    CompletedTestCount++;
    //        //    UpdateTotalProgress();

    //        //    if (logger.IsDebugEnabled)
    //        //        logger.DebugFormat($"完成Die测试: 累计完成 {CompletedTestCount}/{TotalTestCount}");
    //        //});
    //    }

    //    /// <summary>
    //    /// 更新总进度
    //    /// </summary>
    //    private void UpdateTotalProgress()
    //    {
    //        if (TotalTestCount > 0)
    //        {
    //            // 已完成Die的基础进度
    //            double baseProgress = (double)CompletedTestCount / TotalTestCount * 100;

    //            // 当前Die的进度贡献（如果有）
    //            double currentDieContribution = 0;
    //            if (CompletedTestCount < TotalTestCount && SingleDieTestProgress > 0)
    //            {
    //                currentDieContribution = (SingleDieTestProgress / 100) * (1.0 / TotalTestCount) * 100;
    //            }

    //            TotalTestProgress = baseProgress + currentDieContribution;

    //            // 确保不超过100%
    //            if (TotalTestProgress > 100)
    //                TotalTestProgress = 100;
    //        }
    //        else
    //        {
    //            TotalTestProgress = 0;
    //        }
    //    }

    //    /// <summary>
    //    /// 重置进度条
    //    /// </summary>
    //    public void ResetProgressBars()
    //    {
    //        Application.Current?.Dispatcher?.Invoke(() =>
    //        {
    //            SingleDieTestProgress = 0;
    //            TotalTestProgress = 0;
    //            TotalTestCount = 0;
    //            CompletedTestCount = 0;
    //            CurrentDieIndex = 0;
    //            CurrentDieInfo = string.Empty;
    //        });
    //    }

    //    #endregion

    //    //#region 测试阶段定义

    //    ///// <summary>
    //    ///// 测试阶段枚举
    //    ///// </summary>
    //    //public enum TestStage
    //    //{
    //    //    Initialization,     // 初始化阶段 0-10%
    //    //    MovingToPosition,   // 移动位置 10-30%
    //    //    FlowExecution,      // 流程执行 30-80%
    //    //    ResultProcessing,   // 结果处理 80-95%
    //    //    Finalization        // 完成阶段 95-100%
    //    //}

    //    ///// <summary>
    //    ///// 获取阶段的基准进度
    //    ///// </summary>
    //    //private double GetBaseProgressForStage(TestStage stage)
    //    //{
    //    //    return stage switch
    //    //    {
    //    //        TestStage.Initialization => 0,
    //    //        TestStage.MovingToPosition => 10,
    //    //        TestStage.FlowExecution => 30,
    //    //        TestStage.ResultProcessing => 80,
    //    //        TestStage.Finalization => 95,
    //    //        _ => 0
    //    //    };
    //    //}

    //    ///// <summary>
    //    ///// 获取阶段的权重
    //    ///// </summary>
    //    //private double GetWeightForStage(TestStage stage)
    //    //{
    //    //    return stage switch
    //    //    {
    //    //        TestStage.Initialization => 10,
    //    //        TestStage.MovingToPosition => 20,
    //    //        TestStage.FlowExecution => 50,
    //    //        TestStage.ResultProcessing => 15,
    //    //        TestStage.Finalization => 5,
    //    //        _ => 0
    //    //    };
    //    //}

    //    //#endregion

    //}
}
