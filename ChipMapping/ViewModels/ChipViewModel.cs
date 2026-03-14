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
        public ChipData? ChipData { get => _chipData; set { _chipData = value; } }
        
        private static readonly SolidColorBrush _selectedBrush = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush _focusedBrush = new SolidColorBrush(Color.FromRgb(0, 200, 255));
        private Point _position;
        private Point _raw_position;
        //private Point _map_position;
        private double _width = 6;
        private double _height = 3;
        private double _width_old = 6;
        private double _height_old = 3;
        private bool _isSelected;
        private bool _isFocused;
        //private bool _isBlinking;
        //private DispatcherTimer? _blinkTimer;
        //private SolidColorBrush? _originalFill;

        public ChipViewModel(ChipData chipData)
        {
            _chipData = chipData;
            Position = new Point(chipData.X, chipData.Y);
            RawPosition = new Point(chipData.RawX, chipData.RawY);
            //MapPosition = new Point(chipData.Column, chipData.Row);
            //InitializeBlinkTimer();
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
                    OnPropertyChanged(nameof(GetCurrentFill));
                }
            }
        }

        public bool IsFocused
        {
            get => _isFocused;
            set
            {
                if (SetProperty(ref _isFocused, value))
                {
                    if (_isFocused)
                    {
                        _width_old = _width;
                        _height_old = _height;
                        Width += 2;
                        Height += 2;
                    }
                    else
                    {
                        Width = _width_old;
                        Height = _height_old;
                    }
                    OnPropertyChanged(nameof(GetCurrentFill));
                }
            }
        }
        public void SetStatus(ChipStatus status)
        {
            _chipData.Status = status;
            if (status == ChipStatus.WAITING) { _chipData.DataValue = null; }
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(GetCurrentFill)); // 状态改变时也更新颜色
        }


        public SolidColorBrush? GetCurrentFill()
        {
            if (IsFocused)
            {
                return _focusedBrush;
            }
            if (IsSelected)
            {
                return _selectedBrush;
            }
            return ChipStatusTool.GetStatusBrush(Status);
        }

        public string ToolTip =>
            //$"Die ID: {Id}\n" +
            $"Row/Y,Col/X: \n({Row}, {Column})\n" +
            //$"Screen(X,Y): \n({Position.X:F0}, {Position.Y:F0})\n" +
            //$"Original(X,Y): \n({RawPosition.X:F3}, {RawPosition.Y:F3})\n" +
            $"Status: {Status}\n" +
            $"Uniformity: {string.Format("{0:F4}",DataValue)}\n";
            //$"尺寸: {Width}×{Height}";
    }
}