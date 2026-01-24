using ColorVision.Core.Entities;
using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.Core.ViewModels;
using Newtonsoft.Json;

namespace CVWaferProber.ViewModels
{
    public class FlowViewModel : ViewModelBase
    {
        public FlowViewModel(RespDataFlowTempDTO flow)
        {
            this.Id = flow.Id;
            this.Name = flow.Name;
        }

        /// <summary>
        /// 
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }

    }
    public struct BuzProductCfg
    {
        public int Timeout { get; set; }
    }
    public class WPFlowViewModel : ViewModelBase
    {
        public WPFlowViewModel(TScgdBuzProductDetail flow)
        {
            Id = -1;
            if (string.IsNullOrEmpty(flow.Name)) Name = "null";
            else Name = flow.Name;
            switch (flow.Code)
            {
                case "Flow.AOI":
                    FlowType = CVWaferProberFlowType.AOI;
                    break;
                case "Flow.IVL":
                    FlowType = CVWaferProberFlowType.IVL;
                    break; 
                //case "Flow.IVL.SP":
                //    FlowType = CVWaferProberFlowType.IVL_SP;
                //    break;
                //case "Flow.IVL.Camera":
                //    FlowType = CVWaferProberFlowType.IVL_Camera;
                //    break;
                case "Flow.EQE":
                    FlowType = CVWaferProberFlowType.EQE;
                    break;
                case "Flow.VAM":
                    FlowType = CVWaferProberFlowType.VAM;
                    break;
                case "Flow.IV":
                    FlowType = CVWaferProberFlowType.IV;
                    break;
            }
            if (!string.IsNullOrEmpty(flow.CfgJson))
            {
                BuzProductCfg cfg = JsonConvert.DeserializeObject<BuzProductCfg>(flow.CfgJson);
                this.Timeout = cfg.Timeout;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public CVWaferProberFlowType FlowType { get; set; }

        /// <summary>
        /// 超时时间,单位S
        /// </summary>
        public int Timeout { get; set; } = 120;
    }

    public enum CVWaferProberFlowType
    {
        AOI,
        IVL,
        IVL_SP,
        IVL_Camera,
        EQE,
        VAM,
        IV
    }
}
