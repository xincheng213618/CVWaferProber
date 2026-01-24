using CVCommCore;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.StateMachine;

namespace CVWaferProber.Services
{
    public class ProberClientService : ReflectionSingleton<ProberClientService>
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ProberClientService));

        private IWaferProberClient? _clientProber;
        private IStateMachine? _proberState;
        
        private ProberClientService()
        {
        }

        public void Initialize(IWaferProberClient clientProber, IStateMachine proberState)
        {
            this._clientProber = clientProber;
            this._proberState = proberState;

            var eventAggregator = _clientProber.EventAggregator;
            eventAggregator.Subscribe<ConnectionStateChangedEvent>(OnClientProberStateChanged);
            eventAggregator.Subscribe<StateTransitionEvent>(OnStateTransition);
            //
            eventAggregator.Subscribe<StateUpdatedEvent>(OnProberStateUpdated);
        }

        private void OnProberStateUpdated(StateUpdatedEvent @event)
        {
        }

        private void OnStateTransition(StateTransitionEvent @event)
        {
        }

        private void OnClientProberStateChanged(ConnectionStateChangedEvent @event)
        {
        }
    }
}
