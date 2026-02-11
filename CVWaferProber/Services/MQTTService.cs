using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.MQTT;

namespace CVWaferProber.Services
{
    public class MQTTService
    {
        private readonly CVMQTTWPClient mqtt;
        public MQTTService()
        {
            this.mqtt = CVMQTTWPClient.Instance.Init("RC_local");
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
        public async Task<MQTTBaseResponse?> FowRunAndWaitResponseAsync(int flowId, string flowName, string serialNumber)
        {
            MQTTNodeServiceFlow? flowSvr = mqtt.GetFlowService();
            if (flowSvr == null) { return null; }
            var allSvrs = mqtt.GetAllServices();
            string? data = flowSvr.BuildRequest_Run(serialNumber, flowId, flowName, allSvrs);
            if (string.IsNullOrEmpty(data)) { return null; }
            var waitTask = flowSvr.WaitForResponseAsync(serialNumber);
            try
            {
                mqtt.Publish(flowSvr.UpChannel, data);
                // 等待响应
                var response = await waitTask;
                return response;
            }
            catch (Exception)
            {
                // 确保移除等待任务
                flowSvr.SetException(serialNumber,
                    new OperationCanceledException("请求被取消"));
                throw;
            }
        }
    }
}
