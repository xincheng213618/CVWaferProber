using CVWaferProber.Core.ViewModels;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class ILMeasurement : ViewModelBase
    {
        private int _no;
        private DateTime _timestamp;
        private double _l;
        private double _i;

        public ILMeasurement(int no)
        {
            _no = no;
        }

        public ILMeasurement(int no, DateTime timestamp, double i, double l) : this(no)
        {
            _timestamp = timestamp;
            _l = l;
            _i = i;
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
            }
        }
        public double Luminance
        {
            get => _l;
            set
            {
                _l = value;
                OnPropertyChanged(nameof(Luminance));
            }
        }
        public double Current
        {
            get => _i;
            set
            {
                _i = value;
                OnPropertyChanged(nameof(Current));
            }
        }
        public int No
        {
            get => _no;
            set
            {
                SetProperty(ref _no, value);
            }
        }
    }
}
