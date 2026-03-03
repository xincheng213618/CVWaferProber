using CVWaferProber.Core.Events;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Models;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
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
        // 新增：定时获取温度的定时器
        private readonly DispatcherTimer _tempQueryTimer;
        // 定时周期（可自定义，比如5000ms=5秒）
        private const int TempQueryInterval = 5000;
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
                if (SetProperty(ref _Temperature, value))
                {
                    // 本地温度属性变化时，也发送事件（比如手动修改温度输入框）
                    if (_client != null && _client.IsConnected)
                    {
                        TemperatureManager.UpdateTemperature(Convert.ToDouble(Temperature));
                    }
                }
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
            // ========== 初始化温度查询定时器 ==========
            _tempQueryTimer = new DispatcherTimer();
            _tempQueryTimer.Interval = TimeSpan.FromMilliseconds(TempQueryInterval);
            _tempQueryTimer.Tick += TempQueryTimer_Tick;
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
        // ========== 定时器Tick事件：定时获取温度 ==========
        private void TempQueryTimer_Tick(object? sender, EventArgs e)
        {
            if (_client != null && _client.IsConnected)
            {
                // 调用获取温度方法，并处理结果
                GetCurrentTemperatureWithEvent();
            }
        }
        // 新增：获取温度并发送更新事件
        private async void GetCurrentTemperatureWithEvent()
        {
            if (_client == null || !_client.IsConnected) return;

            try
            {
                // 注意：如果你的 GetCurrentTemperatureAsync 有返回值，需要调整这里
                // 假设返回值是 Task<decimal>（如果是void，需要从其他地方获取温度）
                // 先调用接口获取温度
                await _client.GetCurrentTemperatureAsync();

                // ========== 关键：获取到温度后更新本地属性 + 发送事件 ==========
                // 【适配说明】：
                // 如果 GetCurrentTemperatureAsync 有返回值（比如 Task<decimal>），则：
                // decimal temp = await _client.GetCurrentTemperatureAsync();
                // Temperature = temp; // 更新本地属性

                // 发送温度更新事件（不管是否有返回值，都可以用本地Temperature属性）
                TemperatureManager.UpdateTemperature(Convert.ToDouble(Temperature));
            }
            catch (Exception ex)
            {
                logger.Error("定时获取温度失败", ex);
            }
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

      
        // ========== 重构SetTemperature：设置温度后同步发送事件 ==========
        private void SetTemperature()
        {
            if (_client == null || !_client.IsConnected) return;

            _client.SetTemperatureAsync(_Temperature).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    // 设置温度成功后，发送事件更新UI
                    TemperatureManager.UpdateTemperature(Convert.ToDouble(Temperature));
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
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
            // 停止温度查询定时器
            _tempQueryTimer.Stop();
            _tempQueryTimer.Tick -= TempQueryTimer_Tick;
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

            // 连接成功则启动定时器，断开则停止
            if (@event.IsConnected)
            {
                _tempQueryTimer.Start();
                // 连接后立即获取一次温度
                GetCurrentTemperatureWithEvent();
            }
            else
            {
                _tempQueryTimer.Stop();
            }
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
