using System;

namespace WaferComm.Core
{
    /// <summary>
    /// 事件聚合器接口
    /// </summary>
    public interface IEventAggregator
    {
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
        void Publish<TEvent>(TEvent @event) where TEvent : class;
    }
}