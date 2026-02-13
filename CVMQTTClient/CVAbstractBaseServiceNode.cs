using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVMQTTClient
{
    public abstract class CVAbstractBaseServiceNode
    {
        public string ServiceCode { get; set; }
        public string ServiceName { get; set; }
        public string ServiceType { get; set; }
        public string ServiceToken { get; set; }

        public CVAbstractBaseServiceNode()
        {
            this.ServiceToken = string.Empty;
            this.ServiceCode = string.Empty;
            this.ServiceName = string.Empty;
            this.ServiceType = string.Empty;
        }

        public CVAbstractBaseServiceNode(string serviceCode, string serviceName, string serviceType, string serviceToken)
        {
            ServiceToken = serviceToken;
            ServiceCode = serviceCode;
            ServiceName = serviceName;
            ServiceType = serviceType;
        }
        public CVAbstractBaseServiceNode(string serviceCode, string serviceName, string serviceType) : this(serviceCode, serviceName, serviceType, string.Empty)
        {
        }
    }
}
