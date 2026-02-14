using ColorVision.Core.Message.Response;
using ColorVision.Message.Flow;
using CVMQTTLib;
using CVMQTTNodeClient;
using CVWaferProber.Models;
using CVWaferProber.MQTT;
using Newtonsoft.Json;
using System.Reflection;
using WaferComm.Core;

namespace CVWaferProber.Services
{
    public class MQTTService : IFlowService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MQTTService));
        private CVMQTTClientNode mqttClientNode;
        private readonly EventAggregator eventAggregator;
        private MQTTServiceClientNode? nodeThis;

        public ConnectionInfo ConnectionInfo { get; private set; }

        public MQTTService()
        {
            this.ConnectionInfo = new ConnectionInfo("Registed", "UnRegisted") { ServerIP = "127.0.0.1", Port = 8080 };
            this.eventAggregator = new EventAggregator();
            Startup();
        }

        public bool Startup()
        {
            string? currentPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(currentPath))
            {
                if (logger.IsErrorEnabled) logger.Error("Failed to get assembly path");
                return false;
            }

            var configPath = System.IO.Path.Combine(currentPath, "cfg", "MQTT.config");
            if (!System.IO.File.Exists(configPath))
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("MQTT config file not found: {0}", configPath);
                return false;
            }

            MQTTConfig config = new MQTTConfig(configPath);
            CVMQTTConfig mqtt_cfg = new CVMQTTConfig()
            {
                Host = config.Host,
                Port = config.Port,
                IsServer = false,
                IsDebugOut = false
            };
            this.nodeThis = new MQTTServiceClientNode("RC_local");
            this.mqttClientNode = CVMQTTClientNode.Instance.Init(nodeThis,mqtt_cfg);

            mqttClientNode.MQTTRegistedEvent += Mqtt_MQTTRegistedEvent;
            mqttClientNode.MQTTUnRegistedEvent += Mqtt_MQTTUnRegistedEvent;
            mqttClientNode.MQTTFlowNodeResponseEvent += MqttClientNode_MQTTFlowNodeResponseEvent; ;
            return true;
        }

        private void MqttClientNode_MQTTFlowNodeResponseEvent(object? sender, DeviceResponseMessageHeader e)
        {
            if (e.DeviceCode == MQTTFlowDeviceNode.FlowDeviceCode)
            {
                if (logger.IsInfoEnabled) logger.InfoFormat("Flow result => {0}/{1}", e.Message, e.Code);
            }
            else
            {
                if (logger.IsInfoEnabled) logger.InfoFormat("[{0}/{1}]FlowNodeResponse {2} => {3}/{4}", e.DeviceCode, e.ZIndex, e.EventName, e.Message, e.Code);
            }
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
        public async Task<DeviceResponseMessageHeader?> FlowRunAndWaitResponseAsync(int flowId, string flowName, string serialNumber, TimeSpan? timeout = null)
        {
            var dev = mqttClientNode.GetDevice(MQTTFlowDeviceNode.FlowDeviceCode);
            if (dev == null)
            {
                if (logger.IsErrorEnabled) logger.Error("Please reconnect to MQTT.");
                return null;
            }
            MQTTFlowDeviceNode flowSvr = new MQTTFlowDeviceNode(dev);
            var allSvrs = mqttClientNode.GetAllServices();
            FlowDeviceRequestRunMessage? req = flowSvr.BuildRequest(serialNumber, flowId, flowName, allSvrs);
            if (req == null)
            {
                if (logger.IsErrorEnabled) logger.Error("Build MQTT Request is null.");
                return null;
            }
            string msgId = req.MsgID;
            var waitTask = flowSvr.WaitForResponseAsync(msgId, timeout);
            try
            {
                mqttClientNode.Publish(flowSvr.Service.UpChannel, JsonConvert.SerializeObject(req));
                // 等待响应
                var response = await waitTask;
                return response;
            }
            catch (Exception)
            {
                // 确保移除等待任务
                flowSvr.SetException(msgId,
                    new OperationCanceledException("请求被取消"));
                throw;
            }
        }

        public void Reconnect()
        {
            mqttClientNode.ReRegist();
        }
    }
}
