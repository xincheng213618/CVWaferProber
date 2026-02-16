using ColorVision.Core.Message.Response;
using ColorVision.Message.Flow;
using ColorVision.Node.MQTT;
using ColorVision.Services.Proxy;
using CVMQTTLib;
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
        private MQTTNodeClient? mqttClientNode;
        private readonly EventAggregator eventAggregator;
        //private FlowDeviceProxy? flowDeviceProxy;

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
            this.mqttClientNode = new MQTTNodeClient(new ServiceNodeClientConfig("RC_local"), mqtt_cfg);
            //this.mqttClientNode = CVMQTTClientNode.Instance.Init(new ServiceClientNodeConfig("RC_local"), mqtt_cfg);

            mqttClientNode.MQTTRegistedEvent += Mqtt_MQTTRegistedEvent;
            mqttClientNode.MQTTUnRegistedEvent += Mqtt_MQTTUnRegistedEvent;
            mqttClientNode.MQTTFlowNodeResponseEvent += MqttClientNode_MQTTFlowNodeResponseEvent; ;
            return true;
        }

        private void MqttClientNode_MQTTFlowNodeResponseEvent(object? sender, DeviceResponseMessageHeader e)
        {
            if (e.DeviceCode == FlowDeviceProxy.DefaultDeviceCode)
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
            var flowDeviceProxy = mqttClientNode?.GetDefaultFlowDevice();
            FlowDeviceRequestRunMessage? req = flowDeviceProxy?.BuildRequest(serialNumber, flowId, flowName);
            if (req == null)
            {
                if (logger.IsErrorEnabled) logger.Error("Build MQTT Request is null.");
                return null;
            }
            string? topic = flowDeviceProxy?.UpChannel;
            if (string.IsNullOrEmpty(topic))
            {
                if (logger.IsErrorEnabled) logger.Error("Build MQTT Request is null.");
                return null;
            }
            string msgId = req.MsgID;
            var waitTask = flowDeviceProxy?.WaitForResponseAsync(msgId, timeout);
            try
            {
                mqttClientNode?.Publish(topic, JsonConvert.SerializeObject(req));
                // 等待响应
                var response = await waitTask;
                return response;
            }
            catch (Exception)
            {
                // 确保移除等待任务
                flowDeviceProxy?.SetException(msgId,
                    new OperationCanceledException("请求被取消"));
                throw;
            }
        }

        public void Reconnect()
        {
            mqttClientNode?.ReRegist();
        }

        public List<PhysicDeviceProxy>? GetAllDevices()
        {
            return mqttClientNode?.GetAllDevices();
        }

        public async Task<bool> TryRegistAsync()
        {
            for (int i = 0; i < 100; i++)
            {
                if (ConnectionInfo.IsConnected) { break; }
                await Task.Delay(100);
            }

            return ConnectionInfo.IsConnected;
        }

        public List<PhysicDeviceProxy> GetAllDevices()
        {
            return mqttClientNode.GetAllDevices();
        }
    }
}
