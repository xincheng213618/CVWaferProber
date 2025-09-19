using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Restful.DTO
{
    public class Token
    {
        /// <summary>
        /// 
        /// </summary>
        public string AccessToken { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int Expires { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string RefreshToken { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public long Timestamp { get; set; }
    }

    public class RespDataRegDTO
    {
        /// <summary>
        /// 
        /// </summary>
        public string NodeAppId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string NodeName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string RCName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ServiceType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<string> StartupServices { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Token Token { get; set; }
    }
}
