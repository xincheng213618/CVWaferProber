using CVWaferProber.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class Power_LMeasurement : ViewModelBase
    {
        private int _no;
        private DateTime _timestamp;
        private double _v; // Voltage (V)
        private double _i; // Current (mA)
        private double _l; // Luminance

        // 移除无用的 _power 字段
        // private double _power;

        public Power_LMeasurement(int no)
        {
            _no = no;
        }

        // 修正构造函数：传入 Voltage 和 Current，用于计算 Power
        public Power_LMeasurement(int no, DateTime timestamp, double voltage, double current, double luminance) : this(no)
        {
            _timestamp = timestamp;
            _v = voltage;
            _i = current;
            _l = luminance;
        }

        public int No
        {
            get => _no;
            set => SetProperty(ref _no, value);
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
                OnPropertyChanged(nameof(Power)); // Power依赖Current，同步通知更新
            }
        }

        public double Voltage
        {
            get => _v;
            set
            {
                _v = value;
                OnPropertyChanged(nameof(Voltage));
                OnPropertyChanged(nameof(Power)); // Power依赖Voltage，同步通知更新
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

        // 纯计算属性：Power (W) = Voltage (V) * Current (mA) / 1000
        public double Power
        {
            get => (_i * _v) / 1000; // 直接计算，无需私有字段
        }
    }
}
