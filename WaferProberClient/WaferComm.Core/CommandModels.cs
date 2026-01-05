namespace WaferComm.Core
{
    /// <summary>
    /// 机台编号信息
    /// </summary>
    public class MachineInfo
    {
        public string MachineNumber { get; set; }
    }

    /// <summary>
    /// 晶圆信息
    /// </summary>
    public class WaferInfo
    {
        public string WaferId { get; set; }
        public string WaferSize { get; set; }
        public string FlatInfo { get; set; }
    }

    /// <summary>
    /// 坐标信息
    /// </summary>
    public class Coordinate
    {
        public string X { get; set; }
        public string Y { get; set; }

        public override string ToString()
        {
            return $"Y{Y}X{X}";
        }
    }

    /// <summary>
    /// 批次信息
    /// </summary>
    public class LotInfo
    {
        public string LotNumber { get; set; }
    }

    ///// <summary>
    ///// 温度信息
    ///// </summary>
    //public class TemperatureInfo
    //{
    //    public decimal CurrentTemperature { get; set; }
    //    public decimal? SetTemperature { get; set; }
    //    public TemperatureStatus Status { get; set; }
    //}

    ///// <summary>
    ///// 温度状态
    ///// </summary>
    //public enum TemperatureStatus
    //{
    //    Normal,
    //    TooHigh,
    //    TooLow,
    //    NoHeater,
    //    Other
    //}

    /// <summary>
    /// 探针压力信息
    /// </summary>
    public class ProbePressureInfo
    {
        public decimal Pressure1 { get; set; }
        public decimal Pressure2 { get; set; }
        public decimal Pressure3 { get; set; }
        public decimal Pressure4 { get; set; }
    }

    /// <summary>
    /// 测试统计信息
    /// </summary>
    public class TestStatistics
    {
        public int TotalDiceCount { get; set; }
        public int TestedDiceCount { get; set; }
        public bool IsWaferComplete { get; set; }
    }

    /// <summary>
    /// 运动状态
    /// </summary>
    //public class MotionStatus
    //{
    //    public bool IsZUp { get; set; }
    //    public bool IsZDown { get; set; }
    //    public bool IsNeedleDown { get; set; }
    //    public bool IsMoving { get; set; }
    //}

    /// <summary>
    /// 系统状态
    /// </summary>
    public class SystemStatus
    {
        public bool IsRunning { get; set; }
        public bool IsReady { get; set; }
        public bool IsStopping { get; set; }
        public bool IsStopped { get; set; }
    }
}