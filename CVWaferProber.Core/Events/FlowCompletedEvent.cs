using ColorVision.Core.Entities;
using CVWaferProber.Core.ViewModels;

namespace CVWaferProber.Core.Events
{
    public class FlowCompletedEvent : BaseEvent
    {
        public FlowCompletedEvent(ViewModelBase senderVM)
        {
            this.SenderVM = senderVM;
        }

        public ViewModelBase SenderVM { get; protected set; }
    }

    public class VAMFlowCompletedEvent : BaseEvent
    {
        public VAMFlowCompletedEvent(string resultFileName)
        {
            ResultFileName = resultFileName;
        }

        public string ResultFileName {  get; protected set; }
    }

    public class VAMFlowStartingEvent : BaseEvent
    {

    }
    public class VAMResultGUIClearEvent : BaseEvent
    {

    }
    public  class EQEFlowCompletedEvent : BaseEvent
    {
        public EQEFlowCompletedEvent(List<VScgdMeasureResultEqe> results)
        {
            Results = results;
        }

        public List<VScgdMeasureResultEqe> Results { get; protected set; }
    }
    public class EQEResultGUIClearEvent : BaseEvent
    {

    }
}
