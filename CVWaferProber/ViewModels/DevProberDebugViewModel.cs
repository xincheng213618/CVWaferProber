using CVWaferProber.Core.ViewModels;
using MySqlX.XDevAPI;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WaferComm.Client;
using WaferComm.Core;

namespace CVWaferProber.ViewModels
{
    public class DevProberDebugViewModel : ViewModelBase
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(DevProberDebugViewModel));

        public ICommand DevProberConnectCommand { get; }
        public ICommand DevProberDisconnectCommand { get; }

        public string SvrIP { get; set; } = "127.0.0.1";
        public string SvrPort { get; set; } = "8898";

        private IWaferProberClient _client;

        public DevProberDebugViewModel()
        {
            DevProberConnectCommand = new RelayCommand(DevProberConnect);
            DevProberDisconnectCommand = new RelayCommand(DevProberDisconnect);
            InitializeClient();
        }
        private void InitializeClient()
        {
            try
            {
                // 创建事件聚合器
                var eventAggregator = new EventAggregator();

                // 创建客户端
                _client = new WaferProberTCPClient(eventAggregator);

                AddLog("客户端初始化完成", LogType.Info);
            }
            catch (Exception ex)
            {
                AddLog($"初始化失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"客户端初始化失败: {ex.Message}");
            }
        }
        private void DevProberDisconnect(object obj)
        {
        }

        private async void DevProberConnect(object obj)
        {
            try
            {
                string ip = SvrIP.Trim();
                if (string.IsNullOrEmpty(ip))
                {
                    MessageBox.Show("请输入服务器IP地址");
                    return;
                }

                if (!int.TryParse(SvrPort, out int port))
                {
                    MessageBox.Show("请输入有效的端口号");
                    return;
                }

                await _client.ConnectAsync(ip, port);
            }
            catch (Exception ex)
            {
                AddLog($"连接失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"连接失败: {ex.Message}");
            }
        }

        private enum LogType
        {
            Info,
            Send,
            Receive,
            Success,
            Warning,
            Error
        }
        private void AddLog(string message, LogType type = LogType.Info)
        {
        }
    }
}
