using CVWaferProber.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.StateMachine;

namespace CVWaferProber.Components
{
    /// <summary>
    /// DevProberDebugControl.xaml 的交互逻辑
    /// </summary>
    public partial class DevProberDebugControl : UserControl
    {
        private IWaferProberClient _client;
        private IStateMachine _stateMachine;
        private int _messageCount = 0;
        private DateTime _lastMessageTime = DateTime.MinValue;
        // 添加运动相关字段
        private ObservableCollection<MotionCommand> _motionHistory = new ObservableCollection<MotionCommand>();
        // 添加加热监控器字段
        //private HeaterMonitor _heaterMonitor;
        private ObservableCollection<TemperatureRecord> _temperatureHistory = new ObservableCollection<TemperatureRecord>();
        public DevProberDebugControl()
        {
            InitializeComponent();
            this.Loaded += DevProberDebugControl_Loaded;
            this.Unloaded += DevProberDebugControl_Unloaded;
        }

        private void DevProberDebugControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DevProberDebugViewModel viewModel)
            {
                viewModel.Cleanup();

                var eventAggregator = _client.EventAggregator;

                eventAggregator.Unsubscribe<CommandSentEvent>(OnCommandSent);
                eventAggregator.Unsubscribe<CommandReceivedEvent>(OnCommandReceived);
                eventAggregator.Unsubscribe<StateUpdatedEvent>(OnStateUpdated);
                eventAggregator.Unsubscribe<StateTransitionEvent>(OnStateTransition);
                eventAggregator.Unsubscribe<ConnectionStateChangedEvent>(OnConnectionChanged);
            }
        }

        private void DevProberDebugControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext != null && DataContext is DevProberDebugViewModel devVM)
            {
                _client = devVM.ProberClient;
                _stateMachine = devVM.StateMachine;
                var eventAggregator = _client.EventAggregator;

                eventAggregator.Subscribe<CommandSentEvent>(OnCommandSent);
                eventAggregator.Subscribe<CommandReceivedEvent>(OnCommandReceived);
                eventAggregator.Subscribe<StateUpdatedEvent>(OnStateUpdated);
                eventAggregator.Subscribe<StateTransitionEvent>(OnStateTransition);
                eventAggregator.Subscribe<ConnectionStateChangedEvent>(OnConnectionChanged);

                var status = _stateMachine.GetStatus();
                UpdateStateDisplay(status);
            }
        }
        private void OnCommandSent(CommandSentEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                _messageCount++;
                AddLog($"→ Send: {@event.Command}", LogType.Send);
            });
        }

        private void OnCommandReceived(CommandReceivedEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                _messageCount++;
                _lastMessageTime = DateTime.Now;

                if (@event.IsValid)
                {
                    AddLog($"← Recv: {@event.Command}", LogType.Receive);
                }
                else
                {
                    AddLog($"← Invalid: {@event.RawData} ({@event.ErrorMessage})", LogType.Error);
                }
            });
        }
        private void OnStateTransition(StateTransitionEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                AddLog($"StateUpdate: {@event.FromState} → {@event.ToState} ({(@event.Success ? "Success" : "Failed")})",
                @event.Success ? LogType.Info : LogType.Error);

                UpdateStateDisplay();
            });
        }
        private void UpdateStateDisplay()
        {
            if (_stateMachine != null)
            {
                var status = _stateMachine.GetStatus();
                UpdateStateDisplay(status);
            }
        }
        private void OnConnectionChanged(ConnectionStateChangedEvent @event)
        {
            Task.Delay(1).ContinueWith(t => {
                var status = _stateMachine.GetStatus();
                UpdateStateDisplay(status);
            });
            //Dispatcher.Invoke(() =>
            //{
            //    //btnConnect.IsEnabled = !@event.IsConnected;
            //    //btnDisconnect.IsEnabled = @event.IsConnected;
            //    tbStatus.Text = @event.IsConnected ? "已连接" : "未连接";

            //    // 使用预定义的 Brush
            //    tbStatus.Foreground = @event.IsConnected ? Brushes.Green : Brushes.Red;

            //    //tbConnectionStatus.Text = @event.IsConnected ?
            //    //    $"状态: 已连接 ({@event.ServerIp}:{@event.Port})" :
            //    //    "状态: 未连接";
            //});
        }
        private void OnStateUpdated(StateUpdatedEvent @event)
        {
            UpdateStateDisplay(@event.Status);
        }
        private void UpdateStateDisplay(ProberStatus status)
        {
            Dispatcher.Invoke(() =>
            {
                // 更新状态标签
                tbCurrentState.Text = $"当前状态: {GetStateDisplayName(status.CurrentState)}";
                tbStateDuration.Text = $"持续时间: {FormatTimeSpan(status.CurrentStateDuration)}";

                // 更新状态颜色
                Color stateColor = GetStateColor(status.CurrentState);
                tbCurrentState.Foreground = new SolidColorBrush(stateColor);

                // 更新晶圆信息
                tbWaferInfo.Text = $"晶圆: {status.CurrentWaferId ?? "无"} | 批次: {status.CurrentLotId ?? "无"}";
                // 更新晶圆信息
                //tbMechanicalNo.Text = $"机台编号:  {_responseProcessor.GetMachineInfo().MachineNumber ?? "无"}";

                // 更新测试进度
                //tbTestProgress.Text = $"测试进度: {status.TestedDies}/{status.TotalDies} ({status.ProgressPercentage:F1}%)";

                // 更新温度信息
                //tbTemperature.Text = $"温度: {status.CurrentTemperature:F1}℃ | 加热器: {(status.IsHeaterNormal ? "正常" : "异常")}";

                // 更新机械状态
                //tbMechanicalStatus.Text = $"Z轴: {(status.IsZUp ? "Up" : status.IsZDown ? "Down" : "未知")} | " +
                //                        $"探针: {(status.IsNeedleDown ? "Down" : "Up")} | " +
                //                        $"运动: {(status.IsMoving ? "进行中" : "停止")}";

                // 更新心跳状态
                tbHeartbeat.Text = $"心跳: {(status.IsHeartbeatOk ? "正常" : "异常")} | " +
                                 $"最后: {status.LastHeartbeat:HH:mm:ss}";

                // 更新错误信息
                if (!string.IsNullOrEmpty(status.ErrorMessage))
                {
                    tbErrorInfo.Text = $"错误: {status.ErrorMessage}";
                    tbErrorInfo.Foreground = Brushes.Red;
                    tbErrorInfo.Visibility = Visibility.Visible;
                }
                else
                {
                    tbErrorInfo.Visibility = Visibility.Collapsed;
                }
            });
        }

        private string GetStateDisplayName(ProberState state)
        {
            return state switch
            {
                ProberState.Disconnected => "未连接",
                ProberState.Connected => "已连接",
                ProberState.Ready => "就绪",
                ProberState.WaitingForWafer => "等待晶圆",
                ProberState.WaferLoaded => "晶圆已加载",
                ProberState.Aligning => "对齐中",
                ProberState.Testing => "测试中",
                ProberState.Paused => "暂停",
                ProberState.Stopping => "停止中",
                ProberState.Error => "错误",
                ProberState.Maintenance => "维护模式",
                _ => "未知"
            };
        }
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}:{timeSpan:mm\\:ss}";
            else if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan:mm\\:ss}";
            else
                return $"{timeSpan:ss}秒";
        }
        private Color GetStateColor(ProberState state)
        {
            return state switch
            {
                ProberState.Disconnected => Colors.Gray,
                ProberState.Connected => Colors.Blue,
                ProberState.Ready => Colors.Green,
                ProberState.WaitingForWafer => Colors.Orange,
                ProberState.WaferLoaded => Colors.LightGreen,
                ProberState.Aligning => Colors.Yellow,
                ProberState.Testing => Colors.Cyan,
                ProberState.Paused => Colors.Yellow,
                ProberState.Stopping => Colors.OrangeRed,
                ProberState.Error => Colors.Red,
                ProberState.Maintenance => Colors.Purple,
                _ => Colors.Gray
            };
        }

        #region 日志处理
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
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string prefix = type switch
                {
                    LogType.Send => "→ SEND",
                    LogType.Receive => "← RECV",
                    LogType.Success => "✓ SUCC",
                    LogType.Error => "✗ ERROR",
                    _ => "• INFO"
                };

                Color color = type switch
                {
                    LogType.Send => Colors.LightBlue,
                    LogType.Receive => Colors.LightGreen,
                    LogType.Success => Colors.LightGreen,
                    LogType.Error => Colors.LightCoral,
                    _ => Colors.LightGray
                };

                string logEntry = $"[{timestamp}] {prefix}: {message}";

                var doc = txtLog.Document;
                Paragraph paragraph = new Paragraph();
                Run run = new Run(logEntry);
                run.Foreground = new SolidColorBrush(color);
                paragraph.Inlines.Add(run);
                doc.Blocks.Add(paragraph);

                if (chkAutoScroll.IsChecked == true)
                {
                    txtLog.ScrollToEnd();
                }
            });
        }
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Document.Blocks.Clear();
            _messageCount = 0;
            //tbMessageCount.Text = "消息: 0";

            // 添加初始提示
            AddLog("日志已清空", LogType.Info);
        }

        #endregion
    }
}
