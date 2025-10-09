using CVCommCore;
using Newtonsoft.Json;

namespace CVWaferProber.MQTT
{
    public class RCNodeService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(RCNodeService));
        public RCNodeService()
        {
            this.Devices = new Dictionary<string, RCServiceDevice>();
        }

        public RCNodeService(string RCNodeName, int serviceId, string serviceType, string serviceCode, string serviceName) : this()
        {
            this.ServiceId = serviceId;
            this.ServiceType = serviceType;
            this.ServiceCode = serviceCode;
            this.ServiceName = serviceName;
            this.UpChannel = MQTTRCServiceTypeConst.BuildServiceUpTopic(ServiceType, ServiceCode, RCNodeName);//BuildUpChannel();
            this.DownChannel = MQTTRCServiceTypeConst.BuildServiceDownTopic(ServiceType, ServiceCode, RCNodeName); //BuildDownChannel();
        }

        public int ServiceId { get; set; }
        public string ServiceToken { get; set; }
        public string ServiceCode { get; set; }
        public string ServiceName { get; set; }
        public string ServiceType { get; set; }
        public string UpChannel { get; set; }
        public string DownChannel { get; set; }
        public string LiveTime { get; private set; }
        public int OverTime { get; set; }
        public Dictionary<string, RCServiceDevice> Devices { get; set; }
        //public void Update(RCNodeService service)
        //{
        //    this.LiveTime = service.LiveTime;
        //    this.OverTime = service.OverTime;
        //    this.ServiceToken = service.ServiceToken;

        //    foreach (var item in service.Devices)
        //    {
        //        Devices[item.Key] = item.Value;
        //    }
        //}

        public void AddDevice(RCServiceDevice device)
        {
            if (!this.Devices.ContainsKey(device.Code))
            {
                this.Devices.Add(device.Code, device);
            }
        }

        //public void ReloadDevice(string deviceCode)
        //{
        //    var devInfo = SysResourceService.GetDeviceByCode(deviceCode);
        //    if (devInfo != null)
        //    {
        //        if (!this.Devices.ContainsKey(deviceCode))
        //        {
        //            this.AddDevice(new RCServiceDevice() { Code = devInfo.Code, Name = devInfo.Name, JsonCfg = JsonConvert.DeserializeObject(devInfo.TxtValue), Status = DeviceStatusType.Unknown.ToString(), });
        //            logger.DebugFormat("ReloadDevice Add Device => {0}/{1}", devInfo.Code, devInfo.Name);
        //        }
        //        else
        //        {
        //            var dev = Devices[deviceCode];
        //            dev.Status = DeviceStatusType.Unknown.ToString();
        //            dev.JsonCfg = JsonConvert.DeserializeObject(devInfo.TxtValue);
        //            dev.Name = devInfo.Name;
        //        }
        //    }
        //}

        public bool Update(RCServiceHeartbeat shb)
        {
            if (IsThisNode(shb))
            {
                //logger.DebugFormat("Update = > \r\n{0}",JsonConvert.SerializeObject(shb, Formatting.Indented));
                this.DownChannel = shb.DownChannel;
                this.UpChannel = shb.UpChannel;
                this.LiveTime = System.DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss");
                this.OverTime = shb.OverTime;
                foreach (var dev_shb in shb.Devices)
                {
                    if (Devices.ContainsKey(dev_shb.DeviceCode))
                    {
                        Devices[dev_shb.DeviceCode].Status = dev_shb.DeviceStatus;
                    }
                }
                return true;
            }
            return false;
        }

        public bool IsThisNode(RCServiceHeartbeat shb)
        {
            return IsThisNode(shb.ServiceCode, shb.ServiceType);
        }

        public bool IsThisNode(string serviceCode, string serviceType)
        {
            return serviceCode == this.ServiceCode && serviceType == this.ServiceType;
        }

        public bool IsLive()
        {
            System.DateTime dt;
            System.DateTime dt_now = System.DateTime.Now;
            string fmt = "HH:mm:ss";
            if (System.DateTime.TryParse(this.LiveTime, out dt))
            {
                if (this.OverTime > 0) dt = dt.AddMilliseconds(this.OverTime);
                logger.DebugFormat("Node={4}, LiveTime={0}/{3}, OverTime={1}, Now={2}", LiveTime, dt.ToString(fmt), dt_now.ToString(fmt), OverTime, ServiceCode);
                if (dt > dt_now)
                {
                    return true;
                }
            }
            return false;
        }

        private string BuildUpChannel()
        {
            return ServiceType + "/CMD/" + ServiceCode;
        }

        private string BuildDownChannel()
        {
            return ServiceType + "/STATUS/" + ServiceCode;
        }
    }
}
