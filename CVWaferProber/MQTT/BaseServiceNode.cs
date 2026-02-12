namespace CVWaferProber.MQTT
{
    public class BaseServiceNode
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseServiceNode));
        public int ServiceId { get; set; }
        public string ServiceToken { get; set; }
        public string ServiceCode { get; set; }
        public string ServiceName { get; set; }
        public string ServiceType { get; set; }
        public string UpChannel { get; set; }
        public string DownChannel { get; set; }
        public string LiveTime { get; private set; }
        public int OverTime { get; set; }
        public Dictionary<string, BaseServiceNodeDevice> Devices { get; set; }

        public BaseServiceNode()
        {
            this.Devices = new Dictionary<string, BaseServiceNodeDevice>();
        }

        public BaseServiceNode(string RCNodeName, int serviceId, string serviceType, string serviceCode, string serviceName) : this()
        {
            this.ServiceId = serviceId;
            this.ServiceType = serviceType;
            this.ServiceCode = serviceCode;
            this.ServiceName = serviceName;
            this.UpChannel = MQTTRCServiceTypeConst.BuildServiceUpTopic(ServiceType, ServiceCode, RCNodeName);//BuildUpChannel();
            this.DownChannel = MQTTRCServiceTypeConst.BuildServiceDownTopic(ServiceType, ServiceCode, RCNodeName); //BuildDownChannel();
        }

        public void AddDevice(BaseServiceNodeDevice device)
        {
            if (!this.Devices.ContainsKey(device.Code))
            {
                this.Devices.Add(device.Code, device);
            }
        }
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

    public class BaseServiceNodeDevice
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public dynamic JsonCfg { get; set; }
        public string Status { get; set; }
    }

}
