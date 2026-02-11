using CVWaferProber.MQTT;

namespace CVWaferProber.Services
{
    public class MQTTService
    {
        private CVMQTTWPClient mqtt;

        public MQTTService()
        {
            mqtt = CVMQTTWPClient.Instance.Init("RC_local");
        }

        public void Start()
        {
        }

        public bool FowRun(int flowId, string flowName, string sn)
        {
            MQTTNodeServiceFlow? flowSvr = mqtt.GetFlowService();
            if (flowSvr == null) { return false; }
            var allSvrs = mqtt.GetAllServices();
            string? data = flowSvr.BuildRequest_Run(sn, flowId, flowName, allSvrs);
            if (string.IsNullOrEmpty(data)) { return false; }
            mqtt.Publish(flowSvr.UpChannel, data);
            return true;
        }
    }
}
