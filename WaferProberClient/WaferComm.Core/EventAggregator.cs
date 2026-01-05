using System;
using System.Collections.Generic;
using System.Linq;
using WaferComm.Core;

namespace WaferComm.Core
{
    /// <summary>
    /// 事件聚合器实现
    /// </summary>
    public class EventAggregator : IEventAggregator
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        private readonly object _lock = new object();

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_handlers.ContainsKey(eventType))
                {
                    _handlers[eventType] = new List<Delegate>();
                }
                _handlers[eventType].Add(handler);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (_handlers.ContainsKey(eventType))
                {
                    _handlers[eventType].Remove(handler);

                    if (_handlers[eventType].Count == 0)
                    {
                        _handlers.Remove(eventType);
                    }
                }
            }
        }

        public void Publish<TEvent>(TEvent @event) where TEvent : class
        {
            List<Delegate> handlers;
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_handlers.ContainsKey(eventType))
                    return;

                handlers = _handlers[eventType].ToList();
            }

            foreach (var handler in handlers)
            {
                try
                {
                    (handler as Action<TEvent>)?.Invoke(@event);
                }
                catch
                {
                    // 忽略单个处理器的错误
                }
            }
        }
    }
}