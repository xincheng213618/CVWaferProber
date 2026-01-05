using CVWaferProber.ViewModels;
using System.Windows;

namespace CVWaferProber.Views
{
    /// <summary>
    /// ConnectionSettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ConnectionSettingsWindow : Window
    {
        public ConnectionSettingsWindow()
        {
            InitializeComponent();
            Closed += OnWindowClosed;
        }
        private void OnWindowClosed(object? sender, System.EventArgs e)
        {
            if (DataContext is ConnectionSettingsViewModel viewModel)
            {
                viewModel.Cleanup();
            }
        }
    }
}
