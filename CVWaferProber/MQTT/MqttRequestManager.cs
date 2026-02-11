using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CVWaferProber.MQTT
{
    public class MqttRequestManager
    {
        // 存储等待中的请求，key为SerialNumber
        private readonly ConcurrentDictionary<string, TaskCompletionSource<MQTTBaseResponse>> _pendingRequests
            = new ConcurrentDictionary<string, TaskCompletionSource<MQTTBaseResponse>>();

        // 超时时间（默认10秒）
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 创建等待任务
        /// </summary>
        public Task<MQTTBaseResponse> WaitForResponseAsync(string serialNumber, TimeSpan? timeout = null)
        {
            var tcs = new TaskCompletionSource<MQTTBaseResponse>();

            if (!_pendingRequests.TryAdd(serialNumber, tcs))
            {
                return Task.FromException<MQTTBaseResponse>(
                    new InvalidOperationException($"已存在相同的请求序列号: {serialNumber}"));
            }

            // 设置超时
            var cancellationToken = new CancellationTokenSource(timeout ?? _defaultTimeout);
            cancellationToken.Token.Register(() =>
            {
                if (_pendingRequests.TryRemove(serialNumber, out var tcs))
                {
                    tcs.TrySetException(new TimeoutException($"请求超时: {serialNumber}"));
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// 设置响应
        /// </summary>
        public bool SetResponse(MQTTBaseResponse response)
        {
            if (string.IsNullOrEmpty(response?.SerialNumber))
                return false;

            if (_pendingRequests.TryRemove(response.SerialNumber, out var tcs))
            {
                return tcs.TrySetResult(response);
            }

            return false;
        }

        /// <summary>
        /// 设置异常
        /// </summary>
        public bool SetException(string serialNumber, Exception exception)
        {
            if (_pendingRequests.TryRemove(serialNumber, out var tcs))
            {
                return tcs.TrySetException(exception);
            }
            return false;
        }
    }
}
