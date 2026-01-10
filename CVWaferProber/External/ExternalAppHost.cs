using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace CVWaferProber.External
{
    public class ExternalAppHost : HwndHost
    {
        private readonly string _appPath;
        private Process _process;
        private IntPtr _hwnd;
        private HwndSource _hostHwndSource;

        public Process HostedProcess => _process;

        public ExternalAppHost(string appPath)
        {
            _appPath = appPath;
            //this.Loaded += OnLoaded;
            //this.Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hostHwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (_hostHwndSource != null)
            {
                _hostHwndSource.AddHook(WndProc);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_hostHwndSource != null)
            {
                _hostHwndSource.RemoveHook(WndProc);
                _hostHwndSource = null;
            }
        }
        
        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {

            _hwndParent = hwndParent;
            try
            {
                FindWindows();
                Thread.Sleep(100);
                if (mainWin != null)
                {
                    _hwnd = mainWin.Handle;
                    // 修改窗口样式 - 去除所有边框
                    ModifyWindowStyle();

                    // 设置父窗口
                    if (NativeMethods.SetParent(_hwnd, hwndParent.Handle) == IntPtr.Zero)
                    {
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    }

                    // 更新窗口大小
                    //UpdateWindowSize2();

                    return new HandleRef(this, _hwnd);
                }
                else
                {
                    throw new Exception( "Unable to obtain the application main window handle" );//: "无法获取应用程序主窗口句柄"
                }
            }
            catch
            {
                Cleanup();
                throw;
            }
        }
        protected /*override*/ HandleRef BuildWindowCore0(HandleRef hwndParent)
        {
            _hwndParent = hwndParent;
            try
            {
                // 启动外部进程
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _appPath,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(_appPath),
                        WindowStyle = ProcessWindowStyle.Normal,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };

                _process.Start();
                _process.WaitForInputIdle();

                // 等待获取主窗口句柄
                int retries = 0;
                while (_process.MainWindowHandle == IntPtr.Zero && retries < 50)
                {
                    Thread.Sleep(100);
                    _process.Refresh();
                    retries++;
                    if (_process.HasExited)
                    {
                        throw new Exception( "The application has exited and the window handle cannot be obtained.");// : "应用程序已退出，无法获取窗口句柄"
                    }
                }

                if (_process.MainWindowHandle == IntPtr.Zero)
                {
                    throw new Exception( "Unable to obtain the application main window handle");// : "无法获取应用程序主窗口句柄"
                }
                _hwnd = _process.MainWindowHandle;
                // 修改窗口样式 - 去除所有边框
                ModifyWindowStyle();

                // 设置父窗口
                if (NativeMethods.SetParent(_hwnd, hwndParent.Handle) == IntPtr.Zero)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }

                // 更新窗口大小
                //UpdateWindowSize2();

                return new HandleRef(this, _hwnd);
            }
            catch
            {
                Cleanup();
                throw;
            }
        }
        private HandleRef _hwndParent;

        public bool FindAndSetWindow()
        {
            FindWindows();
            Thread.Sleep(100);
            if (mainWin != null)
            {
                _hwnd = mainWin.Handle;
                // 修改窗口样式 - 去除所有边框
                ModifyWindowStyle();

                // 设置父窗口
                if (NativeMethods.SetParent(_hwnd, _hwndParent.Handle) == IntPtr.Zero)
                {
                    //throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    return false;
                }
            }
            return true;
        }
        private void FindWindows()
        {
            try
            {
                //string searchText = "";
                //lstWindows.Items.Clear();
                //AddLog($"开始查找包含 '{searchText}' 的窗口...");

                //List<WindowInfo> windows = new List<WindowInfo>();

                NativeMethods.EnumWindows(EnumWindowsdelegate, IntPtr.Zero);

                //foreach (var window in windows)
                //{
                //    lstWindows.Items.Add(window);
                //}

                //AddLog($"找到 {windows.Count} 个窗口");
                //txtStatus.Text = $"找到 {windows.Count} 个窗口";
            }
            catch (Exception ex)
            {
                //AddLog($"查找窗口时出错: {ex.Message}");
                //MessageBox.Show($"查找窗口时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private WindowInfo mainWin = null;
        private bool EnumWindowsdelegate(IntPtr hWnd, IntPtr lParam)
        {
            string searchText = "ColorVision";
            if (NativeMethods.IsWindowVisible(hWnd))
            {
                StringBuilder titleBuilder = new StringBuilder(256);
                NativeMethods.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);

                StringBuilder classBuilder = new StringBuilder(256);
                NativeMethods.GetClassName(hWnd, classBuilder, classBuilder.Capacity);

                uint processId;
                NativeMethods.GetWindowThreadProcessId(hWnd, out processId);

                string title = titleBuilder.ToString();
                string className = classBuilder.ToString();

                // 如果搜索文本为空或标题包含搜索文本
                if (string.IsNullOrEmpty(searchText) ||
                    title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    mainWin = new WindowInfo
                    {
                        Handle = hWnd,
                        Title = title,
                        ClassName = className,
                        ProcessId = processId
                    };
                    //windows.Add(new WindowInfo
                    //{
                    //    Handle = hWnd,
                    //    Title = title,
                    //    ClassName = className,
                    //    ProcessId = processId
                    //});
                }
            }
            return true;
        }
        private void ModifyWindowStyle()
        {
            // 获取当前样式
            int style = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_STYLE);
            int exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);

            // 移除所有边框和标题栏相关的样式
            style &= ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | 
                      NativeMethods.WS_MINIMIZEBOX | NativeMethods.WS_MAXIMIZEBOX | 
                      NativeMethods.WS_SYSMENU | NativeMethods.WS_BORDER | 
                      NativeMethods.WS_POPUP);
            
            exStyle &= ~(NativeMethods.WS_EX_DLGMODALFRAME | 
                        NativeMethods.WS_EX_CLIENTEDGE | 
                        NativeMethods.WS_EX_STATICEDGE);

            // 必须添加WS_CHILD样式
            style |= NativeMethods.WS_CHILD;

            // 应用新的样式
            NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_STYLE, style);
            NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

            // 强制窗口重绘以应用新样式
            NativeMethods.SetWindowPos(
                _hwnd, IntPtr.Zero, 
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | 
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);

            // 强制重绘窗口
            NativeMethods.RedrawWindow(
                _hwnd, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.RDW_FRAME | NativeMethods.RDW_INVALIDATE |
                NativeMethods.RDW_ERASE | NativeMethods.RDW_ALLCHILDREN);
        }

        public void UpdateWindowSize2()
        {
            if (_hwnd != IntPtr.Zero)
            {
                var source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    // 获取DPI缩放因子
                    double dpiScale = source.CompositionTarget.TransformToDevice.M11;

                    // 计算实际像素大小
                    int width = (int)(ActualWidth * dpiScale);
                    int height = (int)(ActualHeight * dpiScale);

                    // 调整窗口位置和大小
                    NativeMethods.MoveWindow(_hwnd, 0, 0, width, height, true);
                }
            }
        }
        public void UpdateWindowSize()
        {
            if (_hwnd != IntPtr.Zero && _hostHwndSource != null)
            {
                // 获取DPI缩放因子
                var transform = _hostHwndSource.CompositionTarget.TransformToDevice;
                double dpiScaleX = transform.M11;
                double dpiScaleY = transform.M22;

                // 计算实际像素大小
                int width = (int)(ActualWidth * dpiScaleX);
                int height = (int)(ActualHeight * dpiScaleY);

                // 调整窗口位置和大小
                NativeMethods.MoveWindow(_hwnd, 0, 0, width, height, true);
            }
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            //Cleanup();
        }

        private void Cleanup()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    // 尝试正常关闭
                    if (!_process.CloseMainWindow())
                    {
                        _process.Kill();
                    }
                    _process.WaitForExit(5000);
                    _process.Dispose();
                }
            }
            catch
            {
                // 忽略清理过程中的错误
            }
            finally
            {
                _process = null;
                _hwnd = IntPtr.Zero;
            }
        }

        //protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        //{
        //    const int WM_SIZE = 0x0005;

        //    if (msg == WM_SIZE)
        //    {
        //        UpdateWindowSize();
        //        handled = true;
        //    }

        //    return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        //}

        //protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        //{
        //    base.OnRenderSizeChanged(sizeInfo);
        //    UpdateWindowSize();
        //}
    }

    #region 数据结构
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; }
        public string ClassName { get; set; }
        public uint ProcessId { get; set; }
    }
    #endregion
}