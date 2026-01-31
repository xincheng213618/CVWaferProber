using log4net;
using log4net.Config;
using System.IO;
using System.Reflection;

namespace CVWaferProber.Services
{
    public static class LogManagerService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LogManagerService));
        private static readonly string configFile = "log4net.config";

        static LogManagerService()
        {
            ConfigureLog4Net();
        }

        public static void ConfigureLog4Net()
        {
            var configFileInfo = new FileInfo(configFile);
            XmlConfigurator.ConfigureAndWatch(configFileInfo);
        }

        // 动态修改日志级别
        public static void ChangeLogLevel(string level)
        {
            var hierarchy = (log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository();

            switch (level.ToUpper())
            {
                case "ALL":
                    hierarchy.Root.Level = hierarchy.LevelMap["ALL"];
                    break;
                case "DEBUG":
                    hierarchy.Root.Level = hierarchy.LevelMap["DEBUG"];
                    break;
                case "INFO":
                    hierarchy.Root.Level = hierarchy.LevelMap["INFO"];
                    break;
                case "WARN":
                    hierarchy.Root.Level = hierarchy.LevelMap["WARN"];
                    break;
                case "ERROR":
                    hierarchy.Root.Level = hierarchy.LevelMap["ERROR"];
                    break;
                case "FATAL":
                    hierarchy.Root.Level = hierarchy.LevelMap["FATAL"];
                    break;
                case "OFF":
                    hierarchy.Root.Level = hierarchy.LevelMap["OFF"];
                    break;
                default:
                    hierarchy.Root.Level = hierarchy.LevelMap["INFO"];
                    break;
            }

            hierarchy.RaiseConfigurationChanged(EventArgs.Empty);
            //log.Info($"日志级别已更改为: {level}");
        }

        // 获取当前日志级别
        public static string GetCurrentLogLevel()
        {
            var hierarchy = (log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository();
            return hierarchy.Root.Level.ToString();
        }

        // 重新加载配置文件
        public static void ReloadConfiguration()
        {
            XmlConfigurator.Configure(new FileInfo(configFile));
            //log.Info("log4net 配置已重新加载");
        }
    }
}
