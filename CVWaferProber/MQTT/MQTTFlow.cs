using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.MQTT
{
    public class MQTTFlowDeviceNode : BaseServiceNode
    {
        public string DeviceCode { get; set; }
        public MqttRequestManager RequestManager { get; private set; }
        public MQTTFlowDeviceNode(string RCNodeName, int serviceId, string serviceType, string serviceCode, string serviceName, string serviceToken, string deviceCode, MqttRequestManager requestManager) : base(RCNodeName, serviceId, serviceType, serviceCode, serviceName)
        {
            this.ServiceToken = serviceToken;
            this.DeviceCode = deviceCode;
            this.RequestManager = requestManager;
        }
        public string BuildRequestString(string serialNumber, int flowId, string flowName, List<MQTTServiceMO> services)
        {
            return BuildRequestString(serialNumber, serialNumber, flowId, flowName, services);
        } 
        public string BuildRequestString(string serialNumber, string name, int flowId, string flowName, List<MQTTServiceMO> services)
        {
            MQTTCVRequestHeader req = BuildRequest(serialNumber, name, flowId, flowName, services);
            return JsonConvert.SerializeObject(req);
        }
        public MQTTCVRequestHeader BuildRequest(string serialNumber, int flowId, string flowName, List<MQTTServiceMO> services)
        {
            return BuildRequest(serialNumber, serialNumber, flowId, flowName, services);
        } 
        public MQTTCVRequestHeader BuildRequest(string serialNumber, string name, int flowId, string flowName, List<MQTTServiceMO> services)
        {
            DeviceFlowRunParam<MQTTServiceMO> data = new DeviceFlowRunParam<MQTTServiceMO>()
            {
                Name = name,
                Services = services,
                TemplateParam = new CVTemplateParam() { ID = flowId, Name = flowName }
            };
            MQTTFlowRun<MQTTServiceMO> req = new MQTTFlowRun<MQTTServiceMO>(this.ServiceCode, this.DeviceCode, serialNumber, this.ServiceToken, data);
            return req;
        }

        public async Task<MQTTBaseResponse?> WaitForResponseAsync(string msgId, TimeSpan? timeout = null)
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
