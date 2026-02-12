using CVCommCore;
using CVWaferProber.Models;
using CVWaferProber.MQTT;
using WaferComm.Core;

namespace CVWaferProber.Services
{
    public class MQTTService : IFlowService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MQTTService));
        private readonly CVMQTTWPClient mqttClient;
        private readonly EventAggregator eventAggregator;
        private MQTTServiceNode? nodeThis;

        public ConnectionInfo ConnectionInfo { get; private set; }

        public MQTTService()
        {
            this.ConnectionInfo = new ConnectionInfo("Registed", "UnRegisted") { ServerIP = "127.0.0.1", Port = 8080 };
            this.eventAggregator = new EventAggregator();
            this.nodeThis = new MQTTServiceNode("RC_local") { NodeAppId = "app1", NodeKey = "123456", ServiceType = CVServiceType.Client };
            this.mqttClient = CVMQTTWPClient.Instance.Init(nodeThis);

            mqttClient.MQTTRegistedEvent += Mqtt_MQTTRegistedEvent;
            mqttClient.MQTTUnRegistedEvent += Mqtt_MQTTUnRegistedEvent;
        }

        private void Mqtt_MQTTUnRegistedEvent(object? sender, EventArgs e)
        {
            PublishStatus(ConnectionStatus.Disconnected);
        }

        private void Mqtt_MQTTRegistedEvent(object? sender, EventArgs e)
        {
            PublishStatus(ConnectionStatus.Connected);
        }
        private void PublishStatus(ConnectionStatus status)
        {
            ConnectionInfo.SetConnected(status == ConnectionStatus.Connected);
            eventAggregator.Publish(new ConnectionStateChangedEvent(ConnectionInfo.IsConnected, ConnectionInfo.ServerIP, ConnectionInfo.Port));
        }
        public async Task<MQTTBaseResponse?> FowRunAndWaitResponseAsync(int flowId, string flowName, string serialNumber, TimeSpan? timeout = null)
        {
            MQTTFlowDeviceNode? flowSvr = mqttClient.GetFlowService();
            if (flowSvr == null) {
                if (logger.IsErrorEnabled) logger.Error("Please reconnect to MQTT.");
                return null;
            }
            var allSvrs = mqttClient.GetAllServices();
            string? data = flowSvr.BuildRequest_Run(serialNumber, flowId, flowName, allSvrs);
            if (string.IsNullOrEmpty(data)) { return null; }
            var waitTask = flowSvr.WaitForResponseAsync(serialNumber, timeout);
            try
            {
                mqttClient.Publish(flowSvr.UpChannel, data);
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

        public void Reconnect()
        {
            mqttClient.ReRegist();
        }
    }
}
