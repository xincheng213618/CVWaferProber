using CVMQTTNodeClient;

namespace CVWaferProber.Services
{
    public interface IFlowService
    {
        Task<CVMQTTBaseResponse?> FlowRunAndWaitResponseAsync(int flowId, string flowName, string serialNumber, TimeSpan? timeout = null);
        void Reconnect();
    }
}
