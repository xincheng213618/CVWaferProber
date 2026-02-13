using CVMQTTNodeClient;
using Newtonsoft.Json;

namespace CVWaferProber.MQTT
{
    public class MQTTFlowDeviceNode : CVBaseDeviceNode
    {
        public static string FlowDeviceCode  = "DEV.Flow.Default";
        public MQTTFlowDeviceNode(string serviceType, string serviceCode, string serviceName, string serviceToken, string upChannel, string downChannel, string deviceCode, MqttRequestManager requestManager)
            : base(deviceCode, deviceCode, serviceType, serviceCode, serviceName, serviceToken, upChannel, downChannel)
        {
            this.DeviceCode = deviceCode;
            this.RequestManager = requestManager;
        }

        public MQTTFlowDeviceNode(CVBaseDeviceNode dev) :
            this(dev.Service.ServiceType, dev.Service.ServiceCode, dev.Service.ServiceName, dev.Service.ServiceToken, dev.Service.UpChannel, dev.Service.DownChannel, dev.DeviceCode, dev.RequestManager)
        {
        }

        public string BuildRequestString(string serialNumber, int flowId, string flowName, List<FlowServiceMO> services)
        {
            return BuildRequestString(serialNumber, serialNumber, flowId, flowName, services);
        } 
        public string BuildRequestString(string serialNumber, string name, int flowId, string flowName, List<FlowServiceMO> services)
        {
            MQTTCVRequestBaseHeader req = BuildRequest(serialNumber, name, flowId, flowName, services);
            return JsonConvert.SerializeObject(req);
        }
        public MQTTCVRequestBaseHeader BuildRequest(string serialNumber, int flowId, string flowName, List<FlowServiceMO> services)
        {
            return BuildRequest(serialNumber, serialNumber, flowId, flowName, services);
        } 
        public MQTTCVRequestBaseHeader BuildRequest(string serialNumber, string name, int flowId, string flowName, List<FlowServiceMO> services)
        {
            DeviceFlowRunParam<FlowServiceMO> data = new DeviceFlowRunParam<FlowServiceMO>()
            {
                Name = name,
                Services = services,
                TemplateParam = new CVTemplateParam() { ID = flowId, Name = flowName }
            };
            MQTTFlowRun<FlowServiceMO> req = new MQTTFlowRun<FlowServiceMO>(this.Service.ServiceCode, this.DeviceCode, serialNumber, this.Service.ServiceToken, data);
            return req;
        }

        public async Task<CVMQTTBaseResponse?> WaitForResponseAsync(string msgId, TimeSpan? timeout = null)
        {
            return await RequestManager.WaitForResponseAsync(msgId, timeout);
        }

        public bool SetException(string msgId, OperationCanceledException exception)
        {
            return RequestManager.SetException(msgId, exception);
        }
    }

    public class MQTTFlowRun<T> : MQTTCVBaseRequest<DeviceFlowRunParam<T>>
    {
        public MQTTFlowRun(string serviceName, string deviceName, string serialNumber, string token, DeviceFlowRunParam<T> data) : base(serviceName, deviceName, MQTTFlowEventEnum.Event_Flow_Run, serialNumber, token, data)
        {
        }
    }
    public class MQTTFlowEventEnum
    {
        public const string Event_Flow_CombinedRun = "Flow_CombinedRun";
        public const string Event_Flow_Run = "Flow_Run";
        public const string Event_Flow_RunEx = "Flow_RunEx";
        public const string Event_Flow_Stop = "Flow_Stop";
        public const string Event_Flow_StopCombined = "Flow_CombinedStop";
        public const string Event_Flow_Load = "Flow_Load";
        public const string Event_Flow_GetCombinedResult = "Flow_GetCombinedResult";
    }
}
