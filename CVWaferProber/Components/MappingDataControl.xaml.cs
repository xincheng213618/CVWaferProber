using CVWaferProber.ViewModels;
using System.Windows.Controls;

namespace CVWaferProber.Components
{
    /// <summary>
    /// MappingDataControl.xaml 的交互逻辑
    /// </summary>
    public partial class MappingDataControl : UserControl
    {
        public MappingDataControl()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (this.DataContext is MainViewModel viewModel)
                {
                    viewModel.SetDataGrid(TestResultDataGrid);
                }
            };
        }

      
    }
}
