using ColorVision.Core.Message;
using ColorVision.Core.Message.Model;
using ColorVision.Message.Services;
using Newtonsoft.Json;

namespace CVMQTTNodeClient
{
    public class ServiceClientNodeConfig : ServiceNodeConfig
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ServiceClientNodeConfig));
        public string RCName
        {
            get => _RCName;
            set
            {
                this._RCName = value;
                this.RCRegTopic = MQTTCVServiceTopicBuilder.BuildRegTopic(RCName);
                this.RCHBTopic = MQTTCVServiceTopicBuilder.BuildHeartbeatTopic(RCName);
                this.RCTopic = MQTTCVServiceTopicBuilder.BuildPublicTopic(RCName);
                this.NodeName = "client." + Guid.NewGuid().ToString();
                this.NodeTopic = MQTTCVServiceTopicBuilder.BuildNodeTopic(NodeName, RCName);
            }
        }
        public string RCRegTopic { get; private set; }
        public string RCHBTopic { get; private set; }
        public string RCTopic { get; private set; }
        public string HeartbeatData { get; private set; }
        //public CVServiceType ServiceType { get; set; }
        /// <summary>
        /// 节点访问Token
        /// </summary>
        public bool IsNotStartup => this.Token != null && !_isStartup;

        public int HeartbeatTime { get; private set; } = 5000;
        private System.DateTime lastHeartbeatTime;
        private System.TimeSpan overTS;
        private string _RCName;
        private bool _isStartup = false;

        public ServiceClientNodeConfig(string rcName, string nodeAppId = "app1", string nodeKey = "123456")
        {
            this.NodeKey = nodeKey;
            this.NodeAppId = nodeAppId;
            this.ServiceType =  ServiceNodeType.Client;
            this._isStartup = false;
            this.Token = null;
            this.RCRegTopic = string.Empty;
            this.RCHBTopic = string.Empty;
            this.RCTopic = string.Empty;
            this._RCName = string.Empty;
            this.NodeName = string.Empty;
            this.NodeTopic = string.Empty;
            this.HeartbeatData = string.Empty;
            this.RCName = rcName;
        }
        public bool RefreshToken(ServiceNodeToken? token)
        {
            RecvHeartbeat();
            if (Token == null)
            {
                this.Token = token;
                this.HeartbeatData = BuildHeartbeat();
                return true;
            }
            else if (token?.Timestamp - this.Token.Timestamp > 500)
            {
                this.Token = token;
                this.HeartbeatData = BuildHeartbeat();
                return true;
            }
            this.HeartbeatData = string.Empty;
            return false;
        }
        public void Startup(int heartbeatTime = 5000)
        {
            this.HeartbeatTime = heartbeatTime;
            this._isStartup = true;
            this.overTS = System.TimeSpan.FromMilliseconds(this.HeartbeatTime * 1.5);
        }
        public void Reset()
        {
            _isStartup = false;
            Token = null;
            HeartbeatData = string.Empty;
        }

        private string BuildHeartbeat()
        {
            if (Token == null) return string.Empty;
            //ServiceNodeHeartbeatRequest Heartbeat = new ServiceNodeHeartbeatRequest(this.Token.AccessToken, this.NodeName, this.ServiceType.ToString());
            ServiceNodeQueryStatusRequest Heartbeat = MessageBuilder.BuildRequestQueryStatus(this);
            return JsonConvert.SerializeObject(Heartbeat);
        }

        public void RecvHeartbeat()
        {
            lastHeartbeatTime = System.DateTime.Now;
        }
        public bool IsLive()
        {
            System.TimeSpan ts = System.DateTime.Now - lastHeartbeatTime;
            //if (logger.IsDebugEnabled) logger.DebugFormat("IsLive => {0}", ts.ToString());
            if (ts > overTS) return false;
            return true;
        }
    }
}
