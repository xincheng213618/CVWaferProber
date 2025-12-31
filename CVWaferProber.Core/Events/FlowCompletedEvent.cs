using CVWaferProber.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}
