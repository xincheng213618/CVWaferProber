using CVCommCore;
using WaferComm.Client;
using WaferComm.Core;

namespace CVWaferProber.Core.Events
{
    public class CVWPEventAggregatorInstance : ReflectionSingleton<CVWPEventAggregatorInstance>, IEventAggregator
    {

        private readonly IEventAggregator eventAggregator;

        private CVWPEventAggregatorInstance()
        {
            this.eventAggregator = new EventAggregator();
        }

        public void Publish<TEvent>(TEvent @event) where TEvent : class
        {
            eventAggregator.Publish(@event);
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            eventAggregator.Subscribe(handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            eventAggregator.Unsubscribe(handler);
        }
    }
}
