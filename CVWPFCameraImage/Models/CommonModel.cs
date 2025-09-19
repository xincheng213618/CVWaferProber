using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFCameraImage.Models
{
    public struct DetailResult_CommFile_V2
    {
        public string ResultFileName { get; set; }
    }

    public class PoiAnalysis_Result_Data
    {
        /// <summary>
        /// 
        /// </summary>
        public string Content { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double Value { get; set; }
    }

    public class PoiAnalysis
    {
        /// <summary>
        /// 
        /// </summary>
        public PoiAnalysis_Result_Data result { get; set; }
    }

}
