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
using System.Windows.Media;
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
        // 新增：状态提示列表（绑定到UI）
        public ObservableCollection<StatusTip> StatusTips { get; } = new ObservableCollection<StatusTip>();
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
            InitStatusTips();
            Refresh();
        }

        #region 颜色状态说明
        // 初始化状态-颜色-说明的映射
        private void InitStatusTips()
        {
            StatusTips.Clear();
            // 对应ChipStatus的每个状态，配置颜色和说明
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.WAITING,
                Color = new SolidColorBrush(Colors.Blue), // 蓝
                Description = "未检测"
            });
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.TESTING,
                Color = new SolidColorBrush(Colors.Yellow), // 黄
                Description = "正在检测"
            });
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.OK,
                Color = new SolidColorBrush(Colors.Green), // 绿
                Description = "检测OK"
            });
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.AOI_NG,
                Color = new SolidColorBrush(Colors.Red), // 红
                Description = "AOI外观检测NG"
            });
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.DW_NG,
                Color = new SolidColorBrush(Colors.Orange), // 橙
                Description = "定位NG"
            });
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.BLIND,
                Color = new SolidColorBrush(Colors.Gray), // 灰
                Description = "完全不亮"
            });
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.CAL_NG,
                Color = new SolidColorBrush(Colors.Purple), // 紫
                Description = "提取失败"
            });
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.I2C_NG,
                Color = new SolidColorBrush(Colors.White), // 白
                Description = "I2C状态异常"
            });
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.AOI_LINE_NG,
                Color = new SolidColorBrush(Colors.Olive), // 橄榄
                Description = "线缺陷检测NG"
            });
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.IVL_TESTING,
                Color = new SolidColorBrush(Colors.LightYellow), // 浅黄
                Description = "IVL正在检测"
            });
            StatusTips.Add(new StatusTip
            {
                Status = ChipStatus.IVL_COMPLETED,
                Color = new SolidColorBrush(Colors.LightGreen), // 浅绿
                Description = "IVL检测完成"
            });
            // 可继续添加其他状态...
        }
        #endregion

        // 1. 新增：芯片选中事件（供上层ViewModel订阅）
        public event EventHandler<ChipViewModel> ChipSelected;

        // 2. 新增：选中芯片的行列信息（供绑定）
        private int? _selectedChipRow;
        public int? SelectedChipRow
        {
            get => _selectedChipRow;
            set => SetProperty(ref _selectedChipRow, value);
        }

        private int? _selectedChipColumn;

        private double _temperatures = 25;
        private string _pressure="0,0,0,0";
        private int _tdCount = 10;
        private string _sn = "54561891";

        public int? SelectedChipColumn
        {
            get => _selectedChipColumn;
            set => SetProperty(ref _selectedChipColumn, value);
        }
        // 选中芯片ID属性（用于安全绑定）
        public uint? SelectedChipId => SelectedChip?.Id;

        // 选中芯片的显示文本
        public string? SelectedChipDisplay => SelectedChip != null ? SelectedChip.Id.ToString() : "null";

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
                        // 同步行列信息
                        SelectedChipRow = _selectedChip.Row;
                        SelectedChipColumn = _selectedChip.Column;
                        // 触发选中事件
                        ChipSelected?.Invoke(this, _selectedChip);
                    }
                    else
                    {
                        // 清空选中时重置行列
                        SelectedChipRow = null;
                        SelectedChipColumn = null;
                        ChipSelected?.Invoke(this, null);
                    }

                    UpdateChipDetails();
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
            SelectedChip = null;
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
            if (SelectedChip != null)
            {
                SelectedChip.IsSelected = false;
            }
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
        //private ChipViewModel? FindChipAtPosition(System.Windows.Point position)
        //{
        //    const double clickTolerance = 10.0; // 点击容差范围

        //    foreach (var chip in Chips)
        //    {
        //        double distanceX = Math.Abs(position.X - chip.Position.X);
        //        double distanceY = Math.Abs(position.Y - chip.Position.Y);

        //        // 检查是否点击在芯片范围内
        //        if (distanceX <= chip.Width / 2 + clickTolerance &&
        //            distanceY <= chip.Height / 2 + clickTolerance)
        //        {
        //            return chip;
        //        }
        //    }

        //    return null; // 没有找到芯片
        //}
        private ChipViewModel? FindChipAtPosition(System.Windows.Point scaledClickPos)
        {
            // 1. 关键：将缩放后的点击坐标转换为原始画布坐标（除以缩放比例）
            var originalPos = new System.Windows.Point(
                scaledClickPos.X / Scale,
                scaledClickPos.Y / Scale
            );

            // 2. 扩大点击容差（边缘芯片友好，可根据需要调整）
            const double clickTolerance = 4.0;

            foreach (var chip in Chips)
            {
                // 3. 计算芯片的实际显示区域（匹配CenterOffsetConverter的居中偏移）
                // 芯片Position是Canvas.Left/Top的原始值，Rectangle通过RenderTransform偏移了 -Width/2 和 -Height/2
                double chipLeft = chip.Position.X - (chip.Width / 2);   // 芯片左边界
                double chipTop = chip.Position.Y - (chip.Height / 2);  // 芯片上边界
                double chipRight = chipLeft + chip.Width;              // 芯片右边界
                double chipBottom = chipTop + chip.Height;             // 芯片下边界

                // 4. 扩大热区（左右上下各加容差）
                double hitLeft = chipLeft - clickTolerance;
                double hitTop = chipTop - clickTolerance;
                double hitRight = chipRight + clickTolerance;
                double hitBottom = chipBottom + clickTolerance;

                // 5. 精准检测点击是否在芯片热区内
                if (originalPos.X >= hitLeft && originalPos.X <= hitRight &&
                    originalPos.Y >= hitTop && originalPos.Y <= hitBottom)
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
        // 温度属性
        public double Temperatures
        {
            get => _temperatures;
            set => SetProperty(ref _temperatures, value);
        }
        public string Pressure
        {
            get => _pressure;
            set => SetProperty(ref _pressure, value);
        }
        public string SN
        {
            get => _sn;
            set => SetProperty(ref _sn, value);
        }
        public int TDCount
        {
            get => _tdCount;
            set => SetProperty(ref _tdCount, value);
        }

        // 新增：良率转发属性（绑定到UI）
        private string _yieldInfo = "0/0 (0.00%)";
        public string YieldInfo
        {
            get => _yieldInfo;
            set => SetProperty(ref _yieldInfo, value);
        }


        // 计算画布大小
        public double CanvasWidth { get; private set; } = 1000;
        public double CanvasHeight { get; private set; } = 1000;

        private void UpdateCanvasSize()
        {
            //CanvasWidth = _screenWidth;
            //CanvasHeight = _screenHeight;
            //OnPropertyChanged(nameof(CanvasWidth));
            //OnPropertyChanged(nameof(CanvasHeight));
            if (Chips.Count > 0)
            {
                double maxX = Chips.Max(c => c.Position.X) + 50; // 预留50px边缘空间
                double maxY = Chips.Max(c => c.Position.Y) + 50;
                CanvasWidth = Math.Max(maxX, _screenWidth);
                CanvasHeight = Math.Max(maxY, _screenHeight);
            }
            else
            {
                CanvasWidth = _screenWidth;
                CanvasHeight = _screenHeight;
            }
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