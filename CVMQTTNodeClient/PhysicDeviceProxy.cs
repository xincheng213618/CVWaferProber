using ColorVision.Core.Message;
using ColorVision.Core.Message.Response;
using ColorVision.Message.Services;

namespace CVMQTTNodeClient
{
    public class PhysicDeviceProxy
    {
        public string DeviceCode { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceStatus { get; set; } = string.Empty;
        public PhysicServiceProxy ServiceProxy { get; protected set; }
        public DeviceMessageManager RequestManager { get; protected set; } = new DeviceMessageManager();

        public PhysicDeviceProxy(ServiceNodeQueryResponse.NodeDeviceTO dev, ServiceNodeProxy serviceProxy)
        {
            if (dev == null) throw new ArgumentNullException(nameof(dev));

            DeviceCode = dev.Code ?? string.Empty;
            DeviceName = dev.Name ?? string.Empty;
            DeviceStatus = dev.Status ?? string.Empty;

            ServiceProxy = new PhysicServiceProxy(serviceProxy);
        }

        public PhysicDeviceProxy(string deviceCode, string deviceName, string deviceStatus, string serviceType, string serviceCode, string serviceName, string serviceToken, string upChannel, string downChannel)
        {
            DeviceCode = deviceCode;
            DeviceName = deviceName;
            DeviceStatus = deviceStatus;
            ServiceProxy = new PhysicServiceProxy(serviceType, serviceCode, serviceName, serviceToken, upChannel, downChannel);
        }

        public void Update(ServiceNodeQueryStatusResponse.NodeDeviceTO dev)
        {
            if (dev == null) return;
            DeviceStatus = dev.Status ?? string.Empty;
        }
        public void SetResponse(DeviceResponseMessageHeader? resp)
        {
            RequestManager.SetResponse(resp);
        }
        public class PhysicServiceProxy
        {
            public string ServiceCode { get; set; } = string.Empty;
            public string ServiceName { get; set; } = string.Empty;
            public string ServiceType { get; set; } = string.Empty;
            public string ServiceToken { get; set; } = string.Empty;

            public string UpChannel { get; private set; } = string.Empty;
            public string DownChannel { get; private set; } = string.Empty;

            public PhysicServiceProxy(ServiceNodeProxy serviceProxy)
            {
                this.UpChannel = serviceProxy.UpChannel;
                this.DownChannel = serviceProxy.DownChannel;
                this.ServiceCode = serviceProxy.ServiceCode;
                this.ServiceName = serviceProxy.ServiceName;
                this.ServiceType = serviceProxy.ServiceType;
                this.ServiceToken = serviceProxy.ServiceToken;
            }

            public PhysicServiceProxy(string serviceType, string serviceCode, string serviceName, string serviceToken, string upChannel, string downChannel)
            {
                ServiceType = serviceType;
                ServiceCode = serviceCode;
                ServiceName = serviceName;
                ServiceToken = serviceToken;
                UpChannel = upChannel;
                DownChannel = downChannel;
            }
        }
    }
}
