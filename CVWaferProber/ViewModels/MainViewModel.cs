using AvalonDock;
using AvalonDock.Layout;
using ChipMapping.ViewModels;
using CVAVMControl;
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
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using WaferComm.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;


namespace CVWaferProber.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MainViewModel));

        public static MainViewModel? Instance { get; private set; }
        public MappingDataViewModel? DataMappingVM { get; set; }
        public ChipMappingControlViewModel? CustomMappingVM { get; set; }
        public CVCamImagerViewModel? CustomImageVM { get; set; }
        public CVSpectrumViewModel? CustomIVLVM { get; set; }
        // AvalonDock面板引用
        public DockingManager? DockingManager { get; set; }
        public LayoutAnchorable? AnchorableCamera { get; set; }
        public LayoutAnchorable? AnchorableSP { get; set; }
        public LayoutAnchorable? AnchorableVAM { get; set; }
        /// <summary>
        /// 工具栏
        /// </summary>
        public ToolsBarViewModel? ToolsVM { get; set; }
        // SP面板ViewModel引用
        //public CVSpectrumViewModel? SpPanelViewModel { get; set; }

        // 切换委托（对应红框3个选项）
        public Action? ActivateSpectralInnerTabAction { get; set; }
        public Action? ActivateIVLCameraInnerTabAction { get; set; }
        public Action? ActivateEQEOuterTabAction { get; set; }

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
        public ICommand IVLTestCommand { get; }

        public ICommand RCRegCommand { get; }
        public ICommand OpenVEyeWindowCommand { get; }


        public ICommand SysFlowCfgCommand { get; }
        //
        public ICommand ShowConnectionSettingsCommand { get; }
        public ICommand ShowRCConnectionSettingsCommand { get; }
        public ICommand ReconnectDevCommand { get; }
        public ICommand ReconnectRcCommand { get; }
        /// <summary>
        /// 
        /// </summary>
        public bool IsNotProcessing => DataMappingVM.IsNotProcessing;

        private string _AppVersion;
        public string AppVersion
        {
            get => _AppVersion;
            set
            {
                SetProperty(ref _AppVersion, value);
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

        //private readonly Random _random = new Random();

        private RCRestService rcService;

        private CVMQTTWPClient mqtt;
        /// <summary>
        /// 打开Summary导出配置窗口命令
        /// </summary>
        public ICommand OpenSummaryConfigCommand { get; }
        public ICommand OpenGlobalConfigCommand { get; }
        public MainService mainService { get; private set; }

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

        public bool CanStartAuto => 
            _connectionInfo.DevCurrentState == WaferComm.StateMachine.ProberState.WaferLoaded ;


        public IEventAggregator? EventAggregator;
        public MainViewModel()
        {
            Instance = this;

            // 获取主程序集版本
            Version version = Assembly.GetEntryAssembly()?.GetName().Version;

            _AppVersion = string.Format("V{0}", version.ToString());
            rcService = new RCRestService();
            _rcConnectionInfo = rcService.ConnectionInfo;
            //
            DataMappingVM = new MappingDataViewModel();
            DataMappingVM.ActivateCorrespondingPanel += DataMappingVM_ActivateCorrespondingPanel;
            //
            InitializeServive();
            // 初始化重置布局命令

            OpenVEyeWindowCommand = new RelayCommand(OpenVEyeWindow);
            //StartAutoTestCommand = new RelayCommand(_ => StartAutoTest(),
            //    _ => CanStartAuto);
            //StopAutoTestCommand = new RelayCommand(StopAutoTest);

            RCRegCommand = new RelayCommand(_ => RCReg());
            ReconnectDevCommand = new RelayCommand(_ => ReconnectDev());
            ReconnectRcCommand = new RelayCommand(_ => ReconnectRc());
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

            //  打开Summary导出配置窗口
            // Summary配置命令
            OpenSummaryConfigCommand = new CVImgRelayCommand(OpenSummaryConfig);
            OpenGlobalConfigCommand = new RelayCommand(ExecuteOpenGlobalConfig);
            // OpenSummaryConfigCommand = new RelayCommand(OpenSummaryConfig);
            SysFlowCfgCommand = new RelayCommand(SysFlowCfg);

            ShowConnectionSettingsCommand = new RelayCommand(OpenProberDeviceDebug);
            ShowRCConnectionSettingsCommand = new RelayCommand(ShowRcConnectionSettings);

            // 初始化服务
            InitMysqlCfg();

            Snowflake.Instance.SnowflakesInit(1, 1);
            //InitializeSimAutoTestTimer();

            InitializeEvents();

            //LoadMappingFileFromCsv();
            InitMQTT();

            InitRc();

            //var ivlService = new IVLService(CustomIVLVM, CustomMappingVM, rcService);
            //ivlService.SetSpPanelView(SpPanelView);

            // 加载上次保存的面板状态（需先在Settings中配置）
            //IsMappingPanelVisible = Properties.Settings.Default.IsMappingPanelVisible;
            //IsCameraPanelVisible = Properties.Settings.Default.IsCameraPanelVisible;
            //IsSPPanelVisible = Properties.Settings.Default.IsSPPanelVisible;

            ToolsVM = new ToolsBarViewModel(ProberClientService.Instance.ProberClient, ProberClientService.Instance.StateMachine, EventAggregator);
        }

        private void DataMappingVM_ActivateCorrespondingPanel(object? sender, WPFlowViewModel e)
        {
            ActivateCorrespondingPanel(e);
        }

        private void ReconnectRc()
        {
            rcService.RcUnRegist();
            rcService.RcRegist();
        }

        private void ReconnectDev()
        {
            mainService.ReconnectDev();
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
                    DataContext = new ConnectionSettingsViewModel(ProberClientService.Instance.ProberClient, _connectionInfo),
                    Owner = Application.Current.MainWindow
                };

                window.ShowDialog();
            });
        }
        public void InitializeServiveVM(CVVAMAnalyzer vam, CVSpectrumAnalyzer ivl)
        {
            mainService.InitializeService(rcService, vam, ivl);

            CustomMappingVM = mainService.GetMappingVM();
            CustomImageVM = mainService.GetAOIVM();
            CustomIVLVM = mainService.GetIVLVM();
            if (CustomIVLVM != null) CustomIVLVM.CustomEQEVM = mainService.GetEQEVM();
            if (DataMappingVM != null)
            {
                DataMappingVM.CustomMappingVM = CustomMappingVM;
                DataMappingVM.LoadBuzWPFlows();
            }
        }
        private void InitializeServive()
        {
            mainService = MainService.Instance;
            _connectionInfo = ProberClientService.Instance.ConnectionInfo;
            //
            DataMappingVM?.InitializeServive(mainService);
            //
            //Task.Factory.StartNew(async () =>
            //{
            //    await Task.Delay(2000);
            //    MainService.Instance.Startup();
            //});
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

            DataMappingVM?.LoadBuzWPFlows();
        }

        #region 核心方法实现（修复+新增）

        // ========== 打开Summary配置窗口（仅显示动态列） ==========
        private void OpenSummaryConfig()
        {
            DataMappingVM?.OpenSummaryConfig();
        }
        /// <summary>
        /// 执行打开全局配置窗口
        /// </summary>
        private void ExecuteOpenGlobalConfig(object obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var globalConfigWindow = new GlobalConfigWindow
                {
                    Owner = Application.Current.MainWindow
                };

                globalConfigWindow.ShowDialog();
            });
        }

        #endregion

        #region 原有方法
        private void OpenProberDeviceDebug(object obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = new DevProberDebugWindow
                {
                    DataContext = new DevProberDebugViewModel(ProberClientService.Instance.ProberClient,
                    ProberClientService.Instance.StateMachine, _connectionInfo),
                    Owner = Application.Current.MainWindow
                };

                window.ShowDialog();
            });
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

        private void OpenVEyeWindow(object? obj)
        {
            ExternalWindow newWindow = new ExternalWindow();
            newWindow.Show();
        }
        private void InitMQTT()
        {
            mqtt = CVMQTTWPClient.Instance.Init("RC_local");
        }
        private void RCReg()
        {
            bool bR = rcService.RcRegist();
            if (bR)
            {
                var flows = rcService.RcLoadFlows();

               DataMappingVM.LoadFlow(flows);
            }
        }

        private string testingStatus = $"{(string)Application.Current.FindResource("Maping.NoMeasurement")}";
        public string TestingStatus
        {
            get => testingStatus;
            set => SetProperty(ref testingStatus, value);
        }

        public CVSpectrumAnalyzer? SpPanelView { get; set; }


        public void PauseAutoFlow()
        {
            mainService.PauseAutoTesting();
        }
        public void ContinuAutoFlow()
        {
            mainService.ContinuAutoTesting();
        }


        public void SetSelectedDataGridItem(object? obj)
        {
            if (obj != null) DataMappingVM?.SelectItemById((uint)obj);
        }

        public class TestItem
        {
            public DieViewModel Die { get; set; }
            public string TestType { get; set; }
        }
        private void ActivateCorrespondingPanel(WPFlowViewModel SelectedWPFlow)
        {
            if (SelectedWPFlow == null) return;

            if (DockingManager == null || AnchorableSP == null)
            {
                //logger.Warn( "SP panel not initialized, cannot activate" );//"SP面板未初始化，无法激活"
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
                case CVWaferProberFlowType.IVL:
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

        public void ToIntegratingSphere()
        {
            DataMappingVM?.ToIntegratingSphere();
        }

        public void ToAuxCamera()
        {
            DataMappingVM?.ToAuxCamera();
        }

        public void ToMainCamera()
        {
            DataMappingVM?.ToMainCamera();
        }

        public void StopAutoFlow()
        {
            DataMappingVM?.StopAutoFlow();
        }

        public void StartAutoFlow()
        {
            DataMappingVM?.StartAutoFlow();
        }

        #endregion
        //#endregion

        #region 进度条相关
   
        #endregion
    }
}
