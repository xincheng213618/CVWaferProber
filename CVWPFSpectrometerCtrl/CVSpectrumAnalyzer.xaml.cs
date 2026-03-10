using CVWaferProber.Core.Config;
using CVWaferProber.Core.Utils;
using CVWPFSpectrometerCtrl.Models;
using CVWPFSpectrometerCtrl.ViewModels;
using OxyPlot;
using OxyPlot.Wpf;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;


namespace CVWPFSpectrometerCtrl
{

    public static class CVSpectrumAnalyzerRefresh
    {
        public static event EventHandler IVDataGrid;

        public static void RefreshIVDataGrid()
        {
            IVDataGrid?.Invoke(new object(), new EventArgs());
        }
    }


    /// <summary>
    /// CVSpectrumAnalyzer.xaml 的交互逻辑
    /// </summary>
    public partial class CVSpectrumAnalyzer : System.Windows.Controls.UserControl
    {
        
       
        private SpectralData _spectralData;
        public CVSpectrumAnalyzer()
        {

            InitializeComponent();
            //EQEGrid.DataContext = CVEQEViewModel.GetInstance();
            //_spectralData.GenerateSampleData(550, 50);
            //SpectralDisplay.SpectralData = _spectralData;
            // 初始化ComboBox默认选中第一项
            if (cboIVVIMode != null && cboIVVIMode.Items.Count > 0)
            {
                cboIVVIMode.SelectedIndex = 0;
            }
            //_spectralData.GenerateMultiPeakData();
            Loaded += (s, e) =>
            {
                if (this.DataContext is CVSpectrumViewModel viewModel)
                {
                    //viewModel.SetMainWin(this);
                    //viewModel.SetSpectrumCtrl(SpectralDisplay);
                }
            };
            CVSpectrumAnalyzerRefresh.IVDataGrid += (s, e) =>
            {
                if (IVDataGrid.ItemsSource is ObservableCollection<IVMeasurement> iVMeasurements)
                {
                    if (iVMeasurements.Count > 0)
                    {
                        IVMeasurement iVMeasurement = iVMeasurements.Last();
                        if (iVMeasurement != null)
                        {
                            //IVDataGrid.SelectedItem = iVMeasurement;    
                            IVDataGrid.ScrollIntoView(iVMeasurement);

                        }

                    }
                }
            };
        }
       
        private void PlotView_Loaded(object sender, RoutedEventArgs e)
        {
            var plotView = sender as PlotView;


            var vm = DataContext as CVSpectrumViewModel;
            if (plotView != null && vm != null)
            {
                // 监听ViewModel的刷新指令
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(vm.PlotModel))
                    {
                        plotView.Model?.InvalidatePlot(true);
                    }
                };
            }
        }

        private void cboIVVIMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 获取ComboBox的当前选中索引
            int selectedIndex = cboIVVIMode.SelectedIndex;

            // 根据索引控制Panel的显示/隐藏
            if (selectedIndex == 1)
            {
                // 显示IVPanel，隐藏VIPanel
                IVPanel.Visibility = Visibility.Visible;
                OverviewIVPlotView.Visibility = Visibility.Visible;
                VIPanel.Visibility = Visibility.Collapsed;
                OverviewVIPlotView.Visibility = Visibility.Collapsed;
              
            }
            else if (selectedIndex == 0)
            {
                // 显示VIPanel，隐藏IVPanel
                VIPanel.Visibility = Visibility.Visible;
                OverviewVIPlotView.Visibility = Visibility.Visible;
                IVPanel.Visibility = Visibility.Collapsed;
                OverviewIVPlotView.Visibility = Visibility.Collapsed;
               
             
            }
        }

        private void OpenIvlFloder_Click(object sender, RoutedEventArgs e)
        {
            PlatformHelper.OpenFolder(ConfigManager.Config.ExportPathSettings.IvlExportPath);
        }

        private void OpenEQEFloder_Click(object sender, RoutedEventArgs e)
        {
            PlatformHelper.OpenFolder(ConfigManager.Config.ExportPathSettings.EqeExportPath);
        }

        private void measurementsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
        }

        private void DataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
