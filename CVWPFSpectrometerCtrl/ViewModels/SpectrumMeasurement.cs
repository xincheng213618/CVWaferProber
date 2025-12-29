using CVWaferProber.Core.ViewModels;
using OxyPlot;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class SpectrumMeasurement : ViewModelBase
    {
        private DateTime _timestamp;
        private int _no;
        public SpectrumMeasurement(int no)
        {
            _no = no;
        }

        private string _Meas_Id;
        private float _v;
        private float _i;
        /// <summary>
        /// 亮度
        /// </summary>
        private float _luminance;
        /// <summary>
        /// IP
        /// </summary>
        private string _IP;
        private float _Blue;
        private float _cie_x;
        private float _cie_y;
        private float _cie_u;
        private float _cie_v;
        private float _CCT;
        /// <summary>
        /// 主波长
        /// </summary>
        private float _peakWavelength;
        private float _fPur;
        private float _fPlambda;
       
        /// <summary>
        /// 峰值波长
        /// </summary>
        private float _peakIntensity;
        
        
        private int _dataPoints;

        private float _fHW;
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
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
            }
        }
        public string Meas_Id
        {
            get => _Meas_Id;
            set
            {
                _Meas_Id = value;
                OnPropertyChanged(nameof(_Meas_Id));
            }
        }
       
        public float Voltage
        {
            get => _v;
            set
            {
                SetProperty(ref _v, value);
            }
        }
        public float Current
        {
            get => _i;
            set
            {
                SetProperty(ref _i, value);
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
        public float Blue
        {
            get => _Blue;
            set
            {
                _Blue = value;
                OnPropertyChanged(nameof(Blue));
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
        public float CCT
        {
            get => _CCT;
            set
            {
                SetProperty(ref _CCT, value);
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
        public float fPur
        {
            get => _fPur;
            set
            {
                SetProperty(ref _fPur, value);
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
        public float fPlambda
        {
            get => _fPlambda;
            set
            {
                _fPlambda = value;
                OnPropertyChanged(nameof(fPlambda));
            }
        }
        public float FHW
        {
            get => _fHW;
            set
            {
                _fHW = value;
                OnPropertyChanged(nameof(FHW));
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


       
        private OxyColor _rowLineColor = OxyColors.Red; // 默认红色
        /// <summary>
        /// 该行专属的线条颜色（勾选显示所有数据时生效）
        /// </summary>
        public OxyColor RowLineColor
        {
            get => _rowLineColor;
            set
            {
                _rowLineColor = value;
                OnPropertyChanged(nameof(RowLineColor));
            }
        }
        // 原始光谱数据（不显示在DataGrid中）
        public float[] Wavelengths { get; set; }
        public float[] Intensities { get; set; }
        public float RelativeSpectrum { get; set; }
        public float AbsoluteSpectrum { get;  set; }
    }
}
