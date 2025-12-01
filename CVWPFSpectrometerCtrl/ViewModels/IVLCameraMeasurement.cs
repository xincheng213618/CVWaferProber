using CVWaferProber.Core.ViewModels;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class IVLCameraMeasurement : ViewModelBase
    {
        private int _no;
        private DateTime _timestamp;
        private string _filename;
        private string _serialNumber;
        private float? _v;
        private float? _i;
        private float? _luminance;
        public IVLCameraMeasurement(int no)
        {
            _no = no;
        }

        public int No
        {
            get => _no;
            set
            {
              SetProperty(ref _no, value);
            }
        }
        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                SetProperty(ref _timestamp, value);
            }
        }
        public string ImageFile
        {
            get => _filename;
            set
            {
                SetProperty(ref _filename, value);
            }
        }
        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                SetProperty(ref _serialNumber, value);
            }
        }
        public float? V
        {
            get => _v;
            set
            {
                SetProperty(ref _v, value);
            }
        }
        public float? I
        {
            get => _i;
            set
            {
                SetProperty(ref _i, value);
            }
        }
        public float? Luminance
        {
            get => _luminance;
            set
            {
                SetProperty(ref _luminance, value);
            }
        }
        
    }
}
