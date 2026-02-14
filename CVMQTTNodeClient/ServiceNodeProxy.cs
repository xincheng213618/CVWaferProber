using ColorVision.Core.Message;
using ColorVision.Core.Message.Response;
using ColorVision.Message.Services;
using System.Collections.Concurrent;

namespace CVMQTTNodeClient
{
    public class ServiceNodeProxy
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ServiceNodeProxy));

        public string ServiceToken { get; set; } = string.Empty;
        public string ServiceCode { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public string UpChannel { get; set; } = string.Empty;
        public string DownChannel { get; set; } = string.Empty;
        public DateTime LiveTime { get; set; }
        public int OverTime { get; set; } = -1;
        public bool IsLive { get; set; } = false;
        public ConcurrentDictionary<string, PhysicDeviceProxy> Devices { get; }

        public ServiceNodeProxy()
        {
            Devices = new ConcurrentDictionary<string, PhysicDeviceProxy>();
        }

        public ServiceNodeProxy(ServiceNodeQueryResponse.NodeServiceTO svr) : this()
        {
            if (svr == null) throw new ArgumentNullException(nameof(svr));

            ServiceName = svr.ServiceName ?? string.Empty;
            ServiceType = svr.ServiceType ?? string.Empty;
            ServiceToken = svr.ServiceToken ?? string.Empty;
            ServiceCode = svr.ServiceCode ?? string.Empty;
            UpChannel = svr.UpChannel ?? string.Empty;
            DownChannel = svr.DownChannel ?? string.Empty;

            if (svr.Devices != null)
            {
                foreach (var dev in svr.Devices)
                {
                    Devices.TryAdd(dev.Key, new PhysicDeviceProxy(dev.Value,this));
                }
            }
        }

        public void Update(ServiceNodeQueryStatusResponse.NodeServiceTO svr)
        {
            if (svr?.DeviceList == null) return;

            if (DateTime.TryParse(svr.LiveTime, out var liveTime))
            {
                LiveTime = liveTime;
                OverTime = svr.OverTime;
                IsLive = CheckLive();
                foreach (var dev in svr.DeviceList)
                {
                    if (dev?.Code != null && Devices.TryGetValue(dev.Code, out var device))
                    {
                        device.Update(dev);
                    }
                }
            }
        }

        public bool CheckLive()
        {
            var expirationTime = OverTime > 0
                ? LiveTime.AddMilliseconds(OverTime * 1.5)
                : LiveTime;

            return expirationTime > DateTime.Now;
        }

        public class DeviceNodeProxy
        {
            public string Code { get; }
            public string Name { get; }
            public string Status { get; private set; }

            public DeviceNodeProxy(ServiceNodeQueryResponse.NodeDeviceTO dev)
            {
                if (dev == null) throw new ArgumentNullException(nameof(dev));

                Code = dev.Code ?? string.Empty;
                Name = dev.Name ?? string.Empty;
                Status = dev.Status ?? string.Empty;
            }

            public void Update(ServiceNodeQueryStatusResponse.NodeDeviceTO dev)
            {
                if (dev == null) return;
                Status = dev.Status ?? string.Empty;
            }
        }
    }
}
