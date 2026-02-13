using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.MQTT
{
    public class MQTTCVBaseRequest<T> : MQTTCVRequestTokenHeader
    {
        [Newtonsoft.Json.JsonProperty("params")]
        public T Data { get; set; }
        public MQTTCVBaseRequest() : this(null) { }
        public MQTTCVBaseRequest(string eventName) : this(null, null, eventName)
        {
        }
        public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName) : this(serviceName, deviceName, eventName, default)
        {
        }
        public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName, T data) : this(serviceName, deviceName, eventName, string.Empty, string.Empty, data)
        {
        }
        public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName, string serialNumber, T data) : this(serviceName, deviceName, eventName, serialNumber, string.Empty, data)
        {
        }
        public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName, string serialNumber, string token, T data) : this("1.0", serviceName, deviceName, eventName, serialNumber, Guid.NewGuid().ToString("D"), token, data)
        {
        }
        public MQTTCVBaseRequest(string version, string serviceName, string deviceName, string eventName, string serialNumber, string msgID, string token, T data, int zIndex = -1) : base(version, serviceName, deviceName, eventName, serialNumber, msgID, token, zIndex)
        {
            Data = data;
        }
    }
    public class MQTTCVRequestHeader
    {
        public string Version { get; set; }
        public string ServiceName { get; set; }
        public string DeviceCode { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }
        public string MsgID { get; set; }
        public int ZIndex { get; set; }

        public MQTTCVRequestHeader(string version, string serviceName, string deviceCode, string eventName, string serialNumber, string msgID, int zIndex)
        {
            Version = version;
            ServiceName = serviceName;
            DeviceCode = deviceCode;
            EventName = eventName;
            SerialNumber = serialNumber;
            MsgID = msgID;
            ZIndex = zIndex;
        }
        public MQTTCVRequestHeader()
        {

        }
    }
    public class MQTTCVRequestTokenHeader : MQTTCVRequestHeader
    {
        public string Token { get; set; }

        public MQTTCVRequestTokenHeader(string serviceName, string deviceName, string eventName) : this(serviceName, deviceName, eventName, string.Empty)
        {
        }
        public MQTTCVRequestTokenHeader(string serviceName, string deviceName, string eventName, string serialNumber) : this(serviceName, deviceName, eventName, serialNumber, string.Empty)
        {
        }
        public MQTTCVRequestTokenHeader(string serviceName, string deviceName, string eventName, string serialNumber, string token) : this("1.0", serviceName, deviceName, eventName, serialNumber, Guid.NewGuid().ToString("D"), token)
        {
        }
        public MQTTCVRequestTokenHeader(string version, string serviceName, string deviceName, string eventName, string serialNumber, string msgID, string token, int zIndex = -1) : base(version, serviceName, deviceName, eventName, serialNumber, msgID, zIndex)
        {
            Token = token;
        }

        public MQTTCVRequestTokenHeader() : this(string.Empty, string.Empty, string.Empty)
        {
        }

        public bool IsTokenValid(string accessToken)
        {
            string version = this.Version;
            //if (version == null || version.Equals("1.0")) return true;
            return !string.IsNullOrWhiteSpace(this.Token) && this.Token.Equals(accessToken);
        }
    }
}
