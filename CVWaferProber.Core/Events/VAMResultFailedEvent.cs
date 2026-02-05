using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Events
{
    public class VAMResultFailedEvent
    {
        // 传递错误信息给UI层
        public string ErrorMessage { get; }

        public VAMResultFailedEvent(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}
