using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.Models
{
    public class SpectrumPoint
    {
        public double Wavelength { get; set; } // 波长（nm）
        public double Intensity { get; set; } // 光谱强度
    }
}
