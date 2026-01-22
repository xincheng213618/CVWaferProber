using UserControl = System.Windows.Controls.UserControl;

namespace CVWaferProber.Components
{
    /// <summary>
    /// ToolsControl.xaml 的交互逻辑
    /// </summary>
    public partial class ToolsControl : UserControl
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ToolsControl));
        public ToolsControl()
        {
            InitializeComponent();
        }
    }
}
