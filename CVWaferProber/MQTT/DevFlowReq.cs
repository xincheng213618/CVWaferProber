using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.MQTT
{
    public class CVTemplateParam
    {
        public int ID { get; set; }
        public string Name { get; set; }

        public CVTemplateParam()
        {
            this.ID = -1;
            this.Name = string.Empty;
        }

        [JsonIgnore]
        public bool IsValid { get => (ID > 0 || !string.IsNullOrWhiteSpace(Name)); }
    }
    public enum FlowRequestType
    {
        [Description("加载")]
        Load,
        [Description("运行")]
        Run,
        [Description("停止")]
        Stop,
        [Description("复合流程停止")]
        StopCombined,
        [Description("复合流程运行")]
        CombinedRun,
        [Description("获取复合流程结果")]
        GetCombinedResult,
    }
    public interface IDeviceRequest
    {
        string SerialNumber { get; set; }
        string Version { get; set; }
        string DeviceCode { get; set; }
        int ZIndex { get; set; }
        bool Ready { get; set; }
        bool NeedAuth { get; set; }
        string Reason { get; set; }
    }
    public class DeviceCVBaseRequest<C, P> : DeviceCVBaseNoParamRequest<C>
    {
        public P Params { get; set; }

        public DeviceCVBaseRequest(string deviceCode, string serialNumber, C request, P param) : this(deviceCode, serialNumber, -1, request, param)
        {
        }
        public DeviceCVBaseRequest(string deviceCode, string serialNumber, int zindex, C request, P param) : this(deviceCode, serialNumber, zindex, request, string.Empty, param)
        {
        }
        public DeviceCVBaseRequest(string deviceCode, string serialNumber, int zindex, C request, string version, P param) : base(deviceCode, serialNumber, zindex, request, version)
        {
            this.Params = param;
        }
    }
    public class DeviceCVBaseNoParamRequest<C>
    {
        public string DeviceCode { get; set; }
        public string SerialNumber { get; set; }
        public bool Ready { get; set; }
        public string Reason { get; set; }
        public string Version { get; set; }
        public bool NeedAuth { get; set; }
        public int ZIndex { get; set; }
        public C DeviceRequestType { get; set; }
        public DeviceCVBaseNoParamRequest(string deviceCode, string serialNumber, C request) : this(deviceCode, serialNumber, -1, request, string.Empty)
        {
        }
        public DeviceCVBaseNoParamRequest(string deviceCode, string serialNumber, C request, string version) : this(deviceCode, serialNumber, -1, request, version)
        {
        }
        public DeviceCVBaseNoParamRequest(string deviceCode, string serialNumber, int zindex, C request, string version)
        {
            this.DeviceCode = deviceCode;
            this.SerialNumber = serialNumber;
            this.DeviceRequestType = request;
            this.Version = version;
            this.ZIndex = zindex;
            this.Ready = true;
            this.NeedAuth = true;
        }
    }
    public interface IDevFlowRequest : IDeviceRequest
    {
        FlowRequestType DeviceRequestType { get; }
        DateTime StartTime { get; set; }
    }
    public class DeviceFlowRuntimeParam
    {
        public string FlowData { get; set; }
    }
    public class DeviceFlowCombinedRunParam<T>
    {
        public string Name { get; set; }
        public int Timeout { get; set; } = 600;
        public CVTemplateParam TemplateParam { get; set; }
        public List<T> Services { get; set; }
    }
    public class DeviceFlowRunParam<T>
    {
        public string Name { get; set; }
        public CVTemplateParam TemplateParam { get; set; }
        public List<T> Services { get; set; }
    }

    public class DeviceFlowLoadParam<T>
    {
        public CVTemplateParam TemplateParam { get; set; }
        public List<T> Services { get; set; }
    }
    public class DeviceFlowCombinedRuntime<T>
    {
        public string SNSuffixes { get; private set; }
        public string PSerialNumber { get; private set; }
        public string PName { get; private set; }
        public string FlowName { get; private set; }
        [JsonIgnore]
        public string FlowData { get; private set; }
        [JsonIgnore]
        public IDevFlowRequest Request { get; set; }
        [JsonIgnore]
        public IDevFlowResponse Response { get; set; }

        public DeviceFlowCombinedRuntime(CombinedFlowTempCfg flowTemp) : this(flowTemp.Name, flowTemp.FlowData, flowTemp.SNSuffixes)
        {
        }

        public DeviceFlowCombinedRuntime(string flowName, string flowData, string snSuffixes)
        {
            this.FlowName = flowName;
            this.FlowData = flowData;
            this.SNSuffixes = snSuffixes;
            this.PSerialNumber = string.Empty;
            this.PName = string.Empty;
            this.Request = null;
            this.Response = null;
        }

        public DeviceFlowCombinedRuntime(string sNSuffixes, string pSerialNumber, string pName, string flowName, string flowData, IDevFlowRequest request, IDevFlowResponse response) : this(flowName, flowData, sNSuffixes)
        {
            this.PSerialNumber = pSerialNumber;
            this.PName = pName;
            this.Request = request;
            this.Response = response;
        }

        public DeviceFlowCombinedRuntime<T> Clone()
        {
            return new DeviceFlowCombinedRuntime<T>(SNSuffixes, PSerialNumber, PName, FlowName, FlowData, Request, Response);
        }
        public void BuildReq(DeviceFlowCombinedRun<T> req)
        {
            ChangeProduct(req.Params.Name, req.SerialNumber);
            BuildReq(req.DeviceCode);
        }
        public void BuildReq(string deviceCode)
        {
            Request = null;
            Response = null;
            if (!string.IsNullOrEmpty(PSerialNumber) && !string.IsNullOrEmpty(PName))
            {
                string PSN = string.Format("{0}{1}", PSerialNumber, SNSuffixes);
                Request = new DeviceFlowRun<T>(deviceCode, PSN, new DeviceFlowRunParam<T>() { Name = PName }) { };
                Response = null;
            }
        }

        public void ChangeProduct(string PName, string PSerialNumber)
        {
            this.PSerialNumber = PSerialNumber;
            this.PName = PName;
        }

        public void ChangeProduct(DeviceFlowCombinedRuntime<T> product)
        {
            ChangeProduct(product.PName, product.PSerialNumber);
        }
    }
    public abstract class DeviceFlowBaseRequest<P> : DeviceCVBaseRequest<FlowRequestType, P>, IDevFlowRequest
    {
        public DeviceFlowBaseRequest(string deviceCode, string serialNumber, FlowRequestType request, P param) : base(deviceCode, serialNumber, request, param)
        {
        }

        public DeviceFlowBaseRequest(string deviceCode, string serialNumber, int zindex, FlowRequestType request, P param) : base(deviceCode, serialNumber, zindex, request, param)
        {
        }

        public DeviceFlowBaseRequest(string deviceCode, string serialNumber, int zindex, FlowRequestType request, string version, P param) : base(deviceCode, serialNumber, zindex, request, version, param)
        {
        }

        public DateTime StartTime { get; set; } = System.DateTime.Now;
    }
    public class DeviceFlowCombinedRun<T> : DeviceFlowBaseRequest<DeviceFlowCombinedRunParam<T>>
    {
        public DeviceFlowCombinedRuntime<T>[] Runtimes { get; set; }
        public DeviceFlowCombinedRun(string deviceCode, string serialNumber, DeviceFlowCombinedRunParam<T> param) : base(deviceCode, serialNumber, FlowRequestType.CombinedRun, param)
        {
        }
    }
    public class DeviceFlowRun<T> : DeviceFlowBaseRequest<DeviceFlowRunParam<T>>
    {
        public DeviceFlowRuntimeParam RuntimeParam { get; set; }

        public DeviceFlowRun(string deviceCode, string serialNumber, DeviceFlowRunParam<T> param) : base(deviceCode, serialNumber, FlowRequestType.Run, param)
        {
        }
    }

    public class DeviceFlowLoad<T> : DeviceFlowBaseRequest<DeviceFlowLoadParam<T>>
    {
        public DeviceFlowRuntimeParam RuntimeParam { get; set; }
        public DeviceFlowLoad(string deviceCode, string serialNumber, DeviceFlowLoadParam<T> param) : base(deviceCode, serialNumber, FlowRequestType.Load, param)
        {
        }
    }
    //DeviceFlowStop
    public class DeviceFlowStop : DeviceCVBaseNoParamRequest<FlowRequestType>, IDevFlowRequest
    {
        public DeviceFlowStop(string deviceCode, string serialNumber) : base(deviceCode, serialNumber, FlowRequestType.Stop)
        {
        }

        public DateTime StartTime { get; set; } = System.DateTime.Now;
    }
    public class DeviceFlowStopCombined : DeviceCVBaseNoParamRequest<FlowRequestType>, IDevFlowRequest
    {
        public DeviceFlowStopCombined(string deviceCode, string serialNumber) : base(deviceCode, serialNumber, FlowRequestType.StopCombined)
        {
        }

        public DateTime StartTime { get; set; } = System.DateTime.Now;
    }

    public class DeviceFlowGetCombinedResult<T> : DeviceFlowBaseRequest<CVTemplateParam>
    {
        public DeviceFlowCombinedRuntime<T>[] Runtimes { get; set; }
        public DeviceFlowGetCombinedResult(string deviceCode, string serialNumber, CVTemplateParam param) : base(deviceCode, serialNumber, FlowRequestType.GetCombinedResult, param)
        {
        }
    }

    public class CombinedFlowTempCfg
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string FlowData { get; set; }
        public string SNSuffixes { get; set; }
    }
}
