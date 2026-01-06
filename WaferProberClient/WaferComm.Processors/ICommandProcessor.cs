using System.Threading.Tasks;

namespace WaferComm.Processors
{
    /// <summary>
    /// 指令处理器接口
    /// </summary>
    public interface ICommandProcessor : IDisposable
    {
        /// <summary>
        /// 启动处理器
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 停止处理器
        /// </summary>
        Task StopAsync();
    }
}