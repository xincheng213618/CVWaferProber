using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Restful.DTO
{
    public class RequestBaseDTO
    {
        public RequestBaseDTO()
        {
            this.Version = "1.1";
        }

        /// <summary>
        /// 
        /// </summary>
        public string Version { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string DeviceCode { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string SerialNumber { get; set; }
    }
    public class RequestDTO<T> : RequestBaseDTO
    {
        public RequestDTO() : base()
        {
        }
        /// <summary>
        /// 
        /// </summary>
        public T Data { get; set; }
    }

}
