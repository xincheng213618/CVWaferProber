namespace CVMQTTNodeClient
{
    public class MQTTCVServiceTopicBuilder
    {
        public const string RCServiceType = "MQTTRCService";
        public const string RCRegTopic = RCServiceType + "/Regist";
        public const string RCHeartbeatTopic = RCServiceType + "/Heartbeat";
        public const string RCPublicTopic = RCServiceType + "/Public";
        public const string RCAdminTopic = RCServiceType + "/Admin";
        public const string RCNodeTopic = RCServiceType + "/Node";

        public static string BuildNodeName(string serviceType, string nodeName)
        {
            if (string.IsNullOrWhiteSpace(nodeName)) { nodeName = Guid.NewGuid().ToString(); }
            return serviceType + "." + nodeName;
        }

        public static string BuildNodeTopic(string nodeName, string rcName)
        {
            return string.Format("{0}/{1}/{2}", RCNodeTopic, nodeName, rcName);// RCNodeTopic + "/" + nodeName + "/" + Guid.NewGuid().ToString("N");
        }

        public static string BuildNodeTopic(string nodeName)
        {
            return string.Format("{0}/{1}", RCNodeTopic, nodeName);// RCNodeTopic + "/" + nodeName + "/" + Guid.NewGuid().ToString("N");
        }

        public static string BuildRegTopic(string nodeName)
        {
            return RCRegTopic + "/" + nodeName;
        }
        public static string BuildHeartbeatTopic(string nodeName)
        {
            return RCHeartbeatTopic + "/" + nodeName;
        }
        public static string BuildPublicTopic(string nodeName)
        {
            return RCPublicTopic + "/" + nodeName;
        }
        public static string BuildAdminTopic(string nodeName)
        {
            return RCAdminTopic + "/" + nodeName;
        }
        public static string BuildFlowTopic(string nodeName)
        {
            return "MQTTRCService/Flow/" + nodeName;
        }

        public static string BuildArchivedTopic(string nodeName)
        {
            return "MQTTRCService/Archived/" + nodeName;
        }
        public static string BuildServiceUpTopic(string serviceType, string serviceName, string rcName)
        {
            return string.Format("{2}/{0}/{1}/CMD", serviceType, serviceName, rcName); 
            //serviceType + "/CMD/" + serviceName + "/" + serviceId;
            //return serviceType + "/Up/" + serviceName + "/" + serviceId;
        }

        public static string BuildServiceDownTopic(string serviceType, string serviceName, string rcName)
        {
            return string.Format("{2}/{0}/{1}/STATUS", serviceType, serviceName, rcName);
            //serviceType + "/STATUS/" + serviceName + "/" + serviceId;
            //return serviceType + "/Down/" + serviceName + "/" + serviceId;
        }

        public static string BuildSysConfigTopic(string nodeName)
        {
            return "SysRes/config/" + nodeName;
        }

        public static string BuildSysConfigRespTopic(string nodeName)
        {
            return "SysRes/config/Resp/" + nodeName;
        }
    }
}
