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
        /// 是否正在执行移动指令（移动中禁止其他操作）
        /// </summary>
        bool IsMoving { get; }

        /// <summary>
        /// 连接服务器
        /// </summary>
        Task ConnectAsync(string ip, int port);
        /// <summary>
        /// 尝试连接服务器
        /// </summary>
        Task<bool> TryConnectAsync(string ip, int port);

        /// <summary>
        /// 断开连接
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 发送指令
        /// </summary>
        Task SendCommandAsync(string command);

        /// <summary>
        /// 发送移动指令并等待到位确认（$67#）
        /// 在等待期间 IsMoving=true，其他指令将被阻止
        /// </summary>
        /// <param name="command">移动指令</param>
        /// <param name="timeoutSeconds">超时时间（秒），默认60秒</param>
        /// <returns>true=到位成功，false=超时</returns>
        Task<bool> SendMoveCommandAndWaitAsync(string command, int timeoutSeconds = 60);

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

        Task ZToMainCameraCheckAsync();
        Task ZToAuxCameraCheckAsync();
        Task ZToIntegratingSphereAsync();
        Task SendResultAsync(int result);
        Task GetCurrentDieAxisAsync();
    }
}