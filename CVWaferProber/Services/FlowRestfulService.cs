using ColorVision.Core.Message.Response;
using CVWaferProber.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using WaferComm.Core;

namespace CVWaferProber.Services
{
    public class FlowRestfulService : IFlowService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(FlowRestfulService));

        private EventAggregator eventAggregator;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(60);
        private readonly RestfulAPIService restfulAPI;

        public ConnectionInfo ConnectionInfo { get; private set; }
        public bool IsRegistered => restfulAPI.IsRegistered;

        public FlowRestfulService()
        {
            this.ConnectionInfo = new ConnectionInfo("Registed", "UnRegisted") { ServerIP = "127.0.0.1", Port = 8080 };
            this.eventAggregator = new EventAggregator();
            this.restfulAPI = new RestfulAPIService(ConnectionInfo.ServerIP, ConnectionInfo.Port);
            this.restfulAPI.ConnectionStatusChanged += RestfulAPI_ConnectionStatusChanged;
        }

        private void RestfulAPI_ConnectionStatusChanged(object? sender, RestfulAPIService.ConnectionStatus e)
        {
            int iStatus = (int)e;
            ConnectionStatus status = (ConnectionStatus)iStatus;
            PublishStatus(status);
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            eventAggregator.Subscribe(handler);
        }     
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            eventAggregator.Unsubscribe(handler);
        }
        public void RcUnRegist()
        {
            restfulAPI.RcUnRegist();
        }
        private void PublishStatus(ConnectionStatus status)
        {
            ConnectionInfo.SetConnected(status == ConnectionStatus.Connected);
            eventAggregator.Publish(new ConnectionStateChangedEvent(ConnectionInfo.IsConnected, ConnectionInfo.ServerIP, ConnectionInfo.Port));
        }
        public bool RcRegist()
        {
            return restfulAPI.RcRegist();
        }

        public async Task<DeviceResponseMessageHeader?> FlowRunAndWaitResponseAsync(int flowId, string flowName, string serialNumber, TimeSpan? timeout = null)
        {
            // 参数验证
            if (string.IsNullOrWhiteSpace(flowName))
                throw new ArgumentException("Flow name cannot be null or whitespace", nameof(flowName));

            if (string.IsNullOrWhiteSpace(serialNumber))
                throw new ArgumentException("Serial number cannot be null or whitespace", nameof(serialNumber));

            var effectiveTimeout = timeout ?? _defaultTimeout;

            using var cancellationTokenSource = new CancellationTokenSource(effectiveTimeout);

            try
            {
                var flowResult = await restfulAPI.RunFlowAsync(
                    flowName,
                    serialNumber,
                    cancellationTokenSource)
                    .ConfigureAwait(false);

                return flowResult?.IsSuccess == true
                    ? DeviceResponseMessageHeader.OK()
                    : DeviceResponseMessageHeader.Failed();
            }
            catch (OperationCanceledException)
            {
                // 超时处理 - 可以根据需要记录日志
                if (logger.IsErrorEnabled) logger.ErrorFormat("Flow execution timed out for FlowName: {0}, SerialNumber: {1} => {2}", flowName, serialNumber, effectiveTimeout.ToString());
                return DeviceResponseMessageHeader.Failed();
            }
            catch (Exception ex)
            {
                // 其他异常处理 - 可以根据需要记录日志
                if (logger.IsErrorEnabled) logger.ErrorFormat("Flow execution exception for FlowName: {0}, SerialNumber: {1} => {2}", flowName, serialNumber, ex.Message);
                return DeviceResponseMessageHeader.Failed();
            }
        }
        public void Reconnect()
        {
            restfulAPI.RcRegist();
        }
    }
}
