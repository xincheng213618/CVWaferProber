using CVWaferProber.Core.ViewModels;
using CVWaferProber.Models;
using CVWaferProber.Services;
using System.Windows;
using System.Windows.Input;
using WaferComm.Core;
using MessageBox = System.Windows.MessageBox;

namespace CVWaferProber.ViewModels
{
    public class RcConnectionSettingsViewModel : ViewModelBase
    {
        private readonly ConnectionInfo ConnectionInfo;
        private readonly RCRestService? _proberClient;

        //private string _serverIP = "127.0.0.1";
        public string ServerIP
        {
            get => ConnectionInfo.ServerIP;
            set
            {
                ConnectionInfo.ServerIP = value;
            }
        }

        //private int _port = 8080;
        public int Port
        {
            get => ConnectionInfo.Port;
            set 
            { 
                ConnectionInfo.Port = value;
            }
        }

        public string ConnectionStatusText =>
            ConnectionInfo.Status switch
            {
                ConnectionStatus.Connected => $"Connected ({ConnectionInfo.ServerIP}:{ConnectionInfo.Port})",
                ConnectionStatus.Connecting => "Connecting...",
                ConnectionStatus.Error => "Connection Error",
                _ => "Disconnected"
            };

        public string StatusMessage => ConnectionInfo.StatusMessage;

        public bool CanConnect =>
            ConnectionInfo.Status != ConnectionStatus.Connecting &&
            ConnectionInfo.Status != ConnectionStatus.Connected;

        public bool CanDisconnect =>
            ConnectionInfo.Status == ConnectionStatus.Connected;

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand CloseCommand { get; }

        public RcConnectionSettingsViewModel(RCRestService? restClient)
        {
            _proberClient = restClient;
            this.ConnectionInfo = restClient.ConnectionInfo;
            // Initialize with current connection info
            ServerIP = ConnectionInfo.ServerIP;
            Port = ConnectionInfo.Port;

            SetConnected(_proberClient.IsRegistered);

            // Subscribe to service events
            _proberClient.Subscribe<ConnectionStateChangedEvent>(OnConnectionStatusChanged);
            //_proberClient.EventAggregator.Subscribe<ConnectionStateChangedEvent>(OnConnectionStatusChanged);
            //_tcpClientService.StatusMessage += OnStatusMessageChanged;

            // Initialize commands
            ConnectCommand = new RelayCommand(
                async _ => await ConnectAsync(),
                _ => CanConnect
            );

            DisconnectCommand = new RelayCommand(
                _ => Disconnect(),
                _ => CanDisconnect
            );

            CloseCommand = new RelayCommand(CloseWindow);
        }

        private void SetConnected(bool isConnected)
        {
            ConnectionInfo.SetConnected(isConnected);
        }

        private void OnConnectionStatusChanged(ConnectionStateChangedEvent @event)
        {
            SetConnected(@event.IsConnected);
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanDisconnect));
        }
        private void OnConnectionStatusChanged(object? sender, ConnectionStatus status)
        {
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanDisconnect));
        }

        private void OnStatusMessageChanged(object? sender, string message)
        {
            OnPropertyChanged(nameof(StatusMessage));
        }

        private async Task ConnectAsync()
        {
            if (string.IsNullOrWhiteSpace(ServerIP) || Port < 1 || Port > 65535)
            {
                MessageBox.Show("Please enter valid server address and port", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool bR = _proberClient.RcRegist();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Disconnect()
        {
            _proberClient.RcUnRegist();
        }

        private void CloseWindow(object? parameter)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (parameter is Window window)
                {
                    window.Close();
                }
            });
        }

        public void Cleanup()
        {
            _proberClient.Unsubscribe<ConnectionStateChangedEvent>(OnConnectionStatusChanged);
            //_tcpClientService.StatusMessage -= OnStatusMessageChanged;
        }
    }
}