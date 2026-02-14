using ColorVision.Core.Message;
using ColorVision.Core.Message.Response;
using ColorVision.Message.Services;

namespace CVMQTTNodeClient
{
    public class MQTTNodeServiceTO
    {
        public string ServiceToken { get; set; } = string.Empty;
        public string ServiceCode { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public string UpChannel { get; set; } = string.Empty;
        public string DownChannel { get; set; } = string.Empty;
        public Dictionary<string, MQTTDeviceTO> Devices { get; set; }

        public DeviceMessageManager RequestManager { get; private set; }

        public MQTTNodeServiceTO()
        {
            this.RequestManager = new DeviceMessageManager();
            this.Devices = new Dictionary<string, MQTTDeviceTO>();
        }

        public MQTTNodeServiceTO(ServiceNodeQueryResponse.NodeServiceTO svr)
        {
            this.RequestManager = new DeviceMessageManager();
            this.ServiceName = svr.ServiceName;
            this.ServiceType = svr.ServiceType;
            this.ServiceToken = svr.ServiceToken;
            this.ServiceCode = svr.ServiceCode;
            this.UpChannel = svr.UpChannel;
            this.DownChannel = svr.DownChannel;

            this.Devices = new Dictionary<string, MQTTDeviceTO>();
            foreach (var dev in svr.Devices)
            {
                this.Devices.TryAdd(dev.Key, new MQTTDeviceTO(dev.Value));
            }
        }
        public void SetResponse(DeviceResponseMessageHeader? resp)
        {
            RequestManager.SetResponse(resp);
        }

        public class MQTTDeviceTO
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;

            public MQTTDeviceTO(ServiceNodeQueryResponse.NodeDeviceTO dev)
            {
                this.Code = dev.Code;
                this.Name = dev.Name;
            }
        }

    }
}
