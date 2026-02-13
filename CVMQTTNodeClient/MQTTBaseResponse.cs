using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVMQTTNodeClient
{
    public class CVMQTTBaseResponse
    {
        public string MsgId { get; set; }
        public string DeviceCode { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }
        public int ZIndex { get; set; }
        public bool IsOK() => Code == 200;
        public bool IsPending() => Code == 102;

        public static CVMQTTBaseResponse? Failed()
        {
            return new CVMQTTBaseResponse() { Code = -1, Message = "Failed" };
        }

        public static CVMQTTBaseResponse? OK()
        {
            return new CVMQTTBaseResponse() { Code = 200, Message = "OK" };
        }

        public CVMQTTBaseResponse()
        {
            this.Code = -1;
            this.Message = "Failed";
            this.MsgId = string.Empty;
            this.DeviceCode = string.Empty;
            this.EventName = string.Empty;
            this.SerialNumber = string.Empty;
            this.ZIndex = -1;
        }
    }

    public class CVMQTTResponse<T> : CVMQTTBaseResponse
    {
        public T? Data { get; set; }
    }

}
