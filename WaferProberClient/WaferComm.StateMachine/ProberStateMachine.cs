using CVWaferProber.Core.Events;
using CVWaferProber.Core.ViewModels;
using log4net.Repository.Hierarchy;
using System.Timers;
using System.Windows;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.Processors;
using WaferComm.StateMachine;

namespace WaferComm.StateMachine
{

    public class ProberStateStatus:ViewModelBase
    {

        public static ProberStateStatus Instance { get; set; } = new ProberStateStatus();

        public string CurrentWaferId { get => _CurrentWaferId; set { _CurrentWaferId = value; OnPropertyChanged(); } }
        private string _CurrentWaferId = string.Empty;
    }

    public static class ProberStateMachineLocation
    {
        public static event EventHandler<string> LocationChanged;

        public static void LocationChange(string command)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LocationChanged?.Invoke(command, command);
            });
        }

    }

    /// <summary>
    /// 探针台状态机
    /// </summary>
    public class ProberStateMachine : CommandProcessorBase, IStateMachine
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ProberStateMachine));


        private readonly IEventAggregator _eventAggregator;
        private readonly IWaferProberClient _client;
        private readonly System.Timers.Timer _statusTimer;
        private readonly System.Timers.Timer _heartbeatTimer;
        private readonly object _stateLock = new object();
        private readonly Dictionary<ProberState, List<ProberState>> _allowedTransitions;

        private ProberState _currentState = ProberState.Disconnected;
        private DateTime _lastStateChange = DateTime.Now;
        private ProberStatus _currentStatus = new ProberStatus();
        private CancellationTokenSource _monitoringCts;
        //
        private readonly MotionMonitor _motionMonitor;
        private readonly HeaterMonitor _heaterMonitor;

        public ProberState CurrentState
        {
            get
            {
                lock (_stateLock) return _currentState;
            }
            set
            {
                _currentState = value;
            }
        }


        public ProberStateMachine(IEventAggregator eventAggregator, IWaferProberClient client, int defaultXYMotionTimeout, int defaultZMotionTimeout) : base(eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _client = client;

            _motionMonitor = new MotionMonitor(eventAggregator, defaultXYMotionTimeout, defaultZMotionTimeout);
            _heaterMonitor = new HeaterMonitor(eventAggregator, client);
            // 初始化状态转移规则
            _allowedTransitions = InitializeTransitionRules();

            // 初始化状态定时器（每2秒获取一次状态）
            _statusTimer = new System.Timers.Timer(2000);
            _statusTimer.Elapsed += OnStatusTimerElapsed;
            //_statusTimer.AutoReset = true;
            _statusTimer.Start();

            // 初始化心跳定时器（每5秒发送一次心跳）
            //_heartbeatTimer = new System.Timers.Timer(5000);
            //_heartbeatTimer.Elapsed += OnHeartbeatTimerElapsed;
            //_heartbeatTimer.AutoReset = true;

            InitializeStatus();
        }

        protected override void RegisterHandlers()
        {
            EventAggregator.Subscribe<ConnectionStateChangedEvent>(OnConnectionChanged);
            EventAggregator.Subscribe<CommandReceivedEvent>(OnCommandReceived);
            EventAggregator.Subscribe<CommunicationErrorEvent>(OnCommunicationError);
            // 订阅运动相关事件
            EventAggregator.Subscribe<MotionCompletedEvent>(OnMotionCompleted);
            EventAggregator.Subscribe<MotionTimeoutEvent>(OnMotionTimeout);
            EventAggregator.Subscribe<MotionStatusUpdatedEvent>(OnMotionStatusUpdated);
            EventAggregator.Subscribe<PositionUpdatedEvent>(OnPositionUpdated);
        }

        protected override void UnregisterHandlers()
        {
            EventAggregator.Unsubscribe<ConnectionStateChangedEvent>(OnConnectionChanged);
            EventAggregator.Unsubscribe<CommandReceivedEvent>(OnCommandReceived);
            EventAggregator.Unsubscribe<CommunicationErrorEvent>(OnCommunicationError);
            // 取消订阅运动相关事件
            EventAggregator.Unsubscribe<MotionCompletedEvent>(OnMotionCompleted);
            EventAggregator.Unsubscribe<MotionTimeoutEvent>(OnMotionTimeout);
            EventAggregator.Unsubscribe<MotionStatusUpdatedEvent>(OnMotionStatusUpdated);
            EventAggregator.Unsubscribe<PositionUpdatedEvent>(OnPositionUpdated);
        }

        private Dictionary<ProberState, List<ProberState>> InitializeTransitionRules()
        {
            return new Dictionary<ProberState, List<ProberState>>
            {
                [ProberState.Disconnected] = new List<ProberState> { ProberState.Connected },
                [ProberState.Connected] = new List<ProberState>
                {
                    ProberState.Ready,
                    ProberState.Disconnected,
                    ProberState.Error
                },
                [ProberState.Ready] = new List<ProberState>
                {
                    ProberState.WaitingForWafer,
                    ProberState.Disconnected,
                    ProberState.Error,
                    ProberState.Maintenance
                },
                [ProberState.WaitingForWafer] = new List<ProberState>
                {
                    ProberState.WaferLoaded,
                    ProberState.Ready,
                    ProberState.Error
                },
                [ProberState.WaferLoaded] = new List<ProberState>
                {
                    ProberState.Aligning,
                    ProberState.WaitingForWafer,
                    ProberState.Error
                },
                [ProberState.Aligning] = new List<ProberState>
                {
                    ProberState.Testing,
                    ProberState.WaferLoaded,
                    ProberState.Error
                },
                [ProberState.Testing] = new List<ProberState>
                {
                    ProberState.Paused,
                    ProberState.Stopping,
                    ProberState.Ready,
                    ProberState.Error
                },
                [ProberState.Paused] = new List<ProberState>
                {
                    ProberState.Testing,
                    ProberState.Stopping,
                    ProberState.Error
                },
                [ProberState.Stopping] = new List<ProberState>
                {
                    ProberState.Ready,
                    ProberState.Error
                },
                [ProberState.Error] = new List<ProberState>
                {
                    ProberState.Ready,
                    ProberState.Disconnected,
                    ProberState.Maintenance
                },
                [ProberState.Maintenance] = new List<ProberState>
                {
                    ProberState.Ready,
                    ProberState.Disconnected
                }
            };
        }

        private void InitializeStatus()
        {
            _currentStatus = new ProberStatus
            {
                CurrentState = ProberState.Disconnected,
                LastStateChange = DateTime.Now,
                LastHeartbeat = DateTime.Now
            };
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();
            await _motionMonitor.StartAsync();
            await _heaterMonitor.StartAsync();
            StartMonitoring();
        }

        public override async Task StopAsync()
        {
            StopMonitoring();
            await _heaterMonitor.StopAsync();
            await _motionMonitor.StopAsync();
            await base.StopAsync();
        }

        private void StartMonitoring()
        {
            _monitoringCts = new CancellationTokenSource();
            _statusTimer?.Start();
            _heartbeatTimer?.Start();
        }

        private void StopMonitoring()
        {
            _monitoringCts?.Cancel();
            _statusTimer?.Stop();
            _heartbeatTimer?.Stop();
        }
        #region 运动状态处理
        private void OnMotionCompleted(MotionCompletedEvent @event)
        {
            // 运动完成时更新状态
            if (@event.IsSuccess)
            {
                // 根据运动类型更新状态
                if (@event.Command.CommandType.StartsWith("J"))
                //@event.Command.CommandType.StartsWith("S") ||
                //@event.Command.CommandType.StartsWith("A"))
                {
                    // 坐标运动完成，保持当前状态
                    //UpdateMotionStatusInProberStatus();
                }
                else if (@event.Command.CommandType == "Z")
                {
                    // Z Up完成，可以开始测试
                    if (CurrentState == ProberState.WaferLoaded)
                    {
                        TransitionToAsync(ProberState.Testing).Wait();
                    }
                }
                else if (@event.Command.CommandType == "D")
                {
                    // Z Down完成
                    //UpdateMotionStatusInProberStatus();
                }
                //else if (@event.Command.CommandType == "Q")
                //{
                //    // 当前测试坐标完成
                //    _motionMonitor.CurrentMotion.UpdatePosition(@event.ResponseCode);
                //    UpdateQMotionStatusInProberStatus();
                //}
            }
            else
            {
                // 运动失败，转到错误状态
                //TransitionToAsync(ProberState.Error, @event).Wait();
            }
        }

        private void OnMotionTimeout(MotionTimeoutEvent @event)
        {
            // 运动超时，转到错误状态
            //TransitionToAsync(ProberState.Error, @event).Wait();
        }

        private void OnMotionStatusUpdated(MotionStatusUpdatedEvent @event)
        {
            // 更新状态机中的运动状态
            UpdateMotionStatusInProberStatus();
        }

        private void OnPositionUpdated(PositionUpdatedEvent @event)
        {
            // 更新当前位置
            lock (_stateLock)
            {
                _currentStatus.CurrentPosition.FromPositionUpdatedEvent(@event);
            }
        }

        private void UpdateMotionStatusInProberStatus()
        {
            lock (_stateLock)
            {
                _currentStatus.MotionStatus = _motionMonitor.CurrentMotionStatus;
                _currentStatus.CurrentMotionCommand = _motionMonitor.CurrentMotion;
                _currentStatus.CurrentPosition.FromMotionCmd(_motionMonitor.CurrentMotion);

                // 更新Z轴状态
                _currentStatus.ZAxisStatus = DetermineZAxisStatus();

                // 更新统计信息
                var stats = _motionMonitor.GetStatistics();
                _currentStatus.MotionSuccessCount = stats.SuccessfulMotions;
                _currentStatus.MotionFailureCount = stats.FailedMotions;
                _currentStatus.AverageMotionTimeSeconds = stats.AverageDuration.TotalSeconds;

                if (_currentStatus.MotionStatus == MotionStatus.MotionComplete)
                {
                    _currentStatus.LastMotionCompleteTime = DateTime.Now;
                }
            }
            if (logger.IsDebugEnabled) logger.DebugFormat("MotionStatusUpdated => {0}", _currentStatus.MotionStatus.ToString());
            // 发布状态更新
            EventAggregator.Publish(new StateUpdatedEvent(GetStatus()));
        }
        private ZAxisStatus DetermineZAxisStatus()
        {
            // 根据运动和响应确定Z轴状态
            var motion = _motionMonitor.CurrentMotion;
            var status = _motionMonitor.CurrentMotionStatus;

            if (motion != null)
            {
                if (motion.CommandType == "Z")
                {
                    return status == MotionStatus.ZUpMoving ? ZAxisStatus.MovingUp : ZAxisStatus.Up;
                }
                else if (motion.CommandType == "D")
                {
                    return status == MotionStatus.ZDownMoving ? ZAxisStatus.MovingDown : ZAxisStatus.Down;
                }
            }

            // 根据历史响应判断
            return ZAxisStatus.Unknown;
        }

        public void ResetMotionMonitor()
        {
            _motionMonitor.Reset();
        }

        public MotionStatistics GetMotionStatistics()
        {
            return _motionMonitor.GetStatistics();
        }

        #endregion
        private async void OnStatusTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_monitoringCts?.IsCancellationRequested == true) return;

            try
            {
                await UpdateStatusAsync();
            }
            catch (Exception ex)
            {
                EventAggregator.Publish(new StateMachineErrorEvent("状态更新失败", CurrentState, ex));
            }
        }

        private async void OnHeartbeatTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_monitoringCts?.IsCancellationRequested == true) return;
            if (CurrentState != ProberState.Disconnected)
            {
                // 发送心跳
                EventAggregator.Publish(new CommandSentEvent("$E#"));
            }
        }

        private async Task UpdateStatusAsync()
        {
            // 根据当前状态执行不同的状态查询
            switch (CurrentState)
            {
                case ProberState.Disconnected:
                    // 什么都不做
                    //EventAggregator.Publish(new StateUpdatedEvent(GetStatus()));
                    break;

                //case ProberState.Connected:
                //case ProberState.Ready:
                //    // 查询基本状态
                //    await QueryBasicStatusAsync();
                //    break;

                //case ProberState.Testing:
                //case ProberState.Paused:
                //    // 查询测试状态
                //    await QueryTestingStatusAsync();
                //    break;

                //case ProberState.WaferLoaded:
                //case ProberState.Aligning:
                //    // 查询准备状态
                //    await QueryPreparationStatusAsync();
                //    break;

                default:
                    //await QueryBasicStatusAsync();
                    break;
            }
        }

        private async Task QueryBasicStatusAsync()
        {
            // 发布查询指令事件
            _client?.QueryStatusAsync();

            // 更新心跳时间
            _currentStatus.LastHeartbeat = DateTime.Now;

            // 发布状态更新事件
            EventAggregator.Publish(new StateUpdatedEvent(GetStatus()));
        }

        private async Task QueryTestingStatusAsync()
        {
            // 查询当前坐标
            EventAggregator.Publish(new CommandSentEvent("$Q#"));

            // 查询测试完成状态
            EventAggregator.Publish(new CommandSentEvent("$J#"));

            // 查询温度
            EventAggregator.Publish(new CommandSentEvent("$fl#"));

            // 更新心跳时间
            _currentStatus.LastHeartbeat = DateTime.Now;

            // 发布状态更新事件
            EventAggregator.Publish(new StateUpdatedEvent(GetStatus()));
        }

        private async Task QueryPreparationStatusAsync()
        {
            // 查询Z轴状态
            EventAggregator.Publish(new CommandSentEvent("$D#"));

            // 查询加热状态
            EventAggregator.Publish(new CommandSentEvent("$r#"));

            // 更新心跳时间
            _currentStatus.LastHeartbeat = DateTime.Now;

            // 发布状态更新事件
            EventAggregator.Publish(new StateUpdatedEvent(GetStatus()));
        }

        private void OnConnectionChanged(ConnectionStateChangedEvent @event)
        {
            if (@event.IsConnected)
            {
                TransitionToAsync(ProberState.Connected).Wait();
            }
            else
            {
                TransitionToAsync(ProberState.Disconnected).Wait();
                ResetStatus(_currentStatus);
            }
        }

        private void ResetStatus(ProberStatus status)
        {
            status.CurrentLotId = null;
            ProberStateStatus.Instance.CurrentWaferId = null;
        }

        private void OnCommandReceived(CommandReceivedEvent @event)
        {
            if (!@event.IsValid) return;

            string command = @event.Command;
            if (logger.IsDebugEnabled) logger.DebugFormat("RECV => {0}", command);
            // 更新状态信息
            UpdateStatusFromCommand(command);

            // 处理状态转换
            ProcessStateTransition(command);
        }

        private void OnCommunicationError(CommunicationErrorEvent @event)
        {
            // 通信错误，转到错误状态
            //TransitionToAsync(ProberState.Error, @event).Wait();
        }

        private void UpdateStatusFromCommand(string command)
        {
            // 根据收到的指令更新状态信息
             if (command.StartsWith("WaferId:") && command.Length > 1)
            {
                ProberStateStatus.Instance.CurrentWaferId = command.Substring(8);
                logger.Info(ProberStateStatus.Instance.CurrentWaferId);
                EventAggregator.Publish(new StateTransitionEvent(_currentStatus.CurrentState, _currentStatus.CurrentState, true, null));
            }
            else if (command.StartsWith("r") && command.Length == 3)
            {
                //_currentStatus
            }
            else if (command.StartsWith("fl") && command.Length > 2)
            {
                if (decimal.TryParse(command.Substring(2), out decimal temp))
                {
                    _currentStatus.CurrentTemperature = temp;
                }
            }
            else if (command.StartsWith("raxis") && command.Length > 6)
            {
                string axis = command.Substring(5);
                string[] xyzAxises = axis.Split(',');
                if (xyzAxises != null && xyzAxises.Length == 3)
                {
                    if (decimal.TryParse(xyzAxises[0], out decimal x))
                    {
                        _currentStatus.MotionAxisX = x / 1000;
                    }
                    if (decimal.TryParse(xyzAxises[1], out decimal y))
                    {
                        _currentStatus.MotionAxisY = y / 1000;
                    }
                    if (decimal.TryParse(xyzAxises[2], out decimal z))
                    {
                        _currentStatus.MotionAxisZ = z / 1000;
                    }
                    EventAggregator.Publish(new MotionAxisUpdatedEvent(GetXYZAxis()));
                }
            }
            else if (command == "67") // Z Up完成
            {
                _currentStatus.IsZUp = true;
                _currentStatus.IsZDown = false;
                _currentStatus.IsNeedleDown = true;
                //if(_currentStatus.CurrentMotionCommand.CommandType == "J")
                //{
                //    _currentStatus.CurrentPosition.CurrentX = _currentStatus.CurrentPosition.TargetX = _currentStatus.CurrentMotionCommand.TargetX;
                //    _currentStatus.CurrentPosition.CurrentY = _currentStatus.CurrentPosition.TargetY = _currentStatus.CurrentMotionCommand.TargetY;
                //}
            }
            else if (command == "68") // Z Down完成
            {
                _currentStatus.IsZUp = false;
                _currentStatus.IsZDown = true;
                _currentStatus.IsNeedleDown = false;
            }
            else if (command == "81") // 测试完成
            {
                _currentStatus.TestedDies = _currentStatus.TotalDies;
            }
            else if (command.StartsWith("Y") && command.Length > 1)
            {
                if (int.TryParse(command.Substring(1), out int totalDies))
                {
                    _currentStatus.TotalDies = totalDies;
                }
            }
            else if (command.StartsWith("V") && command.Length > 1)
            {
                _currentStatus.CurrentLotId = command.Substring(1);
            } else if (command.StartsWith("rr") && command.Length > 1)
            {
                _currentStatus.CurrentMappingFile = command.Substring(2);
                EventAggregator.Publish(new MappingFileLoadEvent(_currentStatus.CurrentMappingFile));
            }

            ///查询状态位置； e 是错误的
            if (command == "i" || command == "m" || command == "a" || command =="e")
            {
                ProberStateMachineLocation.LocationChange(command);
            }



        }

        private ProberMotionAxisStatus GetXYZAxis()
        {
            lock (_stateLock)
            {
                ProberMotionAxisStatus axisStatus = new ProberMotionAxisStatus()
                {
                    CurrentAxisX = _currentStatus.MotionAxisX,
                    CurrentAxisY = _currentStatus.MotionAxisY,
                    CurrentAxisZ = _currentStatus.MotionAxisZ,
                };
                return axisStatus;
            }
        }

        private void ProcessStateTransition(string command)
        {
            if (command == "70") // 晶圆加载完成
            {
                TransitionToAsync(ProberState.Ready).Wait();
                return;
            }else if(command == "92")
            {
                TransitionToAsync(ProberState.Paused).Wait();
                return;
            }
            else if (command.StartsWith("rr") && command.Length > 5) // 晶圆加载完成
            {
                TransitionToAsync(ProberState.WaferLoaded).Wait();
                return;
            }
            else if (command.StartsWith("b") && command.Length > 1) // 收到晶圆ID
            {
                //TransitionToAsync(ProberState.WaferLoaded).Wait();
            }
            switch (CurrentState)
            {
                case ProberState.Connected:
                    break;

                case ProberState.Ready:
                    if (command.StartsWith("b") && command.Length > 1) // 收到晶圆ID
                    {
                        //TransitionToAsync(ProberState.WaferLoaded).Wait();
                    }
                    break;

                //case ProberState.WaferLoaded:
                //    if (command == "67") // 可以开始测试
                //    {
                //        TransitionToAsync(ProberState.Testing).Wait();
                //    }
                //    break;

                //case ProberState.Testing:
                //    ProcessStateTransitionTesting(command);
                //    break;

                case ProberState.Stopping:
                    if (command == "85") // 停止完成
                    {
                        TransitionToAsync(ProberState.Stoped).Wait();
                    }
                    break;
                case ProberState.Stoped:
                    if (command == "91") // 停止完成
                    {
                        TransitionToAsync(ProberState.Ready).Wait();
                    }
                    break;

                case ProberState.Error:
                    if (command == "91") // 可以开始测试
                    {
                        TransitionToAsync(ProberState.Ready).Wait();
                    }
                    break;
            }
        }

        private void ProcessStateTransitionTesting(string command)
        {
            switch (command)
            {
                case "67":// 测试完成
                    TransitionToAsync(ProberState.Ready).Wait();
                    break;
               case "81":// 测试完成
                    TransitionToAsync(ProberState.Ready).Wait();
                    break;
                case "90":// 停止
                    TransitionToAsync(ProberState.Stopping).Wait();
                    break;
                default:
                    break;
            }
        }

        public async Task<bool> TransitionToAsync(ProberState newState, object data = null)
        {
            lock (_stateLock)
            {
                // 检查状态转换是否允许
                if (!IsTransitionAllowed(_currentState, newState))
                {
                    EventAggregator.Publish(new StateTransitionEvent(_currentState, newState, false, data));
                    return false;
                }

                ProberState oldState = _currentState;
                _currentState = newState;
                _lastStateChange = DateTime.Now;

                // 更新状态信息
                _currentStatus.CurrentState = newState;
                _currentStatus.LastStateChange = _lastStateChange;

                // 发布状态转换事件
                Task state = Task.Factory.StartNew(() =>
                {
                    EventAggregator.Publish(new StateTransitionEvent(oldState, newState, true, data));
                    // 记录状态转换
                    //EventAggregator.Publish(new CommandSentEvent($"$M{oldState}->{newState}#"));
                });

                return true;
            }
        }

        private bool IsTransitionAllowed(ProberState fromState, ProberState toState)
        {
            if(toState == ProberState.Connected) return true;
            if (fromState == ProberState.Maintenance) return false;
            return true;
            //return _allowedTransitions.ContainsKey(fromState) &&
            //       _allowedTransitions[fromState].Contains(toState);
        }

        public ProberStatus GetStatus()
        {
            ProberStatus status = new ProberStatus
            {
                CurrentState = _currentStatus.CurrentState,
                LastStateChange = _currentStatus.LastStateChange,
                CurrentLotId = _currentStatus.CurrentLotId,
                TestedDies = _currentStatus.TestedDies,
                TotalDies = _currentStatus.TotalDies,
                //IsHeaterNormal = _currentStatus.IsHeaterNormal,
                CurrentTemperature = _currentStatus.CurrentTemperature,
                IsNeedleDown = _currentStatus.IsNeedleDown,
                IsZUp = _currentStatus.IsZUp,
                IsZDown = _currentStatus.IsZDown,
                IsMoving = _currentStatus.IsMoving,
                LastHeartbeat = _currentStatus.LastHeartbeat,
                ErrorMessage = _currentStatus.ErrorMessage,
                //
                CurrentMotionCommand = _currentStatus.CurrentMotionCommand,
                MotionStatus = _currentStatus.MotionStatus,
                CurrentPosition = _currentStatus.CurrentPosition,
                ZAxisStatus = _currentStatus.ZAxisStatus,
                CurrentMappingFile = _currentStatus.CurrentMappingFile,
            };
            status.HeaterInfo = _heaterMonitor.GetHeaterInfo();
            status.LastHeaterCheck = DateTime.Now;
            return status;
        }

        public override void Dispose()
        {
            _heaterMonitor?.Dispose();
            _motionMonitor?.Dispose();
            base.Dispose();
            _statusTimer?.Dispose();
            _heartbeatTimer?.Dispose();
            _monitoringCts?.Dispose();
        }

        public TemperatureStatistics GetTemperatureStatistics()
        {
            return _heaterMonitor.GetTemperatureStatistics();
        }

        public async Task StartHeaterMonitorAsync()
        {
            await _heaterMonitor.StartAsync();
        }

        public async Task StopHeaterMonitorAsync()
        {
            await _heaterMonitor.StopAsync();
        }

        public void ReloadSettings(int defaultXYMotionTimeout, int defaultZMotionTimeout)
        {
            _motionMonitor.ReloadSettings(defaultXYMotionTimeout, defaultZMotionTimeout);
        }
    }
}

// 新增状态更新事件
public class StateUpdatedEvent : BaseEvent
{
    public ProberStatus Status { get; }
    public StateUpdatedEvent(ProberStatus status)
    {
        Status = status;
    }
}

public class MotionAxisUpdatedEvent : BaseEvent
{
    public ProberMotionAxisStatus Axis { get; }

    public MotionAxisUpdatedEvent(ProberMotionAxisStatus axis)
    {
        Axis = axis;
    }
}

public class MappingFileLoadEvent : BaseEvent
{
    public string MappingFile { get; }
    public MappingFileLoadEvent(string mappingFile)
    {
        MappingFile = mappingFile;
    }
}