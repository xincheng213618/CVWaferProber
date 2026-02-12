using CVCommCore;
using CVMQTTLib;
using Newtonsoft.Json;

namespace CVWaferProber.MQTT
{
    public class CVMQTTWPClient : ReflectionSingleton<CVMQTTWPClient>
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVMQTTWPClient));

        private CVMQTTControl? CVMQTT_Flow;
        private MQTTServiceNode? nodeThis;
        private Dictionary<string, MQTTNodeService> nodeServers = new Dictionary<string, MQTTNodeService>();
        private Dictionary<string, MQTTNodeService> svrTopics = new Dictionary<string, MQTTNodeService>();
        private CancellationTokenSource? closeToken;
        private Task HBTask;

        public MqttNodeClientStatus Status { get; private set; }

        public event MQTTConnectedEventHandler MQTTConnectedEvent;

        public event MQTTConnectedEventHandler MQTTDisconnectedEvent;

        public event EventHandler MQTTRegistedEvent;
        public event EventHandler MQTTUnRegistedEvent;

        private CVMQTTWPClient() : base()
        {
            Status = MqttNodeClientStatus.Disconnected;
        }
        public CVMQTTWPClient Init(string rcName, string nodeAppId = "app1", string nodeKey = "123456")
        {
            return Init(new MQTTServiceNode(rcName) { NodeAppId = nodeAppId, NodeKey = nodeKey, ServiceType = CVServiceType.Client });
        }
        public CVMQTTWPClient Init(MQTTServiceNode node)
        {
            this.nodeThis = node;
            this.closeToken = new CancellationTokenSource();
            InitFlow();

            return this;
        }

        public void Close()
        {
            closeToken?.Cancel();
            for (int i = 0; i < 10; i++)
            {
                if (HBTask.Status == TaskStatus.RanToCompletion) break;
                else Task.Delay(100).Wait();
            }
            if (logger.IsDebugEnabled) logger.DebugFormat("MQTTService closed => {0}", nodeThis.NodeName);
        }
        private void StartFlow()
        {
            string? currentPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            MQTTConfig config = new MQTTConfig(System.IO.Path.Combine(currentPath, "cfg", "MQTT.config"));
            CVMQTTConfig mqtt_cfg = new CVMQTTConfig() { Host = config.Host, Port = config.Port, IsServer = false, IsDebugOut = false };
            CVMQTT_Flow?.Start(mqtt_cfg);

            this.HBTask = Task.Factory.StartNew(()=> DoKeepLive());
        }

        private async void DoKeepLive()
        {
            if (nodeThis == null) return;
            if (logger.IsInfoEnabled) logger.InfoFormat("DoKeepLive started. {0}", nodeThis.NodeName);
            while (!closeToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(nodeThis.HeartbeatTime, closeToken.Token);
                }
                catch (Exception ex)
                {
                    if (logger.IsDebugEnabled) logger.DebugFormat("{0} DoKeepLive break. Reason:{1}", nodeThis.NodeName, ex.Message);
                    break;
                }
                doRCHeartbeat();
            }

            closeToken.Dispose();
            closeToken = null;
            if (logger.IsInfoEnabled) logger.InfoFormat("DoKeepLive existed. {0}", nodeThis.NodeName);
        }

        private void doRCHeartbeat()
        {
            //if (logger.IsDebugEnabled) logger.DebugFormat("Status = >{0}", Status.ToString());
            if (Status != MqttNodeClientStatus.Disconnected)
            {
                if (!nodeThis.IsLive())
                {
                    if (Status != MqttNodeClientStatus.UnRegisted)
                    {
                        MQTTUnRegistedEvent?.Invoke(nodeThis, EventArgs.Empty);
                        Status = MqttNodeClientStatus.UnRegisted;
                        nodeThis.Reset();
                    }
                }
                else
                {
                    if (Status != MqttNodeClientStatus.Registed)
                    {
                        MQTTRegistedEvent?.Invoke(nodeThis, EventArgs.Empty);
                        Status = MqttNodeClientStatus.Registed;
                    }
                }

                if (Status == MqttNodeClientStatus.UnRegisted)
                {
                    ReRegist();
                }else if (Status == MqttNodeClientStatus.Registed)
                {
                    string serviceHeartbeat = nodeThis?.HeartbeatData;
                    if (!string.IsNullOrEmpty(serviceHeartbeat)) CVMQTT_Flow?.Publish(nodeThis.RCHBTopic, serviceHeartbeat);
                }
            }
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
            Status = MqttNodeClientStatus.Disconnected;
            if (nodeThis != null) 
            {
                MQTTDisconnectedEvent?.Invoke(nodeThis, args);
                nodeThis.Reset();
            } 
        }

        private void CVMQTT_Flow_MQTTConnectedEvent(object sender, MQTTConnectedEventArgs args)
        {
            Status = MqttNodeClientStatus.Connected;
            if (nodeThis != null)
            {
                MQTTConnectedEvent?.Invoke(nodeThis, args);
                CVMQTT_Flow?.Subscribe(nodeThis?.NodeTopic);
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
                    Status = MqttNodeClientStatus.UnRegisted;
                    MQTTUnRegistedEvent?.Invoke(nodeThis, EventArgs.Empty);
                    if (logger.IsDebugEnabled) logger.DebugFormat("Regist falied => {0}", args.Data);
                }
            }
            else if (resp?.EventName == MQTTNodeServiceEventEnum.Event_Startup)
            {
                if (nodeThis != null && nodeThis.IsNotStartup)
                {
                    nodeThis.Startup();
                    MQTTRCServicesQueryRequest request = new MQTTRCServicesQueryRequest(nodeThis.Token.AccessToken);
                    CVMQTT_Flow?.Publish(MQTTRCServiceTypeConst.BuildPublicTopic(nodeThis.RCName), JsonConvert.SerializeObject(request));
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
                                //if (logger.IsDebugEnabled) logger.DebugFormat("MQTT Subscribe => {0}", svr.DownChannel);
                            }
                        }
                    }
                    if (logger.IsInfoEnabled) logger.Info("MQTT Registed ok");
                    MQTTRegistedEvent?.Invoke(nodeThis, EventArgs.Empty);
                    Status = MqttNodeClientStatus.Registed;
                }
                else
                {
                    if (logger.IsDebugEnabled) logger.DebugFormat("Node Recv mqtt => {0}", args.Data);
                }
            }
            else if (resp?.EventName == MQTTNodeServiceEventEnum.Event_ServiceHeartbeat)
            {
                nodeThis?.RecvHeartbeat();
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
                return new MQTTFlowDeviceNode(nodeThis.RCName, -1,node.ServiceType, node.ServiceCode, node.ServiceName, node.ServiceToken, node.Devices.FirstOrDefault().Value.Code, node.RequestManager);
            }

            return null;
        }

        public void Regist(bool isReset = false)
        {
            if(nodeThis != null)
            {
                if (isReset) Reset();
                string data = JsonConvert.SerializeObject(new MQTTNodeServiceRegist(nodeThis));
                CVMQTT_Flow?.Publish(nodeThis.RCRegTopic, data);
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

    public enum MqttNodeClientStatus
    {
        Connected,
        Disconnected,
        Registed,
        UnRegisted,
    }
}
