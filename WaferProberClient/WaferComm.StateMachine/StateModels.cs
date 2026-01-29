using System;

namespace WaferComm.StateMachine
{
    /// <summary>
    /// 探针台状态枚举
    /// </summary>
    public enum ProberState
    {
        /// <summary>
        /// 未连接
        /// </summary>
        Disconnected,

        /// <summary>
        /// 已连接但未初始化
        /// </summary>
        Connected,

        /// <summary>
        /// 就绪状态
        /// </summary>
        Ready,

        /// <summary>
        /// 等待晶圆
        /// </summary>
        WaitingForWafer,

        /// <summary>
        /// 晶圆已加载
        /// </summary>
        WaferLoaded,

        /// <summary>
        /// 对齐中
        /// </summary>
        Aligning,

        /// <summary>
        /// 测试中
        /// </summary>
        Testing,

        /// <summary>
        /// 暂停
        /// </summary>
        Paused,

        /// <summary>
        /// 停止中
        /// </summary>
        Stopping,
                
        /// <summary>
        /// 停止中
        /// </summary>
        Stoped,

        /// <summary>
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 维护模式
        /// </summary>
        Maintenance
    }

    /// <summary>
    /// 状态转换事件
    /// </summary>
    public class StateTransitionEvent
    {
        public ProberState FromState { get; }
        public ProberState ToState { get; }
        public DateTime Timestamp { get; } = DateTime.Now;
        public object TransitionData { get; }
        public bool Success { get; }

        public StateTransitionEvent(ProberState fromState, ProberState toState, bool success, object data = null)
        {
            FromState = fromState;
            ToState = toState;
            Success = success;
            TransitionData = data;
        }
    }

    /// <summary>
    /// 状态机错误事件
    /// </summary>
    public class StateMachineErrorEvent
    {
        public string ErrorMessage { get; }
        public ProberState CurrentState { get; }
        public Exception Exception { get; }

        public StateMachineErrorEvent(string message, ProberState currentState, Exception ex = null)
        {
            ErrorMessage = message;
            CurrentState = currentState;
            Exception = ex;
        }
    }

    /// <summary>
    /// Z轴状态枚举
    /// </summary>
    public enum ZAxisStatus
    {
        Unknown,
        Up,
        Down,
        MovingUp,
        MovingDown
    }
    /// <summary>
    /// 运动状态枚举
    /// </summary>
    public enum MotionStatus
    {
        /// <summary>
        /// 空闲
        /// </summary>
        Idle,

        /// <summary>
        /// 绝对运动中
        /// </summary>
        MovingAbsolute,

        /// <summary>
        /// 相对运动中
        /// </summary>
        MovingRelative,

        /// <summary>
        /// 微米运动中
        /// </summary>
        MovingMicro,

        /// <summary>
        /// Z轴上升中
        /// </summary>
        ZUpMoving,

        /// <summary>
        /// Z轴下降中
        /// </summary>
        ZDownMoving,

        /// <summary>
        /// Z轴上升扎针位置
        /// </summary>
        ZUp,

        /// <summary>
        /// Z轴下降离开扎针位置
        /// </summary>
        ZDown,

        /// <summary>
        /// 运动中（通用）
        /// </summary>
        Moving,

        /// <summary>
        /// 运动完成
        /// </summary>
        MotionComplete,

        /// <summary>
        /// 运动失败
        /// </summary>
        MotionFailed,

        /// <summary>
        /// 运动超时
        /// </summary>
        MotionTimeout
    }

    /// <summary>
    /// 运动指令信息
    /// </summary>
    public class MotionCommand
    {
        public string CommandType { get; set; }  // J, Z, D等
        public string TargetY { get; set; }
        public string TargetX { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public TimeSpan Duration => EndTime.HasValue ?
            EndTime.Value - StartTime :
            DateTime.Now - StartTime;

        public override string ToString()
        {
            return $"{CommandType} Y{TargetY} X{TargetX}";
        }

        public void UpdatePosition(string responseCode)
        {
            if (responseCode.Length > 3)
            {
                // 解析 JY+020X-020 格式
                int xIndex = responseCode.IndexOf('X');
                if (xIndex > 0)
                {
                    string yPart = responseCode.Substring(2, xIndex - 2);
                    string xPart = responseCode.Substring(xIndex + 1);

                    this.TargetY = yPart;
                    this.TargetX = xPart;
                }
            }
        }
    }

    /// <summary>
    /// 运动完成事件
    /// </summary>
    public class MotionCompletedEvent
    {
        public MotionCommand Command { get; }
        public DateTime Timestamp { get; } = DateTime.Now;
        public string ResponseCode { get; }
        public bool IsSuccess { get; }

        public MotionCompletedEvent(MotionCommand command, string responseCode, bool isSuccess)
        {
            Command = command;
            ResponseCode = responseCode;
            IsSuccess = isSuccess;
        }
    }

    /// <summary>
    /// 运动超时事件
    /// </summary>
    public class MotionTimeoutEvent
    {
        public MotionCommand Command { get; }
        public DateTime Timestamp { get; } = DateTime.Now;
        public TimeSpan TimeoutDuration { get; }

        public MotionTimeoutEvent(MotionCommand command, TimeSpan timeoutDuration)
        {
            Command = command;
            TimeoutDuration = timeoutDuration;
        }
    }

    /// <summary>
    /// 坐标信息
    /// </summary>
    public class PositionInfo
    {
        public string CurrentY { get; set; }
        public string CurrentX { get; set; }
        public string TargetY { get; set; }
        public string TargetX { get; set; }
        public DateTime LastUpdate { get; set; }

        public bool IsAtTarget => CurrentY == TargetY && CurrentX == TargetX;

        public override string ToString()
        {
            return $"Y{CurrentY} X{CurrentX}";
        }
        public PositionInfo()
        {

        }
        public PositionInfo(MotionCommand cmd)
        {
            if (cmd != null && cmd.CommandType == "J") FromMotionCmd(cmd);
        }
        public void FromMotionCmd(MotionCommand cmd)
        {
            if (cmd == null) return;
            if (cmd.Success)
            {
                this.CurrentX = this.TargetX = cmd.TargetX;
                this.CurrentY = this.TargetY = cmd.TargetY;
            }
            else
            {
                this.TargetX = cmd.TargetX;
                this.TargetY = cmd.TargetY;
            }
            this.LastUpdate = DateTime.Now;
        }

        public void FromPositionUpdatedEvent(PositionUpdatedEvent @event)
        {
            this.CurrentX = @event.X;
            this.CurrentY = @event.Y;
            this.LastUpdate = DateTime.Now;
        }
    }
    #region  加热吸盘状态
    /// <summary>
    /// 温度历史记录
    /// </summary>
    public class TemperatureHistory
    {
        private readonly List<TemperatureRecord> _records = new List<TemperatureRecord>();
        private const int MAX_RECORDS = 100;

        public void AddRecord(decimal temperature, HeaterChuckStatus status)
        {
            var record = new TemperatureRecord
            {
                Temperature = temperature,
                Status = status,
                Timestamp = DateTime.Now
            };

            _records.Add(record);

            if (_records.Count > MAX_RECORDS)
            {
                _records.RemoveAt(0);
            }
        }

        public IEnumerable<TemperatureRecord> GetRecentRecords(int count)
        {
            return _records.TakeLast(count);
        }

        public decimal? GetAverageTemperature(int lastNRecords = 10)
        {
            if (_records.Count == 0) return null;

            var recent = _records.TakeLast(Math.Min(lastNRecords, _records.Count));
            return recent.Average(r => r.Temperature);
        }

        public decimal? GetTemperatureTrend()
        {
            if (_records.Count < 2) return null;

            // 计算最近10个记录的温度变化趋势
            var recent = _records.TakeLast(Math.Min(10, _records.Count)).ToList();
            if (recent.Count < 2) return null;

            TimeSpan timeSpan = recent.Last().Timestamp - recent.First().Timestamp;
            if (timeSpan.TotalMinutes == 0) return null;

            decimal tempChange = recent.Last().Temperature - recent.First().Temperature;
            return tempChange / (decimal)timeSpan.TotalMinutes; // ℃/min
        }
    }

    /// <summary>
    /// 温度记录
    /// </summary>
    public class TemperatureRecord
    {
        public decimal Temperature { get; set; }
        public HeaterChuckStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
    }
    #endregion
    /// <summary>
    /// 状态机状态
    /// </summary>
    public class ProberStatus
    {
        public ProberState CurrentState { get; set; }
        public DateTime LastStateChange { get; set; }
        public TimeSpan CurrentStateDuration => DateTime.Now - LastStateChange;
        public string CurrentWaferId { get; set; }
        public string CurrentLotId { get; set; }
        public string CurrentMappingFile { get; set; }
        public int TestedDies { get; set; }
        public int TotalDies { get; set; }
        public decimal ProgressPercentage => TotalDies > 0 ? (decimal)TestedDies / TotalDies * 100 : 0;
        //public bool IsHeaterNormal { get; set; }
        public decimal CurrentTemperature { get; set; }
        public bool IsNeedleDown { get; set; }
        public bool IsZUp { get; set; }
        public bool IsZDown { get; set; }
        public bool IsMoving { get; set; }
        // 新增加热吸盘相关属性
        public TemperatureInfo HeaterInfo { get; set; } = new TemperatureInfo();
        public DateTime LastHeaterCheck { get; set; }
        public bool IsHeaterAvailable => HeaterInfo.Status != HeaterChuckStatus.NoHeater;
        public bool IsHeaterNormal => HeaterInfo.Status == HeaterChuckStatus.Normal ||
                                     HeaterInfo.Status == HeaterChuckStatus.TemperatureStable;

        // 新增运动相关属性
        public MotionStatus MotionStatus { get; set; }
        public PositionInfo CurrentPosition { get; set; } = new PositionInfo();
        public MotionCommand CurrentMotionCommand { get; set; }
        public List<MotionCommand> RecentMotions { get; set; } = new List<MotionCommand>();
        public DateTime LastMotionCompleteTime { get; set; }
        public int MotionSuccessCount { get; set; }
        public int MotionFailureCount { get; set; }
        public double AverageMotionTimeSeconds { get; set; }

        // 新增Z轴状态枚举
        public ZAxisStatus ZAxisStatus { get; set; }

        public override string ToString()
        {
            return $"{CurrentState} | Motion: {MotionStatus} | Pos: {CurrentPosition}";
        }
        public DateTime LastHeartbeat { get; set; }
        public bool IsHeartbeatOk => DateTime.Now - LastHeartbeat <= TimeSpan.FromSeconds(10);
        public string ErrorMessage { get; set; }
        public decimal MotionAxisX { get;  set; }
        public decimal MotionAxisY { get;  set; }
        public decimal MotionAxisZ { get;  set; }

        //public override string ToString()
        //{
        //    return $"{CurrentState} | Wafer: {CurrentWaferId} | Progress: {TestedDies}/{TotalDies} ({ProgressPercentage:F1}%) | Temp: {CurrentTemperature:F1}℃";
        //}
    }

    public class ProberMotionAxisStatus
    {
        public decimal CurrentAxisX { get; set; }
        public decimal CurrentAxisY { get; set; }
        public decimal CurrentAxisZ { get; set; }
    }
}