using CVWaferProber.Core.Restful.DTO;
using Newtonsoft.Json;
using RestSharp;

namespace CVWaferProber.Core.Restful
{
    public class CVRestfulHelper
    {
        //private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVRestfulHelper));

        private static string rcRegIp = "127.0.0.1:8080";
        private static string method = "http://";
        private static string rcUrl = method + rcRegIp;
        public static string? RestPost(string baseUrl, string resource, string token, string body)
        {
            var client = new RestClient(baseUrl);
            var request = new RestRequest(resource, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token)) request.AddHeader("Authorization", $"Bearer {token}");
            request.AddHeader("Accept", "*/*");
            request.AddHeader("Host", rcRegIp);
            request.AddHeader("Connection", "keep-alive");
            request.AddParameter("application/json", body, ParameterType.RequestBody);
            RestResponse response = client.Execute(request);
            return response.Content;
        }
        public static string? RestGet(string baseUrl, string resource, string token)
        {
            var client = new RestClient(baseUrl);
            var request = new RestRequest(resource, Method.Get);
            if (!string.IsNullOrEmpty(token)) request.AddHeader("Authorization", $"Bearer {token}");
            request.AddHeader("Accept", "*/*");
            request.AddHeader("Host", rcRegIp);
            request.AddHeader("Connection", "keep-alive");
            request.Method = Method.Get;
            RestResponse response = client.Execute(request);
            return response.IsSuccessful ? response.Content : string.Empty;
        }
        public string? RcLoadFlows(string token)
        {
            string res = "/API/Services/Flow/Templates";
            AddReqMsg(string.Format("GET {0}", res));
            string? contentResp = RestGet(rcUrl,res, token);
            return contentResp;
            //AddLog(contentResp);  
        }

        public string? RcRegist()
        {
            string url = "/API/Node/Regist";
            RequestRegDTO reqBody = new RequestRegDTO() { NodeAppId = "app1", NodeKey = "123456", NodeName = "api.client.1", ServiceType = "client" };
            var body = JsonConvert.SerializeObject(reqBody);
            AddReqMsg(body);
            string? contentResp = RestPost(rcUrl, url, string.Empty, body);
            return contentResp;
        }

        public string? RcRunFlow(int fid,string serialNumber, string accessToken)
        {
            ReqBodyRunFlowDTO data = new ReqBodyRunFlowDTO() { Id = fid };
            RequestDTO<ReqBodyRunFlowDTO> reqBody = new RequestDTO<ReqBodyRunFlowDTO>() { DeviceCode = "DEV.Flow.Default", SerialNumber = serialNumber, Data = data };
            var body = JsonConvert.SerializeObject(reqBody);
            string url = "/API/Services/Flow/SVR.Flow.Default/Startup";
            AddReqMsg(url, reqBody);
            string? contentResp = RestPost(rcUrl, url, accessToken, body);
            return contentResp;

        }   
        public string? RcRunNameFlow(int fid, string serialNumber, string name, string accessToken)
        {
            ReqBodyRunCombinedFlowDTO data = new ReqBodyRunCombinedFlowDTO() { Name = name, Template = new ReqBodyRunFlowDTO() { Id = fid } };
            RequestDTO<ReqBodyRunCombinedFlowDTO> reqBody = new RequestDTO<ReqBodyRunCombinedFlowDTO>() { DeviceCode = "DEV.Flow.Default", SerialNumber = serialNumber, Data = data };
            var body = JsonConvert.SerializeObject(reqBody);
            string url = "/API/Services/Flow/SVR.Flow.Default/StartupEx";
            AddReqMsg(url, reqBody);
            string? contentResp = RestPost(rcUrl, url, accessToken, body);
            return contentResp;

        }
        public string? RcGetFlowResult_POI(string serialNumber, string accessToken)
        {
            string res = string.Format("/API/Services/Flow/Result/{0}/Algorithm_POI_Y", serialNumber);
            AddReqMsg(string.Format("GET {0}", res));
            string? contentResp = RestGet(rcUrl, res, accessToken);
            //AddLog(contentResp);
            return contentResp;
        }
        public string? RcGetFlowResult_POI_Detail(string getURL, string accessToken)
        {
            string? contentResp = RestGet(rcUrl, getURL, accessToken);
            return contentResp;
        }
        private void AddReqMsg<T>(string url, RequestDTO<T> req)
        {
            string msg;
            if (req != null) msg = string.Format("POST {0} => {1}", url, JsonConvert.SerializeObject(req, Formatting.Indented));
            else msg = string.Format("GET {0}", url);
            AddReqMsg(msg);
        }
        private void AddReqMsg(string msg)
        {
            AddLog(msg);
        }
        private void AddLog(string content, bool isClear = false)
        {

        }
    }
}
