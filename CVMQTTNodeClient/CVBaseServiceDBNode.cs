namespace CVMQTTNodeClient
{
    public class CVBaseServiceDBNode : CVAbstractBaseServiceNode
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVBaseServiceDBNode));
        public int ServiceId { get; set; }
        public dynamic? JsonCfg { get; set; }
        public string UpChannel { get; private set; }
        public string DownChannel { get; private set; }
        public string LiveTime { get; private set; }
        public int OverTime { get; set; }
        public Dictionary<string, CVBaseServiceDBNodeDevice> Devices { get; set; }

        public CVBaseServiceDBNode() : base()
        {
            this.Devices = new Dictionary<string, CVBaseServiceDBNodeDevice>();
            this.OverTime = -1;
            this.LiveTime = string.Empty;
            this.UpChannel = string.Empty;
            this.DownChannel = string.Empty;
            this.JsonCfg = null;
        }
        public CVBaseServiceDBNode(string RCNodeName, string serviceType, string serviceCode, string serviceName) : base(serviceCode, serviceName,serviceType)
        {
            this.UpChannel = MQTTCVServiceBuilder.BuildServiceUpTopic(serviceType, serviceCode, RCNodeName);
            this.DownChannel = MQTTCVServiceBuilder.BuildServiceDownTopic(serviceType, serviceCode, RCNodeName);
            this.Devices = new Dictionary<string, CVBaseServiceDBNodeDevice>();
            this.OverTime = -1;
            this.LiveTime = string.Empty;
            this.JsonCfg = null;
        }
        public CVBaseServiceDBNode(string RCNodeName, int serviceId, string serviceType, string serviceCode, string serviceName) : this(RCNodeName, serviceType, serviceCode, serviceName)
        {
            this.ServiceId = serviceId;
        }

        public void AddDevice(CVBaseServiceDBNodeDevice device)
        {
            if (!this.Devices.ContainsKey(device.Code))
            {
                this.Devices.Add(device.Code, device);
            }
        }
        public bool Update(CVServiceHeartbeat shb)
        {
            if (IsThisNode(shb))
            {
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

        public bool IsThisNode(CVServiceHeartbeat shb)
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
    }

    public class CVBaseServiceDBNodeDevice
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public dynamic? JsonCfg { get; set; }
        public string Status { get; set; }
        public CVBaseServiceDBNodeDevice()
        {
            this.Code = string.Empty;
            this.Name = string.Empty;
            this.Status = string.Empty;
            this.JsonCfg = null;
        }
    }
}
