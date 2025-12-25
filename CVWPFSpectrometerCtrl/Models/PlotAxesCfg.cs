using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.Models
{
    public enum TabType
    {
        Overview,
        Spectrum,
        IV,
        IL,
        VL,
        IVLCamera
    }
   
    public struct PlotAxesCfg
    {
        public float DefaultMin;
        public float DefaultMax;
        public float DefaultMaxRange;
    }
}
