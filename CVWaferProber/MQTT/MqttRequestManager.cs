using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CVWaferProber.MQTT
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
                    Tcs.TrySetException(new TimeoutException($"请求超时 ({timeout.TotalSeconds}秒)"));
                });
            }
        }

        public Task<MQTTBaseResponse> WaitForResponseAsync(string serialNumber, TimeSpan? timeout = null)
        {
            var waiter = new RequestWaiter(timeout ?? _defaultTimeout);

            if (!_pendingRequests.TryAdd(serialNumber, waiter))
            {
                return Task.FromException<MQTTBaseResponse>(
                    new InvalidOperationException($"已存在相同的请求序列号: {serialNumber}"));
            }

            return waiter.Tcs.Task;
        }

        public bool SetResponse(MQTTBaseResponse response)
        {
            if (string.IsNullOrEmpty(response?.SerialNumber))
                return false;

            if (response.IsPending())
                return false;

            if (_pendingRequests.TryRemove(response.SerialNumber, out var waiter))
            {
                waiter.CancellationTokenSource?.Dispose();
                return waiter.Tcs.TrySetResult(response);
            }

            return false;
        }

        public bool SetException(string serialNumber, Exception exception)
        {
            if (_pendingRequests.TryRemove(serialNumber, out var waiter))
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
                    waiter.Tcs.TrySetException(new OperationCanceledException("请求管理器已清空"));
                }
            }
        }
    }
}
