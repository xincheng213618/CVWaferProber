using ColorVision.Core.Entities;
using CVCommCore;
using CVDB.Services;
using CVMysql;
using Newtonsoft.Json;
using System;

namespace CVWaferProber.MQTT
{
    public abstract class CVConfig : CustomConfigurationFileReader
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVConfig));
        public CVConfigType CfgType { get; set; }
        public string NodeName { get; set; }
        public CVConfig(string configFileName, CVConfigType cType) : base(configFileName)
        {
            if (logger.IsInfoEnabled) logger.InfoFormat("Load service config file => {0}", configFileName);

            CfgType = cType;
        }
        protected void ReadValue(System.Configuration.Configuration config)
        {
            NodeName = config.AppSettings.Settings["NodeName"]?.Value;
            if (!string.IsNullOrEmpty(NodeName))
            {
                //从数据库读取配置
                var cfg = CfgService.GetCfgByName(NodeName, (int)CfgType);
                if (!string.IsNullOrEmpty(cfg) && LoadFrom(cfg))
                {
                    SaveLocalCfg();
                    return;
                }
            }
            //本地配置文件
            LoadFrom(config);
        }

        protected virtual void SaveLocalCfg()
        {

        }

        protected abstract bool LoadFrom(string config);
        protected abstract bool LoadFrom(System.Configuration.Configuration config);
    }
    public class MQTTConfig : CVConfig
    {
        public MQTTConfig(string configFileName) : base(configFileName, CVConfigType.MQTT)
        {
            ReadValue(Config);
        }
        public MQTTConfig() : this("127.0.0.1", 1883, false)
        {
        }

        public MQTTConfig(string host, int port) : this(host, port, false)
        {
        }

        public MQTTConfig(string host, int port, bool isServer) : this(host, port, string.Empty, string.Empty, isServer, false)
        {
        }

        public MQTTConfig(string host, int port, string userName, string password, bool isServer, bool isDebug) : base(string.Empty, CVConfigType.MQTT)
        {
            Init(host, port, userName, password, isServer, isDebug);
        }
        protected override bool LoadFrom(System.Configuration.Configuration config)
        {
            string MQTTHost = config.AppSettings.Settings["Host"]?.Value;
            string MQTTPort = config.AppSettings.Settings["Port"]?.Value;
            string IsDebugOut = config.AppSettings.Settings["IsDebugOut"]?.Value;
            string IsSvr = config.AppSettings.Settings["IsSever"]?.Value;

            if (string.IsNullOrWhiteSpace(MQTTHost)) MQTTHost = "127.0.0.1";
            if (string.IsNullOrWhiteSpace(MQTTPort)) MQTTPort = "1883";
            if (string.IsNullOrWhiteSpace(IsDebugOut)) IsDebugOut = "False";
            if (string.IsNullOrWhiteSpace(IsSvr)) IsSvr = "False";

            Init(MQTTHost, Convert.ToInt16(MQTTPort), string.Empty, string.Empty, Convert.ToBoolean(IsSvr), Convert.ToBoolean(IsDebugOut));
            return true;
        }
        protected override bool LoadFrom(string cfg)
        {
            TScgdSysMqttCfg mqttCfg = JsonConvert.DeserializeObject<TScgdSysMqttCfg>(cfg);
            string MQTTHost = mqttCfg.Host;
            int MQTTPort = (int)mqttCfg.Port;
            bool IsDebugOut = mqttCfg.IsDebug == 1 ? true : false;
            bool IsSvr = false;
            Init(MQTTHost, MQTTPort, string.Empty, string.Empty, Convert.ToBoolean(IsSvr), IsDebugOut);
            return true;
        }
        public void Init(string host, int port, string userName, string password, bool isServer, bool isDebug)
        {
            Host = host;
            Port = port;
            UserName = userName;
            Password = password;
            IsServer = isServer;
            IsDebugOut = isDebug;
        }
        public static MQTTConfig FromConfigFile()
        {
            string MQTTHost = System.Configuration.ConfigurationManager.AppSettings["Host"];
            string MQTTPort = System.Configuration.ConfigurationManager.AppSettings["Port"];
            string IsDebugOut = System.Configuration.ConfigurationManager.AppSettings["IsDebugOut"];
            string IsSvr = System.Configuration.ConfigurationManager.AppSettings["IsSever"];
            if (string.IsNullOrWhiteSpace(MQTTHost)) MQTTHost = "127.0.0.1";
            if (string.IsNullOrWhiteSpace(MQTTPort)) MQTTPort = "1883";
            if (string.IsNullOrWhiteSpace(IsDebugOut)) IsDebugOut = "False";
            if (string.IsNullOrWhiteSpace(IsSvr)) IsSvr = "False";
            return new MQTTConfig() { Host = MQTTHost, Port = Convert.ToInt16(MQTTPort), IsServer = Convert.ToBoolean(IsSvr), IsDebugOut = Convert.ToBoolean(IsDebugOut) };
        }
        public static MQTTConfig FromCustomConfigFile(string cfgFileName = "MQTT.config")
        {
            string cfgPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            string cfgFile = System.IO.Path.Combine(cfgPath, "cfg", cfgFileName);
            return new MQTTConfig(cfgFile);
        }
        public string Host { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public bool IsServer { get; set; }
        public bool IsDebugOut { get; set; }
    }
}
