using log4net.Repository.Hierarchy;
using System.Timers;
using WaferComm.Core;
using WaferComm.Processors;

namespace WaferComm.StateMachine
{
    /// <summary>
    /// 运动监控器
    /// </summary>
    public class MotionMonitor : CommandProcessorBase
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MotionMonitor));

        private readonly IEventAggregator _eventAggregator;
        private readonly System.Timers.Timer _motionTimeoutTimer;
        private readonly object _motionLock = new object();

        private MotionCommand _currentMotion;
        private DateTime _motionStartTime;
        private TimeSpan _defaultXYMotionTimeout = TimeSpan.FromSeconds(10);
        private TimeSpan _defaultZMotionTimeout = TimeSpan.FromSeconds(5);
        private readonly List<MotionCommand> _motionHistory = new List<MotionCommand>();
        private const int MAX_HISTORY_COUNT = 100;

        public MotionStatus CurrentMotionStatus { get; private set; } = MotionStatus.Idle;
        public MotionCommand CurrentMotion => _currentMotion;
        public IEnumerable<MotionCommand> MotionHistory => _motionHistory.AsReadOnly();

        public MotionMonitor(IEventAggregator eventAggregator, int defaultMotionTimeout, int zMotionTimeout) : base(eventAggregator)
        {
            _eventAggregator = eventAggregator;

            _defaultXYMotionTimeout = TimeSpan.FromSeconds(defaultMotionTimeout);
            _defaultZMotionTimeout = TimeSpan.FromSeconds(zMotionTimeout);
            _motionTimeoutTimer = new System.Timers.Timer();
            _motionTimeoutTimer.Elapsed += OnMotionTimeout;
            _motionTimeoutTimer.AutoReset = false;
        }

        protected override void RegisterHandlers()
        {
            EventAggregator.Subscribe<CommandSentEvent>(OnCommandSent);
            EventAggregator.Subscribe<CommandReceivedEvent>(OnCommandReceived);
            EventAggregator.Subscribe<StateTransitionEvent>(OnStateTransition);
        }

        protected override void UnregisterHandlers()
        {
            EventAggregator.Unsubscribe<CommandSentEvent>(OnCommandSent);
            EventAggregator.Unsubscribe<CommandReceivedEvent>(OnCommandReceived);
            EventAggregator.Unsubscribe<StateTransitionEvent>(OnStateTransition);
        }

        private void OnCommandSent(CommandSentEvent @event)
        {
            // 检测是否是运动指令
            if (IsMotionCommand(@event.Command))
            {
                StartMonitoringMotion(@event.Command);
            }
            //else if (IsQMotionCommand(@event.Command))
            //{
            //    StartQMotion(@event.Command);
            //}
        }

        private void OnCommandReceived(CommandReceivedEvent @event)
        {
            if (!@event.IsValid) return;

            string response = @event.Command;

            // 处理运动响应
            ProcessMotionResponse(response);

            // 更新坐标信息
            UpdatePositionFromResponse(response);
        }

        private void OnStateTransition(StateTransitionEvent @event)
        {
            // 状态转换时，如果当前是错误状态，取消运动监控
            if (@event.ToState == ProberState.Error && CurrentMotionStatus != MotionStatus.Idle)
            {
                CancelCurrentMotion("状态转换到错误状态");
            }
        }
        private bool IsQMotionCommand(string command)
        {
            // 移除协议头和尾
            string cmd = command.Trim('$', '#');

            return cmd.StartsWith("Q");
        }
        private bool IsMotionCommand(string command)
        {
            // 移除协议头和尾
            string cmd = command.Trim('$', '#');

            return cmd.StartsWith("J") || // 绝对运动
                                          //cmd.StartsWith("S") ||  // 相对运动
                                          //cmd.StartsWith("S") ||  // 相对运动
                                          //cmd.StartsWith("A") ||  // 微米运动
                   cmd == "Z" ||            // 起测确认（Z轴运动）
                   cmd == "D";            // Z Down状态确认
        }

        private void StartQMotion(string command)
        {
            lock (_motionLock)
            {
                // 解析运动指令
                _currentMotion = ParseMotionCommand(command);
                _motionStartTime = DateTime.Now;

                // 设置超时定时器
                TimeSpan timeout = GetTimeoutForCommand(command);
                _motionTimeoutTimer.Interval = timeout.TotalMilliseconds;
                _motionTimeoutTimer.Start();
            }
        }

        private void StartMonitoringMotion(string command)
        {
            lock (_motionLock)
            {
                // 解析运动指令
                _currentMotion = ParseMotionCommand(command);
                _motionStartTime = DateTime.Now;

                // 设置运动状态
                SetMotionStatus(GetMotionStatusFromCommand(command));

                // 设置超时定时器
                TimeSpan timeout = GetTimeoutForCommand(command);
                _motionTimeoutTimer.Interval = timeout.TotalMilliseconds;
                _motionTimeoutTimer.Start();

                // 记录运动开始
                //_eventAggregator.Publish(new CommandSentEvent($"$M{CurrentMotionStatus} Start#"));
            }
        }
        private MotionCommand ParseMotionCommand(string command)
        {
            string cmd = command.Trim('$', '#');

            var motionCmd = new MotionCommand
            {
                CommandType = cmd.Substring(0, 1),
                StartTime = DateTime.Now
            };

            // 解析坐标
            if (cmd.Length > 2)
            {
                // 解析 JY+020X-020 格式
                int xIndex = cmd.IndexOf('X');
                if (xIndex > 0)
                {
                    string yPart = cmd.Substring(2, xIndex - 2);
                    string xPart = cmd.Substring(xIndex + 1);

                    motionCmd.TargetY = yPart;
                    motionCmd.TargetX = xPart;
                }
            }

            return motionCmd;
        }

        private MotionStatus GetMotionStatusFromCommand(string command)
        {
            string cmd = command.Trim('$', '#');

            if (cmd.StartsWith("J")) return MotionStatus.MovingAbsolute;
            if (cmd.StartsWith("Q")) return MotionStatus.Idle;
            //if (cmd.StartsWith("SY")) return MotionStatus.MovingRelative;
            //if (cmd.StartsWith("AY")) return MotionStatus.MovingMicro;
            if (cmd == "Z") return MotionStatus.ZUpMoving;
            if (cmd == "D") return MotionStatus.ZDownMoving;

            return MotionStatus.Moving;
        }

        private TimeSpan GetTimeoutForCommand(string command)
        {
            string cmd = command.Trim('$', '#');

            if (cmd == "Z" || cmd == "D")
                return _defaultZMotionTimeout;
            else
                return _defaultXYMotionTimeout;
        }

        private void ProcessMotionResponse(string response)
        {
            lock (_motionLock)
            {
                if (_currentMotion == null) return;

                MotionStatus status = MotionStatus.Idle;
                bool isMotionComplete = false;
                bool isSuccess = false;

                // 根据响应码判断运动结果
                switch (response)
                {
                    case "67":  
                        // Z Up
                        if (_currentMotion.CommandType == "Z")
                        {
                            isMotionComplete = true;
                            isSuccess = true;
                            status = MotionStatus.ZUp;
                        }
                        else if (_currentMotion.CommandType != "D") // 运动完成
                        {
                            isMotionComplete = true;
                            isSuccess = true;
                            status = MotionStatus.MotionComplete;
                        }
                        break;
                    case "81":  // 运动完成，最后一个TouchDown
                        isMotionComplete = true;
                        isSuccess = true;
                        status = MotionStatus.MotionComplete;
                        break;

                    case "76":  // 运动失败
                        isMotionComplete = true;
                        isSuccess = false;
                        status = MotionStatus.MotionFailed;
                        break;

                    case "68":  // Z Down完成
                        if (_currentMotion.CommandType == "D")
                        {
                            isMotionComplete = true;
                            isSuccess = true;
                            status = MotionStatus.ZDown;
                        }
                        break;

                    case "90":  // 开始停止
                    case "85":  // 停止完成
                    case "91":  // 可以开始测试
                        // 这些状态可能影响运动
                        break;
                    default:
                       
                        break;
                }

                if (isMotionComplete)
                {
                    //success ? MotionStatus.MotionComplete : MotionStatus.MotionFailed
                    CompleteMotion(isSuccess, response, status);
                }
            }
        }
        private void CompleteMotion(bool success, string responseCode, MotionStatus status)
        {
            _motionTimeoutTimer.Stop();

            _currentMotion.EndTime = DateTime.Now;
            _currentMotion.Success = success;

            if (!success)
            {
                _currentMotion.ErrorMessage = $"运动失败，响应码: {responseCode}";
            }

            // 更新运动状态
            SetMotionStatus(status);

            // 添加到历史记录
            _motionHistory.Add(_currentMotion);
            if (_motionHistory.Count > MAX_HISTORY_COUNT)
            {
                _motionHistory.RemoveAt(0);
            }

            // 记录日志
            //string logMsg = success ?
            //    $"运动完成: {currentMotionTmp}, 耗时: {currentMotionTmp.Duration.TotalSeconds:F2}秒" :
            //    $"运动失败: {currentMotionTmp}, 错误: {currentMotionTmp.ErrorMessage}";

            // 发布运动完成事件
            _eventAggregator.Publish(new MotionCompletedEvent(_currentMotion, responseCode, success));

            //_eventAggregator.Publish(new CommandSentEvent($"$M{logMsg}#"));

            // 清理当前运动
            _currentMotion = null;

            // 5秒后恢复空闲状态
            Task.Delay(5000).ContinueWith(_ =>
            {
                if (_currentMotion == null) // 确保没有新的运动开始
                {
                    logger.InfoFormat("Return to idle status in 5 seconds,CurrentMotionStatus={0}", CurrentMotionStatus.ToString());
                    SetMotionStatus(MotionStatus.Idle);
                }
            });
        }

        private void OnMotionTimeout(object sender, ElapsedEventArgs e)
        {
            lock (_motionLock)
            {
                if (_currentMotion != null)
                {
                    _currentMotion.EndTime = DateTime.Now;
                    _currentMotion.Success = false;
                    _currentMotion.ErrorMessage = $"运动超时 ({_motionTimeoutTimer.Interval / 1000}秒)";

                    SetMotionStatus(MotionStatus.MotionTimeout);

                    // 发布超时事件
                    _eventAggregator.Publish(new MotionTimeoutEvent(_currentMotion,
                        TimeSpan.FromMilliseconds(_motionTimeoutTimer.Interval)));

                    // 记录日志
                    //_eventAggregator.Publish(new CommandSentEvent(
                    //    $"$M运动超时: {_currentMotion}, 耗时: {_currentMotion.Duration.TotalSeconds:F2}秒#"));

                    _currentMotion = null;
                    // 2秒后恢复空闲状态
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        if (_currentMotion == null) // 确保没有新的运动开始
                        {
                            SetMotionStatus(MotionStatus.Idle);
                        }
                    });
                }
            }
        }

        private void UpdatePositionFromResponse(string response)
        {
            // 更新坐标信息（如果响应中包含坐标）
            if (response.StartsWith("qY") || response.StartsWith("QY"))
            {
                // 解析坐标响应 qY001X001
                try
                {
                    string data = response.Substring(2); // 去掉第一个字符
                    int xIndex = data.IndexOf('X');
                    if (xIndex > 0)
                    {
                        string yStr = data.Substring(0, xIndex); // 去掉Y
                        string xStr = data.Substring(xIndex + 1);

                        // 更新坐标事件
                        _eventAggregator.Publish(new PositionUpdatedEvent(yStr, xStr));
                    }
                }
                catch
                {
                    // 解析失败
                }
            }
        }

        private void SetMotionStatus(MotionStatus status)
        {
            if (CurrentMotionStatus != status)
            {
                CurrentMotionStatus = status;

                // 发布状态更新事件
                _eventAggregator.Publish(new MotionStatusUpdatedEvent(status, _currentMotion));
            }
        }

        private void CancelCurrentMotion(string reason)
        {
            lock (_motionLock)
            {
                if (_currentMotion != null)
                {
                    _motionTimeoutTimer.Stop();

                    _currentMotion.EndTime = DateTime.Now;
                    _currentMotion.Success = false;
                    _currentMotion.ErrorMessage = $"运动被取消: {reason}";

                    SetMotionStatus(MotionStatus.MotionFailed);

                    // 记录日志
                    //_eventAggregator.Publish(new CommandSentEvent(
                    //    $"$M运动取消: {_currentMotion}, 原因: {reason}#"));

                    _currentMotion = null;
                }
            }
        }

        public void Reset()
        {
            lock (_motionLock)
            {
                CancelCurrentMotion("手动重置");
                _motionHistory.Clear();
                SetMotionStatus(MotionStatus.Idle);
            }
        }

        public MotionStatistics GetStatistics()
        {
            lock (_motionLock)
            {
                var completedMotions = _motionHistory.Where(m => m.EndTime.HasValue).ToList();

                return new MotionStatistics
                {
                    TotalMotions = _motionHistory.Count,
                    SuccessfulMotions = completedMotions.Count(m => m.Success),
                    FailedMotions = completedMotions.Count(m => !m.Success),
                    AverageDuration = completedMotions.Any() ?
                        TimeSpan.FromSeconds(completedMotions.Average(m => m.Duration.TotalSeconds)) :
                        TimeSpan.Zero,
                    LastMotionTime = _motionHistory.LastOrDefault()?.StartTime
                };
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _motionTimeoutTimer?.Dispose();
        }

        public void ReloadSettings(int defaultXYMotionTimeout, int defaultZMotionTimeout)
        {
            _defaultXYMotionTimeout = TimeSpan.FromSeconds(defaultXYMotionTimeout);
            _defaultZMotionTimeout = TimeSpan.FromSeconds(defaultZMotionTimeout);
        }
    }

    /// <summary>
    /// 运动状态更新事件
    /// </summary>
    public class MotionStatusUpdatedEvent
    {
        public MotionStatus Status { get; }
        public MotionCommand CurrentMotion { get; }
        public DateTime Timestamp { get; } = DateTime.Now;

        public MotionStatusUpdatedEvent(MotionStatus status, MotionCommand currentMotion)
        {
            Status = status;
            CurrentMotion = currentMotion;
        }
    }

    /// <summary>
    /// 坐标更新事件
    /// </summary>
    public class PositionUpdatedEvent
    {
        public string Y { get; }
        public string X { get; }
        public DateTime Timestamp { get; } = DateTime.Now;

        public PositionUpdatedEvent(string y, string x)
        {
            Y = y;
            X = x;
        }
    }

    /// <summary>
    /// 运动统计信息
    /// </summary>
    public class MotionStatistics
    {
        public int TotalMotions { get; set; }
        public int SuccessfulMotions { get; set; }
        public int FailedMotions { get; set; }
        public double SuccessRate => TotalMotions > 0 ? (double)SuccessfulMotions / TotalMotions * 100 : 0;
        public TimeSpan AverageDuration { get; set; }
        public DateTime? LastMotionTime { get; set; }

        public override string ToString()
        {
            return $"总计: {TotalMotions}, 成功: {SuccessfulMotions}, 失败: {FailedMotions}, 成功率: {SuccessRate:F1}%, 平均耗时: {AverageDuration.TotalSeconds:F2}秒";
        }
    }
}