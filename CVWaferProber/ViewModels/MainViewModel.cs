using ChipMapping.Models;
using ChipMapping.Models.Enums;
using ChipMapping.Models.HZCC;
using ChipMapping.ViewModels;
using CVWaferProber.Views;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace CVWaferProber.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public static MainViewModel? Instance { get; private set; }
        public ChipMappingControlViewModel CustomVM { get; set; }
        public ICommand LoadMappingFileCommand { get; }
        public ICommand ClearMappingCommand { get; }
        public ICommand StartAutoTestCommand { get; }
        public ICommand StopAutoTestCommand { get; }
        public ICommand OpenMappingFileCommand { get; }
        public ICommand RefreshStatusCommand { get; }
        public ObservableCollection<DieViewModel> TestResults { get; } = new ObservableCollection<DieViewModel>();
        public string MappingCsvFilePath { get; set; }
        public bool IsColorEnabled { get; set; }

        private bool _isProcessing = false;

        public bool IsNotProcessing => !_isProcessing;
        public bool IsProcessing { 
            get => _isProcessing;
            set 
            {
                _isProcessing = value;
                OnPropertyChanged(nameof(IsProcessing));
            } 
        }

        private readonly Random _random = new Random();

        private DataGrid? _dataGrid; // 引用DataGrid
        private DispatcherTimer? _simAutoTestTimer;
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
                        if (selfClick) CustomVM.SetSelectedChip((uint)die.Id);
                        else selfClick = true;
                    }
                }
            }
        }
        public MainViewModel()
        {
            Instance = this;
            _selectedItem = null;
            _dataGrid = null;
            CustomVM = new ChipMappingControlViewModel();
            RefreshStatusCommand = new RelayCommand(RefreshStatus);
            OpenMappingFileCommand = new RelayCommand(OpenMappingFile);
            StartAutoTestCommand = new RelayCommand(StartAutoTest);
            StopAutoTestCommand = new RelayCommand(StopAutoTest);
            LoadMappingFileCommand = new RelayCommand(_ => LoadMappingFileFromCsv());
            ClearMappingCommand = new RelayCommand(_ => ClearMapping());
            MappingCsvFilePath = "E:\\work\\cv\\New版\\晶圆台\\CVWaferProber\\ChipMapping\\ScanData_sc.csv";

            InitializeSimAutoTestTimer();

            LoadMappingFileFromCsv();
        }

        private void RefreshStatus(object? obj)
        {
            _dataGrid?.Items.Refresh();
        }

        private void InitializeSimAutoTestTimer()
        {
            _simAutoTestTimer = new DispatcherTimer();
            _simAutoTestTimer.Interval = TimeSpan.FromMilliseconds(3000); // 500ms闪烁一次
            _simAutoTestTimer.Tick += SimAutoTestTimer_Tick;
        }
        /// <summary>
        /// 
        /// </summary>
        private int CurTestDieIdx = 0;
        private void SimAutoTestTimer_Tick(object? sender, EventArgs e)
        {
            var status = (ChipStatus)_random.Next(2, 4);
            TestResults[CurTestDieIdx].ChangeStatus(status,true);
            TestResults[CurTestDieIdx].UnSelected();
            CurTestDieIdx++;
            TestResults[CurTestDieIdx].ChangeStatus(ChipStatus.TESTING);
            var itemToSelect = TestResults[CurTestDieIdx];
            ScrollToItem(itemToSelect);
        }

        private void StartSim()
        {
            foreach (var item in CustomVM.Chips)
            {
                item.SetStatus(ChipStatus.WAITING);
            }
            foreach (var item in TestResults)
            {
                item.TestTime = null;
            }

            _dataGrid?.Items.Refresh();

            CurTestDieIdx = 0;
            _simAutoTestTimer?.Start();
            TestResults[CurTestDieIdx].ChangeStatus(ChipStatus.TESTING);
        }
        private void StartAutoTest(object? obj)
        {
            CustomVM.DisabledInput = IsProcessing = true;
            EnableBtn(false);
            StartSim();
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
            CustomVM.DisabledInput = IsProcessing = false;
            EnableBtn(true);
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
                MappingCsvFilePath = System.IO.Path.GetFileName(openFileDialog.FileName);
                LoadMappingFileFromCsv();
            }
        }

        private void ClearMapping()
        {
            CustomVM.SelectedChip = null;
            CustomVM.Chips.Clear();
            TestResults.Clear();
        }

        private void LoadMappingFileFromCsv()
        {
            List<CVMappingData> mappingData = null;
            //HZCCS2000MappingData data = new HZCCS2000MappingData();
            //HZCCS2000MappingDataTool.LoadMapping("D:\\work\\cv\\CVWaferProber\\ChipMapping\\WaferDevice.cc", ref data);
            var dataMapping = S2000MappingDataReader.Read("E:\\work\\cv\\New版\\晶圆台\\src\\CVWaferProber\\ChipMapping\\WaferDevice.cc");
            // 转换Die数据为ViewModel
            List<ChipMapping.Models.HZCC.DieViewModel> _dieViewModels = dataMapping.DieTestResults?.Select((die, index) => DieDataConverter.ConvertToViewModel(die, index)).ToList();
            //new List<DieViewModel>();
            bool bR = CsvMappingDataTool.LoadMappingCsv(MappingCsvFilePath, ref mappingData);
            if (bR && mappingData != null && mappingData.Count > 0)
            {
                CustomVM.RefreshFromMap(mappingData);
                ObservableCollection<DieViewModel> _TestResults = new ObservableCollection<DieViewModel>();
                foreach (var map in CustomVM.Chips)
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

        //private MainWindow _mainWindow;
        //public void SetMainWin(MainWindow mainWindow)
        //{
        //    this._mainWindow = mainWindow;
        //}
    }
}
