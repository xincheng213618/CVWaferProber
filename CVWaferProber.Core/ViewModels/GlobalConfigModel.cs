using CVWaferProber.Core.Config;
using CVWaferProber.Core.Models;
using log4net;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace CVWaferProber.Core.ViewModels
{
    public class GlobalConfigModel : ViewModelBase
    {
        public ObservableCollection<LogLevelItem> LogLevels { get; set; }

        public int BreakOnErrorNum { get; set; } = 2;
        public bool IsAutoStop { get; set; } = false;
        public bool IsBreakOnError { get; set; } = true;
        public string SelectedLogLevel { get; set; }
        public MappingSettings MapSettings { get; set; } = new();
        public MotionSettings MotionSettings { get; set; } = new();
        public ConnectionSettings ConnectionSettings { get; set; } = new();
        /// <summary>
        /// VAM自动导出默认路径
        /// </summary>
        public string VamExportPath { get; set; } = @"D:\Project\VAM";
        /// <summary>
        /// AOI导出路径
        /// </summary>
        public string AoiExportPath { get; set; } = @"D:\Project\AOI";

        /// <summary>
        /// EQE导出路径
        /// </summary>
        public string EqeExportPath { get; set; } = @"D:\Project\EQE";

        /// <summary>
        /// IVL导出路径
        /// </summary>
        public string IvlExportPath { get; set; } = @"D:\Project\IVL";

        public GlobalConfigModel()
        {
            // 初始化日志级别列表
            LogLevels = new ObservableCollection<LogLevelItem>
            {
                new LogLevelItem("ALL", "全部 - 记录所有日志"),
                new LogLevelItem("DEBUG", "调试 - 最详细的日志信息"),
                new LogLevelItem("INFO", "信息 - 一般信息"),
                new LogLevelItem("WARN", "警告 - 潜在问题"),
                new LogLevelItem("ERROR", "错误 - 错误信息"),
                new LogLevelItem("FATAL", "严重错误 - 严重错误"),
                new LogLevelItem("OFF", "关闭 - 不记录任何日志")
            };

            SelectedLogLevel = GetCurrentLogLevel();
        }
        // 获取当前日志级别
        public static string GetCurrentLogLevel()
        {
            var hierarchy = (log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository();
            return hierarchy.Root.Level.ToString();
        }
        /// <summary>
        /// 验证所有配置路径是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(VamExportPath) &&
                   !string.IsNullOrWhiteSpace(AoiExportPath) &&
                   !string.IsNullOrWhiteSpace(EqeExportPath) &&
                   !string.IsNullOrWhiteSpace(IvlExportPath);
        }

        /// <summary>
        /// 确保所有配置路径对应的文件夹存在
        /// </summary>
        public void EnsureDirectoriesExist()
        {
            CreateDirectoryIfNotExists(VamExportPath);
            CreateDirectoryIfNotExists(AoiExportPath);
            CreateDirectoryIfNotExists(EqeExportPath);
            CreateDirectoryIfNotExists(IvlExportPath);
        }

        /// <summary>
        /// 工具方法：创建文件夹（避免重复代码）
        /// </summary>
        private void CreateDirectoryIfNotExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                log4net.LogManager.GetLogger(typeof(GlobalConfigModel)).Info($"{(string)Application.Current.FindResource("Createdexportdirectory")}：{path}");
            }
        }
    }

    public class LogLevelItem
    {
        public string Level { get; set; }
        public string Description { get; set; }

        public LogLevelItem(string level, string description)
        {
            Level = level;
            Description = description;
        }
    }
}
