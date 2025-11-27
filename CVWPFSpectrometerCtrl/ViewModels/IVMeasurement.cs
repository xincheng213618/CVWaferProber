using CVWaferProber.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class IVMeasurement : ViewModelBase
    {
        private int _no;
        private DateTime _timestamp;
        private double _v;
        private double _i;
        public IVMeasurement(int no)
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
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
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
        public double Voltage
        {
            get => _v;
            set
            {
                _v = value;
                OnPropertyChanged(nameof(Voltage));
            }
        }
        
      
        
    }
}
