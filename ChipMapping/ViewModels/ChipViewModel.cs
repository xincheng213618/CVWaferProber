using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.ViewModels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChipMapping.ViewModels
{
    public class ChipViewModel : ViewModelBase
    {
        private ChipData? _chipData;
        public ChipData? ChipData { get => _chipData; private set { _chipData = value; } }

        private Point _position;
        private Point _raw_position;
        //private Point _map_position;
        private double _width = 6;
        private double _height = 3;
       private double _width_old = 6;
        private double _height_old = 3;
        private bool _isSelected;
        private bool _isBlinking;
        private DispatcherTimer? _blinkTimer;
        private SolidColorBrush? _originalFill;

        public ChipViewModel(ChipData chipData)
        {
            _chipData = chipData;
            Position = new Point(chipData.X, chipData.Y);
            RawPosition = new Point(chipData.RawX, chipData.RawY);
            //MapPosition = new Point(chipData.Column, chipData.Row);
            InitializeBlinkTimer();
        }

        public uint? Id => _chipData?.Id;
        public ChipStatus? Status => _chipData?.Status;
        public int? Row => _chipData?.Row;
        public double? DataValue => _chipData?.DataValue;
        public int? Column => _chipData?.Column;

        public Point RawPosition
        {
            get => _raw_position;
            set => SetProperty(ref _raw_position, value);
        }
        public Point Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    if (_isSelected)
                    {
                        _width_old = _width;
                        _height_old = _height;
                        Width += 5;
                        Height += 5;
                        StartBlinking();
                    }
                    else
                    {
                        StopBlinking();
                        Width = _width_old;
                        Height = _height_old;
                    }
                }
            }
        }

        public bool IsBlinking
        {
            get => _isBlinking;
            set => SetProperty(ref _isBlinking, value);
        }

        // 闪烁时的填充颜色
        public SolidColorBrush? BlinkFill { get; private set; }

        private void InitializeBlinkTimer()
        {
            _blinkTimer = new DispatcherTimer();
            _blinkTimer.Interval = TimeSpan.FromMilliseconds(500); // 500ms闪烁一次
            _blinkTimer.Tick += BlinkTimer_Tick;
        }

        private void StartBlinking()
        {
            // 保存原始颜色
            _originalFill = GetStatusBrush(Status);
            BlinkFill = new SolidColorBrush(Colors.White);
            _blinkTimer?.Start();
        }

        public void StopBlinking()
        {
            _blinkTimer?.Stop();
            IsBlinking = false;
        }

        public void SetStatus(ChipStatus status)
        {
            _chipData.Status = status;
            if (status == ChipStatus.WAITING) { _chipData.DataValue = null; }
            OnPropertyChanged(nameof(Status));         
        }
        private void BlinkTimer_Tick(object? sender, System.EventArgs e)
        {
            IsBlinking = !IsBlinking;
        }

        private SolidColorBrush GetStatusBrush(ChipStatus? status)
        {
            return ChipStatusTool.GetStatusBrush(status);
        }

        public SolidColorBrush? GetCurrentFill()
        {
            if (IsSelected && IsBlinking)
            {
                return BlinkFill;
            }
            return GetStatusBrush(Status);
        }

        public string ToolTip =>
            $"Die ID: {Id}\n" +
            $"Row/Y,Col/X: \n({Row}, {Column})\n" +
            $"Screen(X,Y): \n({Position.X:F0}, {Position.Y:F0})\n" +
            $"Original(X,Y): \n({RawPosition.X:F3}, {RawPosition.Y:F3})\n" +
            $"Status: {Status}\n" +
            $"Brightness: {string.Format("{0:F4}",DataValue)}\n";
            //$"尺寸: {Width}×{Height}";
    }
}