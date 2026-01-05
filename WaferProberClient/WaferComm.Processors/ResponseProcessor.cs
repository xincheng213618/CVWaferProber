using System;
using System.Linq;
using WaferComm.Core;

namespace WaferComm.Processors
{
    /// <summary>
    /// 响应处理器 - 处理所有接收到的指令响应
    /// </summary>
    public class ResponseProcessor : CommandProcessorBase
    {
        private MachineInfo _machineInfo = new MachineInfo();
        private WaferInfo _waferInfo = new WaferInfo();
        private Coordinate _currentPosition = new Coordinate();
        private Coordinate _firstPosition = new Coordinate();
        private LotInfo _lotInfo = new LotInfo();
        //private TemperatureInfo _temperatureInfo = new TemperatureInfo();
        private ProbePressureInfo _pressureInfo = new ProbePressureInfo();
        private TestStatistics _testStatistics = new TestStatistics();
        //private MotionStatus _motionStatus = new MotionStatus();
        private SystemStatus _systemStatus = new SystemStatus();

        public ResponseProcessor(IEventAggregator eventAggregator) : base(eventAggregator)
        {
        }

        protected override void RegisterHandlers()
        {
            EventAggregator.Subscribe<CommandReceivedEvent>(OnCommandReceived);
            EventAggregator.Subscribe<ConnectionStateChangedEvent>(OnConnectionChanged);
        }

        protected override void UnregisterHandlers()
        {
            EventAggregator.Unsubscribe<CommandReceivedEvent>(OnCommandReceived);
            EventAggregator.Unsubscribe<ConnectionStateChangedEvent>(OnConnectionChanged);
        }

        public MachineInfo GetMachineInfo() => _machineInfo;
        public WaferInfo GetWaferInfo() => _waferInfo;
        private void OnCommandReceived(CommandReceivedEvent @event)
        {
            if (!@event.IsValid) return;

            string command = @event.Command;

            if (string.IsNullOrEmpty(command)) return;

            try
            {
                // 根据指令类型进行处理
                if (command.StartsWith("B"))
                {
                    ProcessMachineNumber(command);
                }
                else if (command.StartsWith("b"))
                {
                    ProcessWaferId(command);
                }
                else if (command.StartsWith("q") || command.StartsWith("Q"))
                {
                    ProcessCoordinate(command);
                }
                else if (command.StartsWith("V"))
                {
                    ProcessLotNumber(command);
                }
                else if (command.StartsWith("f") && command.Length > 1 && char.IsDigit(command[1]))
                {
                    //ProcessTemperatureSetResponse(command);
                }
                else if (command.StartsWith("fl"))
                {
                    //ProcessCurrentTemperature(command);
                }
                else if (command.StartsWith("ku"))
                {
                    ProcessWaferInfo(command);
                }
                else if (command.StartsWith("Y"))
                {
                    ProcessTotalDiceCount(command);
                }
                else if (command.StartsWith("QP"))
                {
                    ProcessProbePressure(command);
                }
                else if (command.StartsWith("r"))
                {
                    //ProcessHeaterStatus(command);
                }
                else if (IsStatusCode(command))
                {
                    ProcessStatusCode(command);
                }
                else
                {
                    ProcessGenericCommand(command);
                }
            }
            catch (Exception ex)
            {
                EventAggregator.Publish(new CommunicationErrorEvent("处理响应失败", ex, $"Process: {command}"));
            }
        }

        private bool IsStatusCode(string command)
        {
            if (command.Length >= 2 && char.IsDigit(command[0]) && char.IsDigit(command[1]))
            {
                string codeStr = command.Substring(0, 2);
                int code = int.Parse(codeStr);

                // 检查是否是已知的状态码
                int[] knownCodes = { 67, 68, 70, 76, 81, 85, 90, 91, 94 };
                return Array.IndexOf(knownCodes, code) >= 0;
            }
            return false;
        }
        private void OnConnectionChanged(ConnectionStateChangedEvent @event)
        {
            // 连接状态改变时重置状态
            if (!@event.IsConnected)
            {
                ResetAllStatus();
            }
        }

        private void ResetAllStatus()
        {
            _machineInfo = new MachineInfo();
            _waferInfo = new WaferInfo();
            _currentPosition = new Coordinate();
            _firstPosition = new Coordinate();
            _lotInfo = new LotInfo();
            //_temperatureInfo = new TemperatureInfo();
            _pressureInfo = new ProbePressureInfo();
            _testStatistics = new TestStatistics();
            //_motionStatus = new MotionStatus();
            _systemStatus = new SystemStatus();
        }

        private void ProcessMachineNumber(string command)
        {
            if (command.Length > 1)
            {
                _machineInfo.MachineNumber = command.Substring(1);
                // 可以发布MachineInfoUpdated事件
            }
        }

        private void ProcessWaferId(string command)
        {
            if (command.Length > 1)
            {
                _waferInfo.WaferId = command.Substring(1, Math.Min(command.Length - 1, 19));
            }
        }

        private void ProcessCoordinate(string command)
        {
            // 解析 qY001X001 或 QY001X001 格式
            if (command.Length < 2) return;

            try
            {
                string data = command.Substring(1);
                int xIndex = data.IndexOf('X');
                if (xIndex > 0)
                {
                    string yStr = data.Substring(0, xIndex);
                    string xStr = data.Substring(xIndex + 1);

                    var coord = new Coordinate { X = xStr, Y = yStr.TrimStart('Y') };

                    if (command[0] == 'q')
                        _firstPosition = coord;
                    else if (command[0] == 'Q')
                        _currentPosition = coord;
                }
            }
            catch
            {
                // 解析失败
            }
        }

        private void ProcessLotNumber(string command)
        {
            if (command.Length > 1)
            {
                _lotInfo.LotNumber = command.Substring(1).Trim();
            }
        }
        /*
        private void ProcessTemperatureSetResponse(string command)
        {
            if (command.Length >= 5) // f0250
            {
                try
                {
                    string tempStr = command.Substring(1, 4);
                    if (decimal.TryParse(tempStr, out decimal temp))
                    {
                        _temperatureInfo.SetTemperature = temp / 10; // 转换为实际温度
                    }
                }
                catch { }
            }
        }

        private void ProcessCurrentTemperature(string command)
        {
            if (command.Length >= 5) // fl024.7
            {
                try
                {
                    string tempStr = command.Substring(2);
                    if (decimal.TryParse(tempStr, out decimal temp))
                    {
                        _temperatureInfo.CurrentTemperature = temp;
                    }
                }
                catch { }
            }
        }
        */
        private void ProcessWaferInfo(string command)
        {
            if (command.Length > 2)
            {
                string info = command.Substring(2);
                // 解析Wafer信息，具体格式根据协议
                _waferInfo.WaferSize = ExtractWaferSize(info);
                _waferInfo.FlatInfo = ExtractFlatInfo(info);
            }
        }

        private void ProcessTotalDiceCount(string command)
        {
            if (command.Length > 1)
            {
                string countStr = command.Substring(1);
                if (int.TryParse(countStr, out int count))
                {
                    _testStatistics.TotalDiceCount = count;
                }
            }
        }

        private void ProcessProbePressure(string command)
        {
            if (command.Length > 2)
            {
                string pressureStr = command.Substring(2);
                var pressures = pressureStr.Split(',');

                if (pressures.Length >= 4)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (decimal.TryParse(pressures[i], out decimal pressure))
                        {
                            switch (i)
                            {
                                case 0: _pressureInfo.Pressure1 = pressure; break;
                                case 1: _pressureInfo.Pressure2 = pressure; break;
                                case 2: _pressureInfo.Pressure3 = pressure; break;
                                case 3: _pressureInfo.Pressure4 = pressure; break;
                            }
                        }
                    }
                }
            }
        }

        //private void ProcessHeaterStatus(string command)
        //{
        //    if (command.Length >= 2)
        //    {
        //        char statusChar = command[1];
        //        _temperatureInfo.Status = statusChar switch
        //        {
        //            '@' => TemperatureStatus.NoHeater,
        //            'G' => TemperatureStatus.TooHigh,
        //            'K' => TemperatureStatus.TooLow,
        //            'C' => TemperatureStatus.Normal,
        //            'A' => TemperatureStatus.Other,
        //            _ => TemperatureStatus.Other
        //        };
        //    }
        //}

        private void ProcessStatusCode(string command)
        {
            if (!int.TryParse(command, out int code)) return;

            switch (code)
            {
                case 67: // Z Up完成，开始测试
                    //_motionStatus.IsZUp = true;
                    //_motionStatus.IsZDown = false;
                    //_motionStatus.IsNeedleDown = true;
                    break;
                case 68: // 测试停止(EOT)
                    //_motionStatus.IsNeedleDown = false;
                    break;
                case 70: // 晶圆加载完成
                    break;
                case 76: // 失败
                    // 失败状态处理
                    break;
                case 81: // 当前晶圆测试完成
                    _testStatistics.IsWaferComplete = true;
                    break;
                case 85: // 停止完成
                    _systemStatus.IsStopped = true;
                    _systemStatus.IsStopping = false;
                    _systemStatus.IsRunning = false;
                    break;
                case 90: // 探针台停止
                    _systemStatus.IsStopping = true;
                    _systemStatus.IsRunning = false;
                    break;
                case 91: // 探针台开始运行
                    _systemStatus.IsReady = true;
                    _systemStatus.IsStopped = false;
                    break;
                case 94: // 结批完成
                    break;
            }
        }

        private void ProcessGenericCommand(string command)
        {
            // 处理其他未明确分类的指令
        }

        private string ExtractWaferSize(string info)
        {
            // 根据实际协议解析Wafer尺寸
            return info.Length > 10 ? info.Substring(0, 10) : info;
        }

        private string ExtractFlatInfo(string info)
        {
            // 根据实际协议解析Flat信息
            return info.Length > 20 ? info.Substring(10, 10) : "";
        }
    }
}