namespace CVMQTTNodeClient
{
    public class CVBaseService : CVAbstractBaseServiceNode
    {
        public string UpChannel { get; private set; }
        public string DownChannel { get; private set; }

        public CVBaseService(MQTTNodeServiceTO serviceTO):base(serviceTO.ServiceCode, serviceTO.ServiceName, serviceTO.ServiceType, serviceTO.ServiceToken)
        {
            this.UpChannel = serviceTO.UpChannel;
            this.DownChannel = serviceTO.DownChannel;
        }
    }
    public class CVBaseServiceDevice
    {
        public string DeviceCode { get; set; }
        public string DeviceName { get; set; }
        public CVBaseService Service { get; private set; }

        public CVBaseServiceDevice(string deviceCode, string deviceName, CVBaseService service)
        {
            this.DeviceCode = deviceCode;
            this.DeviceName = deviceName;
            this.Service = service;
        }
    }
}
