using ColorVision.Core.Message.Response;

namespace CVWaferProber.Services
{
    public interface IFlowService
    {
        Task<DeviceResponseMessageHeader?> FlowRunAndWaitResponseAsync(int flowId, string flowName, string serialNumber, TimeSpan? timeout = null);
        void Reconnect();
    }
}
