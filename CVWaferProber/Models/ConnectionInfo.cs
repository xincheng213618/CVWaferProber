using CVWaferProber.Core.ViewModels;
using WaferComm.StateMachine;

namespace CVWaferProber.Models
{
    public class ConnectionInfo : ViewModelBase
    {
        public readonly string ConnectedMsg;
        public readonly string DisconnectedMsg;
        private string _serverIP = "127.0.0.1";
        //private string _serverIP = "192.168.1.100";
        private int _port = 8898;
        private string _serverTipInfo = "127.0.0.1:8898";
        private ConnectionStatus _status = ConnectionStatus.Disconnected;
        private string _statusMessage = "Disconnected";
        private string _devStatusMessage = "Disconnected";
        private ProberState _devCurrentState;

        public ConnectionInfo(string connectedMsg = "Connected", string disconnectedMsg = "Disconnected")
        {
            ConnectedMsg = connectedMsg;
            DisconnectedMsg = disconnectedMsg;
        }

        public string ServerIP
        {
            get => _serverIP;
            set
            {
                SetProperty(ref _serverIP, value);
                this.ServerTipInfo = string.Format("{0}:{1}", _serverIP, _port);
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                SetProperty(ref _port, value);
                this.ServerTipInfo = string.Format("{0}:{1}", _serverIP, _port);
            }
        }

        public ConnectionStatus Status
        {
            get => _status;
            set {
                SetProperty(ref _status, value);
            }
        }
        public ProberState DevCurrentState
        {
            get => _devCurrentState;
            set {
                SetProperty(ref _devCurrentState, value);
                DevStatusMessage = value.ToString();
            }
        }
        public string ServerTipInfo 
        {
            get => _serverTipInfo;
            set => SetProperty(ref _serverTipInfo, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        public string DevStatusMessage
        {
            get => _devStatusMessage;
            set => SetProperty(ref _devStatusMessage, value);
        }

        public bool IsConnected => this.Status == ConnectionStatus.Connected;
        public void SetConnected(bool isConnected)
        {
            if (isConnected)
            {
                this.Status = ConnectionStatus.Connected;
                this.StatusMessage = ConnectedMsg;
                this.DevCurrentState = ProberState.Connected;
            }
            else
            {
                this.Status = ConnectionStatus.Disconnected;
                this.StatusMessage = DisconnectedMsg;
                this.DevCurrentState = ProberState.Disconnected;
            }
            this.ServerTipInfo = string.Format("{0}:{1}", _serverIP, _port);
        }
    }

    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }
}