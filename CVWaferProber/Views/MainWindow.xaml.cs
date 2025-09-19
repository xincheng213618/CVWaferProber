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
            // 等待DataGrid加载完成后设置引用
            Loaded += (s, e) =>
            {
                if (this.DataContext is MainViewModel viewModel)
                {
                    //viewModel.SetMainWin(this);
                    viewModel.SetDataGrid(TestResultDataGrid);
                }
            };
        }

        private void ExecuteCustomCommand(object? obj)
        {
            if(this.DataContext is MainViewModel viewModel)
            {
                viewModel.SetSelectedDataGridItem(obj);
            }
        }
    }
}