using ColorVision.Core.Message.Model;
using ColorVision.Message.Services;
using CVCommCore;
using Newtonsoft.Json;

namespace CVMQTTNodeClient
{
    public class MQTTServiceClientNode
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MQTTServiceClientNode));
        public string RCName
        {
            get => _RCName;
            set
            {
                this._RCName = value;
                this.RCRegTopic = MQTTCVServiceTopicBuilder.BuildRegTopic(RCName);
                this.RCHBTopic = MQTTCVServiceTopicBuilder.BuildHeartbeatTopic(RCName);
                this.NodeName = "client." + Guid.NewGuid().ToString();
                this.NodeTopic = MQTTCVServiceTopicBuilder.BuildNodeTopic(NodeName, RCName);
            }
        }
        public string RCRegTopic { get; private set; }
        public string RCHBTopic { get; private set; }
        public string NodeName { get; private set; }
        public string NodeKey { get; set; }
        public string NodeAppId { get; set; }
        public string NodeTopic { get; private set; }
        public string HeartbeatData { get; private set; }
        public CVServiceType ServiceType { get; set; }
        /// <summary>
        /// 节点访问Token
        /// </summary>
        public ServiceNodeToken? Token { get; set; }
        public bool IsNotStartup => this.Token != null && !_isStartup;

        public int HeartbeatTime { get; private set; } = 5000;
        private System.DateTime lastHeartbeatTime;
        private System.TimeSpan overTS;
        private string _RCName;
        private bool _isStartup = false;

        public MQTTServiceClientNode(string rcName, string nodeAppId = "app1", string nodeKey = "123456")
        {
            this.NodeKey = nodeKey;
            this.NodeAppId = nodeAppId;
            this.ServiceType = CVServiceType.Client;
            this._isStartup = false;
            this.Token = null;
            this.RCRegTopic = string.Empty;
            this.RCHBTopic = string.Empty;
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
        public void Startup()
        {
            _isStartup = true;
            this.overTS = System.TimeSpan.FromMilliseconds(this.HeartbeatTime * 2);
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
            ServiceNodeHeartbeatRequest Heartbeat = new ServiceNodeHeartbeatRequest(this.Token.AccessToken, this.NodeName, this.ServiceType.ToString());
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
            //DateTime dt = new DateTime(Timestamp).AddSeconds(Expires);
            //return DateTime.Now.Ticks > dt.Ticks;
        }

        public void Refresh()
        {
            this.AccessToken = Guid.NewGuid().ToString();
            this.Timestamp = DateTime.Now.Ticks;
        }

        public void Refresh(int expires)
        {
            this.Expires = expires;
            this.Refresh();
        }

        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public long Timestamp { get; set; }
        public int Expires { get; set; }

    }
}
