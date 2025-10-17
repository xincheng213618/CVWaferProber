using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace GSWinMsgSim
{
    public partial class MainWindow : Window
    {
        // Windows消息常量
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_SYSCOMMAND = 0x0112;

        // 虚拟键码
        private const uint VK_RETURN = 0x0D;
        private const uint VK_SPACE = 0x20;
        private const uint VK_A = 0x41;

        public MainWindow()
        {
            InitializeComponent();
            AddLog("应用程序启动完成");
        }
        private void cmbMessageType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbMessageType.SelectedItem is ComboBoxItem selectedItem)
            {
                // 只有当选择"自定义"时才启用 cmbCustomMsg
                if (selectedItem.Content != null) cmbCustomMsg.IsEnabled = selectedItem.Content?.ToString() == "自定义";
            }
        }

        // 初始化时也要设置状态
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始状态设置
            cmbCustomMsg.IsEnabled = false;

            // 如果默认选中"自定义"，则启用
            if (cmbMessageType.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Content?.ToString() == "自定义")
            {
                cmbCustomMsg.IsEnabled = true;
            }
        }
        #region Windows API 声明
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        #region 数据结构
        public class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; }
            public string ClassName { get; set; }
            public uint ProcessId { get; set; }
        }
        #endregion

        #region 事件处理
        private void BtnFindWindows_Click(object sender, RoutedEventArgs e)
        {
            FindWindows();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            FindWindows();
        }

        private void LstWindows_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstWindows.SelectedItem is WindowInfo windowInfo)
            {
                txtStatus.Text = $"已选择: {windowInfo.Title} (句柄: 0x{windowInfo.Handle.ToInt64():X})";
            }
        }

        private void BtnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            SendCustomMessage();
        }

        private void BtnSendText_Click(object sender, RoutedEventArgs e)
        {
            SendTextToWindow();
        }

        private void BtnSendEnter_Click(object sender, RoutedEventArgs e)
        {
            SendEnterKey();
        }

        private void BtnSendClick_Click(object sender, RoutedEventArgs e)
        {
            SendMouseClick();
        }

        private void BtnCloseWindow_Click(object sender, RoutedEventArgs e)
        {
            CloseSelectedWindow();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            MinimizeSelectedWindow();
        }
        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            MaximizeSelectedWindow();
        }
        #endregion

        #region 核心功能
        private void FindWindows()
        {
            try
            {
                string searchText = txtWindowTitle.Text.Trim();
                lstWindows.Items.Clear();
                AddLog($"开始查找包含 '{searchText}' 的窗口...");

                List<WindowInfo> windows = new List<WindowInfo>();

                EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
                {
                    if (IsWindowVisible(hWnd))
                    {
                        StringBuilder titleBuilder = new StringBuilder(256);
                        GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);

                        StringBuilder classBuilder = new StringBuilder(256);
                        GetClassName(hWnd, classBuilder, classBuilder.Capacity);

                        uint processId;
                        GetWindowThreadProcessId(hWnd, out processId);

                        string title = titleBuilder.ToString();
                        string className = classBuilder.ToString();

                        // 如果搜索文本为空或标题包含搜索文本
                        if (string.IsNullOrEmpty(searchText) ||
                            title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            windows.Add(new WindowInfo
                            {
                                Handle = hWnd,
                                Title = title,
                                ClassName = className,
                                ProcessId = processId
                            });
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                foreach (var window in windows)
                {
                    lstWindows.Items.Add(window);
                }

                AddLog($"找到 {windows.Count} 个窗口");
                txtStatus.Text = $"找到 {windows.Count} 个窗口";
            }
            catch (Exception ex)
            {
                AddLog($"查找窗口时出错: {ex.Message}");
                MessageBox.Show($"查找窗口时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendCustomMessage()
        {
            if (lstWindows.SelectedItem is not WindowInfo selectedWindow)
            {
                MessageBox.Show("请先选择一个窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                uint message = 0;
                IntPtr wParam = IntPtr.Zero;
                IntPtr lParam = IntPtr.Zero;

                // 获取消息类型
                if (cmbMessageType.SelectedItem is ComboBoxItem item)
                {
                    string messageType = item.Content.ToString();
                    message = messageType switch
                    {
                        "WM_KEYDOWN" => WM_KEYDOWN,
                        "WM_KEYUP" => WM_KEYUP,
                        "WM_CHAR" => WM_CHAR,
                        "WM_LBUTTONDOWN" => WM_LBUTTONDOWN,
                        "WM_LBUTTONUP" => WM_LBUTTONUP,
                        "WM_CLOSE" => WM_CLOSE,
                        "WM_COMMAND" => WM_COMMAND,
                        "自定义" => ParseCustomMsg(),
                        _ => WM_KEYDOWN
                    };
                }

                // 解析参数
                if (uint.TryParse(txtWParam.Text, out uint wParamValue))
                {
                    wParam = new IntPtr(wParamValue);
                }
                else if (txtWParam.Text.StartsWith("0x"))
                {
                    wParam = new IntPtr(ParseHex(txtWParam.Text));
                }

                if (uint.TryParse(txtLParam.Text, out uint lParamValue))
                {
                    lParam = new IntPtr(lParamValue);
                }
                else if (txtLParam.Text.StartsWith("0x"))
                {
                    lParam = new IntPtr(ParseHex(txtLParam.Text));
                }
                WriteRowCol(int.Parse(txtRow.Text),int.Parse(txtCol.Text));

                // 发送消息
                IntPtr result = SendMessage(selectedWindow.Handle, message, wParam, lParam);

                AddLog($"发送消息到窗口 '{selectedWindow.Title}': " +
                      $"Msg=0x{message:X}, wParam=0x{wParam.ToInt64():X}, lParam=0x{lParam.ToInt64():X}, 结果=0x{result.ToInt64():X}");

                if(message == 0xBDF)
                {
                    if (int.TryParse(txtCol.Text, out int currentValue))
                    {
                        txtCol.Text = (currentValue - 1).ToString();
                    }
                    else
                    {
                        txtCol.Text = "-1";
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"发送消息时出错: {ex.Message}");
                MessageBox.Show($"发送消息时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private uint ParseCustomMsg()
        {
            uint uR = 0;
            // 获取选中的项
            var selectedItem = cmbCustomMsg.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string tagValue = selectedItem.Tag.ToString();

                // 根据不同的tag值计算实际的消息值
                if (tagValue == "WM_GS_SOT_MULTI_SITE")
                {
                    uR = 0x0400 + 2015;
                }
                else if (tagValue == "WM_STOP_GS_TEST")
                {
                    uR = 0x0400 + 2003;
                }
            }

            return uR;
        }

        private void SendTextToWindow()
        {
            if (lstWindows.SelectedItem is not WindowInfo selectedWindow)
            {
                MessageBox.Show("请先选择一个窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string text = Microsoft.VisualBasic.Interaction.InputBox("请输入要发送的文本:", "发送文本", "Hello World");
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    SetForegroundWindow(selectedWindow.Handle);

                    foreach (char c in text)
                    {
                        // 发送WM_CHAR消息
                        PostMessage(selectedWindow.Handle, WM_CHAR, new IntPtr(c), IntPtr.Zero);
                    }

                    AddLog($"已发送文本到窗口 '{selectedWindow.Title}': {text}");
                }
                catch (Exception ex)
                {
                    AddLog($"发送文本时出错: {ex.Message}");
                    MessageBox.Show($"发送文本时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SendEnterKey()
        {
            if (lstWindows.SelectedItem is not WindowInfo selectedWindow)
            {
                MessageBox.Show("请先选择一个窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 发送回车键按下和释放
                PostMessage(selectedWindow.Handle, WM_KEYDOWN, new IntPtr(VK_RETURN), IntPtr.Zero);
                PostMessage(selectedWindow.Handle, WM_KEYUP, new IntPtr(VK_RETURN), IntPtr.Zero);

                AddLog($"已发送回车键到窗口 '{selectedWindow.Title}'");
            }
            catch (Exception ex)
            {
                AddLog($"发送回车键时出错: {ex.Message}");
                MessageBox.Show($"发送回车键时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendMouseClick()
        {
            if (lstWindows.SelectedItem is not WindowInfo selectedWindow)
            {
                MessageBox.Show("请先选择一个窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 发送鼠标左键按下和释放
                PostMessage(selectedWindow.Handle, WM_LBUTTONDOWN, IntPtr.Zero, IntPtr.Zero);
                PostMessage(selectedWindow.Handle, WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);

                AddLog($"已发送鼠标点击到窗口 '{selectedWindow.Title}'");
            }
            catch (Exception ex)
            {
                AddLog($"发送鼠标点击时出错: {ex.Message}");
                MessageBox.Show($"发送鼠标点击时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseSelectedWindow()
        {
            if (lstWindows.SelectedItem is not WindowInfo selectedWindow)
            {
                MessageBox.Show("请先选择一个窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 发送关闭消息
                IntPtr result = SendMessage(selectedWindow.Handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

                AddLog($"已发送关闭消息到窗口 '{selectedWindow.Title}', 结果: 0x{result.ToInt64():X}");
            }
            catch (Exception ex)
            {
                AddLog($"关闭窗口时出错: {ex.Message}");
                MessageBox.Show($"关闭窗口时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MinimizeSelectedWindow()
        {
            if (lstWindows.SelectedItem is not WindowInfo selectedWindow)
            {
                MessageBox.Show("请先选择一个窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 发送最小化消息 (WM_SYSCOMMAND + SC_MINIMIZE)
                PostMessage(selectedWindow.Handle, WM_SYSCOMMAND, new IntPtr(0xF020), IntPtr.Zero);

                AddLog($"已最小化窗口 '{selectedWindow.Title}'");
            }
            catch (Exception ex)
            {
                AddLog($"最小化窗口时出错: {ex.Message}");
                MessageBox.Show($"最小化窗口时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void MaximizeSelectedWindow()
        {
            if (lstWindows.SelectedItem is not WindowInfo selectedWindow)
            {
                MessageBox.Show("请先选择一个窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 发送最大化消息 (WM_SYSCOMMAND + SC_MAXIMIZE)
                PostMessage(selectedWindow.Handle, WM_SYSCOMMAND, new IntPtr(0xF030), IntPtr.Zero);

                AddLog($"已最大化窗口 '{selectedWindow.Title}'");
            }
            catch (Exception ex)
            {
                AddLog($"最大化窗口时出错: {ex.Message}");
                MessageBox.Show($"最大化窗口时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnRowUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtRow.Text, out int currentValue))
            {
                txtRow.Text = (currentValue + 1).ToString();
            }
            else
            {
                txtRow.Text = "1";
            }
        }

        private void BtnRowDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtRow.Text, out int currentValue))
            {
                txtRow.Text = (currentValue - 1).ToString();
            }
            else
            {
                txtRow.Text = "-1";
            }
        }

        private void BtnColUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtCol.Text, out int currentValue))
            {
                txtCol.Text = (currentValue + 1).ToString();
            }
            else
            {
                txtCol.Text = "1";
            }
        }

        private void BtnColDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtCol.Text, out int currentValue))
            {
                txtCol.Text = (currentValue - 1).ToString();
            }
            else
            {
                txtCol.Text = "-1";
            }
        }

        private uint ParseHex(string hexString)
        {
            if (hexString.StartsWith("0x") || hexString.StartsWith("0X"))
            {
                hexString = hexString.Substring(2);
            }
            return uint.Parse(hexString, System.Globalization.NumberStyles.HexNumber);
        }
        private uint ParseCustomInt(string intString)
        {
            return uint.Parse(intString, System.Globalization.NumberStyles.Integer) + 0x0400;
        }

        private void WriteRowCol(int row, int col)
        {
            // 读本地的文件坐标
            string fileName = "C:\\Communication\\GS_MULTI_SITE_INFO.txt";
            System.IO.File.WriteAllText(fileName, string.Format("{0},{1},0,0,0,0,0,0,0^", row, col));
        }

        private void AddLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}\n");
            txtLog.ScrollToEnd();
        }
        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            ClearLog();
        }

        private void ClearLog()
        {
            txtLog.Clear();
            AddLog("日志已清空");
        }
        #endregion
    }
}