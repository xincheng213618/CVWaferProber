using CVCommCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WaferComm.Core;

namespace CVWaferProber.Core.Events
{
    public class CVWPEventAggregatorInstance : ReflectionSingleton<CVWPEventAggregatorInstance>
    {

        private readonly IEventAggregator eventAggregator;

        private CVWPEventAggregatorInstance()
        {
            this.eventAggregator = new EventAggregator();
        }
    }
}
