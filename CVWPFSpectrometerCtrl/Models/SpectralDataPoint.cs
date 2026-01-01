using CVWaferProber.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CVWPFSpectrometerCtrl.Models
{
    public class SpectralDataPoint: ViewModelBase
    {
        public double Wavelength { get; set; }  // 波长 (nm)
        public double Intensity { get; set; }   // 强度 (0-1)
        public Color Color => Converters.WavelengthToColorConverter.ConvertWavelengthToColor(Wavelength);

        public float RelativeSpectrum { get;  set; }
        public float AbsoluteSpectrum { get;  set; }
    }
}
