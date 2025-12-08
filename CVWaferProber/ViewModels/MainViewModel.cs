using ChipMapping.Models;
using ChipMapping.ViewModels;
using ColorVision.Core.Entities;
using CVDB.Services.Buz;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.MQTT;
using CVWaferProber.Services;
using CVWaferProber.Utils;
using CVWaferProber.Views;
using CVWaferProber.WinMsg;
using CVWPFCamImageCtrl;
using CVWPFSpectrometerCtrl.ViewModels;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using static FreeSql.Internal.GlobalFilter;


namespace CVWaferProber.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MainViewModel));

        public static MainViewModel? Instance { get; private set; }
        public ChipMappingControlViewModel CustomMappingVM { get; set; }
        public CVCamImagerViewModel? CustomImageVM { get; set; }
        //public CVCameraImageViewModel? CustomImageVM { get; set; }
        public CVSpectrumViewModel? CustomIVLVM { get; set; }

        private GSWMProcessor _wmProcessor;

        private FlowViewModel? _selectedFlow;
        public FlowViewModel? SelectedFlow
        {
            get => _selectedFlow;
            set
            {
                if (_selectedFlow != value)
                {
                    SetProperty(ref _selectedFlow, value);
                }
            }
        }
        private WPFlowViewModel? _selectedWPFlow;
        public WPFlowViewModel? SelectedWPFlow
        {
            get => _selectedWPFlow;
            set
            {
                if (_selectedWPFlow != value)
                {
                    SetProperty(ref _selectedWPFlow, value);
                }
            }
        }

        public ICommand LoadMappingFileCommand { get; }
        public ICommand ClearMappingCommand { get; }
        public ICommand FlowLoadCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ResetLayoutCommand { get; }
        public ICommand StartAutoTestCommand { get; }
        public ICommand StopAutoTestCommand { get; }
        public ICommand StartManTestCommand { get; }
        public ICommand SaveTestResultCommand { get; }
        public ICommand LoadTestResultCommand { get; }
        public ICommand IVLTestCommand { get; }
        public ICommand OpenMappingFileCommand { get; }
        public ICommand RefreshStatusCommand { get; }
        public ICommand ResetStatusCommand { get; }
        public ICommand RCRegCommand { get; }
        public ICommand OpenVEyeWindowCommand { get; }

        // ========== 1. AOI列的全选/反选命令 ==========
        //public ICommand SelectAllAOICommand { get; }
        //public ICommand SelectAllIVLCommand { get; }
        //public ICommand SelectAllEQECommand { get; }
        //public ICommand SelectAllVAMCommand { get; }
        public ICommand InvertSelectAOICommand { get; }
        public ICommand InvertSelectIVLCommand { get; }
        public ICommand InvertSelectEQECommand { get; }
        public ICommand InvertSelectVAMCommand { get; }
        public ObservableCollection<DieViewModel> TestResults { get; } = new ObservableCollection<DieViewModel>();
        public RangeEnabledObservableCollection<FlowViewModel> FlowItems { get; } = new RangeEnabledObservableCollection<FlowViewModel>();
        public ObservableCollection<WPFlowViewModel> WPFlows { get; } = new ObservableCollection<WPFlowViewModel>();
        public string MappingCsvFilePath { get; set; }
        public string ProberId { get; set; }
        public bool IsColorEnabled { get; set; }

        private bool _isProcessing = false;

        private string _Timestamp;
        public string Timestamp { get => _Timestamp;
            set
            {
                SetProperty(ref _Timestamp, value);
            }
        }

        public bool IsNotProcessing => !_isProcessing;
        public bool IsProcessing {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
            }
        }

        private bool _isIVLCameraEnabled;
        public bool IsIVLCameraEnabled
        {
            get => _isIVLCameraEnabled;
            set
            {
                SetProperty(ref _isIVLCameraEnabled, value);
            }
        }

        private bool _isAutoSN;
        public bool IsAutoSN
        {
            get => _isAutoSN;
            set
            {
                SetProperty(ref _isAutoSN, value);
            }
        }
        // 1. 面板显示状态属性（右上角相机面板默认隐藏）
        private bool _isMappingPanelVisible = true;
        public bool IsMappingPanelVisible
        {
            get => _isMappingPanelVisible;
            set { _isMappingPanelVisible = value; OnPropertyChanged(); }
        }

        private bool _isCameraPanelVisible = false; // 初始隐藏
        public bool IsCameraPanelVisible
        {
            get => _isCameraPanelVisible;
            set { _isCameraPanelVisible = value; OnPropertyChanged(); }
        }

        private bool _isSPPanelVisible = true;
        public bool IsSPPanelVisible
        {
            get => _isSPPanelVisible;
            set { _isSPPanelVisible = value; OnPropertyChanged(); }
        }

        private bool _isLogPanelVisible = true;
        public bool IsLogPanelVisible
        {
            get => _isLogPanelVisible;
            set { _isLogPanelVisible = value; OnPropertyChanged(); }
        }

        private readonly Random _random = new Random();

        private DataGrid? _dataGrid; // 引用DataGrid
        private DispatcherTimer? _simAutoTestTimer;
        private RCRestService rcModel;
        private IVLService ivlService;
        private AOIService aoiService;
        //private AlgResultModel algResultModel;
        /// <summary>
        /// false 外部控件关联触发
        /// </summary>
        private bool selfClick = true;

        private object? _selectedItem;
        public object? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                if (IsNotProcessing)
                {
                    OnPropertyChanged(nameof(SelectedItem));
                    ScrollToSelectedItem();
                    // 选中项变化时的逻辑
                    if (value != null && value is DieViewModel die)
                    {
                        if (selfClick)
                        {
                            CustomMappingVM.SetSelectedChip((uint)die.Id);
                            DieResultDisplay(die);
                        }
                        else selfClick = true;
                    }
                }
            }
        }

        private CVMQTTWPClient mqtt;
        //private int _overTimefRestapi = 30; //S
        public MainViewModel()
        {
            Instance = this;
            _selectedItem = null;
            _selectedFlow = null;
            _dataGrid = null;
            _isIVLCameraEnabled = false;
            _isAutoSN = true;
            rcModel = new RCRestService();
            //algResultModel = new AlgResultModel();
            CustomMappingVM = new ChipMappingControlViewModel();
            CustomImageVM = new CVCamImagerViewModel();
            CustomIVLVM = new CVSpectrumViewModel();
            //
            OpenVEyeWindowCommand = new RelayCommand(OpenVEyeWindow);
            RefreshStatusCommand = new RelayCommand(RefreshStatus);
            OpenMappingFileCommand = new RelayCommand(OpenMappingFile);
            StartAutoTestCommand = new RelayCommand(StartAutoTest);
            StopAutoTestCommand = new RelayCommand(StopAutoTest);
            StartManTestCommand = new RelayCommand(StartManTest);
            SaveTestResultCommand = new RelayCommand(SaveTestResult);
            LoadTestResultCommand = new RelayCommand(LoadTestResult);
            ResetStatusCommand = new RelayCommand(ResetStatus);
            LoadMappingFileCommand = new RelayCommand(_ => LoadMappingFileFromCsv());
            ClearMappingCommand = new RelayCommand(_ => ClearMapping());
            FlowLoadCommand = new RelayCommand(_ => LoadBuzWPFlows());
            RCRegCommand = new RelayCommand(_ => RCReg());

            // 初始化数据源（实际项目中是从文件/接口加载）
            TestResults = new ObservableCollection<DieViewModel>();

            TestResults.CollectionChanged += AOIItems_CollectionChanged;
            TestResults.CollectionChanged += IVLItems_CollectionChanged;
            TestResults.CollectionChanged += EQEItems_CollectionChanged;
            TestResults.CollectionChanged += VAMItems_CollectionChanged;
            
            // 绑定命令到方法
          
            InvertSelectAOICommand = new RelayCommand(ExecuteInvertSelectAOI);
            InvertSelectIVLCommand = new RelayCommand(ExecuteInvertSelectIVL);
            InvertSelectEQECommand = new RelayCommand(ExecuteInvertSelectEQE);
            InvertSelectVAMCommand = new RelayCommand(ExecuteInvertSelectVAM);
           
            // 绑定退出命令：执行 Application.Shutdown() 关闭整个程序
            ExitCommand = new CVImgRelayCommand(() =>
            {
                //添加退出确认提示（用户点击“是”才退出）
                var result = MessageBox.Show(
                    "是否确定退出程序？",
                    "退出提示",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Application.Current.Shutdown(); // 关闭应用程序
                }
            });
            // 绑定重置布局命令（使用你的CVImgRelayCommand）
            ResetLayoutCommand = new CVImgRelayCommand(() =>
            {
                // 恢复所有面板为默认显示（true）
                IsMappingPanelVisible = true;
                IsCameraPanelVisible = true;
                IsSPPanelVisible = true;
              //  IsLogPanelVisible = true;
            });
            InitMysqlCfg();
            //
            ProberId = "CVProber01";
            MappingCsvFilePath = "E:\\work\\cv\\New版\\晶圆台\\CVWaferProber\\ChipMapping\\ScanData_sc.csv";
            if (!System.IO.File.Exists(MappingCsvFilePath)) MappingCsvFilePath = "ScanData_sc.csv";
            Snowflake.Instance.SnowflakesInit(1, 1);
            InitializeSimAutoTestTimer();

            LoadMappingFileFromCsv();

            LoadBuzWPFlows();

            InitMQTT();

            ivlService = new IVLService(CustomIVLVM, rcModel);
            ivlService.ProberId = ProberId;
            ivlService.TestingCompleted += OnTestingCompleted;

            aoiService = new AOIService(CustomImageVM, rcModel);
            aoiService.TestingCompleted += OnTestingCompleted;

            SubscribeItems_AOI(TestResults);
            SubscribeItems_IVL(TestResults);
            SubscribeItems_EQE(TestResults);
            SubscribeItems_VAM(TestResults);


        }
        // ========== AOI列逻辑 ==========
        #region AOI 全选/部分选中
        private bool? _selectAllAOI = false;
        public bool? SelectAllAOI
        {
            get => _selectAllAOI;
            set
            {
                if (_selectAllAOI != value)
                {
                    _selectAllAOI = value;
                    OnPropertyChanged(nameof(SelectAllAOI));

                    if (value.HasValue)
                    {
                        // 避免循环更新
                        _isUpdatingFromHeader_AOI = true;
                        try
                        {
                            // 更新所有项目的选中状态
                            foreach (var item in TestResults)
                            {
                                item.IsAOIEnabled = value.Value;
                            }
                        }
                        finally
                        {
                            _isUpdatingFromHeader_AOI = false;
                        }
                    }
                }
            }
        }
        private bool _isUpdatingFromHeader_AOI;
        private void AOIItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                SubscribeItems_AOI(e.NewItems.Cast<DieViewModel>());
            }

            if (e.OldItems != null)
            {
                UnsubscribeItems_AOI(e.OldItems.Cast<DieViewModel>());
            }

            UpdateSelectAllAOIState();
        }

        private void SubscribeItems_AOI(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
            {
                item.PropertyChanged += AOI_Item_PropertyChanged;
            }
        }

        private void UnsubscribeItems_AOI(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
            {
                item.PropertyChanged -= AOI_Item_PropertyChanged;
            }
        }

        private void AOI_Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsAOIEnabled) && !_isUpdatingFromHeader_AOI)
            {
                UpdateSelectAllAOIState();
            }
        }

        private void UpdateSelectAllAOIState()
        {
            if (TestResults == null || TestResults.Count == 0)
            {
                SelectAllAOI = false;
                return;
            }

            int selectedCount = TestResults.Count(item => item.IsAOIEnabled);
            int totalCount = TestResults.Count;

            if (selectedCount == 0)
            {
                SelectAllAOI = false;
            }
            else if (selectedCount == totalCount)
            {
                SelectAllAOI = true;
            }
            else
            {
                SelectAllAOI = null; // 部分选中状态
            }
        }
        #endregion AOI 全选/部分选中
        #region AOI 反选
        private void ExecuteInvertSelectAOI(object obj)
        {
            // 遍历所有行，将IsAOIEnabled设为True（全选）
            foreach (var item in TestResults)
            {
                item.IsAOIEnabled = !item.IsAOIEnabled;
            }
        }
        #endregion AOI 反选
        // ========== IVL列逻辑 ==========
        #region IVL 全选/部分选中
        private bool? _selectAllIVL = false;
        public bool? SelectAllIVL
        {
            get => _selectAllIVL;
            set
            {
                if (_selectAllIVL != value)
                {
                    _selectAllIVL = value;
                    OnPropertyChanged(nameof(SelectAllIVL));

                    if (value.HasValue)
                    {
                        // 避免循环更新
                        _isUpdatingFromHeader_IVL = true;
                        try
                        {
                            // 更新所有项目的选中状态
                            foreach (var item in TestResults)
                            {
                                item.IsIVLEnabled = value.Value;
                            }
                        }
                        finally
                        {
                            _isUpdatingFromHeader_IVL = false;
                        }
                    }
                }
            }
        }
        private bool _isUpdatingFromHeader_IVL;
        private void IVLItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                SubscribeItems_IVL(e.NewItems.Cast<DieViewModel>());
            }

            if (e.OldItems != null)
            {
                UnsubscribeItems_IVL(e.OldItems.Cast<DieViewModel>());
            }

            UpdateSelectAllIVLState();
        }

        private void SubscribeItems_IVL(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
            {
                item.PropertyChanged += IVL_Item_PropertyChanged;
            }
        }

        private void UnsubscribeItems_IVL(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
            {
                item.PropertyChanged -= IVL_Item_PropertyChanged;
            }
        }

        private void IVL_Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsIVLEnabled) && !_isUpdatingFromHeader_IVL)
            {
                UpdateSelectAllIVLState();
            }
        }

        private void UpdateSelectAllIVLState()
        {
            if (TestResults == null || TestResults.Count == 0)
            {
                SelectAllIVL = false;
                return;
            }

            int selectedCount = TestResults.Count(item => item.IsIVLEnabled);
            int totalCount = TestResults.Count;

            if (selectedCount == 0)
            {
                SelectAllIVL = false;
            }
            else if (selectedCount == totalCount)
            {
                SelectAllIVL = true;
            }
            else
            {
                SelectAllIVL = null; // 部分选中状态
            }
        }
        #endregion AOI 全选/部分选中
        #region IVL 反选
        private void ExecuteInvertSelectIVL(object obj)
        {
            // 遍历所有行，将IsAOIEnabled设为True（全选）
            foreach (var item in TestResults)
            {
                item.IsIVLEnabled = !item.IsIVLEnabled;
            }
        }
        #endregion IVL 反选
        // ========== EQE列逻辑 ==========
        #region EQE 全选/部分选中
        private bool? _selectAllEQE = false;
        public bool? SelectAllEQE
        {
            get => _selectAllEQE;
            set
            {
                if (_selectAllEQE != value)
                {
                    _selectAllEQE = value;
                    OnPropertyChanged(nameof(SelectAllEQE));

                    if (value.HasValue)
                    {
                        // 避免循环更新
                        _isUpdatingFromHeader_EQE = true;
                        try
                        {
                            // 更新所有项目的选中状态
                            foreach (var item in TestResults)
                            {
                                item.IsEQEEnabled = value.Value;
                            }
                        }
                        finally
                        {
                            _isUpdatingFromHeader_EQE = false;
                        }
                    }
                }
            }
        }
        private bool _isUpdatingFromHeader_EQE;
        private void EQEItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                SubscribeItems_EQE(e.NewItems.Cast<DieViewModel>());
            }

            if (e.OldItems != null)
            {
                UnsubscribeItems_EQE(e.OldItems.Cast<DieViewModel>());
            }

            UpdateSelectAllEQEState();
        }

        private void SubscribeItems_EQE(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
            {
                item.PropertyChanged += EQE_Item_PropertyChanged;
            }
        }

        private void UnsubscribeItems_EQE(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
            {
                item.PropertyChanged -= EQE_Item_PropertyChanged;
            }
        }

        private void EQE_Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsAOIEnabled) && !_isUpdatingFromHeader_EQE)
            {
                UpdateSelectAllEQEState();
            }
        }

        private void UpdateSelectAllEQEState()
        {
            if (TestResults == null || TestResults.Count == 0)
            {
                SelectAllEQE = false;
                return;
            }

            int selectedCount = TestResults.Count(item => item.IsEQEEnabled);
            int totalCount = TestResults.Count;

            if (selectedCount == 0)
            {
                SelectAllEQE = false;
            }
            else if (selectedCount == totalCount)
            {
                SelectAllEQE = true;
            }
            else
            {
                SelectAllEQE = null; // 部分选中状态
            }
        }
        #endregion EQE 全选/部分选中
        #region EQE 反选
        private void ExecuteInvertSelectEQE(object obj)
        {
            // 遍历所有行，将IsAOIEnabled设为True（全选）
            foreach (var item in TestResults)
            {
                item.IsEQEEnabled = !item.IsEQEEnabled;
            }
        }
        #endregion EQE 反选
        // ========== VAM列逻辑 ==========
        #region AOI 全选/部分选中
        private bool? _selectAllVAM = false;
        public bool? SelectAllVAM
        {
            get => _selectAllVAM;
            set
            {
                if (_selectAllVAM != value)
                {
                    _selectAllVAM = value;
                    OnPropertyChanged(nameof(SelectAllVAM));

                    if (value.HasValue)
                    {
                        // 避免循环更新
                        _isUpdatingFromHeader_VAM = true;
                        try
                        {
                            // 更新所有项目的选中状态
                            foreach (var item in TestResults)
                            {
                                item.IsVAMEnabled = value.Value;
                            }
                        }
                        finally
                        {
                            _isUpdatingFromHeader_VAM = false;
                        }
                    }
                }
            }
        }
        private bool _isUpdatingFromHeader_VAM;
        private void VAMItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                SubscribeItems_VAM(e.NewItems.Cast<DieViewModel>());
            }

            if (e.OldItems != null)
            {
                UnsubscribeItems_VAM(e.OldItems.Cast<DieViewModel>());
            }

            UpdateSelectAllVAMState();
        }

        private void SubscribeItems_VAM(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
            {
                item.PropertyChanged += VAM_Item_PropertyChanged;
            }
        }

        private void UnsubscribeItems_VAM(IEnumerable<DieViewModel> items)
        {
            foreach (var item in items)
            {
                item.PropertyChanged -= VAM_Item_PropertyChanged;
            }
        }

        private void VAM_Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsVAMEnabled) && !_isUpdatingFromHeader_VAM)
            {
                UpdateSelectAllVAMState();
            }
        }

        private void UpdateSelectAllVAMState()
        {
            if (TestResults == null || TestResults.Count == 0)
            {
                SelectAllVAM = false;
                return;
            }

            int selectedCount = TestResults.Count(item => item.IsVAMEnabled);
            int totalCount = TestResults.Count;

            if (selectedCount == 0)
            {
                SelectAllVAM = false;
            }
            else if (selectedCount == totalCount)
            {
                SelectAllVAM = true;
            }
            else
            {
                SelectAllVAM = null; // 部分选中状态
            }
        }
        #endregion VAM 全选/部分选中
        #region VAM 反选
        private void ExecuteInvertSelectVAM(object obj)
        {
            // 遍历所有行，将IsAOIEnabled设为True（全选）
            foreach (var item in TestResults)
            {
                item.IsVAMEnabled = !item.IsVAMEnabled;
            }
        }
        #endregion VAM 反选
        
        private void InitMysqlCfg()
        {
            var rcExeFile = ServiceHelper.GetServiceExecutablePath("RegistrationCenterService");
            if (!string.IsNullOrEmpty(rcExeFile) && File.Exists(rcExeFile))
            {
                string mysqlCfg = Path.Combine(Path.GetDirectoryName(rcExeFile), "cfg", "MySql.config");
                var assemFile = Path.GetDirectoryName(GetExecutablePath());
                string assemMysqlCfg = Path.Combine(assemFile, "cfg", "MySql.config");
                File.Copy(mysqlCfg, assemMysqlCfg, true);
                if (logger.IsDebugEnabled) logger.DebugFormat("Copy RC Service MySql.config => {0}->{1}", mysqlCfg, assemMysqlCfg);
            }
            else
            {
                if (logger.IsDebugEnabled) logger.DebugFormat("RC Service is empty or not exist => {0}", rcExeFile);
            }
        }
        public static string GetExecutablePath()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().Location;
        }
        private void OnTestingCompleted(object? sender, EventArgs e)
        {
            EndTesting();
        }

        private void OpenVEyeWindow(object? obj)
        {
            ExternalWindow newWindow = new ExternalWindow();
            newWindow.Show();
        }

        public void WinLoadInit(Window win)
        {
            _wmProcessor = new GSWMProcessor(win);
            _wmProcessor.OnStopTest += _wmProcessor_OnStopTest;
            _wmProcessor.OnSOT += _wmProcessor_OnSOT;
        }

        private void _wmProcessor_OnSOT(object sender, int row, int col)
        {
            StartTestingDie(row,col);
        }

        private void _wmProcessor_OnStopTest(object sender)
        {
             StopAutoTest(this);
        }
        private void InitMQTT()
        {
            mqtt = CVMQTTWPClient.Instance.Init("RC_local");
        }

        private void ResetStatus(object? obj)
        {
            foreach (var die in TestResults)
            {
                die.ResetStatus();
            }
        }

        private void RCReg()
        {
            bool bR = rcModel.RcRegist();
            if (bR)
            {
                LoadFlow();
                //LoadBuzWPFlows();
            }
        }
        private void LoadBuzWPFlows()
        {
            rcModel.RcRegist();
            List<TScgdBuzProductDetail> flows = WaferProberDBService.LoadFlows();
            WPFlows.Clear();
            if (flows != null && flows.Count > 0)
            {
                foreach (var flow in flows)
                {
                    WPFlows.Add(new WPFlowViewModel(flow));
                }
                if (WPFlows.Count > 0) SelectedWPFlow = WPFlows[0];
            }
        }
        private void LoadFlow()
        {
            var flows = rcModel.RcLoadFlows();
            FlowItems.Clear();
            SelectedFlow = null;
            if (flows != null)
            {
                foreach (var flow in flows)
                {
                    FlowItems.Add(new FlowViewModel(flow));
                }
                if (FlowItems.Count > 0) SelectedFlow = FlowItems[FlowItems.Count - 1];
            }
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
                try
                {
                    ResultService.SaveToCSV(saveFileDialog.FileName, TestResults);
                    if (logger.IsInfoEnabled) logger.InfoFormat("Save result ok => {0}", saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    if (logger.IsErrorEnabled) logger.Error(ex);
                }
            }
        }
        private void LoadTestResult(object? obj)
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
                try
                {
                    ResultService.LoadFromCSV(openFileDialog.FileName, TestResults);
                    _dataGrid?.Items.Refresh();
                    if (logger.IsInfoEnabled) logger.InfoFormat("Load result ok => {0}", openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    if (logger.IsErrorEnabled) logger.Error(ex);
                }
            }
        }
        private void RefreshStatus(object? obj)
        {
            _dataGrid?.Items.Refresh();
        }

        private void InitializeSimAutoTestTimer()
        {
            _simAutoTestTimer = new DispatcherTimer();
            _simAutoTestTimer.Interval = TimeSpan.FromMilliseconds(1000); // 500ms闪烁一次
            //_simAutoTestTimer.Tick += SimAutoTestTimer_Tick;
            _simAutoTestTimer.Tick += RestGetFlowResultTimer_Tick;
        }

        private void RestGetFlowResultTimer_Tick(object? sender, EventArgs e)
        {
            if (IsProcessing && CurTestDieIdx >= 0)
            {
                DieViewModel dieViewModel = TestResults[CurTestDieIdx];
                var resp = rcModel.RcGetFlowResult_POI(dieViewModel.SerialNumber);
                if (resp != null)
                {
                    if (resp.IsSuccess)
                    {
                        DieResultDisplay(dieViewModel);
                        dieViewModel.ChangeStatus(ChipStatus.OK, true);
                    }
                    else if (resp.ResultStatus == "Pending")
                    {
                        return;
                    }
                    else
                    {
                        dieViewModel.ChangeStatus(ChipStatus.AOI_NG, true);
                    }
                    NextTestingDie();
                }
            }
        }
        //private void IVLResultDisplay(DieViewModel dieViewModel)
        //{
        //    CustomIVLVM?.ClearResult();
        //    CustomIVLVM?.LoadData(dieViewModel.SerialNumber, dieViewModel.IsIVLCameraEnabled);
        //}
        private void DieResultDisplay(DieViewModel dieViewModel)
        {
            if (string.IsNullOrEmpty(dieViewModel.SerialNumber))
            {
                CustomIVLVM.ClearResult();
                CustomImageVM?.ClearImageResult();
            }
            if (dieViewModel.Status == ChipStatus.IVL_TESTING || dieViewModel.Status == ChipStatus.IVL_COMPLETED)
            {
                ivlService.IVLResultDisplay(dieViewModel);
            }
            else
            {
                CustomImageVM?.ClearImageResult();
                CustomImageVM?.LoadImageResult(dieViewModel.chipViewModel.ChipData, dieViewModel.SerialNumber);
            }

            //Task.Factory.StartNew(() => CustomImageVM?.LoadImageResult(dieViewModel.chipViewModel.ChipData, dieViewModel.SerialNumber));
        }
        private void NextTestingDie()
        {
            var sel = TestResults[CurTestDieIdx];
            sel.UnSelected();
            if (IsLocalSim)
            {
                CurTestDieIdx++;
                //
                var itemToSelect = TestResults[CurTestDieIdx];
                ScrollToItem(itemToSelect);

                aoiService.StartTestingAOI(Timestamp, itemToSelect, _selectedWPFlow, false);
                //StartTestingDie(itemToSelect);
            }
            else if (sel.Status.HasValue && sel.MapX.HasValue && sel.MapY.HasValue)
            {
                _wmProcessor.MeasurementProcessResult((ChipStatus)sel.Status, (int)sel.MapY, (int)sel.MapX);
            }
        }

        //private void StartTestingDie(DieViewModel dieViewModel)
        //{
        //    string sn = BuildFlowSN(dieViewModel);
        //    dieViewModel.SerialNumber = sn;
        //    dieViewModel.ChangeStatus(ChipStatus.TESTING);
        //    Task.Factory.StartNew(() => rcModel.RcRunFlowByName(_selectedWPFlow.Name, sn));
        //}
        private void StartTestingDie(int row, int col)
        {
            if (CurTestDieIdx>=0)
            {
                TestResults[CurTestDieIdx].UnSelected();
            }
            var itemToSelect = TestResults.FirstOrDefault(d => d.MapX == col && d.MapY == row);
            if (itemToSelect != null)
            {
                CurTestDieIdx = TestResults.IndexOf(itemToSelect);

                ScrollToItem(itemToSelect);
                aoiService.StartTestingAOI(Timestamp, itemToSelect, _selectedWPFlow, false);
                //Task task = DoAsyncStartTestingDie(itemToSelect, false);
                //StartTestingDie(itemToSelect);
            }
            else
            {
                logger.ErrorFormat("Die not found By Row={0},Col={1}", row, col);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private int CurTestDieIdx = -1;
        private void SimAutoTestTimer_Tick(object? sender, EventArgs e)
        {
            var status = (ChipStatus)_random.Next(2, 4);
            TestResults[CurTestDieIdx].ChangeStatus(status, true);

            DieViewModel dieViewModel = TestResults[CurTestDieIdx];
            Task.Factory.StartNew(() => DieResultDisplay(dieViewModel));

            NextTestingDie();
        }
        /*
        private void StartTestingIVL1(DieViewModel dieViewModel)
        {
            ivlService.StartTestingIVL(Timestamp, dieViewModel, _selectedWPFlow);
        }

        private void StartTestingIVL(DieViewModel dieViewModel)
        {
            string sn = BuildFlowSN(dieViewModel);
            dieViewModel.SerialNumber = sn;
            dieViewModel.ChangeStatus(ChipStatus.IVL_TESTING);
            CustomIVLVM?.ClearResult();
            IsIVLCameraEnabled = _selectedWPFlow.FlowType == CVWaferProberFlowType.IVL_Camera;
            dieViewModel.IsIVLCameraEnabled = IsIVLCameraEnabled;
            if (IsIVLCameraEnabled) CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.IVLCamera;
            else CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.Spectrum;
            //Task.Factory.StartNew(() => RunIVLFlowAsync(_selectedFlow.Id, sn));
            Task task = RunIVLFlowAsync(_selectedWPFlow.Name, dieViewModel);
        }
        private async Task RunIVLFlowAsync(string fname, DieViewModel dieViewModel)
        {
            try
            {
                Task<RespDataBaseFlowResultDTO> resp = AsyncRunIVLFlow(fname, dieViewModel.SerialNumber);
                await resp;
                if (resp.Result.IsSuccess)
                {
                    IVLResultDisplay(dieViewModel);
                    dieViewModel.ChangeStatus(ChipStatus.IVL_COMPLETED, true);
                }
                else
                {
                    dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                }
            }
            catch (OperationCanceledException)
            {
                // 处理取消操作
                if (logger.IsDebugEnabled) logger.Debug("IVL Flow execution was cancelled.");
            }
            catch (Exception ex)
            {
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                // 处理其他异常
                if (logger.IsDebugEnabled) logger.Debug($"IVL Flow execution failed: {ex.Message}");
                throw;
            }
            finally
            {
                EndTesting();
            }
        }
        private async Task<RespDataBaseFlowResultDTO> AsyncRunIVLFlow(string fname, string sn)
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // 启动流程
            rcModel.RcRunFlowByName(fname, sn);

            // 异步轮询结果，避免阻塞UI线程
           return await PollFlowResultWithRxAsync(sn, cancellationToken);
        }
        private async Task<RespDataBaseFlowResultDTO> PollFlowResultWithRxAsync(string sn, CancellationToken cancellationToken)
        {
           return await Observable.Interval(TimeSpan.FromSeconds(1))
                .Select(_ => rcModel.RcGetFlowResult_AOI(sn))
                .Where(resp => resp != null)
                .FirstAsync(resp => (resp.IsSuccess && resp.Data.IsFinished) || !resp.IsSuccess)
                .Select(resp =>
                {
                    if (!resp.IsSuccess) throw new InvalidOperationException($"Flow execution failed: {resp.Message}");
                    return resp.Data;
                })
                .ToTask(cancellationToken);       
        }
        private string BuildFlowSN(DieViewModel dieViewModel)
        {
            if (string.IsNullOrEmpty(ProberId)) return string.Format("{1}[{3},{4}]", ProberId, Timestamp, Snowflake.Instance.NextSeqId(), dieViewModel.MapY, dieViewModel.MapX);
            else return string.Format("{0}_{1}[{3},{4}]", ProberId, Timestamp, Snowflake.Instance.NextSeqId(), dieViewModel.MapY, dieViewModel.MapX);
        }
        //////////////////////*/

        private bool IsLocalSim = false;
        private void StartAutoFlow()
        {
            if (SelectedWPFlow != null)
            {
                CustomMappingVM.DisabledInput = IsProcessing = true;
                EnableBtn(false);

                TestingReady();

                if (IsLocalSim)
                {
                    aoiService.StartTestingAOI(Timestamp, TestResults[CurTestDieIdx], _selectedWPFlow, false);
                    //StartTestingDie(TestResults[CurTestDieIdx]);
                    //获取结果
                    _simAutoTestTimer?.Start();
                }
                else
                {
                    _wmProcessor.MeasurementReady();
                }
            }
        }
        private void StartManFlow()
        {
            if (SelectedWPFlow != null)
            {
                if (SelectedItem != null && SelectedItem is DieViewModel die)
                {
                    CustomMappingVM.DisabledInput = IsProcessing = true;
                    EnableBtn(false);

                    ManTestingReady(die);

                    if (SelectedWPFlow.FlowType == CVWaferProberFlowType.AOI)
                    {
                        //Task task = DoAsyncStartTestingDie(die);
                        aoiService.StartTestingAOI(Timestamp, die, _selectedWPFlow);
                    }
                    else
                    {
                        ivlService.StartTestingIVL(Timestamp, die, _selectedWPFlow);
                    }
                }
                else
                {
                    if (logger.IsErrorEnabled) logger.Error("Die not selected.");
                }
            }
            else
            {
                if (logger.IsErrorEnabled) logger.Error("Flow not selected.");
            }
        }
        private void EndTesting()
        {
            CustomMappingVM.DisabledInput = IsProcessing = false;
            EnableBtn(true);
        }
        /*
        private async Task DoAsyncStartTestingDie(DieViewModel die,bool isEnd = true)
        {
            try
            {
                Task<RespDataBaseFlowResultDTO> resp = AsyncStartTestingDie(die);
                await resp;
                if (resp.Result.IsSuccess)
                {
                    DieResultDisplay(die);
                    die.ChangeStatus(ChipStatus.OK, true);
                }
                else
                {
                    ChipStatus status = GetDieResultStatus(die.SerialNumber);
                    die.ChangeStatus(status, true);
                }
            }
            catch (OperationCanceledException)
            {
                die.ChangeStatus(ChipStatus.OVERTIME, true);
                // 处理取消操作
                if (logger.IsErrorEnabled) logger.Error("Man Flow execution was cancelled.");
            }
            catch (Exception ex)
            {
                die.ChangeStatus(ChipStatus.FAILED, true);
                // 处理其他异常
                if (logger.IsErrorEnabled) logger.Error($"Man Flow execution failed: {ex.Message}");
                throw;
            }finally
            {
                _wmProcessor.MeasurementProcessResult(die);
                if (isEnd) EndTesting();
            }
        }

        private ChipStatus GetDieResultStatus(string serialNumber)
        {
            ChipStatus status = ChipStatus.FAILED;
            var results = AlgResultService.LoadAlgResultByBatchCodeAndType(serialNumber, (int)CVResultType.Algorithm_OLED_AOI_ALL);
            if (results != null && results.Count > 0)
            {
                foreach (var result in results)
                {
                    if (result.ResultCode.HasValue && result.ResultCode.Value != 0)
                    {
                        var aoi = AlgResultService.GetCommDetailResult(result.Id);
                        if (results != null && results.Count == 1)
                        {
                            OLED_AOI_Result_E eResult = JsonConvert.DeserializeObject<OLED_AOI_Result_E>(aoi[0].Result);
                            status = ChipStatusTool.GetStatusFromErrCode(eResult.ResultCode);
                            if(logger.IsInfoEnabled) logger.InfoFormat("AOI Result => {0}", status.ToString());
                            break;
                        }
                    }
                }
            }
            return status;
        }

        private async Task<RespDataBaseFlowResultDTO> AsyncStartTestingDie(DieViewModel die)
        {
            StartTestingDie(die);

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(_overTimefRestapi));
            var cancellationToken = cancellationTokenSource.Token;
            //获取结果
            return await PollFlowResultWithRxAsync(die.SerialNumber, cancellationToken);
        }
        ///*/
        private void ManTestingReady(DieViewModel die)
        {
            die.chipViewModel.SetStatus(ChipStatus.WAITING);
            die.EndTestTime = null;
            die.SerialNumber = null;
            die.StartTestTime = null;
            die.TotalTime = null;

            if(_isAutoSN) Timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fff");
        }
        private void TestingReady()
        {
            CustomMappingVM.Cleanup();
            foreach (var item in CustomMappingVM.Chips)
            {
                item.SetStatus(ChipStatus.WAITING);
            }
            foreach (var item in TestResults)
            {
                item.EndTestTime = null;
                item.SerialNumber = null;
                item.StartTestTime = null;
                item.TotalTime = null;
            }

            _dataGrid?.Items.Refresh();
            CurTestDieIdx = 0;
            if (_isAutoSN) Timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fff");
        }
        private void StartSim()
        {
            TestingReady();
            //
            _simAutoTestTimer?.Start();
            TestResults[CurTestDieIdx].ChangeStatus(ChipStatus.TESTING);
        }
        private void StartAutoTest(object? obj)
        {
            StartAutoFlow();
        }
        private void StartManTest(object? obj)
        {
            StartManFlow();
        }
        private void EnableBtn(bool enabled)
        {
            OnPropertyChanged(nameof(IsNotProcessing));
        }
        private void StopAutoTest(object? obj)
        {
            StopSim();
            for (int i = Math.Max(CurTestDieIdx - 3, 0); i < Math.Min(CurTestDieIdx + 3, TestResults.Count); i++)
                TestResults[i].UnSelected();
            CustomMappingVM.DisabledInput = IsProcessing = false;
            EnableBtn(true);

            _wmProcessor.MeasurementStoped();
        }
        private void StopSim()
        {
            _simAutoTestTimer?.Stop();
        }
        private void OpenMappingFile(object? obj)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                MappingCsvFilePath = openFileDialog.FileName;
                LoadMappingFileFromCsv();
            }
        }

        private void ClearMapping()
        {
            CustomMappingVM.Cleanup();
            CustomMappingVM.Chips.Clear();
            TestResults.Clear();
        }

        private void LoadMappingFileFromCsv()
        {
            List<CVMappingData> mappingData = null;
            if (!System.IO.File.Exists(MappingCsvFilePath))
            {
                if (logger.IsWarnEnabled) logger.WarnFormat("File not exist => {0}", MappingCsvFilePath);
                return;
            }
            bool bR = CsvMappingDataTool.LoadMappingCsv(MappingCsvFilePath, ref mappingData);
            if (bR && mappingData != null && mappingData.Count > 0)
            {
                CustomMappingVM.RefreshFromMap(mappingData);
                ObservableCollection<DieViewModel> _TestResults = new ObservableCollection<DieViewModel>();
                foreach (var map in CustomMappingVM.Chips)
                {
                    DieViewModel dieViewModel = new DieViewModel(map);
                    _TestResults.Add(dieViewModel);
                }
                var sorted = _TestResults.OrderByDescending(x => x.Id).ToList();
                TestResults.Clear();
                foreach (var item in sorted)
                {
                    TestResults.Add(item);
                }
            }
        }
        // 设置DataGrid引用
        public void SetDataGrid(DataGrid dataGrid)
        {
            _dataGrid = dataGrid;
        }
        private void ScrollToSelectedItem()
        {
            ScrollToItem(SelectedItem);
        }
        private void ScrollToItem(object? toItem)
        {
            if (_dataGrid != null && toItem != null)
            {
                _dataGrid.ScrollIntoView(toItem);

                // 确保行完全可见（可选）
                _dataGrid.UpdateLayout();

                //// 如果需要聚焦到选中行
                //var row = _dataGrid.ItemContainerGenerator.ContainerFromItem(SelectedItem) as DataGridRow;
                //row?.Focus();
            }
        }
        // 根据ID选择行
        public void SelectItemById(uint id)
        {
            var itemToSelect = TestResults.FirstOrDefault(item => item.Id == id);
            if (itemToSelect != null)
            {
                selfClick = false;
                SelectedItem = itemToSelect;
            }
        }
        public void SetSelectedDataGridItem(object? obj)
        {
            if (obj != null) SelectItemById((uint)obj);
        }
    }
}
