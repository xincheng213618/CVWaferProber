using ChipMapping.ViewModels;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.ViewModels;
using log4net;
using System.Windows;

namespace CVWaferProber.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));
        public MainWindow()
        {
            InitializeComponent();
            MyChipMappingControl.ValueChangedCommand = new RelayCommand(ExecuteCustomCommand);
            log.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
        }

        private void ExecuteCustomCommand(object? obj)
        {
            if(this.DataContext is MainViewModel viewModel)
            {
                viewModel.SetSelectedDataGridItem(obj);
            }
        }

        private void TestResultDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}