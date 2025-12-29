using CVDB.Services.Buz;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    /// SysFlowCfgWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SysFlowCfgWindow : Window
    {
        private bool _isMaximized = false;
        private Point _dragStartPoint;
        public ObservableCollection<FlowConfigItem> ConfigItems { get; set; }
        public SysFlowCfgWindow()
        {
            InitializeComponent();
            LoadConfigData();
            this.DataContext = this; // 设置数据上下文为窗口本身
        }
        // 窗口拖动
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                this.DragMove();
            }
        }
        private void ToggleMaximize()
        {
            if (_isMaximized)
            {
                this.WindowState = WindowState.Normal;
                this.ResizeMode = ResizeMode.CanResizeWithGrip;
                this.Margin = new Thickness(10);
                _isMaximized = false;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                this.ResizeMode = ResizeMode.NoResize;
                this.Margin = new Thickness(0);
                _isMaximized = true;
            }
            UpdateMaxButtonText();
        }

        private void UpdateMaxButtonText()
        {
            //if (maxButton != null)
            //{
            //    maxButton.Content = _isMaximized ? "🗗" : "🗖";
            //}
        }
        private void LoadConfigData()
        {
            ConfigItems = new ObservableCollection<FlowConfigItem>();

            var flows = WaferProberDBService.LoadAllFlows();
            // 模拟数据 - 假设有不同类型的配置项
            var optionTrueFalse = new ObservableCollection<ConfigOption>();
            foreach (var flow in flows)
            {
                var option = new ConfigOption() {  DisplayName = flow.Name , Value = flow.Name };
                optionTrueFalse.Add(option);
            }
            // 添加配置项
            ConfigItems.Add(new FlowConfigItem
            {
                DisplayName = "AOI",
                ValueType = "AOI",
                AvailableOptions = optionTrueFalse,
                SelectedValue = optionTrueFalse[0] // 默认选择第一个
            });

            ConfigItems.Add(new FlowConfigItem
            {
                DisplayName = "IVL",
                ValueType = "IVL",
                AvailableOptions = optionTrueFalse,
                SelectedValue = optionTrueFalse[0] // 默认选择"中"
            });

            // ... 可以添加更多配置项
        }
        // 处理行加载事件，设置行号（序号）
        private void ConfigDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // 行索引从0开始，所以加1
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
        }
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要重置所有配置吗？", "重置确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                InitializeData();
            }
        }

        private void InitializeData()
        {
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // 配置项数据模型
    public class FlowConfigItem : INotifyPropertyChanged
    {
        public string DisplayName { get; set; }
        public string ValueType { get; set; }
        public ObservableCollection<ConfigOption> AvailableOptions { get; set; }

        private ConfigOption _selectedValue;
        public ConfigOption SelectedValue
        {
            get => _selectedValue;
            set
            {
                if (_selectedValue != value)
                {
                    _selectedValue = value;
                    OnPropertyChanged(nameof(SelectedValue));
                    // 这里可以添加选择值改变后的逻辑，比如保存配置
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 下拉选项数据模型
    public class ConfigOption
    {
        public string DisplayName { get; set; }
        public string Value { get; set; }
    }
}
