using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.MQTT
{
    public class RCServiceHeartbeat
    {
        public string NodeName { get; set; }
        public string Token { get; set; }
        public string ServiceType { get; set; }
        public string ServiceCode { get; set; }
        public string UpChannel { get; set; }
        public string DownChannel { get; set; }
        public int OverTime { get; set; }
        public List<RCServiceDeviceHeartbeat> Devices { get; set; }
    }

    public class RCServiceDeviceHeartbeat
    {
        public string DeviceCode { get; set; }
        public string DeviceStatus { get; set; }

        public RCServiceDeviceHeartbeat(string deviceCode, string status)
        {
            this.DeviceCode = deviceCode;
            this.DeviceStatus = status;
        }

        public RCServiceDeviceHeartbeat(RCServiceDeviceHeartbeat heartbeat)
        {
            this.DeviceCode = heartbeat.DeviceCode;
            this.DeviceStatus = heartbeat.DeviceStatus;
        }

        public RCServiceDeviceHeartbeat() { }
    }
}
