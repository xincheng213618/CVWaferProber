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
        private static readonly string FilePath = Path.Combine(AppFolder, "testresults.json");

        public static void EnsureFolder() => Directory.CreateDirectory(AppFolder);

        public static async Task SaveAsync(IEnumerable<TestResultDto> items)
        {
            try
            {
                EnsureFolder();
                var json = JsonConvert.SerializeObject(items, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                await File.WriteAllTextAsync(FilePath, json);
            }
            catch
            {
                // 不抛出到 UI，调用方可记录日志
            }
        }

        public static List<TestResultDto> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<TestResultDto>();
                var json = File.ReadAllText(FilePath);
                var list = JsonConvert.DeserializeObject<List<TestResultDto>>(json);
                return list ?? new List<TestResultDto>();
            }
            catch
            {
                return new List<TestResultDto>();
            }
        }

        public static async Task<List<TestResultDto>> LoadAsync()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<TestResultDto>();
                var json = await File.ReadAllTextAsync(FilePath);
                var list = JsonConvert.DeserializeObject<List<TestResultDto>>(json);
                return list ?? new List<TestResultDto>();
            }
            catch
            {
                return new List<TestResultDto>();
            }
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
                DisplayStatus = ToStr(Get("DisplayStatus")),
                DataValue = ToStr(Get("DataValue")),
                StartTestTime = ToDate(Get("StartTestTime")),
                EndTestTime = ToDate(Get("EndTestTime")),
                TotalTime = ToStr(Get("TotalTime"))
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
