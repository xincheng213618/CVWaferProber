using AvalonDock;
using AvalonDock.Layout;
using ChipMapping.Models;
using ChipMapping.ViewModels;
using ColorVision.Core.Entities;
using CVAVMControl;
using CVDB.Services.Buz;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Models;
using CVWaferProber.MQTT;
using CVWaferProber.Services;
using CVWaferProber.Utils;
using CVWaferProber.Views;
using CVWPFCamImageCtrl;
using CVWPFSpectrometerCtrl;
using CVWPFSpectrometerCtrl.ViewModels;
using Mysqlx.Crud;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WaferComm.Core;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TabControl = System.Windows.Controls.TabControl;


namespace CVWaferProber.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MainViewModel));

        public static MainViewModel? Instance { get; private set; }
        public ChipMappingControlViewModel? CustomMappingVM { get; set; }
        public CVCamImagerViewModel? CustomImageVM { get; set; }
        public CVSpectrumViewModel? CustomIVLVM { get; set; }
        //public CVEQEViewModel? CustomEQEVM { get; set; }
        // AvalonDock面板引用
        public DockingManager? DockingManager { get; set; }
        public LayoutAnchorable? AnchorableCamera { get; set; }
        public LayoutAnchorable? AnchorableSP { get; set; }
        public LayoutAnchorable? AnchorableVAM { get; set; }
        // SP面板ViewModel引用
        public CVSpectrumViewModel? SpPanelViewModel { get; set; }

        // 切换委托（对应红框3个选项）
        public Action? ActivateSpectralInnerTabAction { get; set; }
        public Action? ActivateIVLCameraInnerTabAction { get; set; }
        public Action? ActivateEQEOuterTabAction { get; set; }

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
        private ConnectionInfo _connectionInfo;
        public ConnectionInfo ConnectionInfo
        {
            get => _connectionInfo;
        }
        private ConnectionInfo _rcConnectionInfo;
        public ConnectionInfo RcConnectionInfo
        {
            get => _rcConnectionInfo;
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
                    ActivateCorrespondingPanel();
                }
            }
        }

        public ICommand LoadMappingFileCommand { get; }
        public ICommand ClearMappingCommand { get; }
        public ICommand FlowLoadCommand { get; }
        public ICommand ExitCommand { get; }
        // 打开帮助命令
        public ICommand OpenHelpCommand { get; }
        public ICommand OpenCommand { get; }
        //打开机台设备调试窗口
        public ICommand OpenProberDeviceDebugCommand { get; }

        // 打开关于命令
        public ICommand OpenAboutCommand { get; }

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
        public ICommand InvertSelectAOICommand { get; }
        public ICommand InvertSelectIVLCommand { get; }
        public ICommand InvertSelectEQECommand { get; }
        public ICommand InvertSelectVAMCommand { get; }
        public ICommand SysFlowCfgCommand { get; }
        //
        public ICommand ShowConnectionSettingsCommand { get; }
        public ICommand ShowRCConnectionSettingsCommand { get; }
        /// <summary>
        /// 
        /// </summary>
        public ObservableCollection<DieViewModel> TestResults { get; } = new ObservableCollection<DieViewModel>();
        public RangeEnabledObservableCollection<FlowViewModel> FlowItems { get; } = new RangeEnabledObservableCollection<FlowViewModel>();
        public ObservableCollection<WPFlowViewModel> WPFlows { get; } = new ObservableCollection<WPFlowViewModel>();
        public string MappingCsvFilePath { get; set; }
        public string ProberId { get; set; }
        public bool IsColorEnabled { get; set; }

        private bool _isProcessing = false;

        private string _Timestamp;
        public string Timestamp
        {
            get => _Timestamp;
            set
            {
                SetProperty(ref _Timestamp, value);
            }
        }

        public bool IsNotProcessing => !_isProcessing;
        public bool IsProcessing
        {
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
        #region 面板显示状态属性
        // 1. 面板显示状态属性（右上角相机面板默认隐藏）
        private bool _isMappingPanelVisible = true;
        /// <summary>
        /// Mapping面板显示/隐藏（双向绑定菜单和面板）
        /// </summary>
        public bool IsMappingPanelVisible
        {
            get => _isMappingPanelVisible;
            set
            {
                if (_isMappingPanelVisible != value)
                {
                    _isMappingPanelVisible = value;
                    OnPropertyChanged(nameof(IsMappingPanelVisible));
                }
            }
        }

        private bool _isCameraPanelVisible = true;
        /// <summary>
        /// Camera面板显示/隐藏
        /// </summary>
        public bool IsCameraPanelVisible
        {
            get => _isCameraPanelVisible;
            set
            {
                if (_isCameraPanelVisible != value)
                {
                    _isCameraPanelVisible = value;
                    OnPropertyChanged(nameof(IsCameraPanelVisible));
                }
            }
        }

        private bool _isSPPanelVisible = true;
        /// <summary>
        /// SP面板显示/隐藏
        /// </summary>
        public bool IsSPPanelVisible
        {
            get => _isSPPanelVisible;
            set
            {
                if (_isSPPanelVisible != value)
                {
                    _isSPPanelVisible = value;
                    OnPropertyChanged(nameof(IsSPPanelVisible));
                }
            }
        }

        private bool _isLogPanelVisible = true;
        /// <summary>
        /// 日志面板显示/隐藏
        /// </summary>
        public bool IsLogPanelVisible
        {
            get => _isLogPanelVisible;
            set
            {
                if (_isLogPanelVisible != value)
                {
                    _isLogPanelVisible = value;
                    OnPropertyChanged(nameof(IsLogPanelVisible));
                }
            }
        }
        #endregion

        #region SN查找功能
        // SN查找输入属性
        private string _searchSN;
        public string SearchSN
        {
            get => _searchSN;
            set => SetProperty(ref _searchSN, value);
        }

        // 筛选后的测试结果集合（绑定到DataGrid）
        private ObservableCollection<DieViewModel> _filteredTestResults;
        public ObservableCollection<DieViewModel> FilteredTestResults
        {
            get => _filteredTestResults;
            set => SetProperty(ref _filteredTestResults, value);
        }

        // 查找命令
        public ICommand SearchCommand { get; }
        #endregion

        private readonly Random _random = new Random();

        private DataGrid? _dataGrid; // 引用DataGrid（静态列+动态列）
        private RCRestService rcService;

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
                SetProperty(ref _selectedItem, value);
                ManScrollToItem(SelectedItem);
                OnSelectedChanged(value);
            }
        }

        private CVMQTTWPClient mqtt;
        private int CurTestDieIdx = -1;

        public MainService mainService { get; private set; }

        #region 静态列+动态列配置
        /// <summary>
        /// 静态列数量（前13列）
        /// </summary>
        private const int StaticColumnCount = 13;

        /// <summary>
        /// 静态列配置（前13列，不可选）
        /// </summary>
        private readonly List<ColumnConfig> _staticColumnConfigs = new List<ColumnConfig>
        {
            new ColumnConfig { ColumnHeader =(string)Application.Current.FindResource("GridHeader.No"), ColumnBindingPath = "Id", ColumnType = ColumnType.Text, IsOptional = false, ColumnKey = ColumnKey.Other },
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

        /// <summary>
        /// 动态可选列配置（第14列及以后）
        /// </summary>
        private List<ColumnConfig> _dynamicColumnConfigs = new List<ColumnConfig>();

        /// <summary>
        /// 当前选中的动态列配置
        /// </summary>
        public ObservableCollection<ColumnConfig> SelectedDynamicColumns { get; set; }

        /// <summary>
        /// 打开Summary导出配置窗口命令
        /// </summary>
        public ICommand OpenSummaryConfigCommand { get; }
        #endregion

        #region 三态全选属性
        // AOI三态全选
        private bool? _selectAllAOI = false;
        public bool? SelectAllAOI
        {
            get => _selectAllAOI;
            set
            {
                if (!Equals(_selectAllAOI, value))
                {
                    _selectAllAOI = value;
                    OnPropertyChanged(nameof(SelectAllAOI));

                    if (value.HasValue && TestResults != null)
                    {
                        _isUpdatingFromHeader_AOI = true;
                        try
                        {
                            foreach (var item in TestResults)
                            {
                                item.IsAOIEnabled = value.Value;
                            }
                            // 强制刷新DataGrid
                            //_dataGrid?.Items.Refresh();
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

        // IVL三态全选
        private bool? _selectAllIVL = false;
        public bool? SelectAllIVL
        {
            get => _selectAllIVL;
            set
            {
                if (!Equals(_selectAllIVL, value))
                {
                    _selectAllIVL = value;
                    OnPropertyChanged(nameof(SelectAllIVL));

                    if (value.HasValue && TestResults != null)
                    {
                        _isUpdatingFromHeader_IVL = true;
                        try
                        {
                            foreach (var item in TestResults)
                            {
                                item.IsIVLEnabled = value.Value;
                            }
                            //_dataGrid?.Items.Refresh();
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

        // EQE三态全选
        private bool? _selectAllEQE = false;
        public bool? SelectAllEQE
        {
            get => _selectAllEQE;
            set
            {
                if (!Equals(_selectAllEQE, value))
                {
                    _selectAllEQE = value;
                    OnPropertyChanged(nameof(SelectAllEQE));

                    if (value.HasValue && TestResults != null)
                    {
                        _isUpdatingFromHeader_EQE = true;
                        try
                        {
                            foreach (var item in TestResults)
                            {
                                item.IsEQEEnabled = value.Value;
                            }
                            //_dataGrid?.Items.Refresh();
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

        // VAM三态全选
        private bool? _selectAllVAM = false;
        public bool? SelectAllVAM
        {
            get => _selectAllVAM;
            set
            {
                if (!Equals(_selectAllVAM, value))
                {
                    _selectAllVAM = value;
                    OnPropertyChanged(nameof(SelectAllVAM));

                    if (value.HasValue && TestResults != null)
                    {
                        _isUpdatingFromHeader_VAM = true;
                        try
                        {
                            foreach (var item in TestResults)
                            {
                                item.IsVAMEnabled = value.Value;
                            }
                            _dataGrid?.Items.Refresh();
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
        #endregion

        #region 良率计算
        private string _yieldInfo = "0/0 (0.00%)";
        public string YieldInfo
        {
            get => _yieldInfo;
            set
            {
                if (_yieldInfo != value)
                {
                    _yieldInfo = value;
                    OnPropertyChanged(nameof(YieldInfo));
                    CustomMappingVM.YieldInfo = value;
                }
            }
        }
        #endregion
        private enum LogType
        {
            Debug,
            Info,
            //Send,
            //Receive,
            //Success,
            Warning,
            Error
        }
       
        public IEventAggregator? EventAggregator;
        public MainViewModel()
        {
            Instance = this;
            _selectedItem = null;
            _selectedFlow = null;
            _dataGrid = null;
            _isIVLCameraEnabled = false;
            _isAutoSN = true;
            rcService = new RCRestService();
            _rcConnectionInfo = rcService.ConnectionInfo;
            //
            InitializeMainServive();
            //
            // 初始化重置布局命令
            SearchCommand = new RelayCommand(ExecuteSearch);

            OpenVEyeWindowCommand = new RelayCommand(OpenVEyeWindow);
            RefreshStatusCommand = new RelayCommand(RefreshStatus);
            OpenMappingFileCommand = new RelayCommand(OpenMappingFile);
            StartAutoTestCommand = new RelayCommand(StartAutoTest);
            StopAutoTestCommand = new RelayCommand(StopAutoTest);
            StartManTestCommand = new RelayCommand(StartManTest);
            SaveTestResultCommand = new RelayCommand(SaveTestResult);
            LoadTestResultCommand = new RelayCommand(LoadTestResult);
            ResetStatusCommand = new RelayCommand(ResetStatus);
            ClearMappingCommand = new RelayCommand(_ => ClearMapping());
            FlowLoadCommand = new RelayCommand(_ => LoadBuzWPFlows());
            RCRegCommand = new RelayCommand(_ => RCReg());
            OpenHelpCommand = new RelayCommand(ExecuteOpenHelp);
            OpenAboutCommand = new RelayCommand(ExecuteOpenAbout);
            OpenProberDeviceDebugCommand = new RelayCommand(OpenProberDeviceDebug);
            ExitCommand = new CVImgRelayCommand(() =>
            {
                var result = MessageBox.Show((string)Application.Current.FindResource("Sureex"), (string)Application.Current.FindResource("ExitPrompt"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Application.Current.Shutdown();
                }
            });
           
        // 反选命令初始化
        InvertSelectAOICommand = new RelayCommand(ExecuteInvertSelectAOI);
            InvertSelectIVLCommand = new RelayCommand(ExecuteInvertSelectIVL);
            InvertSelectEQECommand = new RelayCommand(ExecuteInvertSelectEQE);
            InvertSelectVAMCommand = new RelayCommand(ExecuteInvertSelectVAM);

            // 初始化数据源
            TestResults = new ObservableCollection<DieViewModel>();
            TestResults.CollectionChanged += AOIItems_CollectionChanged;
            TestResults.CollectionChanged += IVLItems_CollectionChanged;
            TestResults.CollectionChanged += EQEItems_CollectionChanged;
            TestResults.CollectionChanged += VAMItems_CollectionChanged;
            //  打开Summary导出配置窗口
            // Summary配置命令
            OpenSummaryConfigCommand = new CVImgRelayCommand(OpenSummaryConfig);
            // OpenSummaryConfigCommand = new RelayCommand(OpenSummaryConfig);
            SysFlowCfgCommand = new RelayCommand(SysFlowCfg);

            ShowConnectionSettingsCommand = new RelayCommand(OpenProberDeviceDebug);
            ShowRCConnectionSettingsCommand = new RelayCommand(ShowRcConnectionSettings);

            // 初始化筛选集合
            FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);
            TestResults.CollectionChanged += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(SearchSN))
                {
                    FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);
                }
                else
                {
                    ExecuteSearch();
                }
            };

            // 初始化列配置
            InitColumnConfigs();

            // 初始化服务
            InitMysqlCfg();
            ProberId = "CVProber01";
            MappingCsvFilePath = "E:\\work\\cv\\New版\\晶圆台\\CVWaferProber\\ChipMapping\\ScanData_sc.csv";
            if (!System.IO.File.Exists(MappingCsvFilePath)) MappingCsvFilePath = "ScanData_sc.csv";
            Snowflake.Instance.SnowflakesInit(1, 1);
            //InitializeSimAutoTestTimer();

            InitializeEvents();

            LoadMappingFileFromCsv();
            LoadBuzWPFlows();
            InitMQTT();

            InitRc();

            SubscribeItems_AOI(TestResults);
            SubscribeItems_IVL(TestResults);
            SubscribeItems_EQE(TestResults);
            SubscribeItems_VAM(TestResults);

            var ivlService = new IVLService(CustomIVLVM, CustomMappingVM, rcService);
            ivlService.SetSpPanelView(SpPanelView);
            // 加载上次保存的面板状态（需先在Settings中配置）
            //IsMappingPanelVisible = Properties.Settings.Default.IsMappingPanelVisible;
            //IsCameraPanelVisible = Properties.Settings.Default.IsCameraPanelVisible;
            //IsSPPanelVisible = Properties.Settings.Default.IsSPPanelVisible;
        }

        private void InitRc()
        {
            Task.Factory.StartNew(() => {
                rcService.RcRegist();
            });
        }

        private void ShowRcConnectionSettings(object obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = new ConnectionSettingsWindow
                {
                    DataContext = new RcConnectionSettingsViewModel(rcService),
                    Owner = Application.Current.MainWindow
                };

                window.ShowDialog();
            });
        }
        private void ShowConnectionSettings(object obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = new ConnectionSettingsWindow
                {
                    DataContext = new ConnectionSettingsViewModel(mainService.ProberClient, _connectionInfo),
                    Owner = Application.Current.MainWindow
                };

                window.ShowDialog();
            });
        }

        private void InitializeMainServive()
        {
            mainService = MainService.Instance;
            _connectionInfo = mainService.ConnectionInfo;
            mainService.InitializeService(ProberId, rcService);
            //
            mainService.TestingCompleted += OnTestingCompleted;
            mainService.AutoTestingNext += OnOneDieTestingNext;
            mainService.ChipSelected += OnChipDieSelected;
            //
            CustomMappingVM = mainService.GetMappingVM();
            CustomImageVM = mainService.GetAOIVM();
            CustomIVLVM = mainService.GetIVLVM();
            if (CustomIVLVM != null) CustomIVLVM.CustomEQEVM = mainService.GetEQEVM();

            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(2000);
                MainService.Instance.Startup();
            });
        }
        private void InitializeEvents()
        {
            try
            {
                // 创建事件聚合器
                 EventAggregator = new EventAggregator();
                // 订阅事件
                //eventAggregator.Subscribe<CommandSentEvent>(OnCommandSent);
            }
            catch (Exception ex)
            {
                AddLog($"Initialize failed: {ex.Message}", LogType.Error);
            }
        }

        private void AddLog(string message, LogType typeLog = LogType.Info)
        {
            switch (typeLog)
            {
                case LogType.Info:
                    if (logger.IsInfoEnabled) logger.Info(message);
                    break;
                case LogType.Debug:
                    if (logger.IsDebugEnabled) logger.Debug(message);
                    break;
                case LogType.Warning:
                    if (logger.IsWarnEnabled) logger.Warn(message);
                    break;
                case LogType.Error:
                    if (logger.IsErrorEnabled) logger.Error(message);
                    break;
                default:
                    break;
            }
        }

        private void SysFlowCfg(object obj)
        {
            SysFlowCfgWindow cfgWindow = new SysFlowCfgWindow();
            cfgWindow.Show();
        }

        #region 核心方法实现（修复+新增）
        // ========== 静态列+动态列配置初始化 ==========
        private void InitColumnConfigs()
        {
            // 初始化动态可选列（第14列及以后）
            _dynamicColumnConfigs = new List<ColumnConfig>
            {
                new ColumnConfig
                {
                    ColumnHeader = "LightOnStatus",
                    ColumnBindingPath = "LightOnStatus",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "RegisterPixels",
                    ColumnBindingPath = "RegisterPixels",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "Final Class",
                    ColumnBindingPath = "FinalClass",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "AOI GradeLevel",
                    ColumnBindingPath = "AOIGradeLevel",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "Black Pattern",
                    ColumnBindingPath = "BlackPattern",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "Luminance(nit)",
                    ColumnBindingPath = "Luminance",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "Voltage(v)",
                    ColumnBindingPath = "Voltage",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "Current(mA)",
                    ColumnBindingPath = "Voltage",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "Dominant Wavelength",
                    ColumnBindingPath = "FinalClass",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "Temperature(℃)",
                    ColumnBindingPath = "AOIGradeLevel",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "Pixel Logic",
                    ColumnBindingPath = "BlackPattern",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "Pin Pressure",
                    ColumnBindingPath = "Luminance",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },  
                new ColumnConfig
                {
                    ColumnHeader = "TouchDown Counts",
                    ColumnBindingPath = "Voltage",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
                new ColumnConfig
                {
                    ColumnHeader = "Probing Card SN",
                    ColumnBindingPath = "Voltage",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text,
                    ColumnKey = ColumnKey.Other
                },
            };

            // 初始化选中的动态列（默认不选）
            SelectedDynamicColumns = new ObservableCollection<ColumnConfig>(
                _dynamicColumnConfigs.Where(c => c.IsSelected));
        }
        private void OnSelectedChanged(object? value)
        {
            if (IsNotProcessing)
            {
                // 选中项变化时的逻辑
                if (value != null && value is DieViewModel die)
                {
                    if (selfClick)
                    {
                        CustomMappingVM?.SetSelectedChip((uint)die.Id);
                        DieResultDisplay(die);
                    }
                    else selfClick = true;
                }
            }
        }

        // ========== 动态列更新（仅处理第14列及以后） ==========
        public void UpdateDataGridColumns()
        {
            if (_dataGrid == null) return;

            // 步骤1：保留前13列静态列，删除所有动态列
            while (_dataGrid.Columns.Count > StaticColumnCount)
            {
                _dataGrid.Columns.RemoveAt(StaticColumnCount);
            }

            // 步骤2：添加选中的动态列
            foreach (var config in SelectedDynamicColumns)
            {
                if (config.ColumnType == ColumnType.CheckBox)
                {
                    var templateColumn = new DataGridTemplateColumn
                    {
                        Header = config.ColumnHeader
                    };
                    var checkBoxFactory = new FrameworkElementFactory(typeof(CheckBox));
                    checkBoxFactory.SetBinding(CheckBox.IsCheckedProperty, new Binding(config.ColumnBindingPath)
                    {
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });
                    checkBoxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
                    checkBoxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
                    templateColumn.CellTemplate = new DataTemplate { VisualTree = checkBoxFactory };
                    _dataGrid.Columns.Add(templateColumn);
                }
                else
                {
                    _dataGrid.Columns.Add(new DataGridTextColumn
                    {
                        Header = config.ColumnHeader,
                        Binding = new Binding(config.ColumnBindingPath)
                        {
                            TargetNullValue = "",
                            StringFormat = config.ColumnBindingPath.Contains("Time") ? "yyyy-MM-dd HH:mm:ss" : null
                        }
                    });
                }
            }
        }

        // ========== 打开Summary配置窗口（仅显示动态列） ==========
        private void OpenSummaryConfig()
        {
            var configWindow = new SummaryConfigWindow(_dynamicColumnConfigs);
            if (configWindow.ShowDialog() == true)
            {
                // 更新选中的动态列
                SelectedDynamicColumns.Clear();
                foreach (var config in configWindow.SelectedColumns)
                {
                    SelectedDynamicColumns.Add(config);
                    // 同步更新配置状态
                    var targetConfig = _dynamicColumnConfigs.First(c => c.ColumnBindingPath == config.ColumnBindingPath);
                    targetConfig.IsSelected = config.IsSelected;
                }
                // 刷新动态列
                UpdateDataGridColumns();
            }
        }

        #region 三态全选状态更新
        private void UpdateSelectAllAOIState()
        {
            if (TestResults == null || TestResults.Count == 0)
            {
                SelectAllAOI = false;
                return;
            }

            int selectedCount = TestResults.Count(item => item.IsAOIEnabled);
            int totalCount = TestResults.Count;

            bool? newState = selectedCount switch
            {
                0 => false,
                var c when c == totalCount => true,
                _ => null
            };

            if (!Equals(SelectAllAOI, newState))
            {
                SelectAllAOI = newState;
                OnPropertyChanged(nameof(SelectAllAOI));
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

            bool? newState = selectedCount switch
            {
                0 => false,
                var c when c == totalCount => true,
                _ => null
            };

            if (!Equals(SelectAllIVL, newState))
            {
                SelectAllIVL = newState;
                OnPropertyChanged(nameof(SelectAllIVL));
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

            bool? newState = selectedCount switch
            {
                0 => false,
                var c when c == totalCount => true,
                _ => null
            };

            if (!Equals(SelectAllEQE, newState))
            {
                SelectAllEQE = newState;
                OnPropertyChanged(nameof(SelectAllEQE));
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

            bool? newState = selectedCount switch
            {
                0 => false,
                var c when c == totalCount => true,
                _ => null
            };

            if (!Equals(SelectAllVAM, newState))
            {
                SelectAllVAM = newState;
                OnPropertyChanged(nameof(SelectAllVAM));
            }
        }

        #endregion
        #region 反选方法（添加刷新） 
        private void ExecuteInvertSelectAOI(object obj)
        {
            foreach (var item in TestResults)
            {
                item.IsAOIEnabled = !item.IsAOIEnabled;
            }
            _dataGrid?.Items.Refresh();
            UpdateSelectAllAOIState();
        }

        private void ExecuteInvertSelectIVL(object obj)
        {
            foreach (var item in TestResults)
            {
                item.IsIVLEnabled = !item.IsIVLEnabled;
            }
            _dataGrid?.Items.Refresh();
            UpdateSelectAllIVLState();
        }

        private void ExecuteInvertSelectEQE(object obj)
        {
            foreach (var item in TestResults)
            {
                item.IsEQEEnabled = !item.IsEQEEnabled;
            }
            _dataGrid?.Items.Refresh();
            UpdateSelectAllEQEState();
        }

        private void ExecuteInvertSelectVAM(object obj)
        {
            foreach (var item in TestResults)
            {
                item.IsVAMEnabled = !item.IsVAMEnabled;
            }
            _dataGrid?.Items.Refresh();
            UpdateSelectAllVAMState();
        }

        #endregion

        #region 良率计算
        public void CalculateYieldBySerialNumber()
        {
            try
            {
                if (TestResults == null || !TestResults.Any())
                {
                    YieldInfo = "0/0 (0.00%)";
                    return;
                }

                var testedDices = TestResults.Where(d => !string.IsNullOrWhiteSpace(d.SerialNumber)).ToList();
                if (!testedDices.Any())
                {
                    YieldInfo = "0/0 (0.00%)";
                    return;
                }

                int successCount = testedDices.Count(d =>
                    d.Status == ChipStatus.OK ||
                    d.Status == ChipStatus.IVL_COMPLETED ||
                    d.Status == ChipStatus.EQE_COMPLETED);

                double yieldRate = (double)successCount / testedDices.Count * 100;
                YieldInfo = $"{successCount}/{testedDices.Count} ({yieldRate:F2}%)";
            }
            catch (Exception ex)
            {
                logger.Error("Yield Calculation Exception", ex);
                YieldInfo = $"{(string)Application.Current.FindResource("CalculationException")}";
            }
        }
        #endregion

        #region 自动导出Summary（适配静态+动态列）

        private void AutoExportSummaryResult()
        {
            try
            {
                if (TestResults == null || !TestResults.Any())
                {
                    logger.Info("No test results, skipping Summary export" );
                    return;
                }

                //固定导出根路径为 F:/Project
                string exportRootPath = @"D:\ Project";

                // 自动创建目录（如果不存在）
                if (!Directory.Exists(exportRootPath))
                {
                    Directory.CreateDirectory(exportRootPath);
                    logger.Info($"Export directory has been created automatically：{exportRootPath}");//已自动创建导出目录
                }

                // 拼接最终保存路径（目录 + 带时间戳的文件名）
                var savePath = Path.Combine(
                    exportRootPath,
                    $"Summary_Result_{DateTime.Now:yyyyMMddHHmmss}.csv");

                // 合并静态列+选中的动态列
                var allSelectedColumns = new List<ColumnConfig>();
                allSelectedColumns.AddRange(_staticColumnConfigs);
                allSelectedColumns.AddRange(SelectedDynamicColumns);

                // 写入CSV
                using (var writer = new StreamWriter(savePath, false, System.Text.Encoding.UTF8))
                {
                    // 表头
                    var headers = allSelectedColumns.Select(c => c.ColumnHeader).ToList();
                    writer.WriteLine(string.Join(",", headers));

                    // 数据行
                    foreach (var die in TestResults)
                    {
                        var rowData = new List<string>();
                        foreach (var config in allSelectedColumns)
                        {
                            var prop = die.GetType().GetProperty(config.ColumnBindingPath);
                            if (prop == null)
                            {
                                rowData.Add("");
                                continue;
                            }

                            var value = prop.GetValue(die);
                            if (value == null || value == DBNull.Value)
                            {
                                rowData.Add("");
                            }
                            else if (value is bool boolValue)
                            {
                                rowData.Add(boolValue ? "Y" : "N");
                            }
                            else if (value is DateTime dateTimeValue)
                            {
                                rowData.Add(dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                            else
                            {
                                rowData.Add(value.ToString());
                            }
                        }
                        // 处理包含逗号的字段，添加双引号包裹
                        writer.WriteLine(string.Join(",", rowData.Select(d => d.Contains(",") ? $"\"{d}\"" : d)));
                    }
                }

                logger.Info($"The summary results have been automatically exported：{savePath}");//" : "Summary结果已自动导出")}
                //Application.Current.Dispatcher.Invoke(() =>
                //{
                //    MessageBox.Show($"{(IsEnglishMode ? "The summary results have been exported to" : "Summary结果已导出至")}：{savePath}", IsEnglishMode? "Export successful" : "导出成功",
                //        MessageBoxButton.OK, MessageBoxImage.Information);
                //});
            }
            catch (Exception ex)
            {
                logger.Error( "Failed to export Summary results", ex);//" : "Summary结果导出失败"
                //Application.Current.Dispatcher.Invoke(() =>
                //{
                //    MessageBox.Show($"导出失败：{ex.Message}", "错误",
                //        MessageBoxButton.OK, MessageBoxImage.Error);
                //});
            }
        }
        #endregion

        #endregion

        #region 原有方法
        private void OpenProberDeviceDebug(object obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = new DevProberDebugWindow
                {
                    DataContext = new DevProberDebugViewModel(mainService.ProberClient, mainService.StateMachine, _connectionInfo),
                    Owner = Application.Current.MainWindow
                };

                window.ShowDialog();
            });
        }

        private void OnChipDieSelected(object? sender, ChipViewModel chip)
        {
            if (chip == null || TestResults.Count == 0)
            {
                SelectedItem = null;
                return;
            }

            var targetDie = TestResults.FirstOrDefault(die => die.Id == chip.Id);
            if (targetDie != null)
            {
                selfClick = false;
                SelectedItem = targetDie;
                ManScrollToItem(targetDie);
            }
        }

        // SN索引字典
        private Dictionary<string, List<DieViewModel>> _snIndex = new Dictionary<string, List<DieViewModel>>(StringComparer.OrdinalIgnoreCase);

        private void BuildSNIndex()
        {
            _snIndex.Clear();
            foreach (var die in TestResults)
            {
                if (string.IsNullOrEmpty(die.SerialNumber)) continue;
                if (!_snIndex.ContainsKey(die.SerialNumber))
                {
                    _snIndex[die.SerialNumber] = new List<DieViewModel>();
                }
                _snIndex[die.SerialNumber].Add(die);
            }
        }

        private void ExecuteSearch(object parameter = null)
        {
            if (string.IsNullOrWhiteSpace(SearchSN))
            {
                FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);
            }
            else
            {
                var searchKey = SearchSN.Trim();
                if (_snIndex.TryGetValue(searchKey, out var results))
                {
                    FilteredTestResults = new ObservableCollection<DieViewModel>(results);
                }
                else
                {
                    var query = TestResults.Where(die =>
                        !string.IsNullOrEmpty(die.SerialNumber) &&
                        die.SerialNumber.Contains(searchKey, StringComparison.OrdinalIgnoreCase));
                    FilteredTestResults = new ObservableCollection<DieViewModel>(query);
                }
            }
        }

        private void ExecuteOpenHelp(object obj)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{(string)Application.Current.FindResource("Failedopen")}：{ex.Message}", $"{ (string)Application.Current.FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteOpenAbout(object obj)
        {
            // 可自行实现AboutWindow
        }

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
            DoEndTesting();
        }
        private void OnOneDieTestingNext(object? sender, (DieViewModel? preDie, DieViewModel nextDie) e)
        {
            if (e.preDie != null) e.preDie.UnSelected();
            SelectedItem = e.nextDie;
        }

        private void OpenVEyeWindow(object? obj)
        {
            ExternalWindow newWindow = new ExternalWindow();
            newWindow.Show();
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
            CalculateYieldBySerialNumber();
        }

        private void RCReg()
        {
            bool bR = rcService.RcRegist();
            if (bR)
            {
                LoadFlow();
            }
        }

        private void LoadBuzWPFlows()
        {
            List<TScgdBuzProductDetail> flows = WaferProberDBService.LoadBuzFlows();
            WPFlows.Clear();
            if (flows != null && flows.Count > 0)
            {
                foreach (var flow in flows)
                {
                    WPFlows.Add(new WPFlowViewModel(flow));
                }
                if (WPFlows.Count > 0) SelectedWPFlow = WPFlows[0];
            }
            else
            {
                
            }
        }

        private string testingStatus = $"{(string)Application.Current.FindResource("Maping.NoMeasurement")}";
        public string TestingStatus
        {
            get => testingStatus;
            set => SetProperty(ref testingStatus, value);
        }
        private void LoadFlow()
        {
            var flows = rcService.RcLoadFlows();
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

        //private void InitializeSimAutoTestTimer()
        //{
        //    _simAutoTestTimer = new DispatcherTimer();
        //    _simAutoTestTimer.Interval = TimeSpan.FromMilliseconds(1000);
        //    _simAutoTestTimer.Tick += RestGetFlowResultTimer_Tick;
        //}

        //private void RestGetFlowResultTimer_Tick(object? sender, EventArgs e)
        //{
        //    if (IsProcessing && CurTestDieIdx >= 0)
        //    {
        //        DieViewModel dieViewModel = TestResults[CurTestDieIdx];
        //        var resp = rcModel.RcGetFlowResult_POI(dieViewModel.SerialNumber);
        //        if (resp != null)
        //        {
        //            if (resp.IsSuccess)
        //            {
        //                DieResultDisplay(dieViewModel);
        //                dieViewModel.ChangeStatus(ChipStatus.OK, true);
        //            }
        //            else if (resp.ResultStatus == "Pending")
        //            {
        //                return;
        //            }
        //            else
        //            {
        //                dieViewModel.ChangeStatus(ChipStatus.AOI_NG, true);
        //            }
        //            NextTestingDie();
        //        }
        //    }
        //}

        private void DieResultDisplay(DieViewModel dieViewModel)
        {
            //if (string.IsNullOrEmpty(dieViewModel.SerialNumber))
            //{
            //    CustomIVLVM.ClearResult();
            //    CustomImageVM?.ClearImageResult();
            //}
            // if (dieViewModel.Status == ChipStatus.IVL_TESTING || dieViewModel.Status == ChipStatus.IVL_COMPLETED)
            // {
            //ivlService.IVLResultDisplay(dieViewModel);
            //
            // else
            // {
            //aoiService.AOIResultDisplay(dieViewModel);
            // }
            //eqeService.EQEResultDisplay(dieViewModel);
            //vamService.VAMResultDisplay(dieViewModel);
            MainService.Instance.ResultDisplay(dieViewModel);
            // 新增：计算良率
            CalculateYieldBySerialNumber();
        }

        //private void NextTestingDie()
        //{
        //    if (_currentTestIndex < _testQueue.Count)
        //    {
        //        var currentDie = _testQueue[_currentTestIndex].Die;
        //        currentDie.UnSelected();
        //    }

        //    _currentTestIndex++;
        //    StartNextTestItem();
        //}

        private void StartTestingDie(int row, int col)
        {
            if (CurTestDieIdx >= 0)
            {
                TestResults[CurTestDieIdx].UnSelected();
            }
            var itemToSelect = TestResults.FirstOrDefault(d => d.MapX == col && d.MapY == row);
            if (itemToSelect != null)
            {
                CurTestDieIdx = TestResults.IndexOf(itemToSelect);

                ManScrollToItem(itemToSelect);
                mainService.DoDieFlowExec(_selectedWPFlow, itemToSelect, false);
                //aoiService.StartTesting(Timestamp, itemToSelect, _selectedWPFlow, false);
                CalculateYieldBySerialNumber();
            }
            else
            {
                logger.ErrorFormat("Die not found By Row={0},Col={1}", row, col);
            }
        }

        private List<TestItem> _testQueue;
        private int _currentTestIndex;

        private void StartAutoFlow()
        {
            if (SelectedWPFlow == null)
            {
                logger.Error("No test process (Flow) selected");
                return;
            }

            _testQueue = GetSelectedTestItems();
            if (_testQueue.Count == 0)
            {
                MessageBox.Show($"{(string)Application.Current.FindResource("Nodata")}", $"{ (string)Application.Current.FindResource("Prompt")}" , MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TestingReady(_testQueue);
            _currentTestIndex = 0;
            EnableBtnGUI(false);

            //StartNextTestItem();
            mainService.StartAutoTesting(_selectedWPFlow, GetSelectedDieTestItems());
        }
        private List<DieViewModel> GetSelectedDieTestItems()
        {
            var testQueue = new List<DieViewModel>();
            var fType = _selectedWPFlow.FlowType;
            foreach (var die in TestResults)
            {
                if (die.IsAOIEnabled && fType == CVWaferProberFlowType.AOI)
                {
                    testQueue.Add(die);
                }
                if (die.IsIVLEnabled && ( fType == CVWaferProberFlowType.IVL_SP || fType == CVWaferProberFlowType.IVL_Camera))
                {
                    testQueue.Add(die);
                }
                if (die.IsEQEEnabled && fType == CVWaferProberFlowType.EQE)
                {
                    testQueue.Add(die);
                }
                if (die.IsVAMEnabled && fType == CVWaferProberFlowType.VAM)
                {
                    testQueue.Add(die);
                }
            }

            return testQueue;
        }
        //private void StartNextTestItem()
        //{
        //    if (_currentTestIndex >= _testQueue.Count)
        //    {
        //        StopAutoTest(null);
        //        MessageBox.Show("所有勾选项测试完成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        //        return;
        //    }

        //    var currentTest = _testQueue[_currentTestIndex];
        //    DieViewModel currentDie = currentTest.Die;
        //    CurTestDieIdx = TestResults.IndexOf(currentDie);

        //    ScrollToItem(currentDie);

        //    mainService.DoDieFlowExec(_selectedWPFlow, currentDie, false);
        //}
        public CVSpectrumAnalyzer? SpPanelView { get; set; }
        private TabControl? _innerTabControl;
        private void StartManFlow()
        {
            if (SelectedWPFlow != null && SelectedItem is DieViewModel die)
            {
                EnableBtnGUI(false);
                ManTestingReady(die);
                mainService.DoDieFlowExec(_selectedWPFlow, die);
                ActivateCorrespondingPanel();
            }
            
        }

        private void DoEndTesting()
        {
            EnableBtnGUI(true);
            CalculateYieldBySerialNumber();
            AutoExportSummaryResult();
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
            //CustomMappingVM.Cleanup();
            //foreach (var item in CustomMappingVM.Chips)
            //{
            //    item.SetStatus(ChipStatus.WAITING);
            //}
            //foreach (var item in TestResults)
            //{
            //    item.EndTestTime = null;
            //    item.SerialNumber = null;
            //    item.StartTestTime = null;
            //    item.TotalTime = null;
            //}

            CurTestDieIdx = 0;
            string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fff");
            if (_isAutoSN) Timestamp = timestamp;
            foreach (var itemT in testItems)
            {
                itemT.Die.TestingReady(ProberId, timestamp);
            }
            //_dataGrid?.Items.Refresh();
        }

        private void StartAutoTest(object? obj)
        {
            StartAutoFlow();
        }

        private void StartManTest(object? obj)
        {
            StartManFlow();
        }

        private void EnableBtnGUI(bool enabled)
        {
            CustomMappingVM.DisabledInput = IsProcessing = !enabled;

            OnPropertyChanged(nameof(IsNotProcessing));
        }

        private void StopAutoTest(object? obj)
        {
            if(_testQueue==null) return;

            foreach (var item in _testQueue)
            {
                item.Die.UnSelected();

            }
            //EnableBtn(true);

            mainService.StopAutoTesting();

            CalculateYieldBySerialNumber();
        }

        private void OpenMappingFile(object? obj)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = (string)Application.Current.FindResource("CSVFile");//CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"

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
            BuildSNIndex();
            if (string.IsNullOrWhiteSpace(SearchSN))
            {
                FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);

            }
            else
            {
                ExecuteSearch();
            }

            CalculateYieldBySerialNumber();
        }

        public void SetDataGrid(DataGrid dataGrid)
        {
            _dataGrid = dataGrid;
            // 初始化动态列
            UpdateDataGridColumns();
        }

        private void ManScrollToItem(object? toItem)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_dataGrid != null && toItem != null)
                {
                    _dataGrid.ScrollIntoView(toItem);
                    _dataGrid.UpdateLayout();
                }
            });
        }

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

        public class TestItem
        {
            public DieViewModel Die { get; set; }
            public string TestType { get; set; }
        }

        private List<TestItem> GetSelectedTestItems()
        {
            var testQueue = new List<TestItem>();

            foreach (var die in TestResults)
            {
                if (die.IsAOIEnabled)
                {
                    testQueue.Add(new TestItem { Die = die, TestType = "AOI" });
                }
                if (die.IsIVLEnabled)
                {
                    testQueue.Add(new TestItem { Die = die, TestType = "IVL" });
                }
                if (die.IsEQEEnabled)
                {
                    testQueue.Add(new TestItem { Die = die, TestType = "EQE" });
                }
                if (die.IsVAMEnabled)
                {
                    testQueue.Add(new TestItem { Die = die, TestType = "VAM" });
                }
             
            }

            return testQueue;
        }

        private void ActivateCorrespondingPanel()
        {
            //if (IsProcessing) return;
            if (SelectedWPFlow == null) return;

            if (DockingManager == null || AnchorableSP == null)
            {
                logger.Warn( "SP panel not initialized, cannot activate" );//"SP面板未初始化，无法激活"
                return;
            }

            switch (SelectedWPFlow.FlowType)
            {
                case CVWaferProberFlowType.AOI:
                    if (AnchorableCamera != null)
                    {
                        AnchorableCamera.Show();
                        AnchorableCamera.IsSelected = true;
                    }
                    else
                    {
                        logger.Warn( "AOI panel not initialized, cannot activate"  );// "AOI面板未初始化，无法激活"
                    }
                    break;
                case CVWaferProberFlowType.IVL_SP:
                case CVWaferProberFlowType.IVL_Camera:
                    ActivateSpectralInnerTabAction?.Invoke();
                    break;

                case CVWaferProberFlowType.EQE:
                    ActivateEQEOuterTabAction?.Invoke();
                    break;

                case CVWaferProberFlowType.VAM:
                    if (AnchorableVAM != null)
                    {
                        AnchorableVAM.Show();
                        AnchorableVAM.IsSelected = true;
                        AnchorableVAM.IsActive = true;
                    }
                    break;
            }

            DockingManager.UpdateLayout();
        }

        #region AOI 全选/部分选中事件
        private void AOIItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        private void AOI_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsAOIEnabled) && !_isUpdatingFromHeader_AOI)
            {
                UpdateSelectAllAOIState();
            }
        }
        #endregion

        #region IVL 全选/部分选中事件
        private void IVLItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        private void IVL_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsIVLEnabled) && !_isUpdatingFromHeader_IVL)
            {
                UpdateSelectAllIVLState();
            }
        }
        #endregion

        #region EQE 全选/部分选中事件
        private void EQEItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        private void EQE_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsEQEEnabled) && !_isUpdatingFromHeader_EQE)
            {
                UpdateSelectAllEQEState();
            }
        }
        #endregion

        #region VAM 全选/部分选中事件
        private void VAMItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        private void VAM_Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DieViewModel.IsVAMEnabled) && !_isUpdatingFromHeader_VAM)
            {
                UpdateSelectAllVAMState();
            }
        }
        #endregion
        #endregion
    }
}
