using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.Processors;

namespace WaferComm.StateMachine
{
    /// <summary>
    /// 加热吸盘监控器
    /// </summary>
    public class HeaterMonitor : CommandProcessorBase
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IWaferProberClient _client;
        private readonly System.Timers.Timer _temperatureCheckTimer;
        private readonly System.Timers.Timer _stabilityCheckTimer;
        private readonly object _heaterLock = new object();

        private TemperatureInfo _currentHeaterInfo = new TemperatureInfo();
        private DateTime _heaterStartTime;
        private bool _isMonitoring = false;
        private decimal? _lastStableTemperature;
        private DateTime _lastStableTime;
        private const decimal TEMPERATURE_STABILITY_THRESHOLD = 0.1m; // 稳定阈值 ±0.1℃
        private const int MAX_UNSTABLE_COUNT = 5;

        public TemperatureInfo CurrentHeaterInfo
        {
            get
            {
                lock (_heaterLock) return _currentHeaterInfo;
            }
        }

        public bool IsHeaterAvailable => _currentHeaterInfo.Status != HeaterChuckStatus.NoHeater;
        public bool IsTemperatureStable => CheckTemperatureStability();

        public HeaterMonitor(IEventAggregator eventAggregator, IWaferProberClient client) : base(eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _client = client;

            // 温度检查定时器（每5秒检查一次）
            _temperatureCheckTimer = new System.Timers.Timer(5000);
            _temperatureCheckTimer.Elapsed += OnTemperatureCheckTimerElapsed;
            _temperatureCheckTimer.AutoReset = true;

            // 温度稳定性检查定时器（每10秒检查一次）
            _stabilityCheckTimer = new System.Timers.Timer(10000);
            _stabilityCheckTimer.Elapsed += OnStabilityCheckTimerElapsed;
            _stabilityCheckTimer.AutoReset = true;

            InitializeHeaterInfo();
        }

        protected override void RegisterHandlers()
        {
            EventAggregator.Subscribe<CommandReceivedEvent>(OnCommandReceived);
            EventAggregator.Subscribe<CommandSentEvent>(OnCommandSent);
            EventAggregator.Subscribe<StateTransitionEvent>(OnStateTransition);
            EventAggregator.Subscribe<ConnectionStateChangedEvent>(OnConnectionChanged);
        }

        protected override void UnregisterHandlers()
        {
            EventAggregator.Unsubscribe<CommandReceivedEvent>(OnCommandReceived);
            EventAggregator.Unsubscribe<CommandSentEvent>(OnCommandSent);
            EventAggregator.Unsubscribe<StateTransitionEvent>(OnStateTransition);
            EventAggregator.Unsubscribe<ConnectionStateChangedEvent>(OnConnectionChanged);
        }

        private void InitializeHeaterInfo()
        {
            lock (_heaterLock)
            {
                _currentHeaterInfo = new TemperatureInfo
                {
                    CurrentTemperature = 0,
                    SetTemperature = null,
                    Status = HeaterChuckStatus.Unknown,
                    LastTemperatureUpdate = DateTime.MinValue
                };
            }
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();
            StartMonitoring();
        }

        public override async Task StopAsync()
        {
            StopMonitoring();
            await base.StopAsync();
        }

        private void StartMonitoring()
        {
            _isMonitoring = true;
            _temperatureCheckTimer.Start();
            _stabilityCheckTimer.Start();
            _heaterStartTime = DateTime.Now;
        }

        private void StopMonitoring()
        {
            _isMonitoring = false;
            _temperatureCheckTimer.Stop();
            _stabilityCheckTimer.Stop();
        }

        private void OnTemperatureCheckTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_isMonitoring || !_client.IsConnected) return;
            try
            {
                // 发送加热吸盘状态查询指令
                //_eventAggregator.Publish(new CommandSentEvent("$r#"));
                _client?.GetCurrentTemperatureAsync();
                _client?.GetHeaterStatusAsync();
            }
            catch (Exception ex)
            {
                _eventAggregator.Publish(new CommunicationErrorEvent("温度检查失败", ex, "HeaterMonitor"));
            }
        }

        private void OnStabilityCheckTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_isMonitoring) return;

            try
            {
                CheckTemperatureStability();
            }
            catch (Exception ex)
            {
                _eventAggregator.Publish(new CommunicationErrorEvent("稳定性检查失败", ex, "HeaterMonitor"));
            }
        }

        private void OnCommandReceived(CommandReceivedEvent @event)
        {
            if (!@event.IsValid) return;

            string response = @event.Command;

            // 处理加热吸盘状态响应
            if (response.StartsWith("r"))
            {
                ProcessHeaterStatusResponse(response);
            }
            // 处理当前温度响应
            else if (response.StartsWith("fl") && response.Length > 2)
            {
                ProcessCurrentTemperatureResponse(response);
            } 
            // 处理温度设置响应
            else if (response.StartsWith("f") && response.Length >= 5)
            {
                ProcessTemperatureSetResponse(response);
            }
        }

        private void OnCommandSent(CommandSentEvent @event)
        {
            // 检测是否是温度设置指令
            if (@event.Command.StartsWith("$f") && @event.Command.Length >= 6)
            {
                ProcessTemperatureSetCommand(@event.Command);
            }
        }

        private void OnStateTransition(StateTransitionEvent @event)
        {
            // 状态转换时，如果转到错误状态，停止加热监控
            if (@event.ToState == ProberState.Error)
            {
                StopMonitoring();
            }
            else if (@event.ToState == ProberState.Ready && @event.FromState == ProberState.Error)
            {
                // 从错误状态恢复，重新开始监控
                StartMonitoring();
            }
        }

        private void OnConnectionChanged(ConnectionStateChangedEvent @event)
        {
            if (@event.IsConnected)
            {
                // 连接成功后延迟启动监控
                Task.Delay(2000).ContinueWith(_ => StartMonitoring());
            }
            else
            {
                StopMonitoring();
                InitializeHeaterInfo();
            }
        }

        private void ProcessHeaterStatusResponse(string response)
        {
            lock (_heaterLock)
            {
                // 解析加热吸盘状态响应
                // r@@ - 无加热吸盘
                // rG@ - 温度过高
                // rK@ - 温度过低
                // rC@ - 正常
                // rA@ - 其他

                HeaterChuckStatus status = HeaterChuckStatus.Unknown;

                if (response.Length >= 2)
                {
                    char statusChar = response[1];
                    status = statusChar switch
                    {
                        '@' => HeaterChuckStatus.NoHeater,
                        'G' => HeaterChuckStatus.TemperatureTooHigh,
                        'K' => HeaterChuckStatus.TemperatureTooLow,
                        'C' => HeaterChuckStatus.Normal,
                        'A' => HeaterChuckStatus.Other,
                        _ => HeaterChuckStatus.Unknown
                    };

                    // 更新状态
                    _currentHeaterInfo.Status = status;
                    _currentHeaterInfo.LastTemperatureUpdate = DateTime.Now;

                    // 记录故障
                    if (status == HeaterChuckStatus.TemperatureTooHigh ||
                        status == HeaterChuckStatus.TemperatureTooLow ||
                        status == HeaterChuckStatus.Other)
                    {
                        _currentHeaterInfo.HeaterFaultCount++;
                    }

                    // 计算运行时间
                    _currentHeaterInfo.HeaterUptime = DateTime.Now - _heaterStartTime;

                    // 发布状态更新事件
                    _eventAggregator.Publish(new HeaterStatusUpdatedEvent(
                        status,
                        _currentHeaterInfo.CurrentTemperature,
                        _currentHeaterInfo.SetTemperature,
                        response
                    ));

                    // 检查是否需要发布警告
                    CheckForTemperatureWarnings();
                }
            }
        }

        private void ProcessTemperatureSetResponse(string response)
        {
            lock (_heaterLock)
            {
                try
                {
                    // 解析温度设置响应 f0250
                    if (response.Length >= 5)
                    {
                        string tempStr = response.Substring(1, 4);
                        if (decimal.TryParse(tempStr, out decimal temp))
                        {
                            _currentHeaterInfo.SetTemperature = temp; // 转换为实际温度

                            // 记录日志
                            //_eventAggregator.Publish(new CommandSentEvent(
                            //    $"$M温度设置: {_currentHeaterInfo.SetTemperature:F1}℃#"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _eventAggregator.Publish(new CommunicationErrorEvent("解析温度设置响应失败", ex, "HeaterMonitor"));
                }
            }
        }

        private void ProcessCurrentTemperatureResponse(string response)
        {
            lock (_heaterLock)
            {
                try
                {
                    // 解析当前温度响应 fl024.7
                    if (response.Length > 2)
                    {
                        string tempStr = response.Substring(2);
                        if (decimal.TryParse(tempStr, out decimal temp))
                        {
                            decimal oldTemp = _currentHeaterInfo.CurrentTemperature;
                            _currentHeaterInfo.CurrentTemperature = temp;
                            _currentHeaterInfo.LastTemperatureUpdate = DateTime.Now;

                            // 添加到历史记录
                            _currentHeaterInfo.TemperatureHistory.AddRecord(temp, _currentHeaterInfo.Status);

                            // 计算温度趋势
                            _currentHeaterInfo.TemperatureTrend = _currentHeaterInfo.TemperatureHistory.GetTemperatureTrend();

                            /*
                            // 检查温度变化
                            if (oldTemp != 0)
                            {
                                decimal tempChange = temp - oldTemp;
                                if (Math.Abs(tempChange) > 1.0m) // 温度变化超过1℃
                                {
                                    _eventAggregator.Publish(new TemperatureWarningEvent(
                                        TemperatureWarningType.TemperatureDrift, temp, _currentHeaterInfo.SetTemperature));
                                }
                            }

                            // 检查温度是否在设定范围内
                            CheckTemperatureInRange();
                            */
                        }
                    }
                }
                catch (Exception ex)
                {
                    _eventAggregator.Publish(new CommunicationErrorEvent("解析当前温度响应失败", ex, "HeaterMonitor"));
                }
            }
        }

        private void ProcessTemperatureSetCommand(string command)
        {
            lock (_heaterLock)
            {
                try
                {
                    // 解析温度设置指令 $f0250#
                    string cmd = command.Trim('$', '#');
                    if (cmd.Length >= 5 && cmd.StartsWith("f"))
                    {
                        string tempStr = cmd.Substring(1, 4);
                        if (decimal.TryParse(tempStr, out decimal temp))
                        {
                            _currentHeaterInfo.SetTemperature = temp / 10;

                            // 记录日志
                            _eventAggregator.Publish(new CommandSentEvent(
                                $"$M设置目标温度: {_currentHeaterInfo.SetTemperature:F1}℃#"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _eventAggregator.Publish(new CommunicationErrorEvent("解析温度设置指令失败", ex, "HeaterMonitor"));
                }
            }
        }

        private void CheckForTemperatureWarnings()
        {
            lock (_heaterLock)
            {
                var status = _currentHeaterInfo.Status;
                var currentTemp = _currentHeaterInfo.CurrentTemperature;
                var setTemp = _currentHeaterInfo.SetTemperature;

                switch (status)
                {
                    case HeaterChuckStatus.TemperatureTooHigh:
                        _eventAggregator.Publish(new TemperatureWarningEvent(
                            TemperatureWarningType.TemperatureTooHigh, currentTemp, setTemp));
                        break;

                    case HeaterChuckStatus.TemperatureTooLow:
                        _eventAggregator.Publish(new TemperatureWarningEvent(
                            TemperatureWarningType.TemperatureTooLow, currentTemp, setTemp));
                        break;

                    case HeaterChuckStatus.Other:
                        _eventAggregator.Publish(new TemperatureWarningEvent(
                            TemperatureWarningType.HeaterFault, currentTemp));
                        break;
                }
            }
        }

        private void CheckTemperatureInRange()
        {
            lock (_heaterLock)
            {
                if (_currentHeaterInfo.SetTemperature.HasValue &&
                    _currentHeaterInfo.TemperatureTolerance.HasValue)
                {
                    decimal target = _currentHeaterInfo.SetTemperature.Value;
                    decimal tolerance = _currentHeaterInfo.TemperatureTolerance.Value;
                    decimal current = _currentHeaterInfo.CurrentTemperature;

                    if (Math.Abs(current - target) > tolerance)
                    {
                        // 温度超出容差范围
                        if (current > target + tolerance)
                        {
                            _eventAggregator.Publish(new TemperatureWarningEvent(
                                TemperatureWarningType.TemperatureTooHigh, current, target));
                        }
                        else
                        {
                            _eventAggregator.Publish(new TemperatureWarningEvent(
                                TemperatureWarningType.TemperatureTooLow, current, target));
                        }
                    }
                }
            }
        }

        private bool CheckTemperatureStability()
        {
            lock (_heaterLock)
            {
                if (_currentHeaterInfo.TemperatureHistory.GetRecentRecords(10).Count() < 5)
                    return false;

                var recentTemps = _currentHeaterInfo.TemperatureHistory.GetRecentRecords(10)
                    .Select(r => r.Temperature).ToList();

                if (recentTemps.Count < 2) return false;

                // 计算温度标准差
                decimal average = recentTemps.Average();
                decimal sumOfSquares = recentTemps.Sum(t => (t - average) * (t - average));
                decimal variance = sumOfSquares / recentTemps.Count;
                decimal stdDev = (decimal)Math.Sqrt((double)variance);

                bool isStable = stdDev <= TEMPERATURE_STABILITY_THRESHOLD;

                // 更新状态
                if (isStable && _currentHeaterInfo.Status == HeaterChuckStatus.Normal)
                {
                    _currentHeaterInfo.Status = HeaterChuckStatus.TemperatureStable;
                }
                else if (!isStable && _currentHeaterInfo.Status == HeaterChuckStatus.TemperatureStable)
                {
                    _currentHeaterInfo.Status = HeaterChuckStatus.TemperatureFluctuating;
                }

                return isStable;
            }
        }

        public void SetTemperatureTolerance(decimal tolerance)
        {
            lock (_heaterLock)
            {
                _currentHeaterInfo.TemperatureTolerance = tolerance;
            }
        }

        public TemperatureInfo GetHeaterInfo()
        {
            lock (_heaterLock)
            {
                return new TemperatureInfo
                {
                    CurrentTemperature = _currentHeaterInfo.CurrentTemperature,
                    SetTemperature = _currentHeaterInfo.SetTemperature,
                    TemperatureTolerance = _currentHeaterInfo.TemperatureTolerance,
                    Status = _currentHeaterInfo.Status,
                    LastTemperatureUpdate = _currentHeaterInfo.LastTemperatureUpdate,
                    TemperatureTrend = _currentHeaterInfo.TemperatureTrend
                };
            }
        }

        public TemperatureStatistics GetTemperatureStatistics()
        {
            lock (_heaterLock)
            {
                var history = _currentHeaterInfo.TemperatureHistory;
                int TotalRecords = 0;
                var recs = history.GetRecentRecords(100);
                decimal MinTemperature = 0;
                decimal MaxTemperature = 0;
                if( recs != null ) { 
                    TotalRecords = recs.Count();
                    if (TotalRecords > 0)
                    {
                        MinTemperature = recs.Min(r => r.Temperature);
                        MaxTemperature = recs.Max(r => r.Temperature);
                    }
                }
                return new TemperatureStatistics
                {
                    AverageTemperature = history.GetAverageTemperature(),
                    TemperatureTrend = history.GetTemperatureTrend(),
                    TotalRecords = TotalRecords,
                    MinTemperature = MinTemperature,
                    MaxTemperature = MaxTemperature,
                    CurrentStatus = _currentHeaterInfo.Status,
                    Uptime = _currentHeaterInfo.HeaterUptime,
                    FaultCount = _currentHeaterInfo.HeaterFaultCount
                };
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _temperatureCheckTimer?.Dispose();
            _stabilityCheckTimer?.Dispose();
        }
    }

    /// <summary>
    /// 温度统计信息
    /// </summary>
    public class TemperatureStatistics
    {
        public decimal? AverageTemperature { get; set; }
        public decimal? TemperatureTrend { get; set; } // ℃/min
        public int TotalRecords { get; set; }
        public decimal MinTemperature { get; set; }
        public decimal MaxTemperature { get; set; }
        public HeaterChuckStatus CurrentStatus { get; set; }
        public TimeSpan Uptime { get; set; }
        public int FaultCount { get; set; }
        public decimal TemperatureRange => MaxTemperature - MinTemperature;

        public override string ToString()
        {
            return $"平均: {AverageTemperature?.ToString("F1") ?? "N/A"}℃ | 范围: {MinTemperature:F1}-{MaxTemperature:F1}℃ | " +
                   $"趋势: {TemperatureTrend?.ToString("F2") ?? "N/A"}℃/min | 状态: {CurrentStatus}";
        }
    }
}