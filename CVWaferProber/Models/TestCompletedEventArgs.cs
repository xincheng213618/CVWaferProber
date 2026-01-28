using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Models
{
    public class TestCompletedEventArgs : EventArgs
    {
        public TestCompletedEventArgs(bool isAuto)
        {
            this.IsAuto = isAuto;
        }

        public bool IsAuto {  get; set; }
    }
}
