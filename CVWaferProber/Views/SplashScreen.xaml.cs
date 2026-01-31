using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CVWaferProber.Views
{
    /// <summary>
    /// SplashScreen.xaml 的交互逻辑
    /// </summary>
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
        }

        // 更新进度和状态文本
        public void UpdateProgress(double value, string status)
        {
            Dispatcher.Invoke(() =>
            {
                // 设置进度条宽度（假设总宽度400）
                progressFill.Width = value * 4;
                tbStatus.Text = status;
            });
        }
    }
}
