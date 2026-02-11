using CVCommCore;

namespace CVWaferProber.MQTT
{
    public class MQTTNodeServiceHeader
    {
        public MQTTNodeServiceHeader()
        {
            this.Version = "1.0";
            this.MsgId = Guid.NewGuid().ToString();
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

        public string Version { get; set; }
        public string MsgId { get; set; }
        public string NodeName { get; set; }
        public string ServiceType { get; set; }
        public string EventName { get; set; }
    }
    public class MQTTNodeServiceTokenHeader : MQTTNodeServiceHeader
    {
        public MQTTNodeServiceTokenHeader(string nodeName, string serviceType, string eventName, string token) : base(nodeName, serviceType, eventName)
        {
            this.Token = token;
        }
        public MQTTNodeServiceTokenHeader() : base() { }

        public string Token { get; set; }

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
        public string Message { get; set; }
        public NodeToken Token { get; set; }

    }
    public class MQTTNodeServiceRegist : MQTTNodeServiceHeader
    {
        public MQTTNodeServiceRegist(string version, string nodeName, string nodeAppId, string nodeKey, string nodeTopic, string serviceType) : base(version, nodeName, serviceType, MQTTNodeServiceEventEnum.Event_Regist)
        {
            this.NodeAppId = nodeAppId;
            this.NodeKey = nodeKey;
            this.NodeTopic = nodeTopic;

            //this.EventName = MQTTNodeServiceEventEnum.Event_Regist;
            //this.MsgId = Guid.NewGuid().ToString();
            this.SendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public MQTTNodeServiceRegist() : base() { }

        public MQTTNodeServiceRegist(string nodeName, string nodeAppId, string nodeKey, string nodeTopic, string serviceType) : this("1.0", nodeName, nodeAppId, nodeKey, nodeTopic, serviceType)
        {
        }

        public MQTTNodeServiceRegist(MQTTServiceNode node) : this(node.NodeName, node.NodeAppId, node.NodeKey, node.NodeTopic, node.ServiceType.ToString().ToLower())
        {

        }

        public string NodeAppId { get; set; }
        public string NodeKey { get; set; }
        public string NodeTopic { get; set; }
        public string SendTime { get; set; }
    }
    public class NodeToken
    {
        public NodeToken(int expires)
        {
            this.AccessToken = Guid.NewGuid().ToString();
            this.RefreshToken = Guid.NewGuid().ToString();
            this.Timestamp = DateTime.Now.Ticks;
            this.Expires = expires;

        }

        public bool IsExpired()
        {
            //TODO 暂时Token永不过期
            return false;
            DateTime dt = new DateTime(Timestamp).AddSeconds(Expires);
            return DateTime.Now.Ticks > dt.Ticks;
        }

        public void Refresh()
        {
            this.AccessToken = Guid.NewGuid().ToString();
            this.Timestamp = DateTime.Now.Ticks;
        }

        public void Refresh(int expires)
        {
            this.Expires = expires;
            Refresh();
        }

        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public long Timestamp { get; set; }
        public int Expires { get; set; }

    }

    public class MQTTServiceNode
    {
        public string NodeName { get; set; }
        public string NodeKey { get; set; }
        public string NodeAppId { get; set; }
        public string NodeTopic { get; set; }
        public CVServiceType ServiceType { get; set; }
        /// <summary>
        /// 节点访问Token
        /// </summary>
        public NodeToken? Token { get; set; }
        public bool IsNotStartup => this.Token!=null && !_isStartup;

        public bool RefreshToken(NodeToken token)
        {
            if (Token == null)
            {
                this.Token = token;
                return true;
            }
            else if (token.Timestamp - this.Token.Timestamp > 500)
            {
                this.Token = token;
                return true;
            }

            return false;
        }

        private bool _isStartup = false;
        public void Startup()
        {
            _isStartup = true;
        }
    }

    public class MQTTBaseResponse
    {
        public string DeviceCode { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }
        public int ZIndex { get; set; }
        public bool IsOK => Code == 0;
    }
    public class MQTTResponse<T> : MQTTBaseResponse
    {

        public T Data { get; set; }
    }
}
