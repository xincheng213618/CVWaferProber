using CVWaferProber.Core.ViewModels;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class SpectrumMeasurement : ViewModelBase
    {
        private DateTime _timestamp;
        private string _measurementId;
        /// <summary>
        /// 主波长
        /// </summary>
        private float _peakWavelength;
        /// <summary>
        /// 峰值波长
        /// </summary>
        private float _peakIntensity;
        /// <summary>
        /// 亮度
        /// </summary>
        private float _luminance;
        /// <summary>
        /// IP
        /// </summary>
        private string _IP;
        private float _cie_x;
        private float _cie_y;
        private float _cie_u;
        private float _cie_v;
        private float _CCT;
        private int _dataPoints;
        private float _v;
        private float _i;

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
            }
        }

        public string MeasurementId
        {
            get => _measurementId;
            set
            {
                _measurementId = value;
                OnPropertyChanged(nameof(MeasurementId));
            }
        }

        public float PeakWavelength
        {
            get => _peakWavelength;
            set
            {
                _peakWavelength = value;
                OnPropertyChanged(nameof(PeakWavelength));
            }
        }

        public float PeakIntensity
        {
            get => _peakIntensity;
            set
            {
                _peakIntensity = value;
                OnPropertyChanged(nameof(PeakIntensity));
            }
        }

        public float Luminance
        {
            get => _luminance;
            set
            {
                _luminance = value;
                OnPropertyChanged(nameof(Luminance));
            }
        }
        public string IP
        {
            get => _IP;
            set
            {
                _IP = value;
                OnPropertyChanged(nameof(IP));
            }
        }

        public int DataPoints
        {
            get => _dataPoints;
            set
            {
                _dataPoints = value;
                OnPropertyChanged(nameof(DataPoints));
            }
        }
        public float CIE_u
        {
            get => _cie_u;
            set
            {
                _cie_u = value;
                OnPropertyChanged(nameof(CIE_u));
            }
        } 
        public float CIE_v
        {
            get => _cie_v;
            set
            {
                _cie_v = value;
                OnPropertyChanged(nameof(CIE_v));
            }
        }  
        public float CIE_x
        {
            get => _cie_x;
            set
            {
                _cie_x = value;
                OnPropertyChanged(nameof(CIE_x));
            }
        } 
        public float CIE_y
        {
            get => _cie_y;
            set
            {
                _cie_y = value;
                OnPropertyChanged(nameof(CIE_y));
            }
        } 
        public float CCT
        {
            get => _CCT;
            set
            {
                SetProperty(ref _CCT, value);
            }
        }
        public float V
        {
            get => _v;
            set
            {
               SetProperty(ref _v, value);
            }
        }
        public float I
        {
            get => _i;
            set
            {
               SetProperty(ref _i, value);
            }
        }

        // 原始光谱数据（不显示在DataGrid中）
        public float[] Wavelengths { get; set; }
        public float[] Intensities { get; set; }
    }
}
