using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CVWaferProber.WinMsg
{
    /// <summary>
    /// Windows 消息处理类
    /// </summary>
    public class WindowMessageProcessor : IDisposable
    {
        private HwndSource _hwndSource;
        private IntPtr _windowHandle;
        private readonly Dictionary<int, List<MessageHandler>> _messageHandlers;
        private bool _disposed = false;
        private bool _dispatch = false;

        /// <summary>
        /// 窗口句柄
        /// </summary>
        public IntPtr Handle => _windowHandle;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _hwndSource != null;

        // 常用 Windows 消息常量
        public static class Messages
        {
            public const int WM_USER = 0x0400;

            public const int WM_CLOSE = 0x0010;
            public const int WM_DESTROY = 0x0002;
            //public const int WM_SIZE = 0x0005;
            //public const int WM_KEYDOWN = 0x0100;
            //public const int WM_KEYUP = 0x0101;
            //public const int WM_LBUTTONDOWN = 0x0201;
            //public const int WM_LBUTTONUP = 0x0202;
            //public const int WM_MOUSEMOVE = 0x0200;
        }

        /// <summary>
        /// 消息处理器委托
        /// </summary>
        /// <param name="wParam">wParam 参数</param>
        /// <param name="lParam">lParam 参数</param>
        /// <param name="handled">是否已处理</param>
        /// <returns>处理结果</returns>
        public delegate IntPtr MessageHandler(IntPtr wParam, IntPtr lParam, ref bool handled);

        /// <summary>
        /// 消息处理器带发送者委托
        /// </summary>
        public delegate IntPtr MessageHandlerWithSender(object sender, IntPtr wParam, IntPtr lParam, ref bool handled);

        /// <summary>
        /// 消息到达事件
        /// </summary>
        public event MessageHandlerWithSender MessageReceived;

        public WindowMessageProcessor()
        {
            _messageHandlers = new Dictionary<int, List<MessageHandler>>();
        }
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    ClearAllHandlers();
                }

                // 释放非托管资源
                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource.Dispose();
                    _hwndSource = null;
                }

                _disposed = true;
            }
        }
      
        ~WindowMessageProcessor()
        {
            Dispose(false);
        }
        /// <summary>
        /// 初始化消息处理器
        /// </summary>
        /// <param name="window">WPF 窗口</param>
        /// <exception cref="ArgumentNullException">窗口为空时抛出</exception>
        public void Initialize(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            if (IsInitialized)
                throw new InvalidOperationException((string)System.Windows.Application.Current.FindResource("Hasbeeninitialized"));

            //window.SourceInitialized += (s, e) =>
            //{
                _windowHandle = new WindowInteropHelper(window).Handle;
                _hwndSource = HwndSource.FromHwnd(_windowHandle);
                _hwndSource.AddHook(WndProc);

                // 触发初始化完成事件
                Initialized?.Invoke(this, EventArgs.Empty);
            //};

            window.Closed += (s, e) => Dispose();
        }

        /// <summary>
        /// 初始化完成事件
        /// </summary>
        public event EventHandler Initialized;
        /// <summary>
        /// 注册消息处理器
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="handler">消息处理器</param>
        public void RegisterMessageHandler(int messageId, MessageHandler handler)
        {
            if (!_messageHandlers.ContainsKey(messageId))
            {
                _messageHandlers[messageId] = new List<MessageHandler>();
            }

            if (!_messageHandlers[messageId].Contains(handler))
            {
                _messageHandlers[messageId].Add(handler);
            }
        }

        /// <summary>
        /// 注销消息处理器
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <param name="handler">消息处理器</param>
        public void UnregisterMessageHandler(int messageId, MessageHandler handler)
        {
            if (_messageHandlers.ContainsKey(messageId))
            {
                _messageHandlers[messageId].Remove(handler);
                if (_messageHandlers[messageId].Count == 0)
                {
                    _messageHandlers.Remove(messageId);
                }
            }
        }

        /// <summary>
        /// 注册自定义消息处理器
        /// </summary>
        /// <param name="customMessageId">自定义消息ID（从 WM_USER + 1 开始）</param>
        /// <param name="handler">消息处理器</param>
        public void RegisterCustomMessageHandler(int customMessageId, MessageHandler handler)
        {
            int messageId = /*Messages.WM_USER +*/ customMessageId;
            RegisterMessageHandler(messageId, handler);
        }

        /// <summary>
        /// 注销所有消息处理器
        /// </summary>
        public void ClearAllHandlers()
        {
            _messageHandlers.Clear();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 触发消息到达事件
            MessageReceived?.Invoke(this, wParam, lParam, ref handled);
            if (handled) return IntPtr.Zero;

            // 执行注册的消息处理器
            if (_dispatch && _messageHandlers.ContainsKey(msg))
            {
                foreach (var handler in _messageHandlers[msg])
                {
                    IntPtr result = handler(wParam, lParam, ref handled);
                    if (handled) return result;
                }
            }

            return IntPtr.Zero;
        }

        // Windows API 导入
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        /// <summary>
        /// 发送消息（同步，等待响应）
        /// </summary>
        public IntPtr SendMessage(int messageId, IntPtr wParam = default, IntPtr lParam = default)
        {
            if (!IsInitialized)
                throw new InvalidOperationException((string)System.Windows.Application.Current.FindResource("Notinitialized"));

            return SendMessage(_windowHandle, (uint)messageId, wParam, lParam);
        }

        /// <summary>
        /// 投递消息（异步，不等待响应）
        /// </summary>
        public bool PostMessage(int messageId, IntPtr wParam = default, IntPtr lParam = default)
        {
            if (!IsInitialized)
                throw new InvalidOperationException((string)System.Windows.Application.Current.FindResource("Notinitialized"));

            return PostMessage(_windowHandle, (uint)messageId, wParam, lParam);
        }

        /// <summary>
        /// 发送消息到指定窗口
        /// </summary>
        public static IntPtr SendMessageToWindow(IntPtr targetHandle, int messageId, IntPtr wParam = default, IntPtr lParam = default)
        {
            return SendMessage(targetHandle, (uint)messageId, wParam, lParam);
        }

        /// <summary>
        /// 投递消息到指定窗口
        /// </summary>
        public static bool PostMessageToWindow(IntPtr targetHandle, int messageId, IntPtr wParam = default, IntPtr lParam = default)
        {
            return PostMessage(targetHandle, (uint)messageId, wParam, lParam);
        }

        /// <summary>
        /// 注册系统范围的消息
        /// </summary>
        public static uint RegisterGlobalMessage(string messageName)
        {
            return RegisterWindowMessage(messageName);
        }

        /// <summary>
        /// 通过窗口标题查找窗口句柄
        /// </summary>
        public static IntPtr FindWindowByTitle(string windowTitle)
        {
            return FindWindow(null, windowTitle);
        }

        /// <summary>
        /// 注册系统常用消息的处理器
        /// </summary>
        public void RegisterSystemMessageHandlers(Window window)
        {
            // 窗口关闭处理
            RegisterMessageHandler(Messages.WM_CLOSE, MessageHandler_Close);
        }

        private IntPtr MessageHandler_Close(IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 可以在这里添加关闭前的验证逻辑
            Closing?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }

        public void StartRecvMsg()
        {
            _dispatch = true;
        }

        public void StopRecvMsg()
        {
            _dispatch = false;
        }

        // 常用事件
        public event EventHandler Closing;
    }
}
