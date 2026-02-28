using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Models
{
    [Serializable]
    public class BreakpointData
    {
        public string Version { get; set; } = "1.0";
        public DateTime SaveTime { get; set; }
        public string ProberId { get; set; }
        public string Timestamp { get; set; }

        // 测试队列信息
        public List<DieBreakpointInfo> TestQueue { get; set; } = new();
        public int CurrentQueueIndex { get; set; }
        public int CompletedCount { get; set; }

        // 当前测试Die信息
        public int? CurrentMapX { get; set; }
        public int? CurrentMapY { get; set; }
        public string CurrentDieSerialNumber { get; set; }
        public double CurrentDieProgress { get; set; }
        public DateTime CurrentDieStartTime { get; set; }

        // 测试流程信息
        public string FlowType { get; set; }
        public string FlowName { get; set; }

        // 测试统计
        public int TotalTestCount { get; set; }
        public int TotalTestedCount { get; set; }

        // 软件状态
        public bool IsAutoTesting { get; set; }
        public bool IsPaused { get; set; }
        public bool IsProcessing { get; set; }
        public string SoftwareState { get; set; } // RUNNING, PAUSED, COMPLETED

        // Die测试结果快照（用于恢复时显示）
        public List<DieTestSnapshot> TestSnapshots { get; set; } = new();

        // 配置信息
        public bool IsAutoSN { get; set; }
        public string MappingCsvFilePath { get; set; }
    }

    [Serializable]
    public class DieBreakpointInfo
    {
        public int? MapX { get; set; }
        public int? MapY { get; set; }
        public string SerialNumber { get; set; }
        public string Status { get; set; }
        public bool IsAOIEnabled { get; set; }
        public bool IsIVLEnabled { get; set; }
        public bool IsEQEEnabled { get; set; }
        public bool IsVAMEnabled { get; set; }
        public DateTime? StartTestTime { get; set; }
        public DateTime? EndTestTime { get; set; }
        public string TestDataJson { get; set; } // JSON格式的测试数据
        public int CurrentTestStep { get; set; }
    }

    [Serializable]
    public class DieTestSnapshot
    {
        public int? MapX { get; set; }
        public int? MapY { get; set; }
        public string DisplayStatus { get; set; }
        public string DataValue { get; set; }
        public string AOIGradeLevel { get; set; }
        public string LightOnStatus { get; set; }
        public string RegisterPixels { get; set; }
        public string FinalClass { get; set; }
        public string BlackPattern { get; set; }
        public string Temperature { get; set; }
        public string PixelLogic { get; set; }
        public string Pressure { get; set; }
        public int TouchDownCounts { get; set; }
        public string ProbingCardSN { get; set; }
        public decimal MotionAxisX { get; set; }
        public decimal MotionAxisY { get; set; }
        public decimal MotionAxisZ { get; set; }
        public DateTime? StartTestTime { get; set; }
        public DateTime? EndTestTime { get; set; }
        public string TotalTime { get; set; }
    }
}
