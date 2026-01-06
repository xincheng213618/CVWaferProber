using System;

namespace WaferComm.Core
{
    /// <summary>
    /// 指令相关事件基类
    /// </summary>
    public abstract class CommandEvent
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public string Command { get; protected set; }
    }

    /// <summary>
    /// 发送指令事件
    /// </summary>
    public class CommandSentEvent : CommandEvent
    {
        public CommandSentEvent(string command)
        {
            Command = command;
        }
    }

    /// <summary>
    /// 接收指令事件
    /// </summary>
    public class CommandReceivedEvent : CommandEvent
    {
        public string RawData { get; }
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        public CommandReceivedEvent(string rawData)
        {
            RawData = rawData;
            Command = rawData;

            // 验证指令格式
            IsValid = ValidateCommand(rawData, out string error);
            ErrorMessage = error;

            if (IsValid)
            {
                Command = ParseCommand(rawData);
            }
        }

        private bool ValidateCommand(string command, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(command))
            {
                error = "指令为空";
                return false;
            }

            if (!command.StartsWith("$"))
            {
                error = "指令缺少起始符$";
                return false;
            }

            if (!command.EndsWith("#"))
            {
                error = "指令缺少结束符#";
                return false;
            }

            if (command.Length < 3) // 至少 $X#
            {
                error = "指令格式错误";
                return false;
            }

            return true;
        }

        private string ParseCommand(string rawData)
        {
            // 移除 $ 和 #
            return rawData.Trim('$', '#');
        }
    }

    /// <summary>
    /// 连接状态改变事件
    /// </summary>
    public class ConnectionStateChangedEvent
    {
        public bool IsConnected { get; }
        public string ServerIp { get; }
        public int Port { get; }

        public ConnectionStateChangedEvent(bool isConnected, string serverIp = null, int port = 0)
        {
            IsConnected = isConnected;
            ServerIp = serverIp;
            Port = port;
        }
    }

    /// <summary>
    /// 通信错误事件
    /// </summary>
    public class CommunicationErrorEvent
    {
        public string ErrorMessage { get; }
        public Exception Exception { get; }
        public string Context { get; }

        public CommunicationErrorEvent(string message, Exception ex = null, string context = null)
        {
            ErrorMessage = message;
            Exception = ex;
            Context = context;
        }
    }
}