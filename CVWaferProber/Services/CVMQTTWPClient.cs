using CVCommCore;
using CVMQTTLib;
using CVWaferProber.MQTT;
using Newtonsoft.Json;

namespace CVWaferProber.Services
{
    //public class CVMQTTWPClient : ReflectionSingleton<CVMQTTWPClient>
    //{
    //    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVMQTTWPClient));

    //    private CVMQTTControl? CVMQTT_Flow;
    //    private string RCName;
    //    private string RCRegTopic;
    //    private MQTTServiceNode? nodeThis;

    //    private CVMQTTWPClient() : base()
    //    {

    //    }
    //    public CVMQTTWPClient Init(string rcName)
    //    {
    //        RCName = rcName;
    //        RCRegTopic = MQTTRCServiceTypeConst.BuildRegTopic(RCName);
    //        string NodeName = "client." + Guid.NewGuid().ToString();
    //        nodeThis = new MQTTServiceNode() { NodeAppId = "app1", NodeName = NodeName, NodeKey = "123456", ServiceType = CVServiceType.Client, NodeTopic = MQTTRCServiceTypeConst.BuildNodeTopic(NodeName, RCName) };

    //        InitFlow();

    //        return this;
    //    }
    //    private void StartFlow()
    //    {
    //        string? currentPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    //        MQTTConfig config = new MQTTConfig(System.IO.Path.Combine(currentPath, "cfg", "MQTT.config"));
    //        CVMQTTConfig mqtt_cfg = new CVMQTTConfig() { Host = config.Host, Port = config.Port, IsServer = false, IsDebugOut = false };
    //        CVMQTT_Flow?.Start(mqtt_cfg);
    //    }
    //    private void InitFlow()
    //    {
    //        CVMQTT_Flow = new CVMQTTControl();
    //        CVMQTT_Flow.MQTTMsgEvent += CVMQTT_Flow_MQTTMsgEvent;
    //        CVMQTT_Flow.MQTTConnectedEvent += CVMQTT_Flow_MQTTConnectedEvent;
    //        CVMQTT_Flow.MQTTDisconnectedEvent += CVMQTT_Flow_MQTTDisconnectedEvent;
    //        //
    //        StartFlow();
    //    }

    //    private void CVMQTT_Flow_MQTTDisconnectedEvent(object sender, MQTTConnectedEventArgs args)
    //    {
    //        if (nodeThis != null) nodeThis.Token = null;
    //    }

    //    private void CVMQTT_Flow_MQTTConnectedEvent(object sender, MQTTConnectedEventArgs args)
    //    {
    //        if (nodeThis != null)
    //        {
    //            CVMQTT_Flow?.Subscribe(nodeThis?.NodeTopic);
    //            //CVMQTT_Flow?.Subscribe(string.Format("{0}/Flow/SVR.Flow.Default/STATUS", RCName));
    //            Regist();
    //        }
    //    }

    //    private void DoRecvThisNode(MQTTMsgEventArgs args)
    //    {
    //        MQTTNodeServiceHeader? resp = JsonConvert.DeserializeObject<MQTTNodeServiceHeader>(args.Data);
    //        if (resp?.EventName == MQTTNodeServiceEventEnum.Event_Regist)
    //        {
    //            MQTTNodeServiceRegistResponse? resp_reg = JsonConvert.DeserializeObject<MQTTNodeServiceRegistResponse>(args.Data);
    //            if (resp_reg?.Code == 0)
    //            {
    //                if (nodeThis != null)
    //                {
    //                    if (nodeThis.RefreshToken(resp_reg.Token) && logger.IsInfoEnabled) logger.Info("Regist ok");
    //                }
    //            }
    //            else
    //            {
    //                if (logger.IsDebugEnabled) logger.DebugFormat("Regist falied => {0}", args.Data);
    //            }
    //        }
    //        else if (resp?.EventName == MQTTNodeServiceEventEnum.Event_Startup)
    //        {
    //            if (nodeThis != null && nodeThis.IsNotStartup)
    //            {
    //                nodeThis.Startup();
    //                MQTTRCServicesQueryRequest request = new MQTTRCServicesQueryRequest(nodeThis.Token.AccessToken);
    //                CVMQTT_Flow?.Publish(MQTTRCServiceTypeConst.BuildPublicTopic(RCName), JsonConvert.SerializeObject(request));
    //                if (logger.IsInfoEnabled) logger.Info("Recv RC Startup ok");
    //            }
    //        }
    //        else if (resp?.EventName == MQTTNodeServiceEventEnum.Event_QueryServices)
    //        {
    //            MQTTResponse<Dictionary<string, List<MQTTNodeService>>>? resp_q = JsonConvert.DeserializeObject<MQTTResponse<Dictionary<string, List<MQTTNodeService>>>>(args.Data);
    //            if (resp_q?.Data != null)
    //            {
    //                Dictionary<string, MQTTNodeService> svrs = new Dictionary<string, MQTTNodeService>();
    //                foreach (var item in resp_q.Data)
    //                {
    //                    foreach (var svr in item.Value)
    //                    {
    //                        svrs.Add(svr.ServiceCode, svr);
    //                        CVMQTT_Flow?.Subscribe(svr.DownChannel);
    //                        if (logger.IsDebugEnabled) logger.DebugFormat("MQTT Subscribe => {0}", svr.DownChannel);
    //                    }
    //                }
    //                if (logger.IsInfoEnabled) logger.Info("QueryServices ok");
    //            }
    //            else
    //            {
    //                if (logger.IsDebugEnabled) logger.DebugFormat("Node Recv mqtt => {0}", args.Data);
    //            }
    //        }
    //        else
    //        {
    //            //if (logger.IsDebugEnabled) logger.DebugFormat("This Node Recv mqtt => {0}", args.Data);
    //        }
    //    }

    //    private void CVMQTT_Flow_MQTTMsgEvent(object sender, MQTTMsgEventArgs args)
    //    {
    //        if (args != null && !string.IsNullOrEmpty(args.Topic) && !string.IsNullOrEmpty(args.Data))
    //        {
    //            if (args.Topic == nodeThis?.NodeTopic)
    //            {
    //                DoRecvThisNode(args);
    //            }
    //            else if (args.Topic == "RC_local/Flow/SVR.Flow.Default/STATUS")
    //            {
    //                //if (logger.IsDebugEnabled) logger.DebugFormat("Recv Flow => {0}", args.Data);
    //            }
    //            else
    //            {
    //                //if (logger.IsDebugEnabled) logger.DebugFormat("Recv mqtt topic {0} => {1}", args.Topic, args.Data);
    //            }
    //        }
    //    }

    //    public void Regist()
    //    {
    //        if(nodeThis != null)
    //        {
    //            string data = JsonConvert.SerializeObject(new MQTTNodeServiceRegist(nodeThis));
    //            CVMQTT_Flow?.Publish(RCRegTopic, data);
    //        }
    //    }
    //}
}
