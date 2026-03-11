using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core
{
    public static class TestResultPersistenceService
    {
        private static readonly string AppFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CVWaferProber");
        //private static readonly string FilePath = Path.Combine(AppFolder, "testresults.json");
        // 会话文件夹：AppData\CVWaferProber\Sessions
        private static readonly string SessionsFolder = Path.Combine(AppFolder, "Sessions");
        private static void EnsureFolders()
        {
            Directory.CreateDirectory(AppFolder);
            Directory.CreateDirectory(SessionsFolder);
        }
        public static async Task SaveAsync(IEnumerable<TestResultDto> items)
        {
            try
            {
                EnsureFolders();
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = Path.Combine(SessionsFolder, $"testresults_{timestamp}.json");

                var json = JsonConvert.SerializeObject(items, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存失败: {ex}");
            }
        }


        public static async Task<List<TestResultDto>> LoadAsync()
        {
            try
            {
                EnsureFolders();
                var files = Directory.EnumerateFiles(SessionsFolder, "testresults_*.json")
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToList();

                if (files.Count == 0) return new List<TestResultDto>();

                var json = await File.ReadAllTextAsync(files[0]);
                return JsonConvert.DeserializeObject<List<TestResultDto>>(json) ?? new List<TestResultDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load: {ex}");
                return new List<TestResultDto>();
            }
        }
        public static void CleanupOldSessions(int keepCount = 10)
        {
            try
            {
                EnsureFolders();
                var sessionFiles = Directory.EnumerateFiles(SessionsFolder, "testresults_*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                if (sessionFiles.Count <= keepCount)
                    return;

                var filesToDelete = sessionFiles.Skip(keepCount).ToList();
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file.FullName);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"删除旧会话文件失败 {file.Name}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"清理了 {filesToDelete.Count} 个旧会话文件");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理旧会话失败: {ex.Message}");
            }
        }
        public static async Task CleanupOldSessionsAsync(int keepCount = 10)
        {
            // 用Task.Run包装IO操作，不阻塞UI
            await Task.Run(() =>
            {
                CleanupOldSessions(keepCount);
            }).ConfigureAwait(false);
        }
    }

    // DTO：仅保存 DataGrid 所需的轻量字段。按需扩展字段。
    public class TestResultDto
    {
        public int Id { get; set; }
        public int? MapY { get; set; }
        public int? MapX { get; set; }
        public bool IsAOIEnabled { get; set; }
        public bool IsIVLEnabled { get; set; }
        public bool IsEQEEnabled { get; set; }
        public bool IsVAMEnabled { get; set; }
        public string SerialNumber { get; set; }
        public string DisplayStatus { get; set; }
        public string DataValue { get; set; }
        public DateTime? StartTestTime { get; set; }
        public DateTime? EndTestTime { get; set; }
        public string TotalTime { get; set; }
        public string AOIGradeLevel { get; set;}
        public string BlackPattern { get; set; }

        //public string LightOnStatus { get; set;}
        //public string RegisterPixels { get; set;}
        //public string FinalClass { get; set;}
        //public string BlackPattern { get; set;}
        //public string Temperature { get; set;}
        //public string PixelLogic { get; set;}
        //public string Pressure { get; set;}
        //public int TouchDownCounts { get; set;}
        //public string ProbingCardSN { get; set;}
        // 新增：保存精确的枚举值
        public string ChipStatus { get; set; }

        public static TestResultDto FromObject(object die)
        {
            if (die == null) return null;
            object Get(string name) => die.GetType().GetProperty(name)?.GetValue(die);
            return new TestResultDto
            {
                Id = ToInt(Get("Id")),
                MapY = ToNullableInt(Get("MapY")),
                MapX = ToNullableInt(Get("MapX")),
                IsAOIEnabled = ToBool(Get("IsAOIEnabled")),
                IsIVLEnabled = ToBool(Get("IsIVLEnabled")),
                IsEQEEnabled = ToBool(Get("IsEQEEnabled")),
                IsVAMEnabled = ToBool(Get("IsVAMEnabled")),
                SerialNumber = ToStr(Get("SerialNumber")),
                // 通过反射获取Status
                ChipStatus = ToStr(Get("Status")),
                DisplayStatus = ToStr(Get("DisplayStatus")),
                DataValue = ToStr(Get("DataValue")),
                StartTestTime = ToDate(Get("StartTestTime")),
                EndTestTime = ToDate(Get("EndTestTime")),
                TotalTime = ToStr(Get("TotalTime")),
                AOIGradeLevel = ToStr(Get("AOIGradeLevel")),
                //LightOnStatus = ToStr(Get("LightOnStatus")),
                //RegisterPixels = ToStr(Get("RegisterPixels")),
                //FinalClass = ToStr(Get("FinalClass")),
                BlackPattern = ToStr(Get("BlackPattern")),
                //Temperature = ToStr(Get("Temperature")),
               
                //PixelLogic = ToStr(Get("PixelLogic")),
                //Pressure = ToStr(Get("Pressure")),
                //TouchDownCounts = ToInt(Get("TouchDownCounts")),
                //ProbingCardSN = ToStr(Get("ProbingCardSN"))
            };
        }

        private static int ToInt(object v) => v == null ? 0 : Convert.ToInt32(v);
        private static int? ToNullableInt(object v) => v == null ? (int?)null : Convert.ToInt32(v);
        private static bool ToBool(object v) => v != null && Convert.ToBoolean(v);
        private static string ToStr(object v) => v?.ToString();
        private static DateTime? ToDate(object v)
        {
            if (v == null) return null;
            if (v is DateTime dt) return dt;
            if (DateTime.TryParse(v.ToString(), out var parsed)) return parsed;
            return null;
        }
    }
}
