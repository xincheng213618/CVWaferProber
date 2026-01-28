using System;
using System.Threading.Tasks;

namespace WaferComm.StateMachine
{
    /// <summary>
    /// 状态机接口
    /// </summary>
    public interface IStateMachine : IDisposable
    {
        /// <summary>
        /// 当前状态
        /// </summary>
        ProberState CurrentState { get; }

        /// <summary>
        /// 启动状态机
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 停止状态机
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 触发状态转换
        /// </summary>
        Task<bool> TransitionToAsync(ProberState newState, object data = null);

        /// <summary>
        /// 获取状态信息
        /// </summary>
        ProberStatus GetStatus();
        MotionStatistics GetMotionStatistics();
        void ResetMotionMonitor();
        TemperatureStatistics GetTemperatureStatistics();
        Task StartHeaterMonitorAsync();
        Task StopHeaterMonitorAsync();
        void ReloadSettings(int defaultXYMotionTimeout, int defaultZMotionTimeout);
    }
}