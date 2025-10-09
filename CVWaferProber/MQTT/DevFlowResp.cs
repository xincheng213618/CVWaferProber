using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.MQTT
{
    public interface IDeviceResponse
    {
        /// <summary>
        /// true:成功或失败都存库; false:成功存库,失败不存库
        /// </summary>
        bool IsAlwaysPersistence { get; set; }
        int Code { get; }
        string Desc { get; }
        bool IsOk();
        bool IsFailed();
        bool IsPending();
        bool IsUnauthorized();
        bool IsSended();
        void SetSended(bool value);
        void ToPending();
        void ToFailed(string desc);
    }
    public enum FlowResultType
    {
        [Description("加载")]
        Load,
        [Description("运行")]
        Run,
        [Description("停止")]
        Stop,
        [Description("组合运行")]
        CombinedRun,
        [Description("获取复合流程结果")]
        GetCombinedResult,
    }
    public interface IDevFlowResponse : IDeviceResponse
    {
        FlowResultType ResultType { get; }
        long TotalTime { get; }
        int MasterId { get; set; }
        string Status { get; set; }
    }
}
