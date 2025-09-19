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
}
