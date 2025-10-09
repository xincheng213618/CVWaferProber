using CVWaferProber.Core.Restful;
using CVWaferProber.Core.Restful.DTO;
using Newtonsoft.Json;

namespace CVWaferProber.Models
{
    public class RCRestModel
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(RCRestModel));

        private CVRestfulHelper restful = new CVRestfulHelper();
        private RespDataRegDTO? RegDTO;

        public bool RcRegist()
        {
            var contentResp = restful.RcRegist();
            if (!string.IsNullOrEmpty(contentResp))
            {
                RespDTO<RespDataRegDTO>? respData = JsonConvert.DeserializeObject<RespDTO<RespDataRegDTO>>(contentResp);
                if (respData != null)
                {
                    if (respData.IsSuccess)
                    {
                        RegDTO = respData.Data;
                        if (logger.IsInfoEnabled) logger.InfoFormat("Rc Regist ok => {0}", JsonConvert.SerializeObject(RegDTO));
                    }
                    return respData.IsSuccess;
                }
            }
            if(logger.IsErrorEnabled) logger.ErrorFormat("Rc Regist failed => {0}", contentResp);
            return false;
        }

        public List<RespDataFlowTempDTO>? RcLoadFlows()
        {
            if (RegDTO != null)
            {
                var contentResp = restful.RcLoadFlows(RegDTO.Token.AccessToken);
                if (!string.IsNullOrEmpty(contentResp))
                {
                    RespDTO<List<RespDataFlowTempDTO>>? respData = JsonConvert.DeserializeObject<RespDTO<List<RespDataFlowTempDTO>>>(contentResp);
                    if (respData != null && respData.IsSuccess)
                    {
                        if (logger.IsInfoEnabled) logger.InfoFormat("LoadFlow ok => {0}", JsonConvert.SerializeObject(respData.Data));
                        return respData.Data;
                    }
                }
                if (logger.IsErrorEnabled) logger.ErrorFormat("LoadFlow failed => {0}", contentResp);
            }
            else
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            }

            return null;
        }

        public RespDataRunFlowDTO RcRunFlowById(int fid, string serialNumber)
        {
            if (RegDTO != null)
            {
                var contentResp = restful.RcRunFlow(fid, serialNumber, RegDTO.Token.AccessToken);
                if (!string.IsNullOrEmpty(contentResp))
                {
                    RespDTO<RespDataRunFlowDTO>? respData = JsonConvert.DeserializeObject<RespDTO<RespDataRunFlowDTO>>(contentResp);
                    if (respData != null && respData.IsSuccess)
                    {
                        if (logger.IsInfoEnabled) logger.InfoFormat("RunFlow ok => {0}", JsonConvert.SerializeObject(respData.Data));
                        return respData.Data;
                    }
                }
                if (logger.IsErrorEnabled) logger.ErrorFormat("RunFlow failed => {0}", contentResp);
            }
            else
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            }
            return null;
        } 
        public RespDataRunFlowDTO RcRunFlowByName(string fname, string serialNumber)
        {
            if (RegDTO != null)
            {
                var contentResp = restful.RcRunFlow(fname, serialNumber, RegDTO.Token.AccessToken);
                if (!string.IsNullOrEmpty(contentResp))
                {
                    RespDTO<RespDataRunFlowDTO>? respData = JsonConvert.DeserializeObject<RespDTO<RespDataRunFlowDTO>>(contentResp);
                    if (respData != null && respData.IsSuccess)
                    {
                        if (logger.IsInfoEnabled) logger.InfoFormat("RunFlow ok => {0}", JsonConvert.SerializeObject(respData.Data));
                        return respData.Data;
                    }
                }
                if (logger.IsErrorEnabled) logger.ErrorFormat("RunFlow failed => {0}", contentResp);
            }
            else
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            }
            return null;
        }

        public RespDataFlowResultDTO<AlgResultItem>? RcGetFlowResult_POI(string serialNumber) 
        {
            if (RegDTO != null)
            {
                var contentResp = restful.RcGetFlowResult_POI(serialNumber, RegDTO.Token.AccessToken);
                if (!string.IsNullOrEmpty(contentResp))
                {
                    RespDTO<RespDataFlowResultDTO<AlgResultItem>>? respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                    if (respData != null && respData.IsSuccess)
                    {
                        if (logger.IsInfoEnabled) logger.InfoFormat("GetFlow Result ok => {0}", JsonConvert.SerializeObject(respData.Data));
                        return respData.Data;
                    }
                }
                if (logger.IsErrorEnabled) logger.ErrorFormat("GetFlow Result failed => {0}", contentResp);

            }
            else
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            }
            return null;
        }

        public List<RespDataDTO_CIE> RcGetFlowResult_POI_Detail(string getURL)
        {
            if (RegDTO != null)
            {
                var contentResp = restful.RcGetFlowResult_POI_Detail(getURL, RegDTO.Token.AccessToken);
                if (!string.IsNullOrEmpty(contentResp))
                {
                    RespDTO<List<RespDataDTO_CIE>>? respData = JsonConvert.DeserializeObject<RespDTO<List<RespDataDTO_CIE>>>(contentResp);
                    if (respData != null && respData.IsSuccess)
                    {
                        if (logger.IsInfoEnabled) logger.InfoFormat("Get POI Result ok => {0}", JsonConvert.SerializeObject(respData.Data));
                        return respData.Data;
                    }
                }
                if (logger.IsErrorEnabled) logger.ErrorFormat("Get POI Result failed => {0}", contentResp);

            }
            else
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            }
            return null;
        }

        public RespDataFlowResultDTO<AlgResultItem>? RcGetFlowResult_SP(string serialNumber)
        {
            if (RegDTO != null)
            {
                var contentResp = restful.RcGetFlowResult_SP(serialNumber, RegDTO.Token.AccessToken);
                if (!string.IsNullOrEmpty(contentResp))
                {
                    RespDTO<RespDataFlowResultDTO<AlgResultItem>>? respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                    if (respData != null && respData.IsSuccess)
                    {
                        if (logger.IsInfoEnabled) logger.InfoFormat("GetFlow Result ok => {0}", JsonConvert.SerializeObject(respData.Data));
                        return respData.Data;
                    }
                }
                if (logger.IsErrorEnabled) logger.ErrorFormat("GetFlow Result failed => {0}", contentResp);

            }
            else
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            }
            return null;
        }

        public RespDTO<RespDataFlowResultDTO<AlgResultItem>>? RcGetFlowResult_AOI(string serialNumber)
        {
            if (RegDTO != null)
            {
                var contentResp = restful.RcGetFlowResult_AOI(serialNumber, RegDTO.Token.AccessToken);
                if (!string.IsNullOrEmpty(contentResp))
                {
                    RespDTO<RespDataFlowResultDTO<AlgResultItem>>? respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                    if (respData != null && respData.IsSuccess)
                    {
                        if (logger.IsInfoEnabled) logger.InfoFormat("GetFlow Result ok => {0}", JsonConvert.SerializeObject(respData.Data));
                    }
                    return respData;
                }
                if (logger.IsErrorEnabled) logger.ErrorFormat("GetFlow Result failed => {0}", contentResp);

            }
            else
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            }
            return null;
        }
    }
}
