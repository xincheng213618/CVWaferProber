using ColorVision.Services.Proxy;
using CVWaferProber.Core.ViewModels;
using System;

namespace CVWaferProber.ViewModels
{
    public class DeviceItemViewModel : ViewModelBase
    {
        private readonly PhysicDeviceProxy _device;
        private bool _isSelected;
        //private DateTime _lastActivityTime;
        public DeviceItemViewModel(PhysicDeviceProxy device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _device.OnStatusChanged += _device_StatusChanged;
        }

        private void _device_StatusChanged(object? sender, EventArgs e)
        {
            UpdateFromDevice();
        }

        public string DeviceCode => _device.DeviceCode;
        public string DeviceName => _device.DeviceName;

        public string DeviceStatus => _device.DeviceStatus;
        //{
        //    get => _device.DeviceStatus;
        //    set
        //    {
        //        if (_device.DeviceStatus != value)
        //        {
        //            _device.DeviceStatus = value;
        //            OnPropertyChanged();
        //            OnPropertyChanged(nameof(StatusColor));
        //            OnPropertyChanged(nameof(StatusText));
        //            OnPropertyChanged(nameof(CanOpen));
        //            OnPropertyChanged(nameof(CanClose));
        //        }
        //    }
        //}

        public bool IsLive => _device.IsLive;
        //{
        //    get => _device.IsLive;
        //    set
        //    {
        //        if (_device.IsLive != value)
        //        {
        //            _device.IsLive = value;
        //            OnPropertyChanged();
        //            OnPropertyChanged(nameof(StatusColor));
        //            OnPropertyChanged(nameof(StatusText));
        //            OnPropertyChanged(nameof(CanOpen));
        //            OnPropertyChanged(nameof(CanClose));
        //        }
        //    }
        //}

        public string ServiceCode => _device.ServiceCode;
        public string UpChannel => _device.UpChannel;
        //public string DownChannel => _device.DownChannel;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastActivityTime => _device.LastLiveTime;
        //{
        //    get => _device.LastLiveTime;
        //    private set
        //    {
        //        _device.LastLiveTime = value;
        //        OnPropertyChanged();
        //        OnPropertyChanged(nameof(LastActivityTimeDisplay));
        //    }
        //}

        public string LastActivityTimeDisplay => LastActivityTime.ToString("yyyy-MM-dd HH:mm:ss");

        public string StatusText => IsLive ? "在线" : "离线";
        public string StatusColor => IsLive ? "#4CAF50" : "#F44336";

        // 打开按钮可用性：
        // 1. 设备必须在线 (IsLive = true)
        // 2. 设备状态必须是 "Closed" 或 "已关闭"
        public bool CanOpen => _device.CanOpen;

        // 关闭按钮可用性：
        // 1. 设备必须在线 (IsLive = true)
        // 2. 设备状态必须是 "Opened" 或 "运行中"
        public bool CanClose => _device.CanClose;


        public void Open()
        {
            _device.Open();
        }
        public void Close()
        {
            _device.Close();
        }
        public void UpdateFromDevice()
        {
            OnPropertyChanged(nameof(LastActivityTimeDisplay));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanOpen));
            OnPropertyChanged(nameof(CanClose));
            OnPropertyChanged(nameof(DeviceStatus));
            OnPropertyChanged(nameof(IsLive));
        }
    }
}
