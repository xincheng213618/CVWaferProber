using CVWaferProber.Core.Config;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;

namespace CVWaferProber.Core.Config
{
    // ConfigManager.cs
    public static class ConfigManager
    {
        private static readonly string ConfigDirectory;
        private static readonly string ConfigFilePath;
        private static AppConfig _currentConfig;

        static ConfigManager()
        {
            ConfigDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ColorVision",
                "CVWaferProber"
            );
            ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");
            LoadConfig();
        }

        public static AppConfig Config => _currentConfig;

        // 加载配置
        public static void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    _currentConfig = JsonConvert.DeserializeObject<AppConfig>(json);
                }
                else
                {
                    _currentConfig = new AppConfig();
                    SaveConfig();
                }
            }
            catch
            {
                _currentConfig = new AppConfig();
            }
        }

        // 保存配置
        public static void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                string json = JsonConvert.SerializeObject(_currentConfig, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                // 处理保存失败的情况
                Debug.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        // 重置为默认配置
        public static void ResetToDefaults()
        {
            _currentConfig = new AppConfig();
            SaveConfig();
        }

        // 导入配置
        public static bool ImportConfig(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                _currentConfig = JsonConvert.DeserializeObject<AppConfig>(json);
                SaveConfig();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 导出配置
        public static bool ExportConfig(string filePath)
        {
            try
            {
                string json = JsonConvert.SerializeObject(_currentConfig, Formatting.Indented);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
