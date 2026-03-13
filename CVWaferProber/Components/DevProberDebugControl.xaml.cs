using CVWaferProber.Core.Config;
using CVWaferProber.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.StateMachine;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;

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

            // 初始化日志文本
            InitializeLogText();
        }


        private void UpdateUITexts()
        {
            // 更新状态显示
            UpdateStateDisplay();

            // 更新加热器显示
            UpdateHeaterDisplay();

            // 更新温度图表标题
            UpdateTemperatureChartTitle();
        }

        private void UpdateTemperatureChartTitle()
        {
            Dispatcher.Invoke(() =>
            {
                // 查找图表标题
                foreach (UIElement element in canvasTempChart.Children)
                {
                    if (element is TextBlock textBlock && textBlock.Text.Contains("Temperature"))
                    {
                        textBlock.Text = GetLocalizedString("Heater.TempHistory") + " (℃)";
                        return;
                    }
                }
            });
        }

        private void InitializeLogText()
        {
            UpdateLogReadyText();
        }

        private void UpdateLogReadyText()
        {
            Dispatcher.Invoke(() =>
            {
                // 获取当前语言的文本
                string readyText = GetLocalizedString("CommLog.Ready");

                // 检查文档是否为空或只有初始文本
                if (txtLog.Document.Blocks.Count == 0)
                {
                    // 创建新段落
                    Paragraph paragraph = new Paragraph();
                    Run run = new Run(readyText)
                    {
                        Foreground = Brushes.LightGray
                    };
                    paragraph.Inlines.Add(run);

                    // 添加到文档
                    txtLog.Document.Blocks.Add(paragraph);
                }
                else if (txtLog.Document.Blocks.Count == 1)
                {
                    var firstBlock = txtLog.Document.Blocks.FirstBlock as Paragraph;
                    if (firstBlock != null && firstBlock.Inlines.Count == 1)
                    {
                        var run = firstBlock.Inlines.FirstInline as Run;
                        if (run != null && (run.Text.Contains("ready") || run.Text.Contains("就绪")))
                        {
                            run.Text = readyText;
                        }
                    }
                }
            });
        }

        private string GetLocalizedString(string key)
        {
            try
            {
                return (string)Application.Current.TryFindResource(key) ?? key;
            }
            catch
            {
                return key;
            }
        }

        private void InitializeClient()
        {
            var eventAggregator = _client.EventAggregator;

            eventAggregator.Subscribe<CommandSentEvent>(OnCommandSent);
            eventAggregator.Subscribe<CommandReceivedEvent>(OnCommandReceived);
            eventAggregator.Subscribe<StateUpdatedEvent>(OnStateUpdated);
            eventAggregator.Subscribe<StateTransitionEvent>(OnStateTransition);
            eventAggregator.Subscribe<ConnectionStateChangedEvent>(OnConnectionChanged);
            // 订阅加热事件
            eventAggregator.Subscribe<HeaterStatusUpdatedEvent>(OnHeaterStatusUpdated);
            eventAggregator.Subscribe<TemperatureWarningEvent>(OnTemperatureWarning);

            eventAggregator.Subscribe<HeaterMonitorStartedEvent>(OnHeaterMonitorStarted);
            eventAggregator.Subscribe<HeaterMonitorStopedEvent>(OnHeaterMonitorStoped);

            var status = _stateMachine.GetStatus();
            UpdateStateDisplay(status);

            // 初始化温度图表
            InitializeTemperatureChart();
        }

        private void OnHeaterMonitorStoped(HeaterMonitorStopedEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                AddLog(GetLocalizedString("Log.HeaterMonitorStopped"), LogType.Info);
            });
        }

        private void OnHeaterMonitorStarted(HeaterMonitorStartedEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                AddLog(GetLocalizedString("Log.HeaterMonitorStarted"), LogType.Info);
            });
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
                //
                eventAggregator.Unsubscribe<HeaterStatusUpdatedEvent>(OnHeaterStatusUpdated);
                eventAggregator.Unsubscribe<TemperatureWarningEvent>(OnTemperatureWarning);

                eventAggregator.Unsubscribe<HeaterMonitorStartedEvent>(OnHeaterMonitorStarted);
                eventAggregator.Unsubscribe<HeaterMonitorStopedEvent>(OnHeaterMonitorStoped);
            }
        }

        private void DevProberDebugControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext != null && DataContext is DevProberDebugViewModel devVM)
            {
                _client = devVM.ProberClient;
                _stateMachine = devVM.StateMachine;
                InitializeClient();
            }
        }

        private void OnCommandSent(CommandSentEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                _messageCount++;
                AddLog($"{GetLocalizedString("Log.Send")}: {@event.Command}", LogType.Send);
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
                    AddLog($"{GetLocalizedString("Log.Receive")}: {@event.Command}", LogType.Receive);
                }
                else
                {
                    AddLog($"{GetLocalizedString("Log.Invalid")}: {@event.RawData} ({@event.ErrorMessage})", LogType.Error);
                }
            });
        }

        private void OnStateTransition(StateTransitionEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                string stateFrom = GetLocalizedStateName(@event.FromState);
                string stateTo = GetLocalizedStateName(@event.ToState);
                string status = @event.Success ? GetLocalizedString("Log.Success") : GetLocalizedString("Log.Failed");

                AddLog($"{GetLocalizedString("Log.StateUpdate")}: {stateFrom} → {stateTo} ({status})",
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
                string currentStateText = GetLocalizedString("MachineStatus.CurrentState");
                string durationText = GetLocalizedString("MachineStatus.Duration");
                string waferText = GetLocalizedString("MachineStatus.WaferInfo");
                string batchText = GetLocalizedString("MachineStatus.BatchInfo");
                string machineText = GetLocalizedString("MachineStatus.MechanicalNo");

                tbCurrentState.Text = $"{currentStateText} {GetLocalizedStateName(status.CurrentState)}";
                tbStateDuration.Text = $"{durationText} {FormatTimeSpan(status.CurrentStateDuration)}";

                // 更新状态颜色
                Color stateColor = GetStateColor(status.CurrentState);
                tbCurrentState.Foreground = new SolidColorBrush(stateColor);

                // 更新晶圆信息

                tbWaferInfo.Text = $"{waferText} {ProberStateStatus.Instance.CurrentWaferId ?? GetLocalizedString("Common.None")} | " +
                                 $"{batchText} {status.CurrentLotId ?? GetLocalizedString("Common.None")}";
            });
        }

        private string GetLocalizedStateName(ProberState state)
        {
            string key = state switch
            {
                ProberState.Disconnected => "State.Disconnected",
                ProberState.Connected => "State.Connected",
                ProberState.Ready => "State.Ready",
                ProberState.WaitingForWafer => "State.WaitingForWafer",
                ProberState.WaferLoaded => "State.WaferLoaded",
                ProberState.Aligning => "State.Aligning",
                ProberState.Testing => "State.Testing",
                ProberState.Paused => "State.Paused",
                ProberState.Stopping => "State.Stopping",
                ProberState.Error => "State.Error",
                ProberState.Maintenance => "State.Maintenance",
                _ => "State.Unknown"
            };

            return GetLocalizedString(key);
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}:{timeSpan:mm\\:ss}";
            else if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan:mm\\:ss}";
            else
                return $"{timeSpan:ss}{GetLocalizedString("Common.Second")}";
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

        #region 加热控制按钮
        private void BtnResetHeaterStats_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _temperatureHistory.Clear();
                InitializeTemperatureChart();
                UpdateHeaterDisplay();

                AddLog(GetLocalizedString("Log.HeaterStatsReset"), LogType.Info);
            }
            catch (Exception ex)
            {
                AddLog($"{GetLocalizedString("Log.HeaterStatsResetFailed")}: {ex.Message}", LogType.Error);
            }
        }
        #endregion

        #region Temperature Chart

        private void InitializeTemperatureChart()
        {
            // Clear the chart
            canvasTempChart.Children.Clear();

            // Draw coordinate axes
            DrawChartAxes();
        }

        private void UpdateTemperatureChart()
        {
            Dispatcher.Invoke(() =>
            {
                if (_temperatureHistory.Count < 2) return;

                // Clear previous chart content (keep axes)
                var toRemove = new List<UIElement>();
                foreach (UIElement element in canvasTempChart.Children)
                {
                    if (element is Line || element is Ellipse)
                    {
                        toRemove.Add(element);
                    }
                }
                foreach (var element in toRemove)
                {
                    canvasTempChart.Children.Remove(element);
                }

                // Calculate chart parameters
                double canvasWidth = canvasTempChart.ActualWidth;
                double canvasHeight = canvasTempChart.ActualHeight;
                double margin = 30;
                double chartWidth = canvasWidth - 2 * margin;
                double chartHeight = canvasHeight - 2 * margin;

                // Get temperature range
                var temps = _temperatureHistory.Select(t => (double)t.Temperature).ToList();
                double minTemp = temps.Min();
                double maxTemp = temps.Max();
                double tempRange = Math.Max(maxTemp - minTemp, 1.0); // At least a 1℃ range

                // Draw temperature curve
                Point? previousPoint = null;
                for (int i = 0; i < _temperatureHistory.Count; i++)
                {
                    var record = _temperatureHistory[i];

                    // Calculate coordinates
                    double x = margin + (i * chartWidth / (_temperatureHistory.Count - 1));
                    double y = canvasHeight - margin - (((double)record.Temperature - minTemp) * chartHeight / tempRange);

                    var point = new Point(x, y);

                    // Draw data point
                    var dot = new Ellipse
                    {
                        Width = 4,
                        Height = 4,
                        Fill = GetTemperatureColor(record.Temperature),
                        Stroke = Brushes.Black,
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(dot, x - 2);
                    Canvas.SetTop(dot, y - 2);
                    canvasTempChart.Children.Add(dot);

                    // Draw connecting line
                    if (previousPoint.HasValue)
                    {
                        var line = new Line
                        {
                            X1 = previousPoint.Value.X,
                            Y1 = previousPoint.Value.Y,
                            X2 = point.X,
                            Y2 = point.Y,
                            Stroke = Brushes.Blue,
                            StrokeThickness = 1
                        };
                        canvasTempChart.Children.Add(line);
                    }

                    previousPoint = point;
                }

                // Add temperature range labels
                AddChartLabels(minTemp, maxTemp, chartHeight, margin);
            });
        }

        private void DrawChartAxes()
        {
            double canvasWidth = canvasTempChart.ActualWidth;
            double canvasHeight = canvasTempChart.ActualHeight;
            double margin = 30;

            // X-axis
            var xAxis = new Line
            {
                X1 = margin,
                Y1 = canvasHeight - margin,
                X2 = canvasWidth - margin,
                Y2 = canvasHeight - margin,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            canvasTempChart.Children.Add(xAxis);

            // Y-axis
            var yAxis = new Line
            {
                X1 = margin,
                Y1 = margin,
                X2 = margin,
                Y2 = canvasHeight - margin,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            canvasTempChart.Children.Add(yAxis);

            // Chart title
            var title = new TextBlock
            {
                Text = GetLocalizedString("Heater.TempHistory") + " (℃)",
                FontSize = 10,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(title, canvasWidth / 2 - 30);
            Canvas.SetTop(title, 5);
            canvasTempChart.Children.Add(title);
        }

        private void AddChartLabels(double minTemp, double maxTemp, double chartHeight, double margin)
        {
            // Y-axis labels
            for (int i = 0; i <= 4; i++)
            {
                double temp = minTemp + (maxTemp - minTemp) * i / 4;
                double y = canvasTempChart.ActualHeight - margin - (chartHeight * i / 4);

                var label = new TextBlock
                {
                    Text = temp.ToString("F1"),
                    FontSize = 8,
                    Foreground = Brushes.Black
                };
                Canvas.SetLeft(label, margin - 25);
                Canvas.SetTop(label, y - 8);
                canvasTempChart.Children.Add(label);
            }

            // X-axis label (Time)
            var timeLabel = new TextBlock
            {
                Text = GetLocalizedString("Chart.Time") + " →",
                FontSize = 8,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(timeLabel, canvasTempChart.ActualWidth - margin - 20);
            Canvas.SetTop(timeLabel, canvasTempChart.ActualHeight - margin + 5);
            canvasTempChart.Children.Add(timeLabel);
        }

        private Brush GetTemperatureColor(decimal temperature)
        {
            if (temperature > 60) return Brushes.Red;
            if (temperature > 40) return Brushes.Orange;
            if (temperature > 20) return Brushes.Green;
            return Brushes.Blue;
        }

        #endregion

        #region Heater Event Handling

        private void OnHeaterStatusUpdated(HeaterStatusUpdatedEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                // Update display
                UpdateHeaterDisplay(@event);

                // Record to history
                if (@event.CurrentTemperature.HasValue)
                {
                    var record = new TemperatureRecord
                    {
                        Temperature = @event.CurrentTemperature.Value,
                        Status = @event.Status,
                        Timestamp = @event.Timestamp
                    };
                    _temperatureHistory.Add(record);

                    // Keep maximum of 100 records
                    if (_temperatureHistory.Count > 100)
                    {
                        _temperatureHistory.RemoveAt(0);
                    }

                    // Update chart
                    UpdateTemperatureChart();
                }

                // Log entry
                string statusText = GetLocalizedHeaterStatusText(@event.Status);
                string logMsg = $"{GetLocalizedString("Log.HeaterStatus")}: {statusText}";
                if (@event.CurrentTemperature.HasValue)
                {
                    logMsg += $", {GetLocalizedString("Log.Temperature")}: {@event.CurrentTemperature.Value:F1}℃";
                }
                AddLog(logMsg, GetLogTypeForHeaterStatus(@event.Status));
            });
        }

        private void OnTemperatureWarning(TemperatureWarningEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                string warningText = @event.WarningType switch
                {
                    TemperatureWarningType.TemperatureTooHigh => GetLocalizedString("Warning.TemperatureTooHigh"),
                    TemperatureWarningType.TemperatureTooLow => GetLocalizedString("Warning.TemperatureTooLow"),
                    TemperatureWarningType.TemperatureUnstable => GetLocalizedString("Warning.TemperatureUnstable"),
                    TemperatureWarningType.HeaterFault => GetLocalizedString("Warning.HeaterFault"),
                    TemperatureWarningType.SensorFault => GetLocalizedString("Warning.SensorFault"),
                    TemperatureWarningType.TemperatureDrift => GetLocalizedString("Warning.TemperatureDrift"),
                    _ => GetLocalizedString("Warning.TemperatureWarning")
                };

                string currentTempText = GetLocalizedString("Log.CurrentTemperature");
                string setTempText = GetLocalizedString("Log.SetTemperature");
                string deviationText = GetLocalizedString("Log.Deviation");

                string details = $"{currentTempText}: {@event.CurrentTemperature:F1}℃";
                if (@event.TargetTemperature.HasValue)
                {
                    details += $", {setTempText}: {@event.TargetTemperature.Value:F1}℃, {deviationText}: {@event.Deviation:F1}℃";
                }

                AddLog($"⚠ {GetLocalizedString("Warning.TemperatureWarning")}: {warningText} - {details}", LogType.Error);
            });
        }

        #endregion

        #region Heater Status Display

        private void UpdateHeaterDisplay()
        {
            var status = _stateMachine.GetStatus();
            if (status != null)
            {
                var heaterInfo = status.HeaterInfo;
                UpdateHeaterDisplay(heaterInfo);
            }
        }

        private void UpdateHeaterDisplay(HeaterStatusUpdatedEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                // Update temperature display
                if (@event.CurrentTemperature.HasValue)
                {
                    string currentTempText = GetLocalizedString("Heater.CurrentTemp");
                    tbCurrentTemp.Text = $"{currentTempText} {@event.CurrentTemperature.Value:F1} ℃";

                    // Set color based on temperature
                    if (@event.CurrentTemperature.Value > 60)
                        tbCurrentTemp.Foreground = new SolidColorBrush(Colors.Red);
                    else if (@event.CurrentTemperature.Value > 40)
                        tbCurrentTemp.Foreground = new SolidColorBrush(Colors.Orange);
                    else
                        tbCurrentTemp.Foreground = new SolidColorBrush(Colors.Black);
                }
                else
                {
                    tbCurrentTemp.Text = $"{GetLocalizedString("Heater.CurrentTemp")} -- ℃";
                    tbCurrentTemp.Foreground = new SolidColorBrush(Colors.Gray);
                }

                if (@event.SetTemperature.HasValue)
                {
                    string setTempText = GetLocalizedString("Heater.SetTemp");
                    tbSetTemp.Text = $"{setTempText} {@event.SetTemperature.Value / 10:F1} ℃";
                }
                else
                {
                    tbSetTemp.Text = $"{GetLocalizedString("Heater.SetTemp")} -- ℃";
                }

                // Update status display
                string statusText = GetLocalizedHeaterStatusText(@event.Status);
                Color statusColor = GetHeaterStatusColor(@event.Status);

                string heaterStatusText = GetLocalizedString("Heater.HeaterStatus");
                tbHeaterStatus.Text = $"{heaterStatusText} {statusText}";
                tbHeaterStatus.Foreground = new SolidColorBrush(statusColor);
            });
        }

        private void UpdateHeaterDisplay(TemperatureInfo heaterInfo)
        {
            Dispatcher.Invoke(() =>
            {
                string currentTempText = GetLocalizedString("Heater.CurrentTemp");
                string setTempText = GetLocalizedString("Heater.SetTemp");
                string heaterStatusText = GetLocalizedString("Heater.HeaterStatus");

                tbCurrentTemp.Text = $"{currentTempText} {heaterInfo.CurrentTemperature:F1} ℃";
                tbSetTemp.Text = heaterInfo.SetTemperature.HasValue ?
                    $"{setTempText} {heaterInfo.SetTemperature.Value:F1} ℃" : $"{setTempText} -- ℃";

                string statusText = GetLocalizedHeaterStatusText(heaterInfo.Status);
                Color statusColor = GetHeaterStatusColor(heaterInfo.Status);

                tbHeaterStatus.Text = $"{heaterStatusText} {statusText}";
                tbHeaterStatus.Foreground = new SolidColorBrush(statusColor);
            });
        }

        private string GetLocalizedHeaterStatusText(HeaterChuckStatus status)
        {
            string key = status switch
            {
                HeaterChuckStatus.Unknown => "HeaterStatus.Unknown",
                HeaterChuckStatus.NoHeater => "HeaterStatus.NoHeater",
                HeaterChuckStatus.TemperatureTooHigh => "HeaterStatus.TemperatureTooHigh",
                HeaterChuckStatus.TemperatureTooLow => "HeaterStatus.TemperatureTooLow",
                HeaterChuckStatus.Normal => "HeaterStatus.Normal",
                HeaterChuckStatus.Other => "HeaterStatus.Other",
                HeaterChuckStatus.Heating => "HeaterStatus.Heating",
                HeaterChuckStatus.Cooling => "HeaterStatus.Cooling",
                HeaterChuckStatus.TemperatureStable => "HeaterStatus.TemperatureStable",
                HeaterChuckStatus.TemperatureFluctuating => "HeaterStatus.TemperatureFluctuating",
                HeaterChuckStatus.SensorFault => "HeaterStatus.SensorFault",
                HeaterChuckStatus.HeaterFault => "HeaterStatus.HeaterFault",
                _ => "HeaterStatus.Unknown"
            };

            return GetLocalizedString(key);
        }

        private Color GetHeaterStatusColor(HeaterChuckStatus status)
        {
            return status switch
            {
                HeaterChuckStatus.Unknown => Colors.Gray,
                HeaterChuckStatus.NoHeater => Colors.Gray,
                HeaterChuckStatus.TemperatureTooHigh => Colors.Red,
                HeaterChuckStatus.TemperatureTooLow => Colors.Blue,
                HeaterChuckStatus.Normal => Colors.Green,
                HeaterChuckStatus.Other => Colors.Orange,
                HeaterChuckStatus.Heating => Colors.Orange,
                HeaterChuckStatus.Cooling => Colors.Blue,
                HeaterChuckStatus.TemperatureStable => Colors.Green,
                HeaterChuckStatus.TemperatureFluctuating => Colors.Yellow,
                HeaterChuckStatus.SensorFault => Colors.Red,
                HeaterChuckStatus.HeaterFault => Colors.Red,
                _ => Colors.Gray
            };
        }

        private LogType GetLogTypeForHeaterStatus(HeaterChuckStatus status)
        {
            return status switch
            {
                HeaterChuckStatus.Normal => LogType.Success,
                HeaterChuckStatus.TemperatureStable => LogType.Success,
                HeaterChuckStatus.Unknown => LogType.Info,
                HeaterChuckStatus.NoHeater => LogType.Warning,
                _ => LogType.Error
            };
        }

        #endregion

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
                    LogType.Send => GetLocalizedString("Log.Send"),
                    LogType.Receive => GetLocalizedString("Log.Receive"),
                    LogType.Success => GetLocalizedString("Log.Success"),
                    LogType.Error => GetLocalizedString("Log.Error"),
                    _ => GetLocalizedString("Log.Info")
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

            // 添加初始提示
            UpdateLogReadyText();

            // 记录清空操作
            AddLog(GetLocalizedString("Log.LogCleared"), LogType.Info);
        }

        #endregion

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            canvasTempChart.InvalidateVisual();
            InitializeTemperatureChart();
        }
    }
}