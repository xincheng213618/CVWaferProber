using CVWaferProber.ViewModels;
using MySqlX.XDevAPI.Common;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
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
                AddLog($"HeaterMonitor Stoped", LogType.Info);
            });
        }

        private void OnHeaterMonitorStarted(HeaterMonitorStartedEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                AddLog($"HeaterMonitor Started", LogType.Info);
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

                //// 更新心跳状态
                //tbHeartbeat.Text = $"心跳: {(status.IsHeartbeatOk ? "正常" : "异常")} | " +
                //                 $"最后: {status.LastHeartbeat:HH:mm:ss}";

                //// 更新错误信息
                //if (!string.IsNullOrEmpty(status.ErrorMessage))
                //{
                //    tbErrorInfo.Text = $"错误: {status.ErrorMessage}";
                //    tbErrorInfo.Foreground = Brushes.Red;
                //    tbErrorInfo.Visibility = Visibility.Visible;
                //}
                //else
                //{
                //    tbErrorInfo.Visibility = Visibility.Collapsed;
                //}
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
        #region 加热控制按钮
        private void BtnResetHeaterStats_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _temperatureHistory.Clear();
                InitializeTemperatureChart();
                UpdateHeaterDisplay();

                AddLog("加热器统计已重置", LogType.Info);
            }
            catch (Exception ex)
            {
                AddLog($"重置加热器统计失败: {ex.Message}", LogType.Error);
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
                Text = "Temperature History (℃)",
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
                Text = "Time →",
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
                string statusText = GetHeaterStatusText(@event.Status);
                string logMsg = $"Heater Status: {statusText}";
                if (@event.CurrentTemperature.HasValue)
                {
                    logMsg += $", Temperature: {@event.CurrentTemperature.Value:F1}℃";
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
                    TemperatureWarningType.TemperatureTooHigh => "Temperature Too High",
                    TemperatureWarningType.TemperatureTooLow => "Temperature Too Low",
                    TemperatureWarningType.TemperatureUnstable => "Temperature Unstable",
                    TemperatureWarningType.HeaterFault => "Heater Fault",
                    TemperatureWarningType.SensorFault => "Sensor Fault",
                    TemperatureWarningType.TemperatureDrift => "Temperature Drift",
                    _ => "Temperature Warning"
                };

                string details = $"Current Temperature: {@event.CurrentTemperature:F1}℃";
                if (@event.TargetTemperature.HasValue)
                {
                    details += $", Set: {@event.TargetTemperature.Value:F1}℃, Deviation: {@event.Deviation:F1}℃";
                }

                AddLog($"⚠ Temperature Warning: {warningText} - {details}", LogType.Error);

                // Display warning in status bar
                //tbErrorInfo.Text = $"Temperature Warning: {warningText}";
                //tbErrorInfo.Foreground = new SolidColorBrush(Colors.Orange);
                //tbErrorInfo.Visibility = Visibility.Visible;
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

                // Update statistics
                //var stats = _stateMachine.GetTemperatureStatistics();
                //UpdateHeaterStatistics(stats);
            }
        }

        private void UpdateHeaterDisplay(HeaterStatusUpdatedEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                // Update temperature display
                if (@event.CurrentTemperature.HasValue)
                {
                    tbCurrentTemp.Text = $"Current Temperature: {@event.CurrentTemperature.Value:F1} ℃";

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
                    tbCurrentTemp.Text = "Current Temperature: -- ℃";
                    tbCurrentTemp.Foreground = new SolidColorBrush(Colors.Gray);
                }

                if (@event.SetTemperature.HasValue)
                {
                    tbSetTemp.Text = $"Set Temperature: {@event.SetTemperature.Value / 10:F1} ℃";
                }
                else
                {
                    tbSetTemp.Text = "Set Temperature: -- ℃";
                }

                // Update status display
                string statusText = GetHeaterStatusText(@event.Status);
                Color statusColor = GetHeaterStatusColor(@event.Status);

                tbHeaterStatus.Text = $"Heater Status: {statusText}";
                tbHeaterStatus.Foreground = new SolidColorBrush(statusColor);

                // Update availability
                //tbHeaterAvailability.Text = $"Heater: {(@event.Status == HeaterChuckStatus.NoHeater ? "Unavailable" : "Available")}";
                //tbHeaterAvailability.Foreground = @event.Status == HeaterChuckStatus.NoHeater ?
                //    new SolidColorBrush(Colors.Gray) : new SolidColorBrush(Colors.Green);

                //// Update last update time
                //tbLastTempUpdate.Text = $"Last Update: {@event.Timestamp:HH:mm:ss}";
            });
        }

        private void UpdateHeaterDisplay(TemperatureInfo heaterInfo)
        {
            Dispatcher.Invoke(() =>
            {
                tbCurrentTemp.Text = $"Current Temperature: {heaterInfo.CurrentTemperature:F1} ℃";
                tbSetTemp.Text = heaterInfo.SetTemperature.HasValue ?
                    $"Set Temperature: {heaterInfo.SetTemperature.Value:F1} ℃" : "Set Temperature: -- ℃";

                string statusText = GetHeaterStatusText(heaterInfo.Status);
                Color statusColor = GetHeaterStatusColor(heaterInfo.Status);

                tbHeaterStatus.Text = $"Heater Status: {statusText}";
                tbHeaterStatus.Foreground = new SolidColorBrush(statusColor);

                /*
                tbTempTolerance.Text = $"Temperature Tolerance: ±{heaterInfo.TemperatureTolerance?.ToString("F1") ?? "0.5"}℃";

                if (heaterInfo.TemperatureTrend.HasValue)
                {
                    string trend = heaterInfo.TemperatureTrend.Value > 0 ? "↑" :
                                  heaterInfo.TemperatureTrend.Value < 0 ? "↓" : "→";
                    tbTempTrend.Text = $"Temperature Trend: {Math.Abs(heaterInfo.TemperatureTrend.Value):F2}℃/min {trend}";
                }
                else
                {
                    tbTempTrend.Text = "Temperature Trend: -- ℃/min";
                }

                tbTempStability.Text = $"Temperature Stability: {(heaterInfo.IsTemperatureStable ? "Stable" : "Unstable")}";
                tbTempStability.Foreground = heaterInfo.IsTemperatureStable ?
                    new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Orange);
                tbLastTempUpdate.Text = $"Last Update: {heaterInfo.LastTemperatureUpdate:HH:mm:ss}";
                 */
            });
        }

        private void UpdateHeaterStatistics(TemperatureStatistics stats)
        {
            Dispatcher.Invoke(() =>
            {
                //tbUptime.Text = $"Uptime: {stats.Uptime:hh\\:mm\\:ss}";
                //tbFaultCount.Text = $"Fault Count: {stats.FaultCount}";

                //// Update statistics text
                //if (stats.TotalRecords > 0)
                //{
                //    tbTempStats.Text = $"Statistics: Min {stats.MinTemperature:F1}℃, " +
                //                     $"Max {stats.MaxTemperature:F1}℃, " +
                //                     $"Avg {stats.AverageTemperature?.ToString("F1") ?? "N/A"}℃";
                //}
            });
        }

        private string GetHeaterStatusText(HeaterChuckStatus status)
        {
            return status switch
            {
                HeaterChuckStatus.Unknown => "Unknown",
                HeaterChuckStatus.NoHeater => "No Heater Chuck",
                HeaterChuckStatus.TemperatureTooHigh => "Temperature Too High",
                HeaterChuckStatus.TemperatureTooLow => "Temperature Too Low",
                HeaterChuckStatus.Normal => "Normal",
                HeaterChuckStatus.Other => "Other Status",
                HeaterChuckStatus.Heating => "Heating",
                HeaterChuckStatus.Cooling => "Cooling",
                HeaterChuckStatus.TemperatureStable => "Temperature Stable",
                HeaterChuckStatus.TemperatureFluctuating => "Temperature Fluctuating",
                HeaterChuckStatus.SensorFault => "Sensor Fault",
                HeaterChuckStatus.HeaterFault => "Heater Fault",
                _ => "Unknown"
            };
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

        /*
        #region 温度图表

        private void InitializeTemperatureChart()
        {
            // 清空图表
            canvasTempChart.Children.Clear();

            // 绘制坐标轴
            DrawChartAxes();
        }

        private void UpdateTemperatureChart()
        {
            Dispatcher.Invoke(() =>
            {
                if (_temperatureHistory.Count < 2) return;

                // 清空之前的图表内容（保留坐标轴）
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

                // 计算图表参数
                double canvasWidth = canvasTempChart.ActualWidth;
                double canvasHeight = canvasTempChart.ActualHeight;
                double margin = 30;
                double chartWidth = canvasWidth - 2 * margin;
                double chartHeight = canvasHeight - 2 * margin;

                // 获取温度范围
                var temps = _temperatureHistory.Select(t => (double)t.Temperature).ToList();
                double minTemp = temps.Min();
                double maxTemp = temps.Max();
                double tempRange = Math.Max(maxTemp - minTemp, 1.0); // 至少1℃的范围

                // 绘制温度曲线
                Point? previousPoint = null;
                for (int i = 0; i < _temperatureHistory.Count; i++)
                {
                    var record = _temperatureHistory[i];

                    // 计算坐标
                    double x = margin + (i * chartWidth / (_temperatureHistory.Count - 1));
                    double y = canvasHeight - margin - (((double)record.Temperature - minTemp) * chartHeight / tempRange);

                    var point = new Point(x, y);

                    // 绘制数据点
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

                    // 绘制连线
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

                // 添加温度范围标签
                AddChartLabels(minTemp, maxTemp, chartHeight, margin);
            });
        }

        private void DrawChartAxes()
        {
            double canvasWidth = canvasTempChart.ActualWidth;
            double canvasHeight = canvasTempChart.ActualHeight;
            double margin = 30;

            // X轴
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

            // Y轴
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

            // 图表标题
            var title = new TextBlock
            {
                Text = "温度历史 (℃)",
                FontSize = 10,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(title, canvasWidth / 2 - 30);
            Canvas.SetTop(title, 5);
            canvasTempChart.Children.Add(title);
        }

        private void AddChartLabels(double minTemp, double maxTemp, double chartHeight, double margin)
        {
            // Y轴标签
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

            // X轴标签（时间）
            var timeLabel = new TextBlock
            {
                Text = "时间 →",
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
        #region 加热事件处理

        private void OnHeaterStatusUpdated(HeaterStatusUpdatedEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                // 更新显示
                UpdateHeaterDisplay(@event);

                // 记录到历史
                if (@event.CurrentTemperature.HasValue)
                {
                    var record = new TemperatureRecord
                    {
                        Temperature = @event.CurrentTemperature.Value,
                        Status = @event.Status,
                        Timestamp = @event.Timestamp
                    };
                    _temperatureHistory.Add(record);

                    // 保持最多100条记录
                    if (_temperatureHistory.Count > 100)
                    {
                        _temperatureHistory.RemoveAt(0);
                    }

                    // 更新图表
                    UpdateTemperatureChart();
                }

                // 记录日志
                string statusText = GetHeaterStatusText(@event.Status);
                string logMsg = $"加热器状态: {statusText}";
                if (@event.CurrentTemperature.HasValue)
                {
                    logMsg += $", 温度: {@event.CurrentTemperature.Value:F1}℃";
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
                    TemperatureWarningType.TemperatureTooHigh => "温度过高",
                    TemperatureWarningType.TemperatureTooLow => "温度过低",
                    TemperatureWarningType.TemperatureUnstable => "温度不稳定",
                    TemperatureWarningType.HeaterFault => "加热器故障",
                    TemperatureWarningType.SensorFault => "传感器故障",
                    TemperatureWarningType.TemperatureDrift => "温度漂移",
                    _ => "温度警告"
                };

                string details = $"当前温度: {@event.CurrentTemperature:F1}℃";
                if (@event.TargetTemperature.HasValue)
                {
                    details += $", 设定: {@event.TargetTemperature.Value:F1}℃, 偏差: {@event.Deviation:F1}℃";
                }

                AddLog($"⚠ 温度警告: {warningText} - {details}", LogType.Error);

                // 在状态栏显示警告
                //tbErrorInfo.Text = $"温度警告: {warningText}";
                //tbErrorInfo.Foreground = new SolidColorBrush(Colors.Orange);
                //tbErrorInfo.Visibility = Visibility.Visible;
            });
        }

        #endregion

        #region 加热状态显示

        private void UpdateHeaterDisplay()
        {
            var status = _stateMachine.GetStatus();
            if (status != null)
            {
                var heaterInfo = status.HeaterInfo;
                UpdateHeaterDisplay(heaterInfo);

                // 更新统计信息
                //var stats = _stateMachine.GetTemperatureStatistics();
                //UpdateHeaterStatistics(stats);
            }
        }

        private void UpdateHeaterDisplay(HeaterStatusUpdatedEvent @event)
        {
            Dispatcher.Invoke(() =>
            {
                // 更新温度显示
                if (@event.CurrentTemperature.HasValue)
                {
                    tbCurrentTemp.Text = $"当前温度: {@event.CurrentTemperature.Value:F1} ℃";

                    // 根据温度设置颜色
                    if (@event.CurrentTemperature.Value > 60)
                        tbCurrentTemp.Foreground = new SolidColorBrush(Colors.Red);
                    else if (@event.CurrentTemperature.Value > 40)
                        tbCurrentTemp.Foreground = new SolidColorBrush(Colors.Orange);
                    else
                        tbCurrentTemp.Foreground = new SolidColorBrush(Colors.Black);
                }
                else
                {
                    tbCurrentTemp.Text = "当前温度: -- ℃";
                    tbCurrentTemp.Foreground = new SolidColorBrush(Colors.Gray);
                }

                if (@event.SetTemperature.HasValue)
                {
                    tbSetTemp.Text = $"设定温度: {@event.SetTemperature.Value/10:F1} ℃";
                }
                else
                {
                    tbSetTemp.Text = "设定温度: -- ℃";
                }

                // 更新状态显示
                string statusText = GetHeaterStatusText(@event.Status);
                Color statusColor = GetHeaterStatusColor(@event.Status);

                tbHeaterStatus.Text = $"加热器状态: {statusText}";
                tbHeaterStatus.Foreground = new SolidColorBrush(statusColor);

                // 更新可用性
                //tbHeaterAvailability.Text = $"加热器: {(@event.Status == HeaterChuckStatus.NoHeater ? "不可用" : "可用")}";
                //tbHeaterAvailability.Foreground = @event.Status == HeaterChuckStatus.NoHeater ?
                //    new SolidColorBrush(Colors.Gray) : new SolidColorBrush(Colors.Green);

                //// 更新最后更新时间
                //tbLastTempUpdate.Text = $"最后更新: {@event.Timestamp:HH:mm:ss}";
            });
        }

        private void UpdateHeaterDisplay(TemperatureInfo heaterInfo)
        {
            Dispatcher.Invoke(() =>
            {
                tbCurrentTemp.Text = $"当前温度: {heaterInfo.CurrentTemperature:F1} ℃";
                tbSetTemp.Text = heaterInfo.SetTemperature.HasValue ?
                    $"设定温度: {heaterInfo.SetTemperature.Value:F1} ℃" : "设定温度: -- ℃";

                string statusText = GetHeaterStatusText(heaterInfo.Status);
                Color statusColor = GetHeaterStatusColor(heaterInfo.Status);

                tbHeaterStatus.Text = $"加热器状态: {statusText}";
                tbHeaterStatus.Foreground = new SolidColorBrush(statusColor);

                
                //tbTempTolerance.Text = $"温度容差: ±{heaterInfo.TemperatureTolerance?.ToString("F1") ?? "0.5"}℃";

                //if (heaterInfo.TemperatureTrend.HasValue)
                //{
                //    string trend = heaterInfo.TemperatureTrend.Value > 0 ? "↑" :
                //                  heaterInfo.TemperatureTrend.Value < 0 ? "↓" : "→";
                //    tbTempTrend.Text = $"温度趋势: {Math.Abs(heaterInfo.TemperatureTrend.Value):F2}℃/min {trend}";
                //}
                //else
                //{
                //    tbTempTrend.Text = "温度趋势: -- ℃/min";
                //}

                //tbTempStability.Text = $"温度稳定性: {(heaterInfo.IsTemperatureStable ? "稳定" : "不稳定")}";
                //tbTempStability.Foreground = heaterInfo.IsTemperatureStable ?
                //    new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Orange);
                //tbLastTempUpdate.Text = $"最后更新: {heaterInfo.LastTemperatureUpdate:HH:mm:ss}";
                 
            });
        }

        private void UpdateHeaterStatistics(TemperatureStatistics stats)
        {
            Dispatcher.Invoke(() =>
            {
                //tbUptime.Text = $"运行时间: {stats.Uptime:hh\\:mm\\:ss}";
                //tbFaultCount.Text = $"故障次数: {stats.FaultCount}";

                //// 更新统计文本
                //if (stats.TotalRecords > 0)
                //{
                //    tbTempStats.Text = $"统计: 最小 {stats.MinTemperature:F1}℃, " +
                //                     $"最大 {stats.MaxTemperature:F1}℃, " +
                //                     $"平均 {stats.AverageTemperature?.ToString("F1") ?? "N/A"}℃";
                //}
            });
        }

        private string GetHeaterStatusText(HeaterChuckStatus status)
        {
            return status switch
            {
                HeaterChuckStatus.Unknown => "未知",
                HeaterChuckStatus.NoHeater => "无加热吸盘",
                HeaterChuckStatus.TemperatureTooHigh => "温度过高",
                HeaterChuckStatus.TemperatureTooLow => "温度过低",
                HeaterChuckStatus.Normal => "正常",
                HeaterChuckStatus.Other => "其他状态",
                HeaterChuckStatus.Heating => "加热中",
                HeaterChuckStatus.Cooling => "冷却中",
                HeaterChuckStatus.TemperatureStable => "温度稳定",
                HeaterChuckStatus.TemperatureFluctuating => "温度波动",
                HeaterChuckStatus.SensorFault => "传感器故障",
                HeaterChuckStatus.HeaterFault => "加热器故障",
                _ => "未知"
            };
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

*/
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
