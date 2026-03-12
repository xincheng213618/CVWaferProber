using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI
{
    //属性继承配置，用于配置属性继承，例如：配置文件中的属性继承
    public interface IConfig
    {

    }
    public interface IConfigService
    {
        T1 GetRequiredService<T1>() where T1 : IConfig;
        void SaveConfigs();
        void LoadConfigs();

        void Save<T1>() where T1 : IConfig;

    }
    public class ConfigService
    {
        public static IConfigService Instance { get; private set; }
        public static void SetInstance(IConfigService instance)
        {
            Instance = instance;
        }
    }

    public class ConfigHandler: IConfigService
    {
        private static ILog log = LogManager.GetLogger(typeof(ConfigHandler));
        private static ConfigHandler _instance;
        private static readonly object _locker = new();
        public static ConfigHandler GetInstance() 
        {
            lock (_locker) 
            {
                _instance ??= new ConfigHandler();
                ConfigService.SetInstance(_instance);
                return _instance; 
            }
        }
        public string ConfigFilePath { get; set; }
        public string BackupFolderPath { get; set; }

        public DateTime InitDateTime { get; set; }

        public string ConfigDIFileName { get; set; }

        public ConfigHandler()
        {
            JsonSerializerSettings  = new JsonSerializerSettings { Formatting = Formatting.Indented };

            InitDateTime = DateTime.Now;
            string AssemblyCompany = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? Assembly.GetEntryAssembly()?.GetName().Name;
            ConfigDIFileName =  $"{Assembly.GetEntryAssembly()?.GetName().Name ?? AssemblyCompany}Config";
            string backupDirName = "Backup";
            if (Directory.Exists("Config"))
            {
                ConfigFilePath = $"Config\\{ConfigDIFileName}.json";
                BackupFolderPath = $"Config\\{backupDirName}\\";
            }
            else
            {
                string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\{AssemblyCompany}\\Config\\";
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);
                ConfigFilePath = DirectoryPath + ConfigDIFileName +".json";
                BackupFolderPath = DirectoryPath + backupDirName + "\\";
            }

            if (!Directory.Exists(BackupFolderPath))
                Directory.CreateDirectory(BackupFolderPath);
            LoadConfigs(ConfigFilePath);
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                if (IsAutoSave)
                    SaveConfigs(ConfigFilePath);
            };
        }


        public bool IsAutoSave { get; set; } = true;

        public void Reload()
        {
            SaveConfigs();
            LoadConfigs(ConfigFilePath);
        }

        public void SaveConfigs() => SaveConfigs(ConfigFilePath);

        internal JsonSerializerSettings JsonSerializerSettings { get; set; } 

        public Dictionary<Type, IConfig> Configs { get; set; }

        private IConfig GetOrCreateConfig(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (!typeof(IConfig).IsAssignableFrom(type))
                throw new ArgumentException("Type must implement IConfig.", nameof(type));

            if (Configs.TryGetValue(type, out var existing))
            {
                return existing;
            }

            IConfig instance = null;
            var configName = type.Name;

            try
            {
                if (jsonObject.TryGetValue(configName, out JToken configToken))
                {
                    var config = configToken.ToObject(type, JsonSerializer.Create(JsonSerializerSettings));
                    if (config is IConfig configInstance)
                    {
                        instance = configInstance;
                    }
                }

                if (instance == null)
                {
                    instance = Activator.CreateInstance(type) as IConfig;
                }
            }
            catch (Exception ex)
            {
                log.Warn(ex);
                instance = Activator.CreateInstance(type) as IConfig;
            }

            if (instance == null)
            {
                throw new InvalidOperationException($"无法创建配置实例: {type.FullName}");
            }

            Configs[type] = instance;
            return instance;
        }

        public T1 GetRequiredService<T1>() where T1 : IConfig
        {
            return (T1)GetOrCreateConfig(typeof(T1));
        }

        public IConfig GetRequiredService(Type type)
        {
            return GetOrCreateConfig(type);
        }


        public void SaveConfigs(string fileName)
        {
            var jObject = new JObject();
            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);
                try
                {
                    jObject = JObject.Parse(json);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

            //防止被修改
            var configsSnapshot = Configs.ToArray();
            JsonSerializer jsonSerializer = JsonSerializer.Create(JsonSerializerSettings);
            foreach (var configPair in configsSnapshot)
            {
                try
                {
                    if (Application.Current == null)
                    {

                        jObject[configPair.Key.Name] = JToken.FromObject(configPair.Value, jsonSerializer);

                    }
                    else if (Application.Current.Dispatcher.CheckAccess())
                    {
                        jObject[configPair.Key.Name] = JToken.FromObject(configPair.Value, jsonSerializer);

                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            jObject[configPair.Key.Name] = JToken.FromObject(configPair.Value, jsonSerializer);


                        });
                    }



                }
                catch(Exception ex)
                {
                    log.Info(configPair.Key);
                    log.Error(ex);
                }
            }

            using (StreamWriter file = File.CreateText(fileName))
            {
                using (JsonTextWriter writer = new JsonTextWriter(file))
                {
                    jObject.WriteTo(writer);
                }
            }
        }

        public void LoadDefaultConfigs()
        {
            try
            {
                var files = Directory.GetFiles(BackupFolderPath, $"{ConfigDIFileName}Backup_*.json")
                    .OrderByDescending(f => f)
                    .ToList();
                if (files.Count !=0)
                {
                    LoadConfigs(files.First());
                    File.Copy(files.First(), ConfigFilePath, true);
                }
                else
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            foreach (var type in assembly.GetTypes().Where(t => typeof(IConfig).IsAssignableFrom(t) && !t.IsAbstract))
                            {
                                if (Activator.CreateInstance(type) is IConfig config)
                                {
                                    Configs[type] = config;
                                }
                            }
                        }
                        catch
                        {

                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }


        }


        public void LoadConfigs() => LoadConfigs(ConfigFilePath);
        private JObject jsonObject = new JObject();

        public void LoadConfigs(string fileName)
        {
            Configs = new Dictionary<Type, IConfig>();
            if (File.Exists(fileName))
            {
                try
                {
                    using (StreamReader file = File.OpenText(fileName))
                    using (JsonTextReader reader = new JsonTextReader(file))
                    {
                        jsonObject = (JObject)JToken.ReadFrom(reader);
                    }
                }
                catch(Exception ex)
                {
                    log.Warn(ex);
                    LoadDefaultConfigs();
                }
            }
            else
            {
                LoadDefaultConfigs();
            }
        }

        public void Save<T1>() where T1 : IConfig
        {
            var type = typeof(T1);
            var configName = type.Name;

            if (Configs == null)
            {
                Configs = new Dictionary<Type, IConfig>();
            }

            // Ensure the config instance exists (will load or create default)
            var configInstance = GetRequiredService<T1>();

            JObject jObject = new JObject();
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    jObject = JObject.Parse(File.ReadAllText(ConfigFilePath));
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    // If parse fails, start with a clean object to avoid corrupt carry-over
                    jObject = new JObject();
                }
            }

            void Persist()
            {
                jObject[configName] = JToken.FromObject(configInstance, JsonSerializer.Create(JsonSerializerSettings));

            }

            try
            {
                if (Application.Current == null)
                {
                    Persist();
                }
                else if (Application.Current.Dispatcher.CheckAccess())
                {
                    Persist();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(Persist);
                }
            }
            catch (Exception ex)
            {
                return;
            }

            try
            {
                using (StreamWriter file = File.CreateText(ConfigFilePath))
                using (JsonTextWriter writer = new JsonTextWriter(file))
                {
                    jObject.WriteTo(writer);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, ex);
            }
        }
    }



}
