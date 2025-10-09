using CVWaferProber.ViewModels;
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
                    //viewModel.SetMainWin(this);
                    viewModel.SetDataGrid(TestResultDataGrid);
                }
            };
        }
    }
}
