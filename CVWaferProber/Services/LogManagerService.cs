using log4net;
using log4net.Config;
using System.IO;
using System.Reflection;
using System.Xml;

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

            SaveConfigureLog4Net(level);
        }
        public static void SaveConfigureLog4Net(string level)
        {
            var log4netConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);

            if (File.Exists(log4netConfigFilePath))
            {
                XmlDocument log4netConfig = new XmlDocument();
                log4netConfig.Load(log4netConfigFilePath);

                // 修改log4net.config中的某些设置
                // 例如，修改root logger的level
                XmlNode root = log4netConfig.DocumentElement.SelectSingleNode("/configuration/log4net/root/level");
                if (root != null)
                {
                    XmlAttribute levelAttribute = root.Attributes["value"];
                    if (levelAttribute != null)
                    {
                        levelAttribute.Value = level; // 修改为DEBUG级别
                    }
                    log4netConfig.Save(log4netConfigFilePath);
                }
            }
        }
        // 获取当前日志级别
        public static string GetCurrentLogLevel()
        {
            var hierarchy = (log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository();
            return hierarchy.Root.Level.ToString();
        }
    }
}
