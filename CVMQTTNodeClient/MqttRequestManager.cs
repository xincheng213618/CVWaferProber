using System.Collections.Concurrent;

namespace CVMQTTNodeClient
{
    public class MqttRequestManager
    {
        private readonly ConcurrentDictionary<string, RequestWaiter> _pendingRequests
            = new ConcurrentDictionary<string, RequestWaiter>();

        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(60);

        private class RequestWaiter
        {
            public TaskCompletionSource<MQTTBaseResponse> Tcs { get; } = new TaskCompletionSource<MQTTBaseResponse>();
            public CancellationTokenSource CancellationTokenSource { get; }
            public DateTime CreateTime { get; } = DateTime.Now;

            public RequestWaiter(TimeSpan timeout)
            {
                CancellationTokenSource = new CancellationTokenSource(timeout);
                CancellationTokenSource.Token.Register(() =>
                {
                    Tcs.TrySetException(new TimeoutException($"Request timeout ({timeout.TotalSeconds} sec)"));
                });
            }
        }

        public Task<MQTTBaseResponse> WaitForResponseAsync(string msgId, TimeSpan? timeout = null)
        {
            var waiter = new RequestWaiter(timeout ?? _defaultTimeout);

            if (!_pendingRequests.TryAdd(msgId, waiter))
            {
                return Task.FromException<MQTTBaseResponse>(
                    new InvalidOperationException($"Duplicate request serial number: {msgId}"));
            }

            return waiter.Tcs.Task;
        }

        public bool SetResponse(MQTTBaseResponse? response)
        {
            if (string.IsNullOrEmpty(response?.MsgId))
                return false;

            if (response.IsPending())
                return false;

            if (_pendingRequests.TryRemove(response.MsgId, out var waiter))
            {
                waiter.CancellationTokenSource?.Dispose();
                return waiter.Tcs.TrySetResult(response);
            }

            return false;
        }

        public bool SetException(string msgId, Exception exception)
        {
            if (_pendingRequests.TryRemove(msgId, out var waiter))
            {
                waiter.CancellationTokenSource?.Dispose();
                return waiter.Tcs.TrySetException(exception);
            }
            return false;
        }

        public void Clear()
        {
            foreach (var item in _pendingRequests)
            {
                if (_pendingRequests.TryRemove(item.Key, out var waiter))
                {
                    waiter.CancellationTokenSource?.Dispose();
                    waiter.Tcs.TrySetException(new OperationCanceledException("Request manager cleared."));
                }
            }
        }
    }
}
