using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Restful.DTO
{
    public class FlowComResp
    {
        /// <summary>
        /// 
        /// </summary>
        public string CreateDateTime { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Result { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int? ResultCode { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string SerialNumber { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int? TotalTime { get; set; }
    }
    public class AlgResultItem
    {
        /// <summary>
        /// 
        /// </summary>
        public string CreateDateTime { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string DeviceCode { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string GetURL { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Remark { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int ResultCode { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ResultType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ResultImageFile { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int TotalTime { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int ZIndex { get; set; }
        public string Version { get; set; }
    }

    public class RespDataBaseFlowResultDTO
    {
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int? ResultCode { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ResultStatus { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ResultType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string SerialNumber { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int TotalTime { get; set; }
        [JsonIgnore]
        public bool IsSuccess { get => ResultCode.HasValue ? ResultCode.Value == 0 : false; }
        [JsonIgnore]
        public bool IsFinished { get => ResultCode.HasValue; }

        public string ToDisString()
        {
            if (IsFinished) return string.Format("Finished:{0}/{1}ms", ResultStatus, TotalTime);
            else return string.Format("Pending");
        }
    }
    public class RespDataFlowResultDTO<T> : RespDataBaseFlowResultDTO
    {

        /// <summary>
        /// 
        /// </summary>
        public List<T> Result { get; set; }
    }


    //如果好用，请收藏地址，帮忙分享。
    public class ImageInfo
    {
        /// <summary>
        /// 
        /// </summary>
        public int bpp { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int channels { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int height { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int width { get; set; }
    }

    public class ImgResultItem
    {
        /// <summary>
        /// 
        /// </summary>
        public string CreateDateTime { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string DeviceCode { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string GetURL { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Remark { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int ResultCode { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int TotalTime { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int ZIndex { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public ImageInfo ImageInfo { get; set; }
    }
}
