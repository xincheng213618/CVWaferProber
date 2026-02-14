using ColorVision.Core.Message;

namespace CVMQTTNodeClient
{
    public class CVBaseService : CVAbstractBaseServiceNode
    {
        public string UpChannel { get; private set; }
        public string DownChannel { get; private set; }

        public CVBaseService(MQTTNodeServiceTO serviceTO):
            base(serviceTO.ServiceCode, serviceTO.ServiceName, serviceTO.ServiceType, serviceTO.ServiceToken)
        {
            this.UpChannel = serviceTO.UpChannel;
            this.DownChannel = serviceTO.DownChannel;
        }
        public CVBaseService(string serviceCode, string serviceName, string serviceType, string serviceToken,string upChannel,string downChannel) :
            base(serviceCode, serviceName, serviceType, serviceToken)
        {
            this.UpChannel = upChannel;
            this.DownChannel = downChannel;
        }
    }
    public class CVBaseDeviceNode
    {
        public string DeviceCode { get; set; }
        public string DeviceName { get; set; }
        public CVBaseService Service { get; protected set; }
        public DeviceMessageManager RequestManager { get; protected set; }

        public CVBaseDeviceNode(string deviceCode, string deviceName, CVBaseService service, DeviceMessageManager? requestManager = null)
        {
            this.DeviceCode = deviceCode;
            this.DeviceName = deviceName;
            this.Service = service;
            if(requestManager == null) this.RequestManager = new DeviceMessageManager();
            else this.RequestManager = requestManager;
        }

        public CVBaseDeviceNode(string deviceCode, string deviceName, string serviceType, string serviceCode, string serviceName, string serviceToken, string upChannel, string downChannel) :
            this(deviceCode, deviceName, new CVBaseService(serviceCode, serviceName, serviceType, serviceToken, upChannel, downChannel))
        {
        }
        public CVBaseDeviceNode(MQTTNodeServiceTO.MQTTDeviceTO device, MQTTNodeServiceTO service) :
            this(device.Code, device.Name, new CVBaseService(service), service.RequestManager)
        {
        }
    }
}
