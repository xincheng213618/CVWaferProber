using CVWaferProber.Config;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Models;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Input;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.StateMachine;
using MessageBox = System.Windows.MessageBox;

namespace CVWaferProber.ViewModels
{
    public class DevProberDebugViewModel : ViewModelBase
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(DevProberDebugViewModel));

        private IWaferProberClient _client;
        private IStateMachine _stateMachine;
        private ConnectionInfo ConnectionInfo;

        public IWaferProberClient ProberClient { get => _client; }
        public IStateMachine StateMachine { get => _stateMachine; }
        public ICommand DevProberConnectCommand { get; }
        public ICommand DevProberDisconnectCommand { get; }
        public ICommand ManualStatusUpdateCommand { get; }
        public ICommand? ResetStateMachineCommand { get; }
        public ICommand SendAbsoluteMoveCommand { get; }
        public ICommand SendBasicCommand { get; }
        public ICommand SendCustomCommand { get; }
        public ICommand SetTemperatureCommand { get; }
        public ICommand GetCurrentTempCommand { get; }
        public ICommand GetHeaterStatusCommand { get; }
        public ICommand StartHeaterMonitorCommand { get; }
        public ICommand StopHeaterMonitorCommand { get; }
        public ICommand? ResetMotionCommand { get; }

        private decimal _Temperature = 25.0M;
        public decimal Temperature
        {
            get => _Temperature;
            set
            {
                SetProperty(ref _Temperature, value);
            }
        }
        private string _CustomCMD = "B";
        public string CustomCMD
        {
            get => _CustomCMD;
            set
            {
                SetProperty(ref _CustomCMD, value);
            }
        }
        private string _AbsAxisY = "+020";
        public string AbsAxisY
        {
            get => _AbsAxisY;
            set
            {
                SetProperty(ref _AbsAxisY, value);
            }
        }
        private string _AbsAxisX = "-020";
        public string AbsAxisX
        {
            get => _AbsAxisX;
            set
            {
                SetProperty(ref _AbsAxisX, value);
            }
        }
        public string ServerIP
        {
            get => ConnectionInfo.ServerIP;
            set
            {
                ConnectionInfo.ServerIP = value;
            }
        }

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

        public bool CanConnect =>
            ConnectionInfo.Status != ConnectionStatus.Connecting &&
            ConnectionInfo.Status != ConnectionStatus.Connected;

        public bool CanDisconnect =>
            ConnectionInfo.Status == ConnectionStatus.Connected;

        public DevProberDebugViewModel(IWaferProberClient proberClient, IStateMachine stateMachine, ConnectionInfo connectionInfo)
        {
            this._client = proberClient;
            this._stateMachine = stateMachine;
            this.ConnectionInfo = connectionInfo;

            ResetMotionCommand = null;
            ResetStateMachineCommand = null;

            DevProberConnectCommand = new RelayCommand(
                async _ => await ConnectAsync(),
                _ => CanConnect);

            DevProberDisconnectCommand = new RelayCommand(
                _ => Disconnect(),
                _ => CanDisconnect);

            ManualStatusUpdateCommand = new RelayCommand(
                _ => ManualStatusUpdate(),
                _ => CanDisconnect);
            //ResetStateMachineCommand = new RelayCommand(
            //    _ => ResetStateMachine(),
            //    _ => CanDisconnect
            //    );
            SendAbsoluteMoveCommand = new RelayCommand(
                _ => SendAbsoluteMove(),
                _ => CanDisconnect);

            SendCustomCommand = new RelayCommand(
                _ => SendCustomCmd(),
                _ => CanDisconnect);

            SetTemperatureCommand = new RelayCommand(
                _ => SetTemperature(),
                _ => CanDisconnect);

            GetCurrentTempCommand = new RelayCommand(
                _ => GetCurrentTemp(),
                _ => CanDisconnect);

            GetHeaterStatusCommand = new RelayCommand(
                _ => GetHeaterStatus(),
                _ => CanDisconnect);

            SendBasicCommand = new RelayCommand(
                (obj) => SendBasicCmd(obj),
                _ => CanDisconnect);

            StartHeaterMonitorCommand = new RelayCommand(
                _ => StartHeaterMonitor(),
                _ => CanDisconnect);

            StopHeaterMonitorCommand = new RelayCommand(
                _ => StopHeaterMonitor(),
                _ => CanDisconnect);

            SetConnected(_client.IsConnected);

            // Subscribe to service events
            _client.EventAggregator.Subscribe<ConnectionStateChangedEvent>(OnConnectionStatusChanged);
        }

        private void StopHeaterMonitor()
        {
            _stateMachine.StopHeaterMonitorAsync();
        }

        private void StartHeaterMonitor()
        {
            _stateMachine.StartHeaterMonitorAsync();
        }

        private void GetHeaterStatus()
        {
            _client.GetHeaterStatusAsync();
        }

        private void GetCurrentTemp()
        {
            _client.GetCurrentTemperatureAsync();
        }

        private void SetTemperature()
        {
            _client.SetTemperatureAsync(_Temperature);
        }

        private void SendCustomCmd()
        {
            _client.SendCommandAsync(_CustomCMD);
        }

        private void SendAbsoluteMove()
        {
            _client.MoveAbsoluteAsync(_AbsAxisY, _AbsAxisX);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Cleanup()
        {
            _client.EventAggregator.Unsubscribe<ConnectionStateChangedEvent>(OnConnectionStatusChanged);
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
        private void SendBasicCmd(object obj)
        {
            _client.SendCommandAsync(obj.ToString());
        }

        private void ManualStatusUpdate()
        {
            _client.QueryStatusAsync();
        }

        private void ResetStateMachine()
        {
            // 重置状态机逻辑
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
                await _client.ConnectAsync(ServerIP, Port);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Disconnect()
        {
            _client.DisconnectAsync();
        }
        private enum LogType
        {
            Info,
            Send,
            Receive,
            Success,
            Warning,
            Error
        }
        private void AddLog(string message, LogType type = LogType.Info)
        {
        }
    }
}
