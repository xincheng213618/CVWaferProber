using CVWaferProber.Services;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using UserControl = System.Windows.Controls.UserControl;

namespace CVWaferProber.Components
{
    /// <summary>
    /// ToolsControl.xaml 的交互逻辑
    /// </summary>
    public partial class ToolsControl : UserControl
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(EQEService));
        public ToolsControl()
        {
            InitializeComponent();
        }
        #region 按钮点击事件
        private void BtnLiftAll_Click(object sender, RoutedEventArgs e)
        {
            logger.Info($"Execute: {(string)FindResource("Toolbar.LiftAll")}");
            // 实际执行抬起所有操作
        }

        private void BtnToMainCamera_Click(object sender, RoutedEventArgs e)
        {
            logger.Info($"Execute: {(string)FindResource("Toolbar.ToMainCamera")}");
            // 实际执行移至主相机位操作
        }

        private void BtnToAuxCamera_Click(object sender, RoutedEventArgs e)
        {
            logger.Info($"Execute: {(string)FindResource("Toolbar.ToAuxCamera")}");
            // 实际执行移至辅相机位操作
        }

        private void BtnToIntegratingSphere_Click(object sender, RoutedEventArgs e)
        {
            logger.Info($"Execute: {(string)FindResource("Toolbar.ToIntegratingSphere")}");
            // 实际执行移至积分球位操作
        }

        
        #endregion
    }
}
