using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Restful.DTO
{
    public class RespDTO<T> : BaseRespDTO
    {
        /// <summary>
        /// 
        /// </summary>
        public T Data { get; set; }
    }
     public class BaseRespDTO
    {
        /// <summary>
        /// 
        /// </summary>
        public int Code { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Message { get; set; }

        [JsonIgnore]
        public bool IsSuccess { get => Code == 200; }
        [JsonIgnore]
        public bool IsProcessing { get => Code == 102; }
    }


    public enum RespDetailType
    {
        Image,
        CIE,
        FOV,
    }
}
