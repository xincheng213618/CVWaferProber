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
            Loaded += MappingDataControl_Loaded;
        }

        private void OnMoveTo(object obj)
        {
            var mainVM = DataContext as MappingDataViewModel;
            if (mainVM != null)
            {
                if (mainVM.IsNotProcessing) mainVM.OnMoveTo(obj as ChipViewModel);
            }
        }
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid?.SelectedItem != null)
            {
                // 获取双击的行数据
                var selectedItem = dataGrid.SelectedItem as DieViewModel;
                var mainVM = DataContext as MappingDataViewModel;
                if (mainVM != null && selectedItem!=null)
                {
                    if (mainVM.IsNotProcessing) mainVM.OnMoveTo(selectedItem);
                }
            }
        }
        private void MappingDataControl_Loaded(object sender, RoutedEventArgs e)
        {
            var mainVM = DataContext as MappingDataViewModel;
            if (mainVM != null)
            {   
                mainVM.SetDataGrid(TestResultDataGrid);
                // 初始化DataGrid列
                mainVM.UpdateDataGridColumns();
            }
        }
    }
}
