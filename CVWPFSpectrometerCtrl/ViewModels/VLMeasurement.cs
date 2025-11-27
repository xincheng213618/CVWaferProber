using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CVWaferProber.Core.ViewModels;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class VLMeasurement: ViewModelBase
    {
        private int _no;
        private DateTime _timestamp;
        private double _v;
        private double _l;

        
        public VLMeasurement(int no)
        {
            _no = no;
        }
        public VLMeasurement(int no, DateTime timestamp, double v, double l) : this(no)
        {
            _timestamp = timestamp;
            _l = l;
            _v = v;
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
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
            }
        }
        public double Voltage
        {
            get => _v;
            set
            {
                _v = value;
                OnPropertyChanged(nameof(Voltage));
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
        
      
    }
}
