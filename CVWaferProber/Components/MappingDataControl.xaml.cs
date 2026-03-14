using ChipMapping.ViewModels;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace CVWaferProber.Components
{
    /// <summary>
    /// MappingDataControl.xaml 的交互逻辑
    /// </summary>
    public partial class MappingDataControl : System.Windows.Controls.UserControl
    {
        public MappingDataControl()
        {
            InitializeComponent();
            MyChipMappingControl.MoveToCommand = new RelayCommand(OnMoveTo);
        }
        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            mainVM = MappingDataViewModel.GetInstance();
            this.DataContext = mainVM;
            mainVM._dataGrid = TestResultDataGrid;
            mainVM.UpdateDataGridColumns();
        }

        public MappingDataViewModel mainVM { get; set; }

        private void OnMoveTo(object obj)
        {
            if (obj is ChipViewModel chipViewModel)
            {
                mainVM.OnMoveTo(chipViewModel);
            }
        }
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if(sender is DataGrid dataGrid && dataGrid.SelectedItem is DieViewModel dieViewModel)
            {
                mainVM.OnMoveTo(dieViewModel);
            }
        }
    }
}
