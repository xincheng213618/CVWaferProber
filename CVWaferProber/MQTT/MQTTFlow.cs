using ColorVision.Core.Message;
using ColorVision.Core.Message.Param;
using ColorVision.Core.Message.Response;
using ColorVision.Message.Flow;
using ColorVision.Message.Model;
using CVMQTTNodeClient;
using Newtonsoft.Json;

namespace CVWaferProber.MQTT
{
    public class MQTTFlowDeviceNode : CVBaseDeviceNode
    {
        public static string FlowDeviceCode  = "DEV.Flow.Default";
        public MQTTFlowDeviceNode(string serviceType, string serviceCode, string serviceName, string serviceToken, string upChannel, string downChannel, string deviceCode, DeviceMessageManager requestManager)
            : base(deviceCode, deviceCode, serviceType, serviceCode, serviceName, serviceToken, upChannel, downChannel)
        {
            this.DeviceCode = deviceCode;
            this.RequestManager = requestManager;
        }

        public MQTTFlowDeviceNode(CVBaseDeviceNode dev) :
            this(dev.Service.ServiceType, dev.Service.ServiceCode, dev.Service.ServiceName, dev.Service.ServiceToken, dev.Service.UpChannel, dev.Service.DownChannel, dev.DeviceCode, dev.RequestManager)
        {
        }

        public string BuildRequestString(string serialNumber, int flowId, string flowName, List<ServiceMO> services)
        {
            return BuildRequestString(serialNumber, serialNumber, flowId, flowName, services);
        } 
        public string BuildRequestString(string serialNumber, string name, int flowId, string flowName, List<ServiceMO> services)
        {
            FlowDeviceRequestRunMessage req = BuildRequest(serialNumber, name, flowId, flowName, services);
            return JsonConvert.SerializeObject(req);
        }
        public FlowDeviceRequestRunMessage BuildRequest(string serialNumber, int flowId, string flowName, List<ServiceMO> services)
        {
            return BuildRequest(serialNumber, serialNumber, flowId, flowName, services);
        } 
        public FlowDeviceRequestRunMessage BuildRequest(string serialNumber, string name, int flowId, string flowName, List<ServiceMO> services)
        {
            FlowDeviceRunRequestParam data = new FlowDeviceRunRequestParam()
            {
                Name = name,
                Services = services,
                TemplateParam = new DeviceTemplateParam(flowId, flowName),
            };
            FlowDeviceRequestRunMessage req = new FlowDeviceRequestRunMessage(this.Service.ServiceCode, this.DeviceCode, serialNumber, this.Service.ServiceToken, data);
            return req;
        }

        public async Task<DeviceResponseMessageHeader?> WaitForResponseAsync(string msgId, TimeSpan? timeout = null)
        {
            return await RequestManager.WaitForResponseAsync(msgId, timeout);
        }

        public bool SetException(string msgId, OperationCanceledException exception)
        {
            return RequestManager.SetException(msgId, exception);
        }
    }
}
