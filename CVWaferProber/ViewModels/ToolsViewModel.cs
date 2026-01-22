using CVWaferProber.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WaferComm.Client;

namespace CVWaferProber.ViewModels
{
    public class ToolsViewModel : ViewModelBase
    {
        private readonly IWaferProberClient? _client;
        public ICommand LiftAllCommand { get; }
        public ICommand ToMainCameraCommand { get; }
        public ICommand ToAuxCameraCommand { get; }
        public ICommand ToIntegratingSphereCommand { get; }

        public ToolsViewModel(IWaferProberClient client)
        {
            _client = client;
            LiftAllCommand = new RelayCommand(_ => LiftAll());
            ToMainCameraCommand = new RelayCommand(_ => ToMainCamera());
            ToAuxCameraCommand = new RelayCommand(_ => ToAuxCamera());
            ToIntegratingSphereCommand = new RelayCommand(_ => ToIntegratingSphere());
        }

        private void ToIntegratingSphere()
        {
            _client?.ZToIntegratingSphereAsync();
        }

        private void ToAuxCamera()
        {
            _client?.ZToAuxCameraAsync();
        }

        private void ToMainCamera()
        {
            _client?.ZToMainCameraAsync();
        }

        private void LiftAll()
        {
            _client?.ZAllUpAsync();
        }
    }
}
