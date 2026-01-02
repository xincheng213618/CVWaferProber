using CVWaferProber.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Services
{
    public interface ITcpClientService
    {
        event EventHandler<ConnectionStatus> ConnectionStatusChanged;
        event EventHandler<string> DataReceived;
        event EventHandler<string> StatusMessage;

        ConnectionInfo ConnectionInfo { get; }
        bool IsConnected { get; }

        Task<bool> ConnectAsync(string serverIP, int port);
        void Disconnect();
        Task SendAsync(string message);
    }
}
