using System;
using System.Threading.Tasks;
using WaferComm.Core;

namespace WaferComm.Client
{
    /// <summary>
    /// 晶圆台客户端接口
    /// </summary>
    public interface IWaferProberClient : IDisposable
    {
        /// <summary>
        /// 事件聚合器
        /// </summary>
        IEventAggregator EventAggregator { get; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接服务器
        /// </summary>
        Task ConnectAsync(string ip, int port);
        /// <summary>
        /// 尝试连接服务器
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        Task<bool> TryConnectAsync(string ip, int port);

        /// <summary>
        /// 断开连接
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 发送指令
        /// </summary>
        Task SendCommandAsync(string command);

        // 预定义指令方法
        Task GetMachineNumberAsync();
        Task GetWaferIdAsync();
        Task GetFirstTestPositionAsync();
        Task GetCurrentPositionAsync();
        Task GetLotNumberAsync();
        Task SetTemperatureAsync(decimal temperature);
        Task GetCurrentTemperatureAsync();
        Task GetWaferInfoAsync();
        Task GetTotalDiceCountAsync();
        Task GetProbePressureAsync();
        Task StartTestConfirmAsync();
        Task GetZDownStatusAsync();
        Task GetHeaterStatusAsync();
        Task GetTestCompletionStatusAsync();
        Task MoveAbsoluteAsync(string y, string x);
        Task MoveRelativeAsync(string y, string x);
        Task MoveMicroAsync(string y, string x);
        Task StopAsync();
        Task SendHeartbeatAsync();
        Task QueryStatusAsync();
        Task GetMappingAsync();
        Task ZAllUpAsync();
        Task ZToMainCameraAsync();
        Task ZToAuxCameraAsync();
        Task ZToIntegratingSphereAsync();
        Task SendResultAsync(int result);
        Task GetCurrentDieAxisAsync();
    }
}