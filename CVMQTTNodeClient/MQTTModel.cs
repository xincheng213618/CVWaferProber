using CVCommCore;

namespace CVMQTTNodeClient
{
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
    public class MQTTNodeServiceHeader
    {
        public MQTTNodeServiceHeader()
        {
            this.Version = "1.0";
            this.MsgId = Guid.NewGuid().ToString();
            this.NodeName = string.Empty;
            this.ServiceType = string.Empty;
            this.EventName = string.Empty;
        }
        public MQTTNodeServiceHeader(string nodeName, string serviceType, string eventName) : this("1.0", nodeName, serviceType, eventName)
        {
        }

        public MQTTNodeServiceHeader(string version, string nodeName, string serviceType, string eventName)
        {
            this.NodeName = nodeName;
            this.ServiceType = serviceType;
            this.EventName = eventName;
            this.Version = version;
            this.MsgId = Guid.NewGuid().ToString();
        }

        public string Version { get; set; } = string.Empty;
        public string MsgId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
    }
    public class MQTTNodeServiceTokenHeader : MQTTNodeServiceHeader
    {
        public MQTTNodeServiceTokenHeader(string nodeName, string serviceType, string eventName, string token) : base(nodeName, serviceType, eventName)
        {
            this.Token = token;
        }
        public MQTTNodeServiceTokenHeader() : base()
        {
            this.Token = string.Empty;
        }

        public string Token { get; set; } = string.Empty;

        public bool TokenCheck(string accessToken)
        {
            string version = this.Version;
            if (version == null || version.Equals("1.0")) return true;
            else return !string.IsNullOrWhiteSpace(this.Token) && this.Token.Equals(accessToken);
        }
    }

    public class MQTTRCServicesQueryRequest : MQTTNodeServiceTokenHeader
    {
        public MQTTRCServicesQueryRequest(string token)
        {
            this.Token = token;
            this.EventName = MQTTNodeServiceEventEnum.Event_QueryServices;
            this.ServiceType = CVServiceType.Client.ToString().ToLower();
        }
    }
    public class MQTTNodeServiceRegistResponse : MQTTNodeServiceHeader
    {
        public MQTTNodeServiceRegistResponse() { }
        public MQTTNodeServiceRegistResponse(string version, string nodeName, string reqMsgId, int code, string message, NodeToken token) : base(version, nodeName, MQTTNodeServiceEventEnum.Event_Regist)
        {
            this.Version = version;
            this.MsgId = reqMsgId;
            this.Code = code;
            this.Message = message;
            this.Token = token;
            this.EventName = MQTTNodeServiceEventEnum.Event_Regist;
        }

        public MQTTNodeServiceRegistResponse(string nodeName, string reqMsgId, int code, string message, NodeToken token) : this("1.0", nodeName, reqMsgId, code, message, token)
        {
        }

        public MQTTNodeServiceRegistResponse(MQTTNodeServiceRegist request, int code, string message, NodeToken token) : this(request.NodeName, request.MsgId, code, message, token)
        {
            this.EventName = request.EventName;
            this.NodeName = request.NodeName;
            this.ServiceType = request.ServiceType;
        }

        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public NodeToken? Token { get; set; } = null;

    }
    public class MQTTNodeServiceRegist : MQTTNodeServiceHeader
    {
        public MQTTNodeServiceRegist(string version, string nodeName, string nodeAppId, string nodeKey, string nodeTopic, string serviceType) : base(version, nodeName, serviceType, MQTTNodeServiceEventEnum.Event_Regist)
        {
            this.NodeAppId = nodeAppId;
            this.NodeKey = nodeKey;
            this.NodeTopic = nodeTopic;
            this.SendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public MQTTNodeServiceRegist() : base() { }

        public MQTTNodeServiceRegist(string nodeName, string nodeAppId, string nodeKey, string nodeTopic, string serviceType) : this("1.0", nodeName, nodeAppId, nodeKey, nodeTopic, serviceType)
        {
        }

        public MQTTNodeServiceRegist(MQTTServiceClientNode node) : this(node.NodeName, node.NodeAppId, node.NodeKey, node.NodeTopic, node.ServiceType.ToString().ToLower())
        {

        }

        public string NodeAppId { get; set; } = string.Empty;
        public string NodeKey { get; set; } = string.Empty;
        public string NodeTopic { get; set; } = string.Empty;
        public string SendTime { get; set; } = string.Empty;
    }

    public class MQTTServiceHeartbeat : MQTTNodeServiceTokenHeader
    {
        public MQTTServiceHeartbeat(string NodeName, string serviceType, string token, int overTime = 10000)
        {
            this.Token = token;
            this.NodeName = NodeName;
            this.ServiceType = serviceType;
            this.EventName = MQTTNodeServiceEventEnum.Event_ServiceHeartbeat;
            this.MsgId = Guid.NewGuid().ToString();
        }
    }

    public class MQTTNodeServiceTO
    {
        public string ServiceToken { get; set; } = string.Empty;
        public string ServiceCode { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public string UpChannel { get; set; } = string.Empty;
        public string DownChannel { get; set; } = string.Empty;
        public Dictionary<string, MQTTDeviceTO> Devices { get; set; }

        public MqttRequestManager RequestManager { get; private set; }

        public MQTTNodeServiceTO()
        {
            this.RequestManager = new MqttRequestManager();
            this.Devices = new Dictionary<string, MQTTDeviceTO>();
        }
        public class MQTTDeviceTO
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public void SetResponse(MQTTBaseResponse? resp)
        {
            RequestManager.SetResponse(resp);
        }
    }

    public class MQTTDeviceMO
    {
        public string DeviceCode { get; set; } = string.Empty;
    }
    public class MQTTServiceMO
    {
        public string ServiceType { get; set; } = string.Empty;
        public string ServiceCode { get; set; } = string.Empty;
        public string SubscribeTopic { get; set; } = string.Empty;
        public string PublishTopic { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;

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
}
