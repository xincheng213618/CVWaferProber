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

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private double readTimeout = 10;//Second
        private CancellationTokenSource _receiveCts;
        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        private readonly object _sendLock = new object();

        public IEventAggregator EventAggregator { get; }
        public bool IsConnected => _tcpClient?.Connected == true;

        public WaferProberTCPClient(IEventAggregator eventAggregator = null)
        {
            readTimeout = 10;//Second
            EventAggregator = eventAggregator ?? new EventAggregator();
        }

        public async Task ConnectAsync(string ip, int port)
        {
            try
            {
                if (IsConnected)
                {
                    logger.Error("Connected to the server.");
                    return;
                    //throw new InvalidOperationException("已经连接到服务器");
                }

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ip, port);
                _stream = _tcpClient.GetStream();

                // 记录连接信息
                _lastConnectedIp = ip;
                _lastConnectedPort = port;

                // 开始接收数据
                _receiveCts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveDataAsync(_receiveCts.Token));

                EventAggregator.Publish(new ConnectionStateChangedEvent(true, ip, port));
            }
            catch (Exception ex)
            {
                EventAggregator.Publish(new CommunicationErrorEvent("连接失败", ex, "Connect"));
                throw;
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
                //EventAggregator.Publish(new ConnectionStateChangedEvent(false));
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
                //throw new InvalidOperationException("未连接到服务器");
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
                    if(_stream!=null) _stream.Write(data, 0, data.Length);
                }

                EventAggregator.Publish(new CommandSentEvent(fullCommand));
            }
            catch (Exception ex)
            {
                EventAggregator.Publish(new CommunicationErrorEvent("发送指令失败", ex, $"Send: {fullCommand}"));
                throw;
            }
        }

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

        private async Task ReceiveDataRobustAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            var timeout = TimeSpan.FromSeconds(readTimeout);

            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    // 使用带超时的读取
                    int bytesRead = await ReadWithTimeoutAsync(buffer, 0, buffer.Length, timeout, cancellationToken);

                    if (bytesRead > 0)
                    {
                        string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        ProcessReceivedData(data);
                    }
                    else
                    {
                        // 连接正常关闭
                        await HandleGracefulDisconnectAsync();
                        break;
                    }
                }
                catch (TimeoutException timeoutEx)
                {
                    // 读取超时
                    //EventAggregator.Publish(new CommunicationErrorEvent("读取超时", timeoutEx, "Receive"));

                    if (_receiveCts != null)
                    {
                        // 尝试恢复连接
                        //if (!await IsConnectionAliveAsync())
                        //{
                        //    await DisconnectAsync();
                        //    break;
                        //}
                    }
                    else
                    {
                        await HandleGracefulDisconnectAsync();
                        break;
                    }

                }
                catch (SocketException socketEx)
                {
                    EventAggregator.Publish(new CommunicationErrorEvent($"Socket错误: {socketEx.SocketErrorCode}", socketEx, "Receive"));
                    await DisconnectAsync();
                    break;
                }
                catch (IOException ioEx)
                {
                    EventAggregator.Publish(new CommunicationErrorEvent("连接断开", ioEx, "Receive"));
                    await DisconnectAsync();
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        EventAggregator.Publish(new CommunicationErrorEvent("接收失败", ex, "Receive"));
                    }
                    break;
                }
            }
        }
        private async Task HandleGracefulDisconnectAsync()
        {
            try
            {
                EventAggregator.Publish(new CommunicationErrorEvent("连接正常关闭", null, "GracefulDisconnect"));

                // 等待一段时间确保所有数据都已接收
                await Task.Delay(100);

                await DisconnectAsync();
            }
            catch
            {
                // 忽略清理错误
            }
        }
        // 添加字段来记录最后连接的信息
        private string _lastConnectedIp;
        private int _lastConnectedPort;
        private async Task<bool> TryRecoverConnectionAsync()
        {
            try
            {
                if (_tcpClient != null)
                {
                    if (_tcpClient.Connected) return true;
                }
                // 先尝试关闭现有连接
                try
                {
                    _stream?.Close();
                    _tcpClient?.Close();
                }
                catch { }

                // 等待一会儿
                await Task.Delay(1000);

                // 尝试重新连接
                if (!string.IsNullOrEmpty(_lastConnectedIp) && _lastConnectedPort > 0)
                {
                    _tcpClient = new TcpClient();
                    await _tcpClient.ConnectAsync(_lastConnectedIp, _lastConnectedPort);
                    _stream = _tcpClient.GetStream();

                    EventAggregator.Publish(new ConnectionStateChangedEvent(true, _lastConnectedIp, _lastConnectedPort));
                    EventAggregator.Publish(new CommunicationErrorEvent("连接已恢复", null, "Recovery"));

                    return true;
                }
            }
            catch
            {
                // 重连失败
            }

            return false;
        }
        // 添加辅助方法：带超时的异步读取
        private async Task<int> ReadWithTimeoutAsync(byte[] buffer, int offset, int count,
                                                     TimeSpan timeout, CancellationToken cancellationToken)
        {
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(timeout);

                try
                {
                    var readTask = _stream.ReadAsync(buffer, offset, count, timeoutCts.Token);

                    // 添加一个延迟任务来检测实际的数据返回
                    var delayTask = Task.Delay(TimeSpan.FromSeconds(1), timeoutCts.Token);

                    var completedTask = await Task.WhenAny(readTask, delayTask);

                    if (completedTask == readTask)
                    {
                        // 读取完成
                        return await readTask;
                    }
                    else
                    {
                        // 延迟任务完成，说明读取太慢，检查连接
                        if (!await IsConnectionAliveAsync())
                        {
                            throw new IOException("连接已断开");
                        }

                        // 继续等待读取
                        return await readTask;
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                {
                    throw new TimeoutException($"读取超时 ({timeout.TotalSeconds}秒)");
                }
            }
        }
        private async Task<bool> IsConnectionAliveAsync()
        {
            try
            {
                if (_tcpClient == null /*|| !_tcpClient.Connected*/)
                    return false;

                // 方法1：检查Socket状态
                bool part1 = _tcpClient.Client.Poll(1000, SelectMode.SelectRead);
                bool part2 = (_tcpClient.Client.Available == 0);
                if (part1 && part2)
                    return false;

                // 方法2：发送测试数据
                try
                {
                    // 发送空操作来测试连接
                    if (_stream?.CanWrite == true)
                    {
                        await _stream.WriteAsync(Array.Empty<byte>(), 0, 0);
                    }
                }
                catch
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
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

                // 发布接收事件
                EventAggregator.Publish(new CommandReceivedEvent(command));

                startIndex = end + 1;
            }

            if (startIndex > 0)
            {
                _receiveBuffer.Remove(0, startIndex);
            }

            // 防止缓冲区过大
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
            if (temp >= 0) tempStr = string.Format("+{0:D4}", temp); // 4位，如 0250
            else tempStr = string.Format("{0:D4}", temp); // 4位，如 0250
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
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Task ZAllUpAsync() => SendCommandAsync("gu");
        public Task ZToMainCameraAsync() => SendCommandAsync("gm");
        public Task ZToAuxCameraAsync() => SendCommandAsync("ga");
        public Task ZToIntegratingSphereAsync() => SendCommandAsync("gi");

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