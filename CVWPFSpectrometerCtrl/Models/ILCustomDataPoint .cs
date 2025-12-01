using OxyPlot;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.Models
{
    public class ILCustomDataPoint
    {
        /// <summary>
        /// 序号
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 时间
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// 电流 (mA)
        /// </summary>
        public double Current { get; set; }

        /// <summary>
        /// 亮度 (cd/m²)
        /// </summary>
        public double Luminance { get; set; }
    }
}
