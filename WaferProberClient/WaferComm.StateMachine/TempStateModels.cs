using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaferComm.StateMachine
{
    /// <summary>
    /// 加热吸盘状态枚举
    /// </summary>
    public enum HeaterChuckStatus
    {
        /// <summary>
        /// 未知
        /// </summary>
        Unknown,

        /// <summary>
        /// 无加热吸盘
        /// </summary>
        NoHeater,

        /// <summary>
        /// 温度过高
        /// </summary>
        TemperatureTooHigh,

        /// <summary>
        /// 温度过低
        /// </summary>
        TemperatureTooLow,

        /// <summary>
        /// 正常
        /// </summary>
        Normal,

        /// <summary>
        /// 其他状态
        /// </summary>
        Other,

        /// <summary>
        /// 加热中
        /// </summary>
        Heating,

        /// <summary>
        /// 冷却中
        /// </summary>
        Cooling,

        /// <summary>
        /// 温度稳定
        /// </summary>
        TemperatureStable,

        /// <summary>
        /// 温度波动
        /// </summary>
        TemperatureFluctuating,

        /// <summary>
        /// 传感器故障
        /// </summary>
        SensorFault,

        /// <summary>
        /// 加热器故障
        /// </summary>
        HeaterFault
    }

    /// <summary>
    /// 温度信息
    /// </summary>
    public class TemperatureInfo
    {
        public decimal CurrentTemperature { get; set; }
        public decimal? SetTemperature { get; set; }
        public decimal? TemperatureTolerance { get; set; } = 0.5m; // 默认容差 ±0.5℃
        public HeaterChuckStatus Status { get; set; }
        public DateTime LastTemperatureUpdate { get; set; }
        public decimal? TemperatureTrend { get; set; } // ℃/min 温度变化趋势
        public int HeaterFaultCount { get; set; }
        public TimeSpan HeaterUptime { get; set; }

        public bool IsTemperatureStable => Status == HeaterChuckStatus.Normal ||
                                          Status == HeaterChuckStatus.TemperatureStable;
        public bool IsTemperatureInRange
        {
            get
            {
                if (!SetTemperature.HasValue || TemperatureTolerance == null)
                    return false;

                decimal min = SetTemperature.Value - TemperatureTolerance.Value;
                decimal max = SetTemperature.Value + TemperatureTolerance.Value;
                return CurrentTemperature >= min && CurrentTemperature <= max;
            }
        }
        // 温度历史记录（用于趋势分析）
        public TemperatureHistory TemperatureHistory { get; set; } = new TemperatureHistory();

        public override string ToString()
        {
            return $"当前: {CurrentTemperature:F1}℃ | 设定: {SetTemperature?.ToString("F1") ?? "N/A"}℃ | 状态: {Status}";
        }
    }

    public class HeaterMonitorStartedEvent
    {

    }  
    public class HeaterMonitorStopedEvent
    {

    }
    /// <summary>
    /// 加热吸盘状态事件
    /// </summary>
    public class HeaterStatusUpdatedEvent
    {
        public HeaterChuckStatus Status { get; }
        public decimal? CurrentTemperature { get; }
        public decimal? SetTemperature { get; }
        public DateTime Timestamp { get; } = DateTime.Now;
        public string RawResponse { get; }

        public HeaterStatusUpdatedEvent(HeaterChuckStatus status, decimal? currentTemp,
                                       decimal? setTemp, string rawResponse)
        {
            Status = status;
            CurrentTemperature = currentTemp;
            SetTemperature = setTemp;
            RawResponse = rawResponse;
        }
    }

    /// <summary>
    /// 温度警告事件
    /// </summary>
    public class TemperatureWarningEvent
    {
        public TemperatureWarningType WarningType { get; }
        public decimal CurrentTemperature { get; }
        public decimal? TargetTemperature { get; }
        public decimal Deviation { get; }
        public DateTime Timestamp { get; } = DateTime.Now;

        public TemperatureWarningEvent(TemperatureWarningType warningType, decimal currentTemp,
                                      decimal? targetTemp = null)
        {
            WarningType = warningType;
            CurrentTemperature = currentTemp;
            TargetTemperature = targetTemp;

            if (targetTemp.HasValue)
            {
                Deviation = Math.Abs(currentTemp - targetTemp.Value);
            }
        }
    }

    /// <summary>
    /// 温度警告类型
    /// </summary>
    public enum TemperatureWarningType
    {
        TemperatureTooHigh,
        TemperatureTooLow,
        TemperatureUnstable,
        HeaterFault,
        SensorFault,
        TemperatureDrift
    }
}
