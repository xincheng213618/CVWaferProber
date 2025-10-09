using CVWaferProber.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class SpectraDataViewModel : ViewModelBase
    {
        private double[] _wavelengths;
        private double[] _intensities;
        private string _name;

        public double[] Wavelengths
        {
            get => _wavelengths;
            set => SetProperty(ref _wavelengths, value);
        }

        public double[] Intensities
        {
            get => _intensities;
            set => SetProperty(ref _intensities, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public SpectraDataViewModel(double[] wavelengths, double[] intensities, string name = "")
        {
            Wavelengths = wavelengths;
            Intensities = intensities;
            Name = name;
        }
    }
}
