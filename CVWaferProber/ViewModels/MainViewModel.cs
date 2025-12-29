using AvalonDock;
using AvalonDock.Layout;
using ChipMapping.Models;
using ChipMapping.ViewModels;
using ColorVision.Core.Entities;
using CVAVMControl;
using CVDB.Services.Buz;
using CVWaferProber.Components;
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
using MySql.Data.MySqlClient.X.XDevAPI.Common;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
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
        public CVEQEViewModel? CustomEQEVM { get; set; }

        private GSWMProcessor _wmProcessor;

        // AvalonDock面板引用
        public DockingManager? DockingManager { get; set; }
        public LayoutAnchorable? AnchorableCamera { get; set; }
        public LayoutAnchorable? AnchorableSP { get; set; }
        public LayoutAnchorable? AnchorableVAM { get; set; }
        public CVVAMAnalyzer? VAMAnalyzer { get; set; }

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

        private WPFlowViewModel? _selectedWPFlow;
        public WPFlowViewModel? SelectedWPFlow
        {
            get => _selectedWPFlow;
            set
            {
                if (_selectedWPFlow != value)
                {
                    _selectedWPFlow = value;
                    OnPropertyChanged();
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
        //// 布局重置命令
        //public ICommand ResetLayoutCommand { get; }

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

        private DataGrid? _dataGrid; // 引用DataGrid
        private DispatcherTimer? _simAutoTestTimer;
        private RCRestService rcModel;
        private IVLService ivlService;
        private EQEService eqeService;
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

            // ========== 订阅芯片选中事件 ==========
            CustomMappingVM.ChipSelected += OnChipSelected;
            CustomImageVM = new CVCamImagerViewModel();
            CustomIVLVM = new CVSpectrumViewModel();
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
            //OpenCommand  = new RelayCommand( Opena);
            ClearMappingCommand = new RelayCommand(_ => ClearMapping());
            FlowLoadCommand = new RelayCommand(_ => LoadBuzWPFlows());
            RCRegCommand = new RelayCommand(_ => RCReg());
            //// 初始化命令
            //ResetLayoutCommand = new RelayCommand(ExecuteResetLayout);
            OpenHelpCommand = new RelayCommand(ExecuteOpenHelp);
            OpenAboutCommand = new RelayCommand(ExecuteOpenAbout);
            //
            OpenProberDeviceDebugCommand = new RelayCommand(OpenProberDeviceDebug);
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


            //  打开Summary导出配置窗口
            OpenSummaryConfigCommand = new RelayCommand(OpenSummaryConfig);

            // 新增：初始化筛选集合为全部数据
            FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);

            // 新增：订阅TestResults集合变化，同步更新筛选结果
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
            InitColumnConfigs();
            ivlService = new IVLService(CustomIVLVM, rcModel);
            ivlService.ProberId = ProberId;
            ivlService.TestingCompleted += OnTestingCompleted;

            aoiService = new AOIService(CustomImageVM, rcModel);
            aoiService.TestingCompleted += OnTestingCompleted;
            //
            eqeService = new EQEService(CustomEQEVM, rcModel);
            eqeService.TestingCompleted += OnTestingCompleted;

            SubscribeItems_AOI(TestResults);
            SubscribeItems_IVL(TestResults);
            SubscribeItems_EQE(TestResults);
            SubscribeItems_VAM(TestResults);
            // 加载上次保存的面板状态（需先在Settings中配置）
            //IsMappingPanelVisible = Properties.Settings.Default.IsMappingPanelVisible;
            //IsCameraPanelVisible = Properties.Settings.Default.IsCameraPanelVisible;
            //IsSPPanelVisible = Properties.Settings.Default.IsSPPanelVisible;

        }

        private void OpenProberDeviceDebug(object obj)
        {
            DevProberDebugWindow newWindow = new DevProberDebugWindow();
            newWindow.Show();
        }


        // ========== 芯片选中事件处理方法 ==========
        private void OnChipSelected(object sender, ChipViewModel chip)
        {
            if (chip == null || TestResults.Count == 0)
            {
                SelectedItem = null;
                return;
            }

            // 根据芯片的ID查找对应的DieViewModel
            var targetDie = TestResults.FirstOrDefault(die => die.Id == chip.Id);
            if (targetDie != null)
            {
                selfClick = false;
                SelectedItem = targetDie;
                ScrollToItem(targetDie);
            }
        }


        //private void Opena(object obj)
        //{
        //    DemoWindow demoWindow = new DemoWindow();
        //    demoWindow.Show();

        //}

        // SN索引字典
        private Dictionary<string, List<DieViewModel>> _snIndex = new Dictionary<string, List<DieViewModel>>(StringComparer.OrdinalIgnoreCase);

        // 构建索引方法
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
                    // 模糊匹配时遍历（或扩展索引支持模糊匹配）
                    var query = TestResults.Where(die =>
                        !string.IsNullOrEmpty(die.SerialNumber) &&
                        die.SerialNumber.Contains(searchKey, StringComparison.OrdinalIgnoreCase));
                    FilteredTestResults = new ObservableCollection<DieViewModel>(query);
                }
            }
        }

        



        /// <summary>
        /// 重置布局请求事件（View需订阅此事件）
        /// </summary>
        //public event EventHandler ResetLayoutRequested;
        //private void ExecuteResetLayout(object obj)
        //{
        //    ResetLayoutRequested?.Invoke(this, EventArgs.Empty);
        //}
        /// <summary>
        /// 打开帮助文档执行逻辑
        /// </summary>
        private void ExecuteOpenHelp(object obj)
        {
            // 示例：打开帮助PDF/网页
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    //FileName = "https://www.example.com/help", // 替换为实际帮助地址/文件路径
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开帮助失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <summary>
        /// 打开关于窗口执行逻辑
        /// </summary>
        private void ExecuteOpenAbout(object obj)
        {
            // 示例：弹出关于窗口（需定义AboutWindow）
            //var aboutWindow = new AboutWindow(); // 需自行创建AboutWindow视图
            //aboutWindow.ShowDialog();
        }
        // 保存面板状态（窗口关闭时调用）
        //public void SavePanelStates()
        //{
        //    Properties.Settings.Default.IsMappingPanelVisible = IsMappingPanelVisible;
        //    Properties.Settings.Default.IsCameraPanelVisible = IsCameraPanelVisible;
        //    Properties.Settings.Default.IsSPPanelVisible = IsSPPanelVisible;
        //    Properties.Settings.Default.Save();
        //}
        // ViewModel中新增重新加载Mapping面板的方法
        public void ReloadMappingPanel(DockingManager dockingManager)
        {
            // 重新创建Mapping面板并添加到AvalonDock布局
            var layoutRoot = dockingManager.Layout;
            var leftPaneGroup = layoutRoot.Descendents().OfType<LayoutAnchorablePaneGroup>()
                .FirstOrDefault(g => g.DockWidth == new GridLength(300));

            if (leftPaneGroup != null)
            {
                var mappingAnchorable = new LayoutAnchorable
                {
                    Title = "Mapping",
                    Content = new MappingDataControl(),
                    IsVisible = true
                };
                leftPaneGroup.Children.Add((ILayoutAnchorablePane)mappingAnchorable);
            }
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
            if (e.PropertyName == nameof(DieViewModel.IsEQEEnabled) && !_isUpdatingFromHeader_EQE)
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

            // 新增：计算良率
            CalculateYieldBySerialNumber();
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
                aoiService.AOIResultDisplay(dieViewModel);
                //CustomImageVM?.ClearImageResult();
                //CustomImageVM?.LoadImageResult(dieViewModel.chipViewModel.ChipData, dieViewModel.SerialNumber);
            }
            // 新增：计算良率
            CalculateYieldBySerialNumber();
            //Task.Factory.StartNew(() => CustomImageVM?.LoadImageResult(dieViewModel.chipViewModel.ChipData, dieViewModel.SerialNumber));
        }
        private void NextTestingDie()
        {
            // 标记当前项完成
            if (_currentTestIndex < _testQueue.Count)
            {
                var currentDie = _testQueue[_currentTestIndex].Die;
                currentDie.UnSelected();
            }

            // 推进到下一个测试项
            _currentTestIndex++;
            StartNextTestItem();
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


                // 新增：计算良率（序列号赋值后立即更新）
                CalculateYieldBySerialNumber();
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
        private List<TestItem> _testQueue; // 待测试队列
        private int _currentTestIndex; // 当前测试项索引
        
        private void StartAutoFlow()
        {
            if (SelectedWPFlow == null)
            {
                logger.Error("未选择测试流程（Flow）");
                return;
            }

            // 1. 收集勾选的测试项（带类型）
            _testQueue = GetSelectedTestItems();
            if (_testQueue.Count == 0)
            {
                MessageBox.Show("请先勾选需要测试的项", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. 初始化测试状态
            CustomMappingVM.DisabledInput = IsProcessing = true;
            EnableBtn(false);
            TestingReady();
            _currentTestIndex = 0;

            // 3. 启动第一个测试项
            StartNextTestItem();
        }

        private void StartNextTestItem()
        {
            // 测试完成
            if (_currentTestIndex >= _testQueue.Count)
            {
                StopAutoTest(null);
                MessageBox.Show("所有勾选项测试完成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 获取当前测试项
            var currentTest = _testQueue[_currentTestIndex];
            DieViewModel currentDie = currentTest.Die;
            CurTestDieIdx = TestResults.IndexOf(currentDie);

            // 滚动到当前测试项
            ScrollToItem(currentDie);

            // 4. 根据测试类型执行对应逻辑
            switch (currentTest.TestType)
            {
                case "AOI":
                    aoiService.StartTestingAOI(Timestamp, currentDie, _selectedWPFlow, false);
                    break;
                case "IVL":
                    ivlService.StartTestingIVL(Timestamp, currentDie, _selectedWPFlow);
                    break;
                case "EQE":
                    // 补充EQE测试逻辑（若有对应Service）
                    // eqeService.StartTestingEQE(Timestamp, currentDie, _selectedWPFlow);
                    break;
                case "VAM":
                    // 补充VAM测试逻辑（若有对应Service）
                    // vamService.StartTestingVAM(Timestamp, currentDie, _selectedWPFlow);
                    break;
            }
        }

        /*private bool IsLocalSim = false;
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
        }*/
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
                    else if (SelectedWPFlow.FlowType == CVWaferProberFlowType.IVL_SP)
                    {
                        ivlService.StartTestingIVL(Timestamp, die, _selectedWPFlow);
                    }
                    else if (SelectedWPFlow.FlowType == CVWaferProberFlowType.EQE)
                    {
                        eqeService.StartTestingEQE(Timestamp, die, _selectedWPFlow);
                    }
                    else
                    {
                        //vamService.StartTestingVAM(Timestamp, die, _selectedWPFlow);
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
            CalculateYieldBySerialNumber(); // 自动同步到CustomMappingVM.YieldInfo
                                            // 测试完成后自动导出Summary结果
            AutoExportSummaryResult();
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
            if (_wmProcessor != null)
            {
                _wmProcessor.MeasurementStoped();
            }
            // 新增：计算良率
            CalculateYieldBySerialNumber();
            //_wmProcessor.MeasurementStoped();
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
            BuildSNIndex();//重建索引
            // 同步筛选结果
            if (string.IsNullOrWhiteSpace(SearchSN))
            {
                FilteredTestResults = new ObservableCollection<DieViewModel>(TestResults);
            }
            else
            {
                ExecuteSearch();
            }

            // 新增：计算良率
            CalculateYieldBySerialNumber();
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

                // 确保行完全可见
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
        #region 勾选列表选项后，点击按钮自动依次测试勾选项
        // 定义测试项类型（区分AOI/IVL等）
        private class TestItem
        {
            public DieViewModel Die { get; set; }
            public string TestType { get; set; } // "AOI"/"IVL"/"EQE"/"VAM"
        }
        private List<TestItem> GetSelectedTestItems()
        {
            var testQueue = new List<TestItem>();

            // 遍历所有Die，按【行顺序】收集勾选的测试项
            foreach (var die in TestResults)
            {
                // 按AOI→IVL→EQE→VAM的顺序（可按实际需求调整）
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

        #endregion


        /***********************激活相应面板************************/
        private void ActivateCorrespondingPanel()
        {
            if (SelectedWPFlow == null) return;

            // 空值校验
            if (DockingManager == null || AnchorableSP == null)
            {
                logger.Warn("SP面板未初始化，无法激活");
                return;
            }

            // 根据FlowType激活对应面板
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
                        logger.Warn("AOI面板未初始化，无法激活");
                    }
                    break;
                case CVWaferProberFlowType.IVL_SP: // 光谱（内层索引0）
                    ActivateSpectralInnerTabAction?.Invoke();
                    break;

                case CVWaferProberFlowType.IVL_Camera: // IVLCamera（内层索引5）
                    ActivateIVLCameraInnerTabAction?.Invoke();
                    break;

                case CVWaferProberFlowType.EQE: // EQE（外层索引1）
                    ActivateEQEOuterTabAction?.Invoke();
                    break;

                case CVWaferProberFlowType.VAM: // AOI面板
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

        #region 计算良率
        // 良率核心属性
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
                    // 同步到CustomMappingVM，供ChipMappingControl绑定
                    CustomMappingVM.YieldInfo = value;
                }
            }
        }
        // 新增：基于序列号统计良率的核心方法
        public void CalculateYieldBySerialNumber()
        {
            try
            {
                if (TestResults == null || !TestResults.Any())
                {
                    YieldInfo = "0/0 (0.00%)";
                    return;
                }

                // 筛选已测试芯片（序列号非空）
                var testedDices = TestResults.Where(d => !string.IsNullOrWhiteSpace(d.SerialNumber)).ToList();
                if (!testedDices.Any())
                {
                    YieldInfo = "0/0 (0.00%)";
                    return;
                }

                // 统计成功数
                int successCount = testedDices.Count(d =>
                    d.Status == ChipStatus.OK ||
                    d.Status == ChipStatus.IVL_COMPLETED ||
                    d.Status == ChipStatus.EQE_COMPLETED);

                // 计算良率
                double yieldRate = (double)successCount / testedDices.Count * 100;
                YieldInfo = $"{successCount}/{testedDices.Count} ({yieldRate:F2}%)";


                //// 手动同步（兜底）
                //CustomMappingVM.YieldInfo = YieldInfo;
                //CustomMappingVM.OnPropertyChanged(nameof(CustomMappingVM.YieldInfo));
            }
            catch (Exception ex)
            {
                logger.Error("良率计算异常", ex);
                YieldInfo = "计算异常";
            }
        }
        #endregion

        #region Summary导出配置相关
        /// <summary>
        /// DataGrid实例（用于动态生成列）
        /// </summary>
        private DataGrid _dataGrid1;

        /// <summary>
        /// 所有列配置（含默认列+可选列）
        /// </summary>
        private List<ColumnConfig> _allColumnConfigs;

        /// <summary>
        /// 当前选中的列配置（用于动态生成DataGrid列）
        /// </summary>
        public ObservableCollection<ColumnConfig> SelectedColumnConfigs { get; set; }

        /// <summary>
        /// 打开Summary导出配置窗口命令
        /// </summary>
        public ICommand OpenSummaryConfigCommand { get; }

        /// <summary>
        /// 初始化列配置（严格匹配DieViewModel的属性）
        /// </summary>
        private void InitColumnConfigs()
        {
            // 初始化所有列配置（默认列设为不可选，其他为可选）
            _allColumnConfigs = new List<ColumnConfig>
            {
                // 默认必选列（不可取消）
                new ColumnConfig
                {
                    ColumnHeader = "序号",
                    ColumnBindingPath = "Id",
                    IsSelected = true,
                    IsOptional = false,
                    ColumnType = ColumnType.Text
                },
                new ColumnConfig
                {
                    ColumnHeader = "行",
                    ColumnBindingPath = "MapY",
                    IsSelected = true,
                    IsOptional = false,
                    ColumnType = ColumnType.Text
                },
                new ColumnConfig
                {
                    ColumnHeader = "列",
                    ColumnBindingPath = "MapX",
                    IsSelected = true,
                    IsOptional = false,
                    ColumnType = ColumnType.Text
                },
                // 可选列（严格匹配DieViewModel的属性名）
                new ColumnConfig
                {
                    ColumnHeader = "AOI",
                    ColumnBindingPath = "IsAOIEnabled",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.CheckBox
                },
                new ColumnConfig
                {
                    ColumnHeader = "IVL",
                    ColumnBindingPath = "IsIVLEnabled",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.CheckBox
                },
                new ColumnConfig
                {
                    ColumnHeader = "EQE",
                    ColumnBindingPath = "IsEQEEnabled",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.CheckBox
                },
                new ColumnConfig
                {
                    ColumnHeader = "VAM",
                    ColumnBindingPath = "IsVAMEnabled",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.CheckBox
                },
                new ColumnConfig
                {
                    ColumnHeader = "序列号",
                    ColumnBindingPath = "SerialNumber",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text
                },
                new ColumnConfig
                {
                    ColumnHeader = "测试状态",
                    ColumnBindingPath = "DisplayStatus",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text
                },
                new ColumnConfig
                {
                    ColumnHeader = "均匀性",
                    ColumnBindingPath = "DataValue",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text
                },
                new ColumnConfig
                {
                    ColumnHeader = "开始测试时间",
                    ColumnBindingPath = "StartTestTime",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text
                },
                new ColumnConfig
                {
                    ColumnHeader = "结束测试时间",
                    ColumnBindingPath = "EndTestTime",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text
                },
                new ColumnConfig
                {
                    ColumnHeader = "总耗时",
                    ColumnBindingPath = "TotalTime",
                    IsSelected = false,
                    IsOptional = true,
                    ColumnType = ColumnType.Text
                }
            };

            // 初始化选中列（默认只选必选列）
            SelectedColumnConfigs = new ObservableCollection<ColumnConfig>(
                _allColumnConfigs.Where(c => c.IsSelected));
        }

        /// <summary>
        /// 打开Summary导出配置窗口
        /// </summary>
        private void OpenSummaryConfig()
        {
            var configWindow = new SummaryConfigWindow(_allColumnConfigs);
            if (configWindow.ShowDialog() == true)
            {
                // 更新选中列配置
                SelectedColumnConfigs.Clear();
                foreach (var config in configWindow.SelectedColumns)
                {
                    SelectedColumnConfigs.Add(config);
                    // 同步更新_allColumnConfigs的选中状态（下次打开窗口保留选择）
                    var targetConfig = _allColumnConfigs.First(c => c.ColumnBindingPath == config.ColumnBindingPath);
                    targetConfig.IsSelected = config.IsSelected;
                }
                // 刷新DataGrid列
                UpdateDataGridColumns();
            }
        }

        /// <summary>
        /// 动态更新DataGrid列（适配DieViewModel的属性类型）
        /// </summary>
        public void UpdateDataGridColumns()
        {
            if (_dataGrid1 == null || SelectedColumnConfigs.Count == 0) return;

            // 清空原有列
            _dataGrid1.Columns.Clear();

            // 按配置生成列
            foreach (var config in SelectedColumnConfigs)
            {
                if (config.ColumnType == ColumnType.CheckBox)
                {
                    // CheckBox列（AOI/IVL/EQE/VAM）
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
                    checkBoxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    checkBoxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                    templateColumn.CellTemplate = new DataTemplate { VisualTree = checkBoxFactory };
                    _dataGrid1.Columns.Add(templateColumn);
                }
                else
                {
                    // 文本列（适配可空类型：uint?/int?/DateTime?）
                    _dataGrid1.Columns.Add(new DataGridTextColumn
                    {
                        Header = config.ColumnHeader,
                        Binding = new Binding(config.ColumnBindingPath)
                        {
                            TargetNullValue = "", // 空值显示为空字符串
                            StringFormat = config.ColumnBindingPath.Contains("Time") ? "yyyy-MM-dd HH:mm:ss" : null // 时间列格式化
                        }
                    });
                }
            }

            // 保留行样式（状态颜色）
            var rowStyle = _dataGrid1.RowStyle ?? _dataGrid1.FindResource(typeof(DataGridRow)) as Style;
            if (rowStyle != null)
            {
                _dataGrid1.RowStyle = rowStyle;
            }
        }

        /// <summary>
        /// 自动导出Summary结果（适配DieViewModel的属性）
        /// </summary>
        private void AutoExportSummaryResult()
        {
            try
            {
                if (TestResults == null || !TestResults.Any())
                {
                    logger.Info("无测试结果，跳过Summary导出");
                    return;
                }

                var savePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"Summary_Result_{DateTime.Now:yyyyMMddHHmmss}.csv");

                // 写入CSV（处理中文+可空类型）
                using (var writer = new StreamWriter(savePath, false, System.Text.Encoding.UTF8))
                {
                    // 写入表头
                    var headers = SelectedColumnConfigs.Select(c => c.ColumnHeader).ToList();
                    writer.WriteLine(string.Join(",", headers));

                    // 写入数据行（遍历DieViewModel列表）
                    foreach (var die in TestResults)
                    {
                        var rowData = new List<string>();
                        foreach (var config in SelectedColumnConfigs)
                        {
                            // 获取DieViewModel的属性值（处理可空类型）
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
                                rowData.Add(boolValue ? "是" : "否");
                            }
                            else if (value is DateTime dateTimeValue)
                            {
                                rowData.Add(dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                            else
                            {
                                // 处理数字/字符串类型
                                rowData.Add(value.ToString());
                            }
                        }
                        // 写入行（处理逗号转义）
                        writer.WriteLine(string.Join(",", rowData.Select(d => d.Contains(",") ? $"\"{d}\"" : d)));
                    }
                }

                logger.Info($"Summary结果已自动导出：{savePath}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Summary结果已导出至：{savePath}", "导出成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                logger.Error("Summary结果导出失败", ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"导出失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

       
        #endregion
    }
}
