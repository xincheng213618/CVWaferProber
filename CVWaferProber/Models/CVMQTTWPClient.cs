using CVCommCore;
using CVMQTTLib;

namespace CVWaferProber.Models
{
    public class CVMQTTWPClient : ReflectionSingleton<CVMQTTWPClient>
    {
        private CVMQTTControl? CVMQTT_Flow;
        private string RCName;

        private CVMQTTWPClient() : base()
        {

        }
        public CVMQTTWPClient Init(string rcName)
        {
            RCName = rcName;
            InitFlower();

            StartFlow();
            return this;
        }
        public void StartFlow()
        {
            string currentPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            MQTTConfig config = new MQTTConfig(System.IO.Path.Combine(currentPath,"cfg/MQTT.config"));
            CVMQTTConfig mqtt_cfg = new CVMQTTConfig() { Host = config.Host, Port = config.Port, IsServer = false, IsDebugOut = false };
            CVMQTT_Flow.Start(mqtt_cfg);
        }
        private void InitFlower()
        {
            CVMQTT_Flow = new CVMQTTControl();
            CVMQTT_Flow.MQTTMsgEvent += CVMQTT_Flow_MQTTMsgEvent;
            CVMQTT_Flow.MQTTConnectedEvent += CVMQTT_Flow_MQTTConnectedEvent;
            CVMQTT_Flow.MQTTDisconnectedEvent += CVMQTT_Flow_MQTTDisconnectedEvent;
        }

        private void CVMQTT_Flow_MQTTDisconnectedEvent(object sender, MQTTConnectedEventArgs args)
        {
            throw new NotImplementedException();
        }

        private void CVMQTT_Flow_MQTTConnectedEvent(object sender, MQTTConnectedEventArgs args)
        {
            CVMQTT_Flow.Subscribe(string.Format("{0}/Flow/SVR.Flow.Default/STATUS", RCName));
        }

        private void CVMQTT_Flow_MQTTMsgEvent(object sender, MQTTMsgEventArgs args)
        {
            throw new NotImplementedException();
        }
    }
}
