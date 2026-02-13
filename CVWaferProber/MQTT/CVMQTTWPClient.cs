using CVCommCore;
using CVMQTTLib;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Reflection;

namespace CVWaferProber.MQTT
{
    public class CVMQTTWPClient : ReflectionSingleton<CVMQTTWPClient>, IDisposable
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVMQTTWPClient));

        private CVMQTTControl? _mqttControl;
        private MQTTServiceNode? _nodeThis;

        // 使用ConcurrentDictionary替代Dictionary，线程安全
        private readonly ConcurrentDictionary<string, MQTTNodeService> _nodeServers = new();
        private readonly ConcurrentDictionary<string, MQTTNodeService> _svrTopics = new();

        private CancellationTokenSource? _closeTokenSource;
        private Task? _hbTask;

        private readonly object _statusLock = new();
        private MqttNodeClientStatus _status;

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

        private CVMQTTWPClient() : base()
        {
            Status = MqttNodeClientStatus.Disconnected;
        }

        /// <summary>
        /// 初始化MQTT客户端
        /// </summary>
        public CVMQTTWPClient Init(string rcName, string nodeAppId = "app1", string nodeKey = "123456")
        {
            return Init(new MQTTServiceNode(rcName)
            {
                NodeAppId = nodeAppId,
                NodeKey = nodeKey,
                ServiceType = CVServiceType.Client
            });
        }

        /// <summary>
        /// 初始化MQTT客户端
        /// </summary>
        public CVMQTTWPClient Init(MQTTServiceNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            _nodeThis = node;
            _closeTokenSource?.Cancel();
            _closeTokenSource?.Dispose();
            _closeTokenSource = new CancellationTokenSource();

            InitMqttControl();
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

        private void InitMqttControl()
        {
            _mqttControl = new CVMQTTControl();
            _mqttControl.MQTTMsgEvent += OnMqttMsgEvent;
            _mqttControl.MQTTConnectedEvent += OnMqttConnectedEvent;
            _mqttControl.MQTTDisconnectedEvent += OnMqttDisconnectedEvent;

            StartFlow();
        }

        private void StartFlow()
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

                MQTTConfig config = new MQTTConfig(configPath);
                CVMQTTConfig mqtt_cfg = new CVMQTTConfig()
                {
                    Host = config.Host,
                    Port = config.Port,
                    IsServer = false,
                    IsDebugOut = false
                };

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
                        _nodeThis.Reset();
                    }

                    if (Status == MqttNodeClientStatus.UnRegisted)
                    {
                        ReRegist();
                    }
                }
                else
                {
                    //if (currentStatus != MqttNodeClientStatus.Registed)
                    //{
                    //    _mqttRegistedEvent?.Invoke(_nodeThis, EventArgs.Empty);
                    //    Status = MqttNodeClientStatus.Registed;
                    //}

                    if (Status == MqttNodeClientStatus.Registed)
                    {
                        string serviceHeartbeat = _nodeThis.HeartbeatData;
                        if (!string.IsNullOrEmpty(serviceHeartbeat))
                        {
                            _mqttControl?.Publish(_nodeThis.RCHBTopic, serviceHeartbeat);
                        }
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
                Status = MqttNodeClientStatus.Connected;

            // 如果节点未初始化或状态不是已连接，直接返回
            if (_nodeThis == null || Status != MqttNodeClientStatus.Connected)
                return;

            // 触发连接事件
            _mqttConnectedEvent?.Invoke(_nodeThis, args);

            // 订阅和注册
            _mqttControl?.Subscribe(_nodeThis.NodeTopic);
            Regist();
        }

        private void OnMqttDisconnectedEvent(object sender, MQTTConnectedEventArgs args)
        {
            Status = MqttNodeClientStatus.Disconnected;

            if (_nodeThis != null)
            {
                _mqttDisconnectedEvent?.Invoke(_nodeThis, args);
                _nodeThis.Reset();

                // 清空服务缓存
                _nodeServers.Clear();
                _svrTopics.Clear();
            }
        }

        private void OnMqttMsgEvent(object sender, MQTTMsgEventArgs args)
        {
            if (args == null || string.IsNullOrEmpty(args.Topic) || string.IsNullOrEmpty(args.Data))
                return;

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

            var resp = JsonConvert.DeserializeObject<MQTTNodeServiceHeader>(data);
            if (resp == null) return;

            switch (resp.EventName)
            {
                case MQTTNodeServiceEventEnum.Event_Regist:
                    ProcessRegistResponse(data);
                    break;

                case MQTTNodeServiceEventEnum.Event_Startup:
                    ProcessStartupResponse();
                    break;

                case MQTTNodeServiceEventEnum.Event_QueryServices:
                    ProcessQueryServicesResponse(data);
                    break;

                case MQTTNodeServiceEventEnum.Event_ServiceHeartbeat:
                    _nodeThis.RecvHeartbeat();
                    break;

                default:
                    if (logger.IsDebugEnabled)
                        logger.DebugFormat("This Node Recv mqtt => {0}", data);
                    break;
            }
        }

        private void ProcessRegistResponse(string data)
        {
            var resp_reg = JsonConvert.DeserializeObject<MQTTNodeServiceRegistResponse>(data);
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
                _nodeThis.Startup();
                var request = new MQTTRCServicesQueryRequest(_nodeThis.Token.AccessToken);
                var topic = MQTTRCServiceTypeConst.BuildPublicTopic(_nodeThis.RCName);
                _mqttControl?.Publish(topic, JsonConvert.SerializeObject(request));
            }
        }

        private void ProcessQueryServicesResponse(string data)
        {
            var resp_q = JsonConvert.DeserializeObject<MQTTResponse<Dictionary<string, List<MQTTNodeService>>>>(data);
            if (resp_q?.Data == null)
            {
                if (logger.IsDebugEnabled) logger.DebugFormat("Node Recv mqtt => {0}", data);
                return;
            }

            bool hasNewService = false;

            foreach (var item in resp_q.Data)
            {
                foreach (var svr in item.Value)
                {
                    if (_nodeServers.TryAdd(svr.ServiceCode, svr))
                    {
                        _mqttControl?.Subscribe(svr.DownChannel);
                        _svrTopics.TryAdd(svr.DownChannel, svr);
                        hasNewService = true;
                    }
                }
            }

            if (hasNewService)
            {
                if (logger.IsInfoEnabled) logger.Info("MQTT Registed ok");
                _mqttRegistedEvent?.Invoke(_nodeThis, EventArgs.Empty);
                Status = MqttNodeClientStatus.Registed;
            }
        }

        private void ProcessServiceMessage(MQTTNodeService svr, string data)
        {
            try
            {
                var resp = JsonConvert.DeserializeObject<MQTTBaseResponse>(data);
                if (logger.IsDebugEnabled)
                    logger.DebugFormat("Recv {0} => {1}", svr.ServiceName, JsonConvert.SerializeObject(resp));

                svr.SetResponse(resp);
            }
            catch (Exception ex)
            {
                if (logger.IsErrorEnabled)
                    logger.ErrorFormat("Process service message failed. Service:{0}, Error:{1}",
                        svr.ServiceName, ex.Message);
            }
        }

        /// <summary>
        /// 获取Flow服务节点
        /// </summary>
        public MQTTFlowDeviceNode? GetFlowService()
        {
            const string svrCode = "SVR.Flow.Default";

            if (_nodeServers.TryGetValue(svrCode, out var node) && _nodeThis != null)
            {
                var device = node.Devices?.FirstOrDefault().Value;
                if (device != null)
                {
                    return new MQTTFlowDeviceNode(
                        _nodeThis.RCName,
                        -1,
                        node.ServiceType,
                        node.ServiceCode,
                        node.ServiceName,
                        node.ServiceToken,
                        device.Code,
                        node.RequestManager);
                }
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

                var data = JsonConvert.SerializeObject(new MQTTNodeServiceRegist(_nodeThis));
                _mqttControl?.Publish(_nodeThis.RCRegTopic, data);
            }
            catch (Exception ex)
            {
                if (logger.IsErrorEnabled) logger.Error("Regist failed", ex);
            }
        }

        private void ClearCache()
        {
            _nodeThis?.Reset();
            _nodeServers.Clear();
            _svrTopics.Clear();
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
        public void Publish(string topic, MQTTCVRequestHeader request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Publish(topic, JsonConvert.SerializeObject(request));
        }

        /// <summary>
        /// 获取所有服务
        /// </summary>
        public List<MQTTServiceMO> GetAllServices()
        {
            var services = new List<MQTTServiceMO>(_nodeServers.Count);

            foreach (var service in _nodeServers.Values)
            {
                var svrMO = new MQTTServiceMO(
                    service.ServiceType,
                    service.ServiceCode,
                    service.DownChannel,
                    service.UpChannel,
                    service.ServiceToken);

                if (service.Devices != null)
                {
                    foreach (var dev in service.Devices)
                    {
                        svrMO.Devices.TryAdd(dev.Key, new MQTTDeviceMO()
                        {
                            DeviceCode = dev.Value.Code
                        });
                    }
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