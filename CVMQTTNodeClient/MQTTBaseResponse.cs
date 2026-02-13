using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVMQTTNodeClient
{
    public class MQTTBaseResponse
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

        public static MQTTBaseResponse? Failed()
        {
            return new MQTTBaseResponse() { Code = -1, Message = "Failed" };
        }

        public static MQTTBaseResponse? OK()
        {
            return new MQTTBaseResponse() { Code = 200, Message = "OK" };
        }

        public MQTTBaseResponse()
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

    public class MQTTResponse<T> : MQTTBaseResponse
    {
        public T? Data { get; set; }
    }

}
