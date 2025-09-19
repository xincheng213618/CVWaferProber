using ChipMapping.Models;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace ChipMapping.ViewModels
{
    public class ChipMappingControlViewModel : ViewModelBase
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ChipMappingControlViewModel));

        private const int CHIP_COUNT = 5000;
        private readonly Random _random = new Random();
        private readonly DispatcherTimer _renderTimer;
        private double _scale = 1.0;
        private string _statusText = "就绪";
        private string _mousePositionText = "X: 0, Y: 0";
        private double _renderProgress;
        private bool _isRendering;
        //private ChipStatus _filterStatus = ChipStatus.Normal | ChipStatus.Warning | ChipStatus.Error | ChipStatus.Offline;
        private ChipStatus _filterStatus = ChipStatus.OK | ChipStatus.WAITING;

        // 行列布局参数
        private int _rows = 30;
        private int _columns = 40;
        private double _horizontalSpacing = 20;
        private double _verticalSpacing = 16;
        private double _startX = 5;
        private double _startY = 5;
        private int _screenWidth = 640;
        private int _screenHeight = 480;

        private bool selfClick = true;

        private ChipViewModel? _selectedChip;
        private string _chipDetails = "请点击芯片查看详细信息";

        private bool _DisabledInput;
        public bool DisabledInput { get => _DisabledInput; set {
                _DisabledInput = value;
                SelectedChip = null;
                OnPropertyChanged(nameof(DisabledInput));
                OnPropertyChanged(nameof(EnabledInput));

            } }
        public bool EnabledInput => !DisabledInput;

        public ObservableCollection<ChipViewModel> Chips { get; } = new ObservableCollection<ChipViewModel>();
        public ICollectionView FilteredChips { get; }

        public ICommand RefreshCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetCommand { get; }

        public ChipMappingControlViewModel()
        {
            FilteredChips = CollectionViewSource.GetDefaultView(Chips);
            FilteredChips.Filter = ChipFilter;

            RefreshCommand = new RelayCommand(_ => Refresh());
            ZoomInCommand = new RelayCommand(_ => Scale *= 1.1);
            ZoomOutCommand = new RelayCommand(_ => Scale *= 0.9);
            ResetCommand = new RelayCommand(_ => Scale =1);

            _renderTimer = new DispatcherTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(100);
            _renderTimer.Tick += RenderTimer_Tick;

            DisabledInput = false;
            // 初始化画布大小
            UpdateCanvasSize();

            Refresh();
        }

        // 选中芯片ID属性（用于安全绑定）
        public uint? SelectedChipId => SelectedChip?.Id;

        // 选中芯片的显示文本
        public string? SelectedChipDisplay => SelectedChip != null ? SelectedChip.Id.ToString() : "无";

        // 选中芯片属性
        public ChipViewModel? SelectedChip
        {
            get => _selectedChip;
            set
            {
                // 清除之前选中的芯片
                if (_selectedChip != null)
                {
                    _selectedChip.IsSelected = false;
                }

                if (SetProperty(ref _selectedChip, value))
                {
                    // 设置新选中的芯片
                    if (_selectedChip != null)
                    {
                        _selectedChip.IsSelected = true;
                    }
                    UpdateChipDetails();

                    // 通知相关属性变化
                    OnPropertyChanged(nameof(SelectedChipId));
                    OnPropertyChanged(nameof(SelectedChipDisplay));
                }
            }
        }

        // 清理资源的方法
        public void Cleanup()
        {
            // 停止所有芯片的闪烁计时器
            foreach (var chip in Chips)
            {
                chip.StopBlinking();
            }

            if (SelectedChip != null)
            {
                SelectedChip.IsSelected = false;
            }
        }

        // 芯片详细信息
        public string ChipDetails
        {
            get => _chipDetails;
            set => SetProperty(ref _chipDetails, value);
        }

        // 更新芯片详细信息
        private void UpdateChipDetails()
        {
            if (SelectedChip != null)
            {
                ChipDetails = SelectedChip.ToolTip;
            }
            else
            {
                ChipDetails = "请点击芯片查看详细信息";
            }
        }

        // 处理芯片点击
        public void HandleChipClick(System.Windows.Point clickPosition)
        {
            if (!DisabledInput)
            {
                selfClick = true;
                // 查找点击位置附近的芯片
                var clickedChip = FindChipAtPosition(clickPosition);
                SelectedChip = clickedChip;
            }
        }

        public void SetSelectedChip(uint id)
        {
            foreach (var chip in Chips)
            {
                if(chip.Id == id)
                {
                    SelectedChip = chip;
                    break;
                }
            }
        }

        // 在指定位置查找芯片
        private ChipViewModel? FindChipAtPosition(System.Windows.Point position)
        {
            const double clickTolerance = 10.0; // 点击容差范围

            foreach (var chip in Chips)
            {
                double distanceX = Math.Abs(position.X - chip.Position.X);
                double distanceY = Math.Abs(position.Y - chip.Position.Y);

                // 检查是否点击在芯片范围内
                if (distanceX <= chip.Width / 2 + clickTolerance &&
                    distanceY <= chip.Height / 2 + clickTolerance)
                {
                    return chip;
                }
            }

            return null; // 没有找到芯片
        }
        private bool ChipFilter(object item)
        {
            return true;
            if (item is ChipViewModel chip)
            {
                bool bR = FilterStatus.Equals(chip.Status);
                logger.InfoFormat("{0}={1} bR={2}", FilterStatus.ToString(), chip.Status.ToString(), bR);
                return bR;
            }
            return false;
        }

        public double Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, Math.Max(0.1, Math.Min(5.0, value)));
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string MousePositionText
        {
            get => _mousePositionText;
            set => SetProperty(ref _mousePositionText, value);
        }

        public double RenderProgress
        {
            get => _renderProgress;
            set => SetProperty(ref _renderProgress, value);
        }

        public bool IsRendering
        {
            get => _isRendering;
            set => SetProperty(ref _isRendering, value);
        }

        public ChipStatus FilterStatus
        {
            get => _filterStatus;
            set
            {
                if (SetProperty(ref _filterStatus, value))
                {
                    FilteredChips.Refresh();
                }
            }
        }

        // 行列布局属性
        public int Rows
        {
            get => _rows;
            set
            {
                if (SetProperty(ref _rows, value))
                {
                    UpdateCanvasSize();
                    Refresh();
                }
            }
        }

        public int Columns
        {
            get => _columns;
            set
            {
                if (SetProperty(ref _columns, value))
                {
                    UpdateCanvasSize();
                    Refresh();
                }
            }
        }

        public double HorizontalSpacing
        {
            get => _horizontalSpacing;
            set
            {
                if (SetProperty(ref _horizontalSpacing, value))
                {
                    UpdateCanvasSize();
                    Refresh();
                }
            }
        }

        public double VerticalSpacing
        {
            get => _verticalSpacing;
            set
            {
                if (SetProperty(ref _verticalSpacing, value))
                {
                    UpdateCanvasSize();
                    Refresh();
                }
            }
        }

        public double StartX
        {
            get => _startX;
            set
            {
                if (SetProperty(ref _startX, value))
                {
                    Refresh();
                }
            }
        }

        public double StartY
        {
            get => _startY;
            set
            {
                if (SetProperty(ref _startY, value))
                {
                    Refresh();
                }
            }
        }

        // 计算画布大小
        public double CanvasWidth { get; private set; } = 1000;
        public double CanvasHeight { get; private set; } = 1000;

        private void UpdateCanvasSize()
        {
            CanvasWidth = _screenWidth;
            CanvasHeight = _screenHeight;
            OnPropertyChanged(nameof(CanvasWidth));
            OnPropertyChanged(nameof(CanvasHeight));
        }

        private void Refresh()
        {
            this.SelectedChip = null;
            Chips.Clear();
            //GenerateChipData_FromCsv("E:\\work\\cv\\New版\\晶圆台\\CVWaferProber\\ChipMapping\\ScanData_sc.csv");
            //GenerateChipData_Circle();
            StartProgressiveRendering();
        }
        public void RefreshFromCsv(string csvFile)
        {
            this.SelectedChip = null;
            Chips.Clear();
            GenerateChipData_FromCsv(csvFile);
            StartProgressiveRendering();
        } 
        public void RefreshFromMap(List<CVMappingData> mappingData)
        {
            this.SelectedChip = null;
            Chips.Clear();
            GenerateChipData_FromMap(mappingData);
            StartProgressiveRendering();
        }

        private void GenerateChipData_Rect()
        {
            int chipCount = Rows * Columns;
            chipCount = Math.Min(chipCount, CHIP_COUNT); // 不超过最大数量

            for (int i = 0; i < chipCount; i++)
            {
                int row = i / Columns;
                int col = i % Columns;

                double x = StartX + col * HorizontalSpacing;
                double y = StartY + row * VerticalSpacing;

                var status = (ChipStatus)_random.Next(0, 4);
                var lv = _random.Next(30, 100);

                var chipData = new ChipData
                {
                    Id = (uint)i + 1,
                    X = x,
                    Y = y,
                    Status = status,
                    DataValue = lv,
                    Row = row,
                    Column = col
                };

                var chipViewModel = new ChipViewModel(chipData)
                {
                    Width = 12,
                    Height = 9
                };

                Chips.Add(chipViewModel);
            }
        }
        private void GenerateChipData_Circle()
        {

            int chipCount = Rows * Columns;
            chipCount = Math.Min(chipCount, CHIP_COUNT);

            double centerX = StartX + (Columns - 1) * HorizontalSpacing / 2.0;
            double centerY = StartY + (Rows - 1) * VerticalSpacing / 2.0;
            double radius = Math.Min(Columns * HorizontalSpacing, Rows * VerticalSpacing) / 2.0;

            for (int i = 0; i < chipCount; i++)
            {
                int row = i / Columns;
                int col = i % Columns;

                double x = StartX + col * HorizontalSpacing;
                double y = StartY + row * VerticalSpacing;

                // 检查点是否在圆内
                double distanceFromCenter = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                if (distanceFromCenter <= radius)
                {
                    var status = (ChipStatus)_random.Next(0, 4);
                    var lv = _random.Next(30, 100);

                    var chipData = new ChipData
                    {
                        Id = (uint)(i + 1),
                        X = x,
                        Y = y, 
                        Status = status,
                        DataValue = lv,
                        Row = row,
                        Column = col
                    };

                    var chipViewModel = new ChipViewModel(chipData)
                    {
                        Width = 14,
                        Height = 12
                    };

                    Chips.Add(chipViewModel);
                }
            }
        }

        private void GenerateChipData_FromMap(List<CVMappingData> mappingData)
        {
            if (mappingData != null)
            {
                MappingPosDataRange dataRange = CsvMappingDataTool.GetPosDataRange(mappingData);
                var Wid = dataRange.MaxPosX - dataRange.MinPosX;
                var Hei = dataRange.MaxPosY - dataRange.MinPosY;

                // 创建屏幕（假设为800x600像素）
                System.Drawing.Size screenSize = new System.Drawing.Size(_screenWidth-10, _screenHeight-10);

                // 定义数学坐标系范围（x从-10到10，y从-5到5）
                RectangleF mathBounds = new RectangleF((int)dataRange.MinPosX, (int)dataRange.MinPosY, (int)Wid, (int)Hei);
                // 创建坐标转换器
                CoordinateConverter converter = new CoordinateConverter(screenSize, mathBounds);
                int i = 0;
                foreach (var posMath in mappingData)
                {
                    var posSc = converter.MathToScreen(new System.Drawing.Point((int)posMath.PosX, (int)posMath.PosY));

                    double x = StartX + posSc.X;
                    double y = StartY + posSc.Y;

                    var status = (ChipStatus)_random.Next(0, 8);
                    var lv = _random.Next(30, 100);
                    var chipData = new ChipData
                    {
                        Id = posMath.Id,
                        X = x,
                        Y = y,
                        RawX = posMath.AxisPosX,
                        RawY = posMath.AxisPosY,
                        Status = status,
                        DataValue = lv,
                        Row = posMath.MapY,
                        Column = posMath.MapX,
                    };

                    var chipViewModel = new ChipViewModel(chipData)
                    {
                        Width = 12,
                        Height = 9
                    };

                    Chips.Add(chipViewModel);
                    i++;
                }
            }
        }
        private void GenerateChipData_FromCsv(string csvFile)
        {
            List<CVMappingData> mappingData = null;
            if(CsvMappingDataTool.LoadMappingCsv(csvFile, ref mappingData))
            {
                GenerateChipData_FromMap(mappingData);
            }
        }

        private void StartProgressiveRendering()
        {
            RenderProgress = 0;
            IsRendering = true;
            _renderTimer.Start();
        }

        private void RenderTimer_Tick(object? sender, EventArgs e)
        {
            const int BATCH_SIZE = 100;
            int rendered = (int)RenderProgress;
            int toRender = Math.Min(rendered + BATCH_SIZE, Chips.Count);

            var sw = Stopwatch.StartNew();

            // 在实际项目中，这里可以添加渲染逻辑
            // 由于使用数据绑定，渲染由WPF自动处理

            sw.Stop();

            RenderProgress = toRender;
            StatusText = $"渲染中... {toRender}/{Chips.Count} ({sw.ElapsedMilliseconds}ms)";

            if (toRender >= Chips.Count)
            {
                _renderTimer.Stop();
                IsRendering = false;
                StatusText = $"渲染完成 - 共 {Chips.Count} 芯片 ({Rows}行×{Columns}列)";
            }
        }

        public void UpdateMousePosition(System.Windows.Point position)
        {
            MousePositionText = $"X: {position.X:F0}, Y: {position.Y:F0}";
        }
    }
}