using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Models
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

    public class PoiAnalysis<T>
    {
        /// <summary>
        /// 
        /// </summary>
        public T result { get; set; }
    }
    public class PoiAnalysis_Avg_Result_Data
    {
        /// <summary>
        /// 
        /// </summary>
        public string Content { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public float average_cieX { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public float average_cieY { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public float average_lum { get; set; }
    }

    public struct OLED_AOI_Result_E
    {
        /// <summary>
        /// 
        /// </summary>
        public string ResultCode { get; set; }
        /// <summary>
        /// SDK API运行错误
        /// </summary>
        public string ResultDesc { get; set; }
    }

}
