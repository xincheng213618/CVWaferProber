using CVWaferProber.Core.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ChipMapping.Models
{
    public class StatusTip
    {
        public ChipStatus Status { get; set; }
        public Brush Color { get; set; }
        public string Description { get; set; }
    }
}
