using CVWaferProber.Core.ViewModels;
using System.Windows.Input;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.StateMachine;

namespace CVWaferProber.ViewModels
{
    public class ToolsBarViewModel : ViewModelBase
    {
        private readonly IWaferProberClient _client;
        private readonly IStateMachine _proberState;
        private readonly MainViewModel _mainVM;

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

        //private bool _CanContinuAutoTest;
        public bool CanStartAutoTest => _proberState.CurrentState == ProberState.WaferLoaded ||
            _proberState.CurrentState == ProberState.Stoped;
        public bool CanContinuAutoTest => _proberState.CurrentState == ProberState.Paused &&
            _mainVM.IsNotProcessing;
        public bool CanStopAutoTest => _mainVM.IsNotProcessing && 
            (_proberState.CurrentState == ProberState.Testing || _proberState.CurrentState == ProberState.WaferLoaded ||
            _proberState.CurrentState == ProberState.Paused || _proberState.CurrentState == ProberState.Stoped);
        public bool CanPauseAutoTest => _proberState.CurrentState == ProberState.Testing;

        public ToolsBarViewModel(MainViewModel mainVM,IWaferProberClient client, IStateMachine proberState, IEventAggregator? eventAggregator = null)
        {
            _mainVM = mainVM;
            _client = client;
            _proberState = proberState;
            _client.EventAggregator.Subscribe<ZAxisPosChangedEvent>(OnZAxisPosChanged);

            //_EventAggregator = eventAggregator;
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
            _mainVM.PauseAutoFlow();
        }

        private void StopAutoTest()
        {
            _mainVM.StopAutoFlow();
        }

        private void ContinuAutoTest()
        {
            _mainVM.ContinuAutoFlow();
        }

        private void StartAutoTest()
        {
            _mainVM.StartAutoFlow();
        }

        private void OnZAxisPosChanged(ZAxisPosChangedEvent @event)
        {
        }

        private async void ToIntegratingSphere()
        {
            await _client?.ZToIntegratingSphereAsync();
            _mainVM?.ToIntegratingSphere();
        }

        private async void ToAuxCamera()
        {
            await _client?.ZToAuxCameraAsync();
            _mainVM?.ToAuxCamera();
        }

        private async void ToMainCamera()
        {
            await _client?.ZToMainCameraAsync();
            _mainVM?.ToMainCamera();
        }

        private async void LiftAll()
        {
           await _client?.ZAllUpAsync();
        }

        public void FireUI()
        {
            this.OnPropertyChanged(nameof(ContinuAutoTestCommand));
            this.OnPropertyChanged(nameof(PauseAutoTestCommand));
            this.OnPropertyChanged(nameof(CanPauseAutoTest));
            this.OnPropertyChanged(nameof(CanContinuAutoTest));
        }
    }
}
