using ColorVision.Core.Entities;
using CVMysql;
using Newtonsoft.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace CVDB.Services
{
    //public enum CVConfigType
    //{
    //    RC,
    //    WinService,
    //    MQTT,
    //    ArchService
    //}



    public static class CfgService
    {
        public static string GetCfgByName(string name, int cType)
        {
            string jsonCfg = string.Empty;
            switch (cType)
            {
                case 0:
                    var configRc = GetRCCfgByName(name);
                    jsonCfg = JsonConvert.SerializeObject(configRc);
                    break;
                case 3:
                    var configArch = GetRCCfgByName(name);
                    jsonCfg = JsonConvert.SerializeObject(configArch);
                    break;
                case 1:
                    var configSevice = GetCfgByName(name);
                    jsonCfg = JsonConvert.SerializeObject(configSevice);
                    break;
                case 2:
                    var configMQTT = GetMQTTCfgByName(name);
                    jsonCfg = JsonConvert.SerializeObject(configMQTT);
                    break;
                default:
                    break;
            }
            return jsonCfg;
        }
        public static TScgdSysMqttCfg GetMQTTCfgByName(string name)
        {
            return MysqlControler.GetInstance().Sql.Select<TScgdSysMqttCfg>().Where(a => a.Name == name).ToOne();
        }
        public static VScgdSysConfigSevice GetCfgByName(string name)
        {
            return MysqlControler.GetInstance().Sql.Select<VScgdSysConfigSevice>().Where(a => a.Name == name).ToOne();
        }

        public static VScgdSysConfigRc GetRCCfgByName(string name)
        {
            return MysqlControler.GetInstance().Sql.Select<VScgdSysConfigRc>().Where(a => a.Name == name).ToOne();
        }
        public static VScgdSysConfigArchived GetArchCfgById(int id)
        {
            return MysqlControler.GetInstance().Sql.Select<VScgdSysConfigArchived>().Where(a => a.Id == id).ToOne();
        }
    }
}
