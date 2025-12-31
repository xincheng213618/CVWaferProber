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

        public string ResultFileName {  get; set; }
    }

    public class VAMFlowStartingEvent : BaseEvent
    {

    }
}
