using CVWaferProber.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Models
{
    public class TestCompletedEventArgs : EventArgs
    {
        public TestCompletedEventArgs(DieViewModel? dieViewModel, bool isAuto)
        {
            this.DieVM = dieViewModel;
            this.IsAuto = isAuto;
        }

        public DieViewModel? DieVM { get; set; }
        public bool IsAuto {  get; set; }
    }
}
