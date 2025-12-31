using ColorVision.Core.Entities;
using CVDB.Services.Buz;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            if (ConfigItems == null) ConfigItems = new ObservableCollection<FlowConfigItem>();
            ConfigItems.Clear();
            List<TScgdBuzProductDetail> buz_flows = WaferProberDBService.LoadBuzFlows();
            if (buz_flows == null || buz_flows.Count == 0)
            {
                return;
            }
            var flows = WaferProberDBService.LoadAllFlows();
            // 模拟数据 - 假设有不同类型的配置项
            var flowAll = new ObservableCollection<ConfigOption>();
            var option_emp = new ConfigOption() { DisplayName = "空", Value = null };
            flowAll.Add(option_emp);
            foreach (var flow in flows)
            {
                var option = new ConfigOption() {  DisplayName = flow.Name , Value = flow.Name };
                flowAll.Add(option);
            }

            foreach (var flow in buz_flows)
            {
                var option = string.IsNullOrEmpty(flow.Name)
                    ? flowAll[0]
                    : flowAll.FirstOrDefault(item => item.DisplayName == flow.Name) ?? flowAll[0];
                FlowTimeoutCfg flowTimeoutCfg = new FlowTimeoutCfg();
                if (string.IsNullOrEmpty(flow.CfgJson)) flowTimeoutCfg.Timeout = 120;
                else flowTimeoutCfg = JsonConvert.DeserializeObject<FlowTimeoutCfg>(flow.CfgJson);
                ConfigItems.Add(new FlowConfigItem
                {
                    Id = flow.Id,
                    DisplayName = flow.Code,
                    ValueType = flow.Code,
                    Timeout = flowTimeoutCfg.Timeout,
                    AvailableOptions = flowAll,
                    SelectedValue = option
                });
            }
        }

        // 处理行加载事件，设置行号（序号）
        private void ConfigDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // 行索引从0开始，所以加1
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ConfigItems)
            {
                TScgdBuzProductDetail buzProductDetail = WaferProberDBService.GetBuzDetail(item.Id);
                buzProductDetail.Name = item.SelectedValue.Value;
                buzProductDetail.CfgJson = JsonConvert.SerializeObject(new FlowTimeoutCfg() { Timeout = item.Timeout });
                WaferProberDBService.UpdateBuzDetail(buzProductDetail);
            }
            MessageBox.Show("Save Completed");
        }
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要重置所有配置吗？", "重置确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                InitializeData();
                LoadConfigData();
            }
        }

        private void InitializeData()
        {
            WaferProberDBService.InitBuzWaferProber_10001();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
    public struct FlowTimeoutCfg
    {
        public int Timeout;
    }
    // 配置项数据模型
    public class FlowConfigItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public string ValueType { get; set; }
        public int Timeout { get; set; }
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
