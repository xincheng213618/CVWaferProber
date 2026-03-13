using System.IO;
using System.Net.Sockets;
using System.Text;
using WaferComm.Core;

namespace WaferComm.Client
{
    /// <summary>
    /// 晶圆台客户端实现
    /// </summary>
    public class WaferProberTCPClient : IWaferProberClient
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(WaferProberTCPClient));

        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private double readTimeout = 10;//Second
        private CancellationTokenSource? _receiveCts;
        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        private readonly object _sendLock = new object();
        private string _lastConnectedIp = string.Empty;
        private int _lastConnectedPort;

        // ===== 新增：移动等待相关字段 =====
        private volatile bool _isMoving;
        private TaskCompletionSource<bool>? _moveCompletionSource;
        private readonly object _moveLock = new object();
        private const string MOVE_COMPLETE_RESPONSE = "$$67#"; // 到位确认响应

        /// <summary>
        /// 是否正在执行移动指令
        /// </summary>
        public bool IsMoving => _isMoving;
        // ===== 新增结束 =====

        public IEventAggregator EventAggregator { get; }
        public bool IsConnected => _tcpClient?.Connected == true;

        public WaferProberTCPClient(IEventAggregator eventAggregator = null)
        {
            readTimeout = 10;//Second
            EventAggregator = eventAggregator ?? new EventAggregator();
        }

        public async Task<bool> TryConnectAsync(string ip, int port)
        {
            try
            {
                TcpClient tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(ip, port);
                tcpClient.Close();
                return true;
            }
            catch (Exception ex)
            {
                EventAggregator.Publish(new CommunicationErrorEvent("连接失败", ex, "Connect"));
                return false;
            }
        }

        public async Task ConnectAsync(string ip, int port)
        {
            try
            {
                if (IsConnected)
                {
                    logger.Info("Connected to the server.");
                    return;
                }

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ip, port);
                _stream = _tcpClient.GetStream();

                _lastConnectedIp = ip;
                _lastConnectedPort = port;

                _receiveCts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveDataAsync(_receiveCts.Token));

                EventAggregator.Publish(new ConnectionStateChangedEvent(true, ip, port));
            }
            catch (Exception ex)
            {
                EventAggregator.Publish(new CommunicationErrorEvent("连接失败", ex, "Connect"));
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _receiveCts?.Cancel();
                _receiveCts = null;
                _stream?.Close();
                _stream = null;
                _tcpClient?.Close();
                _tcpClient = null;
            }
            catch (Exception ex)
            {
                EventAggregator.Publish(new CommunicationErrorEvent("断开连接失败", ex, "Disconnect"));
            }
        }

        public async Task SendCommandAsync(string command)
        {
            if (!IsConnected || _stream == null)
            {
                logger.Error("Unable to connect to the server.");
                return;
            }

            string fullCommand = command.StartsWith("$") ? command : $"${command}#";

            if (!fullCommand.EndsWith("#"))
            {
                fullCommand += "#";
            }

            try
            {
                byte[] data = Encoding.ASCII.GetBytes(fullCommand);

                lock (_sendLock)
                {
                    if (_stream != null) _stream.Write(data, 0, data.Length);
                }

                EventAggregator.Publish(new CommandSentEvent(fullCommand));
            }
            catch (Exception ex)
            {
                EventAggregator.Publish(new CommunicationErrorEvent("发送指令失败", ex, $"Send: {fullCommand}"));
                throw;
            }
        }

        // ===== 新增：发送移动指令并等待到位确认 =====
        /// <summary>
        /// 发送移动指令并阻塞等待到位确认（$67#）
        /// </summary>
        /// <param name="command">移动指令（如 gc, gm, ga, gi, gu）</param>
        /// <param name="timeoutSeconds">超时时间（秒），默认60秒</param>
        /// <returns>true=收到到位确认，false=超时</returns>
        public async Task<bool> SendMoveCommandAndWaitAsync(string command, int timeoutSeconds = 60)
        {
            if (_isMoving)
            {
                logger.Warn($"移动指令被拒绝：当前正在执行移动操作，指令={command}");
                return false;
            }

            lock (_moveLock)
            {
                if (_isMoving) return false;
                _isMoving = true;
                _moveCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            try
            {
                logger.Info($"发送移动指令: {command}，等待到位确认...");

                // 发送移动指令
                await SendCommandAsync(command);

                // 等待到位确认或超时
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
                {
                    cts.Token.Register(() =>
                    {
                        _moveCompletionSource?.TrySetResult(false);
                    });

                    bool result = await _moveCompletionSource.Task;

                    if (result)
                    {
                        logger.Info($"移动指令 {command} 到位确认成功");
                    }
                    else
                    {
                        logger.Warn($"移动指令 {command} 等待到位超时（{timeoutSeconds}秒）");
                        EventAggregator.Publish(new CommunicationErrorEvent($"移动指令超时: {command}", null, "MoveTimeout"));
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"移动指令执行失败: {command}", ex);
                EventAggregator.Publish(new CommunicationErrorEvent("移动指令执行失败", ex, $"Move: {command}"));
                return false;
            }
            finally
            {
                lock (_moveLock)
                {
                    _isMoving = false;
                    _moveCompletionSource = null;
                }
            }
        }
        // ===== 新增结束 =====

        private async Task ReceiveDataAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];

            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        ProcessReceivedData(data);
                    }
                }
                catch (OperationCanceledException)
                {
                    EventAggregator.Publish(new CommunicationErrorEvent("Socket closed"));
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        EventAggregator.Publish(new CommunicationErrorEvent("接收数据失败", ex, "Receive"));
                    }
                    break;
                }
            }

            EventAggregator.Publish(new CommunicationErrorEvent("Receive Task Exited"));
            EventAggregator.Publish(new ConnectionStateChangedEvent(false));
        }

        // ... (ReceiveDataRobustAsync, HandleGracefulDisconnectAsync, TryRecoverConnectionAsync,
        //      ReadWithTimeoutAsync, IsConnectionAliveAsync 保持不变) ...

        private void ProcessReceivedData(string data)
        {
            _receiveBuffer.Append(data);

            string buffer = _receiveBuffer.ToString();
            int startIndex = 0;

            while (true)
            {
                int begin = buffer.IndexOf('$', startIndex);
                if (begin < 0) break;

                int end = buffer.IndexOf('#', begin);
                if (end < 0) break;

                string command = buffer.Substring(begin, end - begin + 1);

                // ===== 新增：检查是否为移动到位确认 =====
                if (command == MOVE_COMPLETE_RESPONSE)
                {
                    lock (_moveLock)
                    {
                        _moveCompletionSource?.TrySetResult(true);
                    }
                    logger.Info("收到移动到位确认: $67#");
                }
                else
                {
                    lock (_moveLock)
                    {
                        _moveCompletionSource?.TrySetResult(false);
                    }
                }


                // ===== 新增结束 =====

                // 发布接收事件（保留原有逻辑）
                EventAggregator.Publish(new CommandReceivedEvent(command));

                startIndex = end + 1;
            }

            if (startIndex > 0)
            {
                _receiveBuffer.Remove(0, startIndex);
            }

            if (_receiveBuffer.Length > 4096)
            {
                _receiveBuffer.Remove(0, _receiveBuffer.Length - 2048);
            }
        }

        #region 预定义指令方法

        public Task GetMachineNumberAsync() => SendCommandAsync("B");
        public Task GetWaferIdAsync() => SendCommandAsync("b");
        public Task GetFirstTestPositionAsync() => SendCommandAsync("q");
        public Task GetCurrentPositionAsync() => SendCommandAsync("Q");
        public Task GetLotNumberAsync() => SendCommandAsync("V");

        public Task SetTemperatureAsync(decimal temperature)
        {
            if (temperature > 999)
            {
                EventAggregator.Publish(new CommunicationErrorEvent("连接失败"));
                return Task.CompletedTask;
            }
            int temp = (int)(temperature * 10);
            string tempStr;
            if (temp >= 0) tempStr = string.Format("+{0:D4}", temp);
            else tempStr = string.Format("{0:D4}", temp);
            return SendCommandAsync($"f{tempStr}");
        }
        public Task SendResultAsync(int result)
        {
            string resu = result == 1 ? "bin1" : "bin2";
            return SendCommandAsync($"{resu}");
        }
        public Task GetCurrentTemperatureAsync() => SendCommandAsync("fl");
        public Task GetWaferInfoAsync() => SendCommandAsync("ku");
        public Task GetTotalDiceCountAsync() => SendCommandAsync("Y");
        public Task GetProbePressureAsync() => SendCommandAsync("QP");
        public Task StartTestConfirmAsync() => SendCommandAsync("Z");
        public Task GetZDownStatusAsync() => SendCommandAsync("D");
        public Task GetHeaterStatusAsync() => SendCommandAsync("r");
        public Task GetTestCompletionStatusAsync() => SendCommandAsync("J");

        public Task MoveAbsoluteAsync(string y, string x) => SendCommandAsync($"JY{y}X{x}");
        public Task MoveRelativeAsync(string y, string x) => SendCommandAsync($"SY{y}X{x}");
        public Task MoveMicroAsync(string y, string x) => SendCommandAsync($"AY{y}X{x}");

        public Task StopAsync() => SendCommandAsync("K");
        public Task SendHeartbeatAsync() => SendCommandAsync("E");
        public Task QueryStatusAsync() => SendCommandAsync("A");
        public Task GetMappingAsync() => SendCommandAsync("rr");
        public Task ZAllUpAsync() => SendCommandAsync("gu");
        public Task ZToMainCameraAsync() => SendCommandAsync("gm");
        public Task ZToAuxCameraAsync() => SendCommandAsync("ga");

        public Task ZToMainCameraCheckAsync() => SendCommandAsync("gmc");
        public Task ZToAuxCameraCheckAsync() => SendCommandAsync("gac");

        public Task ZToIntegratingSphereAsync() => SendCommandAsync("gi");
        public Task GetCurrentDieAxisAsync() => SendCommandAsync("raxis");

        #endregion

        public void Dispose()
        {
            _receiveCts?.Cancel();
            _stream?.Dispose();
            _tcpClient?.Dispose();
            _receiveCts?.Dispose();
        }
    }
}