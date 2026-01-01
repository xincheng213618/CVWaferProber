using CVWPFSpectrometerCtrl.Models;
using CVWPFSpectrometerCtrl.ViewModels;
using OxyPlot.Wpf;
using System.Windows;


namespace CVWPFSpectrometerCtrl
{
    /// <summary>
    /// CVSpectrumAnalyzer.xaml 的交互逻辑
    /// </summary>
    public partial class CVSpectrumAnalyzer : System.Windows.Controls.UserControl
    {
       
        private SpectralData _spectralData;
        public CVSpectrumAnalyzer()
        {
            InitializeComponent();
          
            _spectralData = new SpectralData();
            //_spectralData.GenerateSampleData(550, 50);
            //SpectralDisplay.SpectralData = _spectralData;

            //_spectralData.GenerateMultiPeakData();
            Loaded += (s, e) =>
            {
                if (this.DataContext is CVSpectrumViewModel viewModel)
                {
                    //viewModel.SetMainWin(this);
                    //viewModel.SetSpectrumCtrl(SpectralDisplay);
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

    }
}
