using ColorVision.Core.Entities;
using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.Core.ViewModels;

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

    public class WPFlowViewModel : ViewModelBase
    {
        public WPFlowViewModel(TScgdBuzProductDetail flow)
        {
            Id = -1;
            Name = flow.Name;
            switch (flow.Code)
            {
                case "Flow.AOI":
                    FlowType = CVWaferProberFlowType.AOI;
                    break;
                case "Flow.IVL.SP":
                    FlowType = CVWaferProberFlowType.IVL_SP;
                    break;
                case "Flow.IVL.Camera":
                    FlowType = CVWaferProberFlowType.IVL_Camera;
                    break;
                case "Flow.EQE":
                    FlowType = CVWaferProberFlowType.EQE;
                    break;
                case "Flow.VAM":
                    FlowType = CVWaferProberFlowType.VAM;
                    break;
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
    }

    public enum CVWaferProberFlowType
    {
        AOI,
        IVL_SP,
        IVL_Camera,
        EQE,
        VAM
    }
}
