namespace CVMQTTNodeClient
{
    public class CVServiceHeartbeat
    {
        public string NodeName { get; set; }
        public string Token { get; set; }
        public string ServiceType { get; set; }
        public string ServiceCode { get; set; }
        public string UpChannel { get; set; }
        public string DownChannel { get; set; }
        public int OverTime { get; set; }
        public List<CVServiceDeviceHeartbeat> Devices { get; set; }

        public CVServiceHeartbeat()
        {
            this.NodeName = string.Empty;
            this.Token = string.Empty;
            this.ServiceType = string.Empty;
            this.ServiceCode = string.Empty;
            this.UpChannel = string.Empty;
            this.DownChannel = string.Empty;
            this.OverTime = 5000;
            this.Devices = new List<CVServiceDeviceHeartbeat>();
        }
    }

    public class CVServiceDeviceHeartbeat
    {
        public string DeviceCode { get; set; }
        public string DeviceStatus { get; set; }

        public CVServiceDeviceHeartbeat(string deviceCode, string status)
        {
            this.DeviceCode = deviceCode;
            this.DeviceStatus = status;
        }

        public CVServiceDeviceHeartbeat(CVServiceDeviceHeartbeat heartbeat) : this(heartbeat.DeviceCode, heartbeat.DeviceStatus)
        {
        }

        public CVServiceDeviceHeartbeat() : this(string.Empty, string.Empty) { }
    }
}
