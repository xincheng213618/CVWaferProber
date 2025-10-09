using CVWPFSpectrometerCtrl.ViewModels;
using CVWPFSpectrumControl.Models;
using System.Windows.Controls;

namespace CVWPFSpectrometerCtrl
{
    /// <summary>
    /// CVSpectrumAnalyzer.xaml 的交互逻辑
    /// </summary>
    public partial class CVSpectrumAnalyzer : UserControl
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
    }
}
