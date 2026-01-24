using CVWaferProber.Core.ViewModels;
using System.Windows.Input;
using WaferComm.Client;
using WaferComm.Core;

namespace CVWaferProber.ViewModels
{
    public class ToolsViewModel : ViewModelBase
    {
        private readonly IWaferProberClient? _client;
        public ICommand LiftAllCommand { get; }
        public ICommand ToMainCameraCommand { get; }
        public ICommand ToAuxCameraCommand { get; }
        public ICommand ToIntegratingSphereCommand { get; }

        public bool CanLiftAll => true;
        public bool CanToMainCamera => true;
        public bool CanToAuxCamera => true;
        public bool CanToIntegratingSphere => true;

        private IEventAggregator? _EventAggregator;

        public event EventHandler ToIntegratingSpherePos;
        public event EventHandler ToAuxCameraPos;
        public event EventHandler ToMainCameraPos;
        public event EventHandler LiftAllPos;

        public ToolsViewModel(IWaferProberClient client, IEventAggregator? eventAggregator = null)
        {
            _client = client;
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
        }

        private void OnZAxisPosChanged(ZAxisPosChangedEvent @event)
        {
        }

        private async void ToIntegratingSphere()
        {
            await _client?.ZToIntegratingSphereAsync();
        }

        private async void ToAuxCamera()
        {
            await _client?.ZToAuxCameraAsync();
        }

        private async void ToMainCamera()
        {
            await _client?.ZToMainCameraAsync();
        }

        private async void LiftAll()
        {
           await _client?.ZAllUpAsync();
        }
    }
}
