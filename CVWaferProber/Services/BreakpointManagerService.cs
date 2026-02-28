using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;

namespace CVWaferProber.Services
{
    /// <summary>
    /// 断点记忆服务 - 保存测试断点状态，支持异常恢复
    /// </summary>
    public class BreakpointMemoryService
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(BreakpointMemoryService));
        private static readonly string AppFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CVWaferProber");
        private static readonly string BreakpointFilePath = Path.Combine(AppFolder, "breakpoint.json");
        private static readonly string TempBreakpointFilePath = Path.Combine(AppFolder, "breakpoint_temp.json");

        /// <summary>
        /// 断点数据结构
        /// </summary>
        public class BreakpointData
        {
            public DateTime Timestamp { get; set; }
            public string ProberId { get; set; }
            public string TestFlowType { get; set; }
            public string TestFlowName { get; set; }

            // 测试进度信息
            public int TotalTestCount { get; set; }
            public int CompletedTestCount { get; set; }
            public double SingleDieProgress { get; set; }
            public double TotalProgress { get; set; }
            public string CurrentDieInfo { get; set; }
            public bool IsManualTesting { get; set; }
            public bool IsAutoTesting { get; set; }

            // 当前测试Die信息
            public int? CurrentDieId { get; set; }
            public int? CurrentDieMapX { get; set; }
            public int? CurrentDieMapY { get; set; }
            public string CurrentDieSerialNumber { get; set; }
            public string CurrentDieStatus { get; set; }

            // 测试队列信息
            public List<BreakpointDieInfo> TestQueue { get; set; } = new List<BreakpointDieInfo>();
            public int CurrentQueueIndex { get; set; } = -1;

            // 进度时间信息
            public DateTime CurrentDieStartTime { get; set; }
            public int CurrentDieElapsedSeconds { get; set; }
            public int CurrentDiePredictSeconds { get; set; }

            // 测试状态
            public bool IsPaused { get; set; }
            public DateTime? PauseTime { get; set; }
        }

        /// <summary>
        /// Die信息简化结构
        /// </summary>
        public class BreakpointDieInfo
        {
            public int? Id { get; set; }
            public int? MapX { get; set; }
            public int? MapY { get; set; }
            public string SerialNumber { get; set; }
            public string Status { get; set; }
            public bool IsAOIEnabled { get; set; }
            public bool IsIVLEnabled { get; set; }
            public bool IsEQEEnabled { get; set; }
            public bool IsVAMEnabled { get; set; }
            public bool IsCompleted { get; set; }
        }

        /// <summary>
        /// 保存断点数据
        /// </summary>
        public static async Task SaveBreakpointAsync(
            MappingDataViewModel mappingVM,
            MainService mainService,
            WPFlowViewModel selectedFlow = null)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);

                var data = new BreakpointData
                {
                    Timestamp = DateTime.Now,
                    ProberId = mappingVM.WaferId,
                    TestFlowType = selectedFlow?.FlowType.ToString(),
                    TestFlowName = selectedFlow?.Name,

                    // 测试进度信息
                    TotalTestCount = mappingVM.TotalTestCount,
                    CompletedTestCount = mappingVM.CompletedTestCount,
                    SingleDieProgress = mappingVM.SingleDieTestProgress,
                    TotalProgress = mappingVM.TotalTestProgress,
                    CurrentDieInfo = mappingVM.CurrentDieInfo,
                    IsManualTesting = mappingVM.IsManualTesting,
                    IsAutoTesting = mainService.autoTestingItem != null,

                    // 进度时间信息
                    CurrentDieStartTime = mappingVM._currentDieStartTime,
                    CurrentDiePredictSeconds = mappingVM._currentDiePredictSeconds,

                    // 测试状态
                    IsPaused = mainService.autoTestingItem?.IsPaused ?? false,
                    PauseTime = mainService.autoTestingItem?.IsPaused == true ? DateTime.Now : null
                };

                // 查找当前测试的Die
                if (!string.IsNullOrEmpty(mappingVM.CurrentDieInfo))
                {
                    var currentDie = mappingVM.TestResults.FirstOrDefault(d =>
                        $"{d.MapX}/{d.MapY}" == mappingVM.CurrentDieInfo);

                    if (currentDie != null)
                    {
                        data.CurrentDieId = (int?)currentDie.Id;
                        data.CurrentDieMapX = currentDie.MapX;
                        data.CurrentDieMapY = currentDie.MapY;
                        data.CurrentDieSerialNumber = currentDie.SerialNumber;
                        data.CurrentDieStatus = currentDie.Status?.ToString();
                        data.CurrentDieElapsedSeconds = (int)(DateTime.Now - mappingVM._currentDieStartTime).TotalSeconds;
                    }
                }

                // 保存测试队列信息
                if (mainService.autoTestingItem != null && mainService.autoTestingItem.TestingDieVMList != null)
                {
                    data.CurrentQueueIndex = mainService.autoTestingItem.CurTestingIndex;

                    foreach (var die in mainService.autoTestingItem.TestingDieVMList)
                    {
                        data.TestQueue.Add(new BreakpointDieInfo
                        {
                            Id = (int?)die.Id,
                            MapX = die.MapX,
                            MapY = die.MapY,
                            SerialNumber = die.SerialNumber,
                            Status = die.Status?.ToString(),
                            IsAOIEnabled = die.IsAOIEnabled,
                            IsIVLEnabled = die.IsIVLEnabled,
                            IsEQEEnabled = die.IsEQEEnabled,
                            IsVAMEnabled = die.IsVAMEnabled,
                            IsCompleted = die.IsCompleted
                        });
                    }
                }

                // 先保存到临时文件，然后替换原文件（防止写入过程中崩溃）
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                await File.WriteAllTextAsync(TempBreakpointFilePath, json);

                // 原子替换
                if (File.Exists(BreakpointFilePath))
                    File.Delete(BreakpointFilePath);
                File.Move(TempBreakpointFilePath, BreakpointFilePath);

                logger.InfoFormat("Breakpoint saved: {0}/{1}, Current Die: {2}",
                    data.CompletedTestCount, data.TotalTestCount, data.CurrentDieInfo);
            }
            catch (Exception ex)
            {
                logger.Error("Failed to save breakpoint", ex);
            }
        }

        /// <summary>
        /// 加载断点数据
        /// </summary>
        public static async Task<BreakpointData> LoadBreakpointAsync()
        {
            try
            {
                if (!File.Exists(BreakpointFilePath))
                    return null;

                var json = await File.ReadAllTextAsync(BreakpointFilePath);
                var data = JsonConvert.DeserializeObject<BreakpointData>(json);

                // 检查断点是否过期（超过24小时）
                if (data != null && (DateTime.Now - data.Timestamp).TotalHours > 24)
                {
                    logger.Warn("Breakpoint expired (older than 24 hours), discarding");
                    ClearBreakpoint();
                    return null;
                }

                logger.InfoFormat("Breakpoint loaded: {0}/{1}, Current Die: {2}",
                    data.CompletedTestCount, data.TotalTestCount, data.CurrentDieInfo);

                return data;
            }
            catch (Exception ex)
            {
                logger.Error("Failed to load breakpoint", ex);
                return null;
            }
        }

        /// <summary>
        /// 清除断点数据
        /// </summary>
        public static void ClearBreakpoint()
        {
            try
            {
                if (File.Exists(BreakpointFilePath))
                    File.Delete(BreakpointFilePath);
                if (File.Exists(TempBreakpointFilePath))
                    File.Delete(TempBreakpointFilePath);

                logger.Info("Breakpoint cleared");
            }
            catch (Exception ex)
            {
                logger.Error("Failed to clear breakpoint", ex);
            }
        }

        /// <summary>
        /// 检查是否有可恢复的断点
        /// </summary>
        public static bool HasRecoverableBreakpoint()
        {
            if (!File.Exists(BreakpointFilePath))
                return false;

            try
            {
                var json = File.ReadAllText(BreakpointFilePath);
                var data = JsonConvert.DeserializeObject<BreakpointData>(json);

                // 检查断点是否有效
                if (data == null || data.TotalTestCount <= 0)
                    return false;

                // 检查是否过期
                if ((DateTime.Now - data.Timestamp).TotalHours > 24)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
