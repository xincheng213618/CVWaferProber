using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Restful.DTO
{
    public class ReqBodyRunFlowDTO
    {
        /// <summary>
        /// 
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }
    } 
    
    public class ReqBodyRunCombinedFlowDTO
    {
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }
        public ReqBodyRunFlowDTO Template { get; set; }
    }
}
