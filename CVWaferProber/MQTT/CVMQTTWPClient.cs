using CVCommCore;
using CVMQTTLib;
using Newtonsoft.Json;

namespace CVWaferProber.MQTT
{
    public class CVMQTTWPClient : ReflectionSingleton<CVMQTTWPClient>
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVMQTTWPClient));

        private CVMQTTControl? CVMQTT_Flow;
        private string RCName;
        private string RCRegTopic;
        private MQTTServiceNode? nodeThis;
        private Dictionary<string, MQTTNodeService> nodeServers = new Dictionary<string, MQTTNodeService>();
        private Dictionary<string, MQTTNodeService> svrTopics = new Dictionary<string, MQTTNodeService>();

        public event MQTTConnectedEventHandler MQTTConnectedEvent;

        public event MQTTConnectedEventHandler MQTTDisconnectedEvent;

        public event EventHandler MQTTRegistedEvent;

        private CVMQTTWPClient() : base()
        {

        }
        public CVMQTTWPClient Init(string rcName, string nodeAppId = "app1", string nodeKey = "123456")
        {
            this.RCName = rcName;
            this.RCRegTopic = MQTTRCServiceTypeConst.BuildRegTopic(RCName);
            string nodeName = "client." + Guid.NewGuid().ToString();
            this.nodeThis = new MQTTServiceNode() { NodeAppId = nodeAppId, NodeName = nodeName, NodeKey = nodeKey, ServiceType = CVServiceType.Client, NodeTopic = MQTTRCServiceTypeConst.BuildNodeTopic(nodeName, RCName) };

            InitFlow();

            return this;
        }
        private void StartFlow()
        {
            string? currentPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            MQTTConfig config = new MQTTConfig(System.IO.Path.Combine(currentPath, "cfg", "MQTT.config"));
            CVMQTTConfig mqtt_cfg = new CVMQTTConfig() { Host = config.Host, Port = config.Port, IsServer = false, IsDebugOut = false };
            CVMQTT_Flow?.Start(mqtt_cfg);

            Task.Factory.StartNew(()=> DoQueryServiceStatus());
        }

        private void DoQueryServiceStatus()
        {
        }

        private void InitFlow()
        {
            CVMQTT_Flow = new CVMQTTControl();
            CVMQTT_Flow.MQTTMsgEvent += CVMQTT_Flow_MQTTMsgEvent;
            CVMQTT_Flow.MQTTConnectedEvent += CVMQTT_Flow_MQTTConnectedEvent;
            CVMQTT_Flow.MQTTDisconnectedEvent += CVMQTT_Flow_MQTTDisconnectedEvent;
            //
            StartFlow();
        }

        private void CVMQTT_Flow_MQTTDisconnectedEvent(object sender, MQTTConnectedEventArgs args)
        {
            if (nodeThis != null) 
            {
                MQTTDisconnectedEvent?.Invoke(nodeThis, args);
                nodeThis.Token = null;
            } 
        }

        private void CVMQTT_Flow_MQTTConnectedEvent(object sender, MQTTConnectedEventArgs args)
        {
            if (nodeThis != null)
            {
                MQTTConnectedEvent?.Invoke(nodeThis, args);
                CVMQTT_Flow?.Subscribe(nodeThis?.NodeTopic);
                //CVMQTT_Flow?.Subscribe(string.Format("{0}/Flow/SVR.Flow.Default/STATUS", RCName));
                Regist();
            }
        }

        private void DoRecvThisNode(MQTTMsgEventArgs args)
        {
            MQTTNodeServiceHeader? resp = JsonConvert.DeserializeObject<MQTTNodeServiceHeader>(args.Data);
            if (resp?.EventName == MQTTNodeServiceEventEnum.Event_Regist)
            {
                MQTTNodeServiceRegistResponse? resp_reg = JsonConvert.DeserializeObject<MQTTNodeServiceRegistResponse>(args.Data);
                if (resp_reg?.Code == 0)
                {
                    if (nodeThis != null)
                    {
                        if (nodeThis.RefreshToken(resp_reg.Token) && logger.IsDebugEnabled) logger.Debug("Refresh Token ok");
                    }
                }
                else
                {
                    if (logger.IsDebugEnabled) logger.DebugFormat("Regist falied => {0}", args.Data);
                }
            }
            else if (resp?.EventName == MQTTNodeServiceEventEnum.Event_Startup)
            {
                if (nodeThis != null && nodeThis.IsNotStartup)
                {
                    nodeThis.Startup();
                    MQTTRCServicesQueryRequest request = new MQTTRCServicesQueryRequest(nodeThis.Token.AccessToken);
                    CVMQTT_Flow?.Publish(MQTTRCServiceTypeConst.BuildPublicTopic(this.RCName), JsonConvert.SerializeObject(request));
                    //if (logger.IsInfoEnabled) logger.Info("Recv RC Startup ok");
                }
            }
            else if (resp?.EventName == MQTTNodeServiceEventEnum.Event_QueryServices)
            {
                MQTTResponse<Dictionary<string, List<MQTTNodeService>>>? resp_q = JsonConvert.DeserializeObject<MQTTResponse<Dictionary<string, List<MQTTNodeService>>>>(args.Data);
                if (resp_q?.Data != null)
                {
                    foreach (var item in resp_q.Data)
                    {
                        foreach (var svr in item.Value)
                        {
                            if(nodeServers.TryAdd(svr.ServiceCode, svr))
                            {
                                CVMQTT_Flow?.Subscribe(svr.DownChannel);
                                svrTopics.TryAdd(svr.DownChannel, svr);
                                if (logger.IsDebugEnabled) logger.DebugFormat("MQTT Subscribe => {0}", svr.DownChannel);
                            }
                        }
                    }
                    if (logger.IsInfoEnabled) logger.Info("MQTT Registed ok");
                    MQTTRegistedEvent?.Invoke(nodeThis, EventArgs.Empty);
                }
                else
                {
                    if (logger.IsDebugEnabled) logger.DebugFormat("Node Recv mqtt => {0}", args.Data);
                }
            }
            else
            {
                if (logger.IsDebugEnabled) logger.DebugFormat("This Node Recv mqtt => {0}", args.Data);
            }
        }

        private void CVMQTT_Flow_MQTTMsgEvent(object sender, MQTTMsgEventArgs args)
        {
            if (args != null && !string.IsNullOrEmpty(args.Topic) && !string.IsNullOrEmpty(args.Data))
            {
                if (args.Topic == nodeThis?.NodeTopic)
                {
                    DoRecvThisNode(args);
                }
                else
                {
                    if (svrTopics.ContainsKey(args.Topic))
                    {
                        var svr = svrTopics[args.Topic];
                        MQTTBaseResponse resp = JsonConvert.DeserializeObject<MQTTBaseResponse>(args.Data);
                        if (logger.IsDebugEnabled) logger.DebugFormat("Recv {0} => {1}", svr.ServiceCode, JsonConvert.SerializeObject(resp));
                        svr.SetResponse(resp);
                    }
                    else
                    {
                        if (logger.IsWarnEnabled) logger.WarnFormat("Unprocessed Topic Recv {0} => {1}", args.Topic, args.Data);
                    }
                }
            }
        }

        public MQTTFlowDeviceNode? GetFlowService()
        {
            string svrCode = "SVR.Flow.Default";
            if (nodeServers.ContainsKey(svrCode))
            {
                var node = nodeServers[svrCode];
                return new MQTTFlowDeviceNode(RCName, -1,node.ServiceType, node.ServiceCode, node.ServiceName, node.ServiceToken, node.Devices.FirstOrDefault().Value.Code, node.RequestManager);
            }

            return null;
        }

        public void Regist(bool isReset = false)
        {
            if(nodeThis != null)
            {
                if (isReset) Reset();
                string data = JsonConvert.SerializeObject(new MQTTNodeServiceRegist(nodeThis));
                CVMQTT_Flow?.Publish(RCRegTopic, data);
            }
        }
        private void Reset()
        {
            nodeThis?.Reset();
            nodeServers.Clear();
            svrTopics.Clear();
        }
        public void ReRegist()
        {
            Regist(true);
        }
        public void Publish(string topic, string data)
        {
            CVMQTT_Flow?.Publish(topic, data);
        } 
        
        public void Publish(string topic, MQTTCVRequestHeader request)
        {
            CVMQTT_Flow?.Publish(topic, JsonConvert.SerializeObject(request));
        }

        public List<MQTTServiceMO> GetAllServices()
        {
            List<MQTTServiceMO> services = new List<MQTTServiceMO>();
            foreach (var service in nodeServers.Values)
            {
                MQTTServiceMO svrMO = new MQTTServiceMO(service.ServiceType, service.ServiceCode, service.DownChannel, service.UpChannel, service.ServiceToken);
                foreach (var dev in service.Devices)
                {
                    svrMO.Devices.TryAdd(dev.Key, new MQTTDeviceMO() { DeviceCode = dev.Value.Code });
                }
                services.Add(svrMO);
            }
            return services;
        }
    }
}
