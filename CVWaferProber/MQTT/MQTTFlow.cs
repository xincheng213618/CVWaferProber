using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.MQTT
{
    public class GatewayEventRequest
    {
        public string ServiceCode { get; set; }
        public string DeviceCode { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }
        public dynamic Data { get; set; }
    }
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

        public string BuildRequest(GatewayEventRequest request, List<MQTTServiceMO> services)
        {
            string result = string.Empty;
            switch (request.EventName)
            {
                case MQTTFlowEventEnum.Event_Flow_Run:
                    result = BuildRequest_Run(request, services);
                    break;
                //case MQTTFlowEventEnum.Event_Flow_RunEx:
                //    result = BuildRequest_RunEx(request, services);
                //    break;
                //case MQTTFlowEventEnum.Event_Flow_CombinedRun:
                //    result = BuildRequest_CombinedRun(request, services);
                //    break;
                //case MQTTFlowEventEnum.Event_Flow_Stop:
                //    result = BuildRequest_Stop(request);
                //    break;
                //case MQTTFlowEventEnum.Event_Flow_StopCombined:
                //    result = BuildRequest_StopCombined(request);
                //    break;
                //case MQTTFlowEventEnum.Event_Flow_GetCombinedResult:
                //    result = BuildRequest_GetCombinedResult(request);
                //    break;
                default:
                    break;
            }
            return result;
        }

        //private string BuildRequest_GetCombinedResult(GatewayEventRequest request)
        //{
        //    MQTTFlowGetCombinedResult req = new MQTTFlowGetCombinedResult(this.ServiceCode, request.DeviceCode, request.SerialNumber, this.ServiceToken, new MQTTMessageLib.CVTemplateParam() { ID = request.Data.Id, Name = request.Data.Name });
        //    return JsonConvert.SerializeObject(req);
        //}

        //private string BuildRequest_CombinedRun(GatewayEventRequest request, List<MQTTServiceMO> services)
        //{
        //    DeviceFlowCombinedRunParam<MQTTServiceMO> data = new DeviceFlowCombinedRunParam<MQTTServiceMO>()
        //    {
        //        Name = request.Data.Name,
        //        Services = services,
        //        TemplateParam = new MQTTMessageLib.CVTemplateParam() { ID = request.Data.Template.Id, Name = request.Data.Template.Name }
        //    };
        //    MQTTFlowCombinedRun<MQTTServiceMO> req = new MQTTFlowCombinedRun<MQTTServiceMO>(this.ServiceCode, request.DeviceCode, request.SerialNumber, this.ServiceToken, data);
        //    return JsonConvert.SerializeObject(req);
        //}
        //private string BuildRequest_RunEx(GatewayEventRequest request, List<MQTTServiceMO> services)
        //{
        //    DeviceFlowRunParam<MQTTServiceMO> data = new DeviceFlowRunParam<MQTTServiceMO>()
        //    {
        //        Name = request.Data.Name,
        //        Services = services,
        //        TemplateParam = new MQTTMessageLib.CVTemplateParam() { ID = request.Data.Template.Id, Name = request.Data.Template.Name }
        //    };
        //    MQTTFlowRun<MQTTServiceMO> req = new MQTTFlowRun<MQTTServiceMO>(this.ServiceCode, request.DeviceCode, request.SerialNumber, this.ServiceToken, data);
        //    return JsonConvert.SerializeObject(req);
        //}
        //private string BuildRequest_StopCombined(GatewayEventRequest request)
        //{
        //    MQTTFlowStopCombined req = new MQTTFlowStopCombined(this.ServiceCode, request.DeviceCode, request.SerialNumber, this.ServiceToken, new MQTTMessageLib.CVTemplateParam() { ID = request.Data.Id, Name = request.Data.Name });
        //    return JsonConvert.SerializeObject(req);
        //}
        //private string BuildRequest_Stop(GatewayEventRequest request)
        //{
        //    MQTTFlowStop req = new MQTTFlowStop(this.ServiceCode, request.DeviceCode, request.SerialNumber, this.ServiceToken);
        //    return JsonConvert.SerializeObject(req);
        //}

        public string BuildRequest_Run(GatewayEventRequest request, List<MQTTServiceMO> services)
        {
            DeviceFlowRunParam<MQTTServiceMO> data = new DeviceFlowRunParam<MQTTServiceMO>()
            {
                Name = request.SerialNumber,
                Services = services,
                TemplateParam = new CVTemplateParam() { ID = request.Data.Id, Name = request.Data.Name }
            };
            MQTTFlowRun<MQTTServiceMO> req = new MQTTFlowRun<MQTTServiceMO>(this.ServiceCode, request.DeviceCode, request.SerialNumber, this.ServiceToken, data);
            return JsonConvert.SerializeObject(req);
        }
        public string BuildRequest_Run(string serialNumber, int flowId, string flowName, List<MQTTServiceMO> services)
        {
            return BuildRequest_Run(serialNumber, serialNumber, flowId, flowName, services);
        } 
        public string BuildRequest_Run(string serialNumber, string name, int flowId, string flowName, List<MQTTServiceMO> services)
        {
            DeviceFlowRunParam<MQTTServiceMO> data = new DeviceFlowRunParam<MQTTServiceMO>()
            {
                Name = name,
                Services = services,
                TemplateParam = new CVTemplateParam() { ID = flowId, Name = flowName }
            };
            MQTTFlowRun<MQTTServiceMO> req = new MQTTFlowRun<MQTTServiceMO>(this.ServiceCode, this.DeviceCode, serialNumber, this.ServiceToken, data);
            return JsonConvert.SerializeObject(req);
        }

        public async Task<MQTTBaseResponse?> WaitForResponseAsync(string serialNumber, TimeSpan? timeout = null)
        {
            return await RequestManager.WaitForResponseAsync(serialNumber, timeout);
        }

        public bool SetException(string serialNumber, OperationCanceledException exception)
        {
            return RequestManager.SetException(serialNumber, exception);
        }
    }
    public class MQTTCVBaseRequest<T> : MQTTCVRequestTokenHeader
    {
        [Newtonsoft.Json.JsonProperty("params")]
        public T Data { get; set; }
        public MQTTCVBaseRequest() : this(null) { }
        public MQTTCVBaseRequest(string eventName) : this(null, null, eventName)
        {
        }
        public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName) : this(serviceName, deviceName, eventName, default)
        {
        }
        public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName, T data) : this(serviceName, deviceName, eventName, string.Empty, string.Empty, data)
        {
        }
        public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName, string serialNumber, T data) : this(serviceName, deviceName, eventName, serialNumber, string.Empty, data)
        {
        }
        public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName, string serialNumber, string token, T data) : this("1.0", serviceName, deviceName, eventName, serialNumber, Guid.NewGuid().ToString("N"), token, data)
        {
        }
        public MQTTCVBaseRequest(string version, string serviceName, string deviceName, string eventName, string serialNumber, string msgID, string token, T data, int zIndex = -1) : base(version, serviceName, deviceName, eventName, serialNumber, msgID, token, zIndex)
        {
            Data = data;
        }
    }
    public class MQTTCVRequestHeader
    {
        public string Version { get; set; }
        public string ServiceName { get; set; }
        public string DeviceCode { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }
        public string MsgID { get; set; }
        public int ZIndex { get; set; }

        public MQTTCVRequestHeader(string version, string serviceName, string deviceCode, string eventName, string serialNumber, string msgID, int zIndex)
        {
            Version = version;
            ServiceName = serviceName;
            DeviceCode = deviceCode;
            EventName = eventName;
            SerialNumber = serialNumber;
            MsgID = msgID;
            ZIndex = zIndex;
        }
        public MQTTCVRequestHeader()
        {

        }
    }
    public class MQTTCVRequestTokenHeader : MQTTCVRequestHeader
    {
        public string Token { get; set; }

        public MQTTCVRequestTokenHeader(string serviceName, string deviceName, string eventName) : this(serviceName, deviceName, eventName, string.Empty)
        {
        }
        public MQTTCVRequestTokenHeader(string serviceName, string deviceName, string eventName, string serialNumber) : this(serviceName, deviceName, eventName, serialNumber, string.Empty)
        {
        }
        public MQTTCVRequestTokenHeader(string serviceName, string deviceName, string eventName, string serialNumber, string token) : this("1.0", serviceName, deviceName, eventName, serialNumber, Guid.NewGuid().ToString("N"), token)
        {
        }
        public MQTTCVRequestTokenHeader(string version, string serviceName, string deviceName, string eventName, string serialNumber, string msgID, string token, int zIndex = -1) : base(version, serviceName, deviceName, eventName, serialNumber, msgID, zIndex)
        {
            Token = token;
        }

        public MQTTCVRequestTokenHeader() : this(string.Empty, string.Empty, string.Empty)
        {
        }

        public bool IsTokenValid(string accessToken)
        {
            string version = this.Version;
            //if (version == null || version.Equals("1.0")) return true;
            return !string.IsNullOrWhiteSpace(this.Token) && this.Token.Equals(accessToken);
        }
    }
    public class MQTTFlowRun<T> : MQTTCVBaseRequest<DeviceFlowRunParam<T>>
    {
        public MQTTFlowRun(string serviceName, string deviceName, string serialNumber, string token, DeviceFlowRunParam<T> data) : base(serviceName, deviceName, MQTTFlowEventEnum.Event_Flow_Run, serialNumber, token, data)
        {
        }
    }

    public class MQTTDeviceMO
    {
        public string ID { get; set; }
        public string DeviceCode { get; set; }
    }
    public class MQTTServiceMO
    {
        public string ServiceType { get; set; }
        public string ServiceCode { get; set; }
        public string SubscribeTopic { get; set; }
        public string PublishTopic { get; set; }
        public string Token { get; set; }

        public Dictionary<string, MQTTDeviceMO> Devices { get; }

        public MQTTServiceMO()
        {
            this.Devices = new Dictionary<string, MQTTDeviceMO>();
        }

        public MQTTServiceMO(string serviceType, string serviceCode, string subscribeTopic, string publishTopic, string token) : this()
        {
            ServiceType = serviceType;
            ServiceCode = serviceCode;
            SubscribeTopic = subscribeTopic;
            PublishTopic = publishTopic;
            Token = token;
        }
    }
    public class MQTTNodeService
    {
        public string ServiceToken { get; set; }
        public string ServiceCode { get; set; }
        public string ServiceName { get; set; }
        public string ServiceType { get; set; }
        public string UpChannel { get; set; }
        public string DownChannel { get; set; }
        public Dictionary<string, MQTTDevice> Devices { get; set; }

        public MqttRequestManager RequestManager { get; private set; }

        public MQTTNodeService()
        {
            RequestManager = new MqttRequestManager();
        }
        public class MQTTDevice
        {
            public string Code { get; set; }
            public string Name { get; set; }
        }

        public void SetResponse(MQTTBaseResponse? resp)
        {
            RequestManager.SetResponse(resp);
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

    public class MQTTNodeServiceEventEnum
    {
        public const string Event_SetToken = "SetToken";
        public const string Event_Regist = "Regist";
        public const string Event_NotRegist = "NotRegist";
        public const string Event_Startup = "Startup";
        public const string Event_AddService = "AddService";
        public const string Event_StopService = "StopService";
        public const string Event_StopAllServices = "StopAllServices";
        public const string Event_LoadAllServices = "LoadAllServices";
        public const string Event_ReloadService = "ReloadService";
        public const string Event_QueryServices = "QueryServices";
        public const string Event_QueryServiceStatus = "QueryServiceStatus";
        public const string Event_ServiceHeartbeat = "ServiceHeartbeat";
    }
    public class MQTTRCServiceTypeConst
    {
        public const string RCServiceType = "MQTTRCService";
        public const string RCRegTopic = RCServiceType + "/Regist";
        public const string RCHeartbeatTopic = RCServiceType + "/Heartbeat";
        public const string RCPublicTopic = RCServiceType + "/Public";
        public const string RCAdminTopic = RCServiceType + "/Admin";
        public const string RCNodeTopic = RCServiceType + "/Node";

        public static string BuildNodeName(string serviceType, string nodeName)
        {
            if (string.IsNullOrWhiteSpace(nodeName)) { nodeName = Guid.NewGuid().ToString(); }
            return serviceType + "." + nodeName;
        }

        public static string BuildNodeTopic(string nodeName, string rcName)
        {
            return string.Format("{0}/{1}/{2}", RCNodeTopic, nodeName, rcName);// RCNodeTopic + "/" + nodeName + "/" + Guid.NewGuid().ToString("N");
        }

        public static string BuildNodeTopic(string nodeName)
        {
            return string.Format("{0}/{1}", RCNodeTopic, nodeName);// RCNodeTopic + "/" + nodeName + "/" + Guid.NewGuid().ToString("N");
        }

        public static string BuildRegTopic(string nodeName)
        {
            return RCRegTopic + "/" + nodeName;
        }
        public static string BuildHeartbeatTopic(string nodeName)
        {
            return RCHeartbeatTopic + "/" + nodeName;
        }
        public static string BuildPublicTopic(string nodeName)
        {
            return RCPublicTopic + "/" + nodeName;
        }
        public static string BuildAdminTopic(string nodeName)
        {
            return RCAdminTopic + "/" + nodeName;
        }
        public static string BuildFlowTopic(string nodeName)
        {
            return "MQTTRCService/Flow/" + nodeName;
        }

        public static string BuildArchivedTopic(string nodeName)
        {
            return "MQTTRCService/Archived/" + nodeName;
        }
        public static string BuildServiceUpTopic(string serviceType, string serviceName, string rcName)
        {
            return string.Format("{2}/{0}/{1}/CMD", serviceType, serviceName, rcName); //serviceType + "/CMD/" + serviceName + "/" + serviceId;
            //return serviceType + "/Up/" + serviceName + "/" + serviceId;
        }

        public static string BuildServiceDownTopic(string serviceType, string serviceName, string rcName)
        {
            return string.Format("{2}/{0}/{1}/STATUS", serviceType, serviceName, rcName);// serviceType + "/STATUS/" + serviceName + "/" + serviceId;
            //return serviceType + "/Down/" + serviceName + "/" + serviceId;
        }

        public static string BuildSysConfigTopic(string nodeName)
        {
            return "SysRes/config/" + nodeName;
        }

        public static string BuildSysConfigRespTopic(string nodeName)
        {
            return "SysRes/config/Resp/" + nodeName;
        }
    }
}
