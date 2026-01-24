using CVWaferProber.Core.ViewModels;
using System.Windows.Input;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.StateMachine;

namespace CVWaferProber.ViewModels
{
    public class ToolsBarViewModel : ViewModelBase
    {
        private readonly IWaferProberClient? _client;
        private readonly IStateMachine? _proberState;

        public ICommand LiftAllCommand { get; }
        public ICommand ToMainCameraCommand { get; }
        public ICommand ToAuxCameraCommand { get; }
        public ICommand ToIntegratingSphereCommand { get; }
        public ICommand StartAutoTestCommand { get; }
        public ICommand ContinuAutoTestCommand { get; }
        public ICommand StopAutoTestCommand { get; }
        public ICommand PauseAutoTestCommand { get; }

        public bool CanLiftAll => _proberState.CurrentState == ProberState.WaferLoaded ||
            _proberState.CurrentState == ProberState.Stoped;
        public bool CanToMainCamera => _proberState.CurrentState == ProberState.WaferLoaded || 
            _proberState.CurrentState == ProberState.Stoped;
        public bool CanToAuxCamera => _proberState.CurrentState == ProberState.WaferLoaded ||
            _proberState.CurrentState == ProberState.Stoped;
        public bool CanToIntegratingSphere => _proberState.CurrentState == ProberState.WaferLoaded ||
            _proberState.CurrentState == ProberState.Stoped;
        public bool CanStartAutoTest => _proberState.CurrentState == ProberState.WaferLoaded ||
            _proberState.CurrentState == ProberState.Stoped;
        public bool CanContinuAutoTest => _proberState.CurrentState == ProberState.Paused;
        public bool CanStopAutoTest => _proberState.CurrentState == ProberState.Testing ||
            _proberState.CurrentState == ProberState.Paused || _proberState.CurrentState == ProberState.Stoped;
        public bool CanPauseAutoTest => _proberState.CurrentState == ProberState.Testing;

        private IEventAggregator? _EventAggregator;

        public event EventHandler ToIntegratingSpherePos;
        public event EventHandler ToAuxCameraPos;
        public event EventHandler ToMainCameraPos;
        public event EventHandler LiftAllPos;

        public ToolsBarViewModel(IWaferProberClient client, IStateMachine? proberState, IEventAggregator? eventAggregator = null)
        {
            _client = client;
            _proberState = proberState;
            _client.EventAggregator.Subscribe<ZAxisPosChangedEvent>(OnZAxisPosChanged);

            _EventAggregator = eventAggregator;
            LiftAllCommand = new RelayCommand(
                 _ => LiftAll(),
                _ => CanLiftAll);
            ToMainCameraCommand = new RelayCommand(
                _ => ToMainCamera(),
                _ => CanToMainCamera);
            ToAuxCameraCommand = new RelayCommand(
                _ => ToAuxCamera(),
                 _ => CanToAuxCamera);
            ToIntegratingSphereCommand = new RelayCommand(
                _ => ToIntegratingSphere(),
                _ => CanToIntegratingSphere);

            StartAutoTestCommand = new RelayCommand(
                _ => StartAutoTest(),
                _ => CanStartAutoTest);
            ContinuAutoTestCommand = new RelayCommand(
                _ => ContinuAutoTest(),
                _ => CanContinuAutoTest);
            StopAutoTestCommand = new RelayCommand(
                _ => StopAutoTest(),
                _ => CanStopAutoTest);
            PauseAutoTestCommand = new RelayCommand(
                _ => PauseAutoTest(),
                _ => CanPauseAutoTest);
        }

        private void PauseAutoTest()
        {
            MainViewModel.Instance.PauseAutoFlow();
        }

        private void StopAutoTest()
        {
            MainViewModel.Instance.StopAutoFlow();
        }

        private void ContinuAutoTest()
        {
            MainViewModel.Instance.ContinuAutoFlow();
        }

        private void StartAutoTest()
        {
            MainViewModel.Instance.StartAutoFlow();
        }

        private void OnZAxisPosChanged(ZAxisPosChangedEvent @event)
        {
        }

        private async void ToIntegratingSphere()
        {
            await _client?.ZToIntegratingSphereAsync();
            foreach (var item in MainViewModel.Instance.WPFlows)
            {
                if (item.FlowType == CVWaferProberFlowType.EQE)
                {
                    MainViewModel.Instance.SelectedWPFlow = item;
                }
            }
        }

        private async void ToAuxCamera()
        {
            await _client?.ZToAuxCameraAsync();
            foreach (var item in MainViewModel.Instance.WPFlows)
            {
                if (item.FlowType == CVWaferProberFlowType.VAM)
                {
                    MainViewModel.Instance.SelectedWPFlow = item;
                }
            }
        }

        private async void ToMainCamera()
        {
            await _client?.ZToMainCameraAsync();
            foreach (var item in MainViewModel.Instance.WPFlows)
            {
                if (item.FlowType == CVWaferProberFlowType.AOI)
                {
                    MainViewModel.Instance.SelectedWPFlow = item;
                }
            }
        }

        private async void LiftAll()
        {
           await _client?.ZAllUpAsync();
        }
    }
}
