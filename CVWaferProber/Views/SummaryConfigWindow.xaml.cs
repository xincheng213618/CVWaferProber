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
using System.Windows.Shapes;

namespace CVWaferProber.Views
{
    /// <summary>
    /// SummaryConfigWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SummaryConfigWindow : Window
    {
        /// <summary>
        /// 用户选中的列配置
        /// </summary>
        public List<ColumnConfig> SelectedColumns { get; private set; }

        public SummaryConfigWindow(List<ColumnConfig> initialColumns)
        {
            InitializeComponent();
            ColumnListBox.ItemsSource = initialColumns;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedColumns = (ColumnListBox.ItemsSource as List<ColumnConfig>)
                .Where(c => c.IsSelected)
                .ToList();
            DialogResult = true;
            Close();
        }
    }
}
