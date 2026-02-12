using CVWaferProber.MQTT;

namespace CVWaferProber.Services
{
    public interface IFlowService
    {
        Task<MQTTBaseResponse?> FowRunAndWaitResponseAsync(int flowId, string flowName, string serialNumber, TimeSpan? timeout = null);
        void Reconnect();
    }
}
