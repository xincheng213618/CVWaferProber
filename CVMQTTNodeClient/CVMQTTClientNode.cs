using ColorVision.Core.Message;
using ColorVision.Core.Message.Request;
using ColorVision.Core.Message.Response;
using ColorVision.Message.Model;
using ColorVision.Message.Services;
using ColorVision.Services.Proxy;
using CVCommCore;
using CVMQTTLib;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Reflection;

namespace CVMQTTNodeClient
{
    public class CVMQTTClientNode : ReflectionSingleton<CVMQTTClientNode>, IDisposable
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVMQTTClientNode));

        private CVMQTTControl? _mqttControl;
        private ServiceClientNodeConfig? _nodeThis;

        // 使用ConcurrentDictionary替代Dictionary，线程安全
        private readonly ConcurrentDictionary<string, ServiceNodeProxy> _nodeServers = new();
        private readonly ConcurrentDictionary<string, ServiceNodeProxy> _svrTopics = new();
        private readonly ConcurrentDictionary<string, PhysicDeviceProxy> _devices = new();
        private readonly List<ServiceMO> _flowSvrs = new();

        private CancellationTokenSource? _closeTokenSource;
        private Task? _hbTask;

        private readonly object _statusLock = new();
        private MqttNodeClientStatus _status;

        public ServiceClientNodeConfig? Config => _nodeThis;

        public MqttNodeClientStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    lock (_statusLock)
                    {
                        _status = value;
                    }
                }
            }
        }

        // 使用显式接口或委托定义事件，避免内存泄漏
        private event MQTTConnectedEventHandler? _mqttConnectedEvent;
        private event MQTTConnectedEventHandler? _mqttDisconnectedEvent;
        private event EventHandler? _mqttRegistedEvent;
        private event EventHandler? _mqttUnRegistedEvent;
        private event EventHandler<DeviceResponseMessageHeader>? _mqttFlowNodeResponseEvent;

        public event MQTTConnectedEventHandler MQTTConnectedEvent
        {
            add => _mqttConnectedEvent += value;
            remove => _mqttConnectedEvent -= value;
        }

        public event MQTTConnectedEventHandler MQTTDisconnectedEvent
        {
            add => _mqttDisconnectedEvent += value;
            remove => _mqttDisconnectedEvent -= value;
        }

        public event EventHandler MQTTRegistedEvent
        {
            add => _mqttRegistedEvent += value;
            remove => _mqttRegistedEvent -= value;
        }

        public event EventHandler MQTTUnRegistedEvent
        {
            add => _mqttUnRegistedEvent += value;
            remove => _mqttUnRegistedEvent -= value;
        }
               
        public event EventHandler<DeviceResponseMessageHeader> MQTTFlowNodeResponseEvent
        {
            add => _mqttFlowNodeResponseEvent += value;
            remove => _mqttFlowNodeResponseEvent -= value;
        }

        private CVMQTTClientNode() : base()
        {
            Status = MqttNodeClientStatus.Disconnected;
        }

        /// <summary>
        /// 初始化MQTT客户端
        /// </summary>
        public CVMQTTClientNode Init(CVMQTTConfig mqtt_cfg, string rcName, string nodeAppId = "app1", string nodeKey = "123456")
        {
            return Init(new ServiceClientNodeConfig(rcName, nodeAppId, nodeKey), mqtt_cfg);
        }

        /// <summary>
        /// 初始化MQTT客户端
        /// </summary>
        public CVMQTTClientNode Init(ServiceClientNodeConfig node, CVMQTTConfig mqtt_cfg)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            _nodeThis = node;
            _closeTokenSource?.Cancel();
            _closeTokenSource?.Dispose();
            _closeTokenSource = new CancellationTokenSource();

            InitMqttControl(mqtt_cfg);
            return this;
        }

        /// <summary>
        /// 关闭客户端
        /// </summary>
        public void Close()
        {
            try
            {
                _closeTokenSource?.Cancel();

                // 等待心跳任务完成，使用异步等待避免死锁
                if (_hbTask != null)
                {
                    try
                    {
                        _hbTask.Wait(TimeSpan.FromSeconds(3));
                    }
                    catch (AggregateException)
                    {
                        // 忽略任务取消异常
                    }
                }

                _mqttControl?.Close();
                _mqttControl = null;

                if (logger.IsDebugEnabled && _nodeThis != null)
                    logger.DebugFormat("MQTTService closed => {0}", _nodeThis.NodeName);
            }
            catch (Exception ex)
            {
                if (logger.IsErrorEnabled)
                    logger.Error("Close MQTT client failed", ex);
            }
        }

        public void Dispose()
        {
            Close();
            _closeTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void InitMqttControl(CVMQTTConfig mqtt_cfg)
        {
            _mqttControl = new CVMQTTControl();
            _mqttControl.MQTTMsgEvent += OnMqttMsgEvent;
            _mqttControl.MQTTConnectedEvent += OnMqttConnectedEvent;
            _mqttControl.MQTTDisconnectedEvent += OnMqttDisconnectedEvent;

            StartMQTT(mqtt_cfg);
        }

        private void StartMQTT(CVMQTTConfig mqtt_cfg)
        {
            try
            {
                string? currentPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(currentPath))
                {
                    if (logger.IsErrorEnabled) logger.Error("Failed to get assembly path");
                    return;
                }

                var configPath = System.IO.Path.Combine(currentPath, "cfg", "MQTT.config");
                if (!System.IO.File.Exists(configPath))
                {
                    if (logger.IsErrorEnabled) logger.ErrorFormat("MQTT config file not found: {0}", configPath);
                    return;
                }

                _mqttControl?.Start(mqtt_cfg);

                // 使用Task.Run替代StartNew
                _hbTask = Task.Run(DoKeepLive);
            }
            catch (Exception ex)
            {
                if (logger.IsErrorEnabled) logger.Error("StartFlow failed", ex);
            }
        }

        private async Task DoKeepLive()
        {
            if (_nodeThis == null) return;

            var nodeName = _nodeThis.NodeName;
            var token = _closeTokenSource?.Token ?? CancellationToken.None;

            if (logger.IsInfoEnabled)
                logger.InfoFormat("DoKeepLive started. {0}", nodeName);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 使用Try-catch包装Delay，避免TaskCanceledException频繁抛出
                    try
                    {
                        await Task.Delay(_nodeThis.HeartbeatTime, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    DoHeartbeat();
                }
            }
            catch (Exception ex)
            {
                if (logger.IsErrorEnabled)
                    logger.ErrorFormat("DoKeepLive error. {0}, Reason:{1}", nodeName, ex.Message);
            }
            finally
            {
                if (logger.IsInfoEnabled)
                    logger.InfoFormat("DoKeepLive exited. {0}", nodeName);
            }
        }

        private void DoHeartbeat()
        {
            //if (logger.IsDebugEnabled) logger.DebugFormat("currentStatus => {0}", Status.ToString());
            if (_nodeThis == null) return;

            var currentStatus = Status;
            if (currentStatus == MqttNodeClientStatus.Disconnected)
                return;

            try
            {
                if (!_nodeThis.IsLive())
                {
                    if (currentStatus != MqttNodeClientStatus.UnRegisted)
                    {
                        _mqttUnRegistedEvent?.Invoke(_nodeThis, EventArgs.Empty);
                        Status = MqttNodeClientStatus.UnRegisted;
                        ClearCache();
                    }

                    if (Status == MqttNodeClientStatus.UnRegisted)
                    {
                        ReRegist();
                    }
                }
                else if (Status == MqttNodeClientStatus.Registed)
                {
                    string serviceHeartbeat = _nodeThis.HeartbeatData;
                    if (!string.IsNullOrEmpty(serviceHeartbeat))
                    {
                        //_mqttControl?.Publish(_nodeThis.RCHBTopic, serviceHeartbeat);
                        _mqttControl?.Publish(_nodeThis.RCTopic, serviceHeartbeat);
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger.IsErrorEnabled)
                    logger.ErrorFormat("DoHeartbeat failed: {0}", ex.Message);
            }
        }

        private void OnMqttConnectedEvent(object sender, MQTTConnectedEventArgs args)
        {
            // 只有当状态为Disconnected时才更新为Connected
            if (Status == MqttNodeClientStatus.Disconnected)
            {
                Status = MqttNodeClientStatus.Connected;
                // 如果节点未初始化或状态不是已连接，直接返回
                if (_nodeThis == null)
                    return;
                // 触发连接事件
                _mqttConnectedEvent?.Invoke(_nodeThis, args);
                // 订阅和注册
                _mqttControl?.Subscribe(_nodeThis.NodeTopic);
                Regist();
            }
        }

        private void OnMqttDisconnectedEvent(object sender, MQTTConnectedEventArgs args)
        {
            Status = MqttNodeClientStatus.Disconnected;

            if (_nodeThis != null)
            {
                _mqttDisconnectedEvent?.Invoke(_nodeThis, args);
                ClearCache();
            }
        }

        private void OnMqttMsgEvent(object sender, MQTTMsgEventArgs args)
        {
            if (args == null || string.IsNullOrEmpty(args.Topic) || string.IsNullOrEmpty(args.Data))
            {
                if (logger.IsWarnEnabled)
                    logger.Warn("MQTTMsgEventArgs is null / Topic or Data is empty");
                return;
            }

            try
            {
                if (args.Topic == _nodeThis?.NodeTopic)
                {
                    ProcessNodeMessage(args.Data);
                }
                else if (_svrTopics.TryGetValue(args.Topic, out var svr))
                {
                    ProcessServiceMessage(svr, args.Data);
                }
                else
                {
                    if (logger.IsWarnEnabled)
                        logger.WarnFormat("Unprocessed Topic Recv {0} => {1}", args.Topic, args.Data);
                }
            }
            catch (Exception ex)
            {
                if (logger.IsErrorEnabled)
                    logger.ErrorFormat("Process MQTT message failed. Topic:{0}, Error:{1}",
                        args.Topic, ex.Message);
            }
        }

        private void ProcessNodeMessage(string data)
        {
            if (_nodeThis == null) return;

            var resp = JsonConvert.DeserializeObject<ServiceNodeResponseHeader>(data);
            if (resp == null) return;

            switch (resp.EventName)
            {
                case ServiceNodeEventEnum.Event_Regist:
                    ProcessRegistResponse(data);
                    break;

                case ServiceNodeEventEnum.Event_Startup:
                    ProcessStartupResponse();
                    break;

                case ServiceNodeEventEnum.Event_QueryServices:
                    ProcessQueryServicesResponse(data);
                    break;

               case ServiceNodeEventEnum.Event_QueryServiceStatus:
                    ProcessQueryServiceStatusResponse(data);
                    break;

                case ServiceNodeEventEnum.Event_ServiceHeartbeat:
                    //if (logger.IsDebugEnabled) logger.DebugFormat("ServiceHeartbeat => {0}",data);
                    _nodeThis.RecvHeartbeat();
                    break;

                default:
                    if (logger.IsWarnEnabled)
                        logger.WarnFormat("Unprocessed msg. This Node Recv mqtt => {0}", data);
                    break;
            }
        }

        private void ProcessQueryServiceStatusResponse(string data)
        {
            _nodeThis?.RecvHeartbeat();
            //if (logger.IsDebugEnabled) logger.DebugFormat("QueryServiceStatusResponse => {0}", data);
            var resp_q = JsonConvert.DeserializeObject<ServiceNodeQueryStatusResponse>(data);
            if (resp_q == null || resp_q.Data == null || resp_q.Data.Count == 0)
            {
                if (logger.IsDebugEnabled) logger.DebugFormat("Node Recv mqtt => {0}", data);
                return;
            }
            foreach (var svr in resp_q.Data)
            {
                if(_nodeServers.TryGetValue(svr.ServiceCode,out var nodeSvr))
                {
                    nodeSvr.Update(svr);
                    //if (logger.IsDebugEnabled) logger.DebugFormat("service update => {0}", JsonConvert.SerializeObject(nodeSvr, Formatting.Indented));
                }
            }
        }

        private void ProcessRegistResponse(string data)
        {
            var resp_reg = JsonConvert.DeserializeObject<ServiceNodeRegistResponse>(data);
            if (resp_reg?.Code == 0)
            {
                if (_nodeThis != null && _nodeThis.RefreshToken(resp_reg.Token) && logger.IsDebugEnabled)
                    logger.Debug("Refresh Token ok");
            }
            else
            {
                Status = MqttNodeClientStatus.UnRegisted;
                _mqttUnRegistedEvent?.Invoke(_nodeThis, EventArgs.Empty);

                if (logger.IsDebugEnabled)
                    logger.DebugFormat("Regist failed => {0}", data);
            }
        }

        private void ProcessStartupResponse()
        {
            if (_nodeThis != null && _nodeThis.IsNotStartup)
            {
                if (_nodeThis.Token != null && !string.IsNullOrEmpty(_nodeThis.Token.AccessToken))
                {
                    _nodeThis.Startup();
                    var request = MessageBuilder.BuildRequestQuery(_nodeThis);
                    //var topic = MQTTCVServiceTopicBuilder.BuildPublicTopic(_nodeThis.RCName);
                    _mqttControl?.Publish(_nodeThis.RCTopic, JsonConvert.SerializeObject(request));
                }
            }
        }

        private void ProcessQueryServicesResponse(string data)
        {
            var resp_q = JsonConvert.DeserializeObject<ServiceNodeQueryResponse>(data);
            if (resp_q == null || resp_q.Data == null || resp_q.Data.Count == 0)
            {
                if (logger.IsDebugEnabled) logger.DebugFormat("Node Recv mqtt => {0}", data);
                return;
            }

            bool hasNewService = false;

            foreach (var item in resp_q.Data)
            {
                foreach (var _svr in item.Value)
                {
                    ServiceNodeProxy svr = new ServiceNodeProxy(_svr);
                    if (_nodeServers.TryAdd(svr.Config.ServiceCode, svr))
                    {
                        _mqttControl?.Subscribe(svr.Config.DownChannel);
                        //if (logger.IsDebugEnabled) logger.DebugFormat("{0} Subscribe => {1}", svr.ServiceCode, svr.DownChannel);
                        _svrTopics.TryAdd(svr.Config.DownChannel, svr);
                        _flowSvrs.Add(MessageBuilder.Build(_svr));
                        foreach (var device in svr.Devices.Values)
                        {
                            //CVBaseDeviceNode device = new CVBaseDeviceNode(dev,svr);
                            _devices.TryAdd(device.DeviceCode, device);
                        }
                        hasNewService = true;
                    }
                }
            }

            if (hasNewService)
            {
                var dev = this.GetDevice(FlowDeviceProxy.DefaultDeviceCode);
                if (dev != null)
                {
                    var flowDeviceProxy = dev as FlowDeviceProxy;
                    var allSvrs = this.GetAllServices();
                    flowDeviceProxy?.Init(allSvrs);
                }

                if (logger.IsInfoEnabled) logger.Info("MQTT Registed ok");
                _mqttRegistedEvent?.Invoke(_nodeThis, EventArgs.Empty);
                Status = MqttNodeClientStatus.Registed;
            }
        }

        private void ProcessServiceMessage(ServiceNodeProxy svrProxy, string data)
        {
            try
            {
                var resp = JsonConvert.DeserializeObject<DeviceResponseMessageHeader>(data);
                if (resp == null) { return; }
                if (logger.IsDebugEnabled)
                    logger.DebugFormat("[{0}]Recv => {1}", svrProxy.Config.ServiceName, JsonConvert.SerializeObject(resp));
                if (resp.EventName == "Heartbeat")
                {
                    var hb_resp = JsonConvert.DeserializeObject<ServiceNodeHeartbeatResponse>(data);
                    if (_nodeServers.TryGetValue(svrProxy.Config.ServiceName,out var svr))
                    {
                        svr.Update(hb_resp);
                    }
                    //if (logger.IsDebugEnabled)
                    //    logger.DebugFormat("[{0}]Heartbeat => {1}", svr.ServiceName, data);
                }
                else if (_devices.TryGetValue(resp.DeviceCode, out var device))
                {
                    _mqttFlowNodeResponseEvent?.Invoke(device, resp);
                    device.SetResponse(resp);
                }
            }
            catch (Exception ex)
            {
                if (logger.IsErrorEnabled)
                    logger.ErrorFormat("Process service message failed. Service:{0}, Error:{1}",
                        svrProxy.Config.ServiceName, ex.Message);
            }
        }

        /// <summary>
        /// 获取Flow服务节点
        /// </summary>
        public PhysicDeviceProxy? GetDevice(string devCode)
        {
            if (_devices.TryGetValue(devCode, out var device) && _nodeThis != null)
            {
               return device;
            }

            return null;
        }

        /// <summary>
        /// 注册到MQTT服务器
        /// </summary>
        public void Regist(bool isReset = false)
        {
            if (_nodeThis == null) return;

            try
            {
                if (isReset) ClearCache();

                var data = JsonConvert.SerializeObject(BuildHBReq());
                _mqttControl?.Publish(_nodeThis.RCRegTopic, data);
            }
            catch (Exception ex)
            {
                if (logger.IsErrorEnabled) logger.Error("Regist failed", ex);
            }
        }

        private ServiceNodeRegistRequest BuildHBReq()
        {
            return MessageBuilder.BuildRequestRegist(_nodeThis);
        }

        private void ClearCache()
        {
            _nodeThis?.Reset();
            foreach (var svr in _nodeServers.Values)
            {
                _mqttControl?.Unsubscribe(svr.Config.DownChannel);  
            }
            _nodeServers.Clear();
            _svrTopics.Clear();
            _flowSvrs.Clear();
        }

        /// <summary>
        /// 重新注册
        /// </summary>
        public void ReRegist() => Regist(true);

        /// <summary>
        /// 发布消息
        /// </summary>
        public void Publish(string topic, string data)
        {
            if (string.IsNullOrEmpty(topic)) throw new ArgumentNullException(nameof(topic));
            if (data == null) throw new ArgumentNullException(nameof(data));

            _mqttControl?.Publish(topic, data);
        }

        /// <summary>
        /// 发布消息
        /// </summary>
        public void Publish(string topic, DeviceRequestTokenMessageHeader request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Publish(topic, JsonConvert.SerializeObject(request));
        }

        /// <summary>
        /// 获取所有服务
        /// </summary>
        public List<ServiceMO> GetAllServices()
        {
            var services = new List<ServiceMO>(_flowSvrs.Count);

            foreach (var service in _flowSvrs)
            {
                services.Add(service);
            }

            return services;
        }
        public List<PhysicDeviceProxy> GetAllDevices()
        {
            var devices = new List<PhysicDeviceProxy>(_devices.Count);

            foreach (var dev in _devices.Values)
            {
                devices.Add(dev);
            }

            return devices;
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