using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Restful.DTO
{
    public class Data
    {
        /// <summary>
        /// 
        /// </summary>
        public double CCT { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double Wave { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double X { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double Y { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double Z { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double u { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double v { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double x { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double y { get; set; }
    }

    public class Point
    {
        /// <summary>
        /// 
        /// </summary>
        public int Height { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int PixelX { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int PixelY { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string PointType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int Width { get; set; }
    }

    public class RespDataDTO_CIE
    {
        /// <summary>
        /// 
        /// </summary>
        public Data Data { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Point Point { get; set; }
    }

    public class RespDataDTO_FOV
    {
        /// <summary>
        /// 
        /// </summary>
        public float Degrees { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int Pattern { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int Type { get; set; }
    }

}
