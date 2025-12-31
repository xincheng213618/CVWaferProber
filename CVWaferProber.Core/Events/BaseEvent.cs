using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Events
{
    public class BaseEvent
    {
        public DateTime Timestamp { get; } = DateTime.Now;
    }
}
