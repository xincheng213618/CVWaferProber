using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Models
{
    public class ConnectionSettings
    {
        public string ServerIP { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8898;
    }
}
