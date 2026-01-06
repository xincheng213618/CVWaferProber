using System;
using System.Threading.Tasks;
using WaferComm.Core;

namespace WaferComm.Processors
{
    /// <summary>
    /// 指令处理器基类
    /// </summary>
    public abstract class CommandProcessorBase : ICommandProcessor, IDisposable
    {
        protected readonly IEventAggregator EventAggregator;
        protected bool _isStarted;

        protected CommandProcessorBase(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        }

        public virtual Task StartAsync()
        {
            if (_isStarted) return Task.CompletedTask;

            RegisterHandlers();
            _isStarted = true;

            return Task.CompletedTask;
        }

        public virtual Task StopAsync()
        {
            if (!_isStarted) return Task.CompletedTask;

            UnregisterHandlers();
            _isStarted = false;

            return Task.CompletedTask;
        }

        protected abstract void RegisterHandlers();
        protected abstract void UnregisterHandlers();

        public virtual void Dispose()
        {
            StopAsync().Wait();
        }
    }
}