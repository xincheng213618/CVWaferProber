using ColorVision.Services.Proxy;
using CVWaferProber.Core.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace CVWaferProber.ViewModels
{
    public class DeviceListViewModel : ViewModelBase
    {
        private ObservableCollection<DeviceItemViewModel> _devices;
        private ICollectionView _filteredDevices;
        private DeviceItemViewModel _selectedDevice;
        private string _searchText = string.Empty;
        private bool _showOnlineOnly;
        private string _statusMessage = "就绪";

        public DeviceListViewModel()
        {
            Devices = new ObservableCollection<DeviceItemViewModel>();
            _filteredDevices = CollectionViewSource.GetDefaultView(Devices);
            _filteredDevices.Filter = FilterDevices;

            OpenDeviceCommand = new RelayCommand(OpenDevice, CanOpenDevice);
            CloseDeviceCommand = new RelayCommand(CloseDevice, CanCloseDevice);
            RefreshCommand = new RelayCommand(RefreshDevices);
        }

        public ObservableCollection<DeviceItemViewModel> Devices
        {
            get => _devices;
            set
            {
                _devices = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(OnlineCount));
                OnPropertyChanged(nameof(OfflineCount));
            }
        }

        public ICollectionView FilteredDevices => _filteredDevices;

        public DeviceItemViewModel SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                _selectedDevice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDeviceSelected));
                UpdateStatusMessage();
            }
        }

        public bool IsDeviceSelected => SelectedDevice != null;

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilteredDevices.Refresh();
                UpdateStatusMessage();
            }
        }

        public bool ShowOnlineOnly
        {
            get => _showOnlineOnly;
            set
            {
                _showOnlineOnly = value;
                OnPropertyChanged();
                FilteredDevices.Refresh();
                UpdateStatusMessage();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public int TotalCount => Devices.Count;
        public int OnlineCount => Devices.Count(d => d.IsLive);
        public int OfflineCount => Devices.Count(d => !d.IsLive);

        public ICommand OpenDeviceCommand { get; }
        public ICommand CloseDeviceCommand { get; }
        public ICommand RefreshCommand { get; }

        private bool FilterDevices(object item)
        {
            if (item is not DeviceItemViewModel device)
                return false;

            // 在线过滤
            if (ShowOnlineOnly && !device.IsLive)
                return false;

            // 搜索文本过滤
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                return device.DeviceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       device.DeviceCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       device.ServiceCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private bool CanOpenDevice(object parameter)
        {
            var device = parameter as DeviceItemViewModel ?? SelectedDevice;
            return device != null && device.CanOpen;
        }

        private void OpenDevice(object parameter)
        {
            var device = parameter as DeviceItemViewModel ?? SelectedDevice;
            if (device != null)
            {
                //device.IsLive = true;
                //device.DeviceStatus = "运行中";

                //StatusMessage = $"设备 {device.DeviceName} 已打开";
                FilteredDevices.Refresh();
                OnPropertyChanged(nameof(OnlineCount));
                OnPropertyChanged(nameof(OfflineCount));
            }
        }

        private bool CanCloseDevice(object parameter)
        {
            var device = parameter as DeviceItemViewModel ?? SelectedDevice;
            return device != null && device.CanClose;
        }

        private void CloseDevice(object parameter)
        {
            var device = parameter as DeviceItemViewModel ?? SelectedDevice;
            if (device != null)
            {
                //device.IsLive = false;
                //device.DeviceStatus = "已关闭";

                //StatusMessage = $"设备 {device.DeviceName} 已关闭";
                FilteredDevices.Refresh();
                OnPropertyChanged(nameof(OnlineCount));
                OnPropertyChanged(nameof(OfflineCount));
            }
        }

        private void RefreshDevices(object parameter)
        {
            StatusMessage = "正在刷新设备列表...";

            // 模拟刷新操作
            Task.Delay(1000).ContinueWith(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                 {
                     FilteredDevices.Refresh();
                     StatusMessage = $"已刷新设备列表，共 {TotalCount} 台设备，{OnlineCount} 台在线";
                     OnPropertyChanged(nameof(OnlineCount));
                     OnPropertyChanged(nameof(OfflineCount));
                 });
            });
        }

        private void UpdateStatusMessage()
        {
            if (SelectedDevice != null)
            {
                StatusMessage = $"已选择: {SelectedDevice.DeviceName}";
            }
            else
            {
                var filteredCount = FilteredDevices.Cast<object>().Count();
                StatusMessage = $"显示 {filteredCount} 台设备 (共 {TotalCount} 台)";
            }
        }

        public void AddDevice(PhysicDeviceProxy device)
        {
            var vm = new DeviceItemViewModel(device);
            Devices.Add(vm);
            FilteredDevices.Refresh();
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(OnlineCount));
            OnPropertyChanged(nameof(OfflineCount));
            UpdateStatusMessage();
        }

        public void RemoveDevice(string deviceCode)
        {
            var device = Devices.FirstOrDefault(d => d.DeviceCode == deviceCode);
            if (device != null)
            {
                Devices.Remove(device);
                FilteredDevices.Refresh();
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(OnlineCount));
                OnPropertyChanged(nameof(OfflineCount));
                UpdateStatusMessage();
            }
        }

        public void LoadDevices(List<PhysicDeviceProxy>? pro_devices)
        {
            if (pro_devices == null) return;
            foreach (var pro_device in pro_devices)
            {
                var vm = new DeviceItemViewModel(pro_device);
                _devices.Add(vm);
            }
        }
    }
}
