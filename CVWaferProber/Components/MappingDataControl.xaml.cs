using CVWaferProber.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Windows;
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
            Loaded += MappingDataControl_Loaded;
        }

        private void MappingDataControl_Loaded(object sender, RoutedEventArgs e)
        {
            var mainVM = DataContext as MainViewModel;
            if (mainVM != null)
            {
                mainVM.SetDataGrid(TestResultDataGrid);
                // 初始化DataGrid列
                mainVM.UpdateDataGridColumns();
            }
        }

    
    }
}
