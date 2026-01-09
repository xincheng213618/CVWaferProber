using CVWaferProber.Core.Restful;
using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.Models;
using Newtonsoft.Json;
using WaferComm.Client;
using WaferComm.Core;

namespace CVWaferProber.Services
{
    public class RCRestService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(RCRestService));

        private CVRestfulHelper restful = new CVRestfulHelper();
        private RespDataRegDTO? RegDTO;
        private const int MaxRetryCount = 2; // 注册/接口调用最大重试次数
        private EventAggregator eventAggregator;
        public static bool IsEnglishMode = false;
        public RCRestService()
        {
            ConnectionInfo = new ConnectionInfo("Registed", "UnRegisted") { ServerIP = "127.0.0.1", Port = 8080 };
            eventAggregator = new EventAggregator();
        }
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            eventAggregator.Subscribe(handler);
        }     
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            eventAggregator.Unsubscribe(handler);
        }
        public ConnectionInfo ConnectionInfo { get; private set; }
        /// <summary>
        /// 确保已注册（未注册则自动触发注册）
        /// </summary>
        /// <returns>注册是否success</returns>
        public bool EnsureRegistered()
        {
            // 已注册且Token有效（简单判断，可根据实际Token过期规则优化）
            if (IsRegistered) return true;

            // 未注册，执行注册（最多重试2次）
            for (int i = 0; i < MaxRetryCount; i++)
            {
                logger.InfoFormat(IsEnglishMode? $"Start registration #{0}" : $"开始第{0}次注册...", i + 1);
                if (RcRegist())
                {
                    logger.Info("Regist success");
                    return true;
                }
                logger.WarnFormat(IsEnglishMode? $"Registration attempt {0} failed, retrying after 1 second" : $"第{0}次注册failed，等待1秒后重试...", i + 1);
                System.Threading.Thread.Sleep(1000); // 重试间隔1秒
            }

            logger.Error(IsEnglishMode? "Registration failed, maximum retry attempts reached" : "Regist failed，已达到最大重试次数");
            return false;
        }

        public bool IsRegistered => (RegDTO != null && !string.IsNullOrEmpty(RegDTO.Token?.AccessToken));

        public bool RcUnRegist()
        {
            RegDTO = null;
            restful.RcUnRegist(ConnectionInfo.ServerIP, ConnectionInfo.Port);
            PublishStatus(ConnectionStatus.Disconnected);
            return true;
        }

        private void PublishStatus(ConnectionStatus status)
        {
            ConnectionInfo.SetConnected(status == ConnectionStatus.Connected);
            eventAggregator.Publish(new ConnectionStateChangedEvent(ConnectionInfo.IsConnected, ConnectionInfo.ServerIP, ConnectionInfo.Port));
        }
        public bool RcRegist()
        {
            try
            {
                var contentResp = restful.RcRegist(ConnectionInfo.ServerIP, ConnectionInfo.Port);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.Error(IsEnglishMode? "Regist failed：The interface returned empty content" : "Regist failed：接口返回空内容");
                    PublishStatus(ConnectionStatus.Disconnected);
                    return false;
                }

                // 捕获反序列化异常
                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataRegDTO>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat(IsEnglishMode ? $"Regist failed：The returned content cannot be deserialized => {0}" : $"Regist failed：返回内容无法反序列化 => {0}", contentResp);
                    PublishStatus(ConnectionStatus.Disconnected);
                    return false;
                }

                if (respData.IsSuccess && respData.Data != null && !string.IsNullOrEmpty(respData.Data.Token?.AccessToken))
                {
                    RegDTO = respData.Data;
                    PublishStatus(ConnectionStatus.Connected);
                    logger.InfoFormat("Regist success => {0}", JsonConvert.SerializeObject(RegDTO, Formatting.Indented));
                    return true;
                }

                logger.ErrorFormat("Regist failed：{0} => {1}", respData.Message, contentResp);
                PublishStatus(ConnectionStatus.Disconnected);
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(IsEnglishMode? "Registration process error" : "注册过程异常", ex);
                PublishStatus(ConnectionStatus.Disconnected);
                return false;
            }
            /*var contentResp = restful.RcRegist();
            if (!string.IsNullOrEmpty(contentResp))
            {
                RespDTO<RespDataRegDTO>? respData = JsonConvert.DeserializeObject<RespDTO<RespDataRegDTO>>(contentResp);
                if (respData != null)
                {
                    if (respData.IsSuccess)
                    {
                        RegDTO = respData.Data;
                        if (logger.IsInfoEnabled) logger.InfoFormat("Rc Regist ok => {0}", JsonConvert.SerializeObject(RegDTO, Formatting.Indented));
                    }
                    return respData.IsSuccess;
                }
            }
            if (logger.IsErrorEnabled) logger.ErrorFormat("Rc Regist failed => {0}", contentResp);
            return false;*/
        }

        public List<RespDataFlowTempDTO>? RcLoadFlows()
        {
            // 先确保已注册
            if (!EnsureRegistered()) return null;

            try
            {
                var contentResp =  restful.RcLoadFlows(RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.Error(IsEnglishMode? "Loading process failed: interface returned empty content" : "加载流程failed：接口返回空内容");
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<List<RespDataFlowTempDTO>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat(IsEnglishMode? $"Loading process failed: returned content could not be deserialized => {0}" : $"加载流程failed：返回内容无法反序列化 => {0}", contentResp);
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat(IsEnglishMode? $"Load process succeeded, a total of {0} processes" : $"加载流程success，共{0}个流程", respData.Data?.Count ?? 0);
                    return respData.Data;
                }

                // Token过期？重新注册后重试一次
                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn(IsEnglishMode? "The token has expired, please try again after re-registering..." : "Token已过期，重新注册后重试...");
                    RegDTO = null; // 清空过期Token
                    return EnsureRegistered() ? RcLoadFlows() : null;
                }

                logger.ErrorFormat(IsEnglishMode? $"Load process failed: {0} => {1}" : $"加载流程failed：{0} => {1}", respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(IsEnglishMode? "Abnormal loading process" : "加载流程过程异常", ex);
                return null;
            }
            /*if (RegDTO != null)
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

            return null;*/
        }

        public RespDataRunFlowDTO? RcRunFlowById(int fid, string serialNumber)
        {
            if (!EnsureRegistered())
                return null;

            try
            {
                var contentResp = restful.RcRunFlow(fid, serialNumber, RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.ErrorFormat($"Execution process（ID：{0}）failed：{(IsEnglishMode? "The interface returned empty content":"接口返回空内容")}", fid);
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataRunFlowDTO>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat($"Execution process（ID：{0}）failed：{(IsEnglishMode ? "The interface returned empty content" : "返回内容无法反序列化")} => {1}", fid, contentResp);
                    return null;
                }

                if (respData.IsSuccess || respData.IsProcessing)
                {
                    logger.InfoFormat($"Execution process（ID：{0}）success，{(IsEnglishMode? "Status":"状态")}：{1}", fid, respData.IsProcessing ? "processing" : "success");
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn(IsEnglishMode? "The token has expired, please try again after re-registering..." : "Token已过期，重新注册后重试...");
                    RegDTO = null;
                    return EnsureRegistered() ? RcRunFlowById(fid, serialNumber) : null;
                }

                logger.ErrorFormat($"Execution process（ID：{0}）failed：{1} => {2}", fid, respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat($"Execution process（ID：{0}）{(IsEnglishMode ? "Process exception" : "过程异常")}", fid, ex);
                return null;
            }
        }
        public bool RcRunFlowByName(string fname, string serialNumber)
        {
            if (!EnsureRegistered())
                return false;

            try
            {
                var contentResp = restful.RcRunFlow(fname, serialNumber, RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.ErrorFormat($"Execution process（Name：{0}）failed：{(IsEnglishMode? "The interface returned empty content":"接口返回空内容")}", fname);
                    return false;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataRunFlowDTO>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat($"Execution process（Name：{0}）failed：{(IsEnglishMode ? "The interface returned empty content" : "返回内容无法反序列化")} => {1}", fname, contentResp);
                    return false;
                }

                if (respData.IsSuccess || respData.IsProcessing)
                {
                    logger.InfoFormat($"Execution process（Name={0},sn={1}）completed, status：{2}", fname, serialNumber, respData.IsProcessing ? "processing" : "success");
                    return true;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn(IsEnglishMode ? "The token has expired, please try again after re-registering..." : "Token已过期，重新注册后重试...");
                    RegDTO = null;
                    return EnsureRegistered() ? RcRunFlowByName(fname, serialNumber) : false;
                }

                logger.ErrorFormat($"Execution process（Name：{0}）failed：{1} => {2}", fname, respData.Message, contentResp);
                return false;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat($"Execution process（Name：{0}）{(IsEnglishMode ? "Process exception" : "过程异常")}", fname, ex);
                return false;
            }
        }

        public RespDataFlowResultDTO<AlgResultItem>? RcGetFlowResult_POI(string serialNumber)
        {
            if (!EnsureRegistered()) return null;

            try
            {
                var contentResp = restful.RcGetFlowResult_POI(serialNumber, RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.ErrorFormat(IsEnglishMode?$"Failed to get POI process result (SN: {0}): interface returned empty content" :$"获取POI流程结果（SN：{0}）failed：接口返回空内容", serialNumber);
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat(IsEnglishMode?$"Failed to get POI process result (SN: {0}): returned content could not be deserialized => {1}" :$"获取POI流程结果（SN：{0}）failed：返回内容无法反序列化 => {1}", serialNumber, contentResp);
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat(IsEnglishMode?$"Retrieve POI process result (SN: {0}) successful, status: {1}" :"获取POI流程结果（SN：{0}）success，状态：{1}", serialNumber, (bool)(respData.Data?.IsFinished) ? "已完成" : "processing");
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn(IsEnglishMode ? "The token has expired, please try again after re-registering..." : "Token已过期，重新注册后重试...");
                    RegDTO = null;
                    return EnsureRegistered() ? RcGetFlowResult_POI(serialNumber) : null;
                }

                logger.ErrorFormat(IsEnglishMode? $"Failed to get POI process result (SN: {0}): {1} => {2}" : $"获取POI流程结果（SN：{0}）failed：{1} => {2}", serialNumber, respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat(IsEnglishMode? $"An exception occurred during the process of obtaining the POI result (SN: {0})" : $"获取POI流程结果（SN：{0}）过程异常", serialNumber, ex);
                return null;
            }
        }

        public List<RespDataDTO_CIE>? RcGetFlowResult_POI_Detail(string getURL)
        {
            if (!EnsureRegistered()) return null;

            try
            {
                var contentResp = restful.RcGetFlowResult_POI_Detail(getURL, RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.ErrorFormat(IsEnglishMode? $"Failed to get POI details (URL: {0}): API returned empty content" : $"获取POI详情（URL：{0}）failed：接口返回空内容", getURL);
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<List<RespDataDTO_CIE>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat(IsEnglishMode?$"Failed to get POI details (URL: {0}): returned content could not be deserialized => {1}" :$"获取POI详情（URL：{0}）failed：返回内容无法反序列化 => {1}", getURL, contentResp);
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat(IsEnglishMode?$"Successfully retrieved POI details (URL: {0}), with a total of {1} entries" :$"获取POI详情（URL：{0}）success，共{1}条数据", getURL, respData.Data?.Count ?? 0);
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn(IsEnglishMode ? "The token has expired, please try again after re-registering..." : "Token已过期，重新注册后重试...");
                    RegDTO = null;
                    return EnsureRegistered() ? RcGetFlowResult_POI_Detail(getURL) : null;
                }

                logger.ErrorFormat(IsEnglishMode ? $"Failed to get POI details (URL: {0}): {1} => {2}" : $"获取POI详情（URL：{0}）failed：{1} => {2}", getURL, respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat(IsEnglishMode ? $"An exception occurred while retrieving POI details (URL: {0})" : $"获取POI详情（URL：{0}）过程异常", getURL, ex);
                return null;
            }
        }

        public RespDataFlowResultDTO<AlgResultItem>? RcGetFlowResult_SP(string serialNumber)
        {
            if (!EnsureRegistered()) return null;

            try
            {
                var contentResp = restful.RcGetFlowResult_SP(serialNumber, RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.ErrorFormat(IsEnglishMode?$"Failed to obtain SP process result (SN: {0}): API returned empty content" :$"获取SP流程结果（SN：{0}）failed：接口返回空内容", serialNumber);
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat(IsEnglishMode ? $"Failed to obtain SP process result (SN: {0}): the returned content cannot be deserialized => {1}" : $"获取SP流程结果（SN：{0}）failed：返回内容无法反序列化 => {1}", serialNumber, contentResp);
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat($"{(IsEnglishMode? "Get SP process results" : "获取SP流程结果")}（SN：{0}）success", serialNumber);
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn(IsEnglishMode ? "The token has expired, please try again after re-registering..." : "Token已过期，重新注册后重试...");
                    RegDTO = null;
                    return EnsureRegistered() ? RcGetFlowResult_SP(serialNumber) : null;
                }

                logger.ErrorFormat($"{(IsEnglishMode ? "Get SP process results" : "获取SP流程结果")}（SN：{0}）failed：{1} => {2}", serialNumber, respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat(IsEnglishMode?$"An exception occurred while retrieving the SP process result (SN: {0})" :$"获取SP流程结果（SN：{0}）过程异常", serialNumber, ex);
                return null;
            }
        }

        public RespDTO<RespDataFlowResultDTO<AlgResultItem>>? RcGetFlowResult_AOI(string serialNumber)
        {
            if (!EnsureRegistered()) return null;

            try
            {
                var contentResp = restful.RcGetFlowResult_AOI(serialNumber, RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.ErrorFormat($"Get process result（SN：{0}）failed：{(IsEnglishMode? "The interface returned empty content" : "接口返回空内容")}", serialNumber);
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat($"Get process result（SN：{0}）failed：{(IsEnglishMode ? "The returned content cannot be deserialized" : "返回内容无法反序列化")} => {1}", serialNumber, contentResp);
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat($"Get process result（SN：{0}）completed, Status：{1}", serialNumber, (bool)(respData.Data?.IsFinished) ? "Finished" : "Processing");
                }
                else
                {
                    // Token过期重试
                    if (IsTokenExpired(respData.Message))
                    {
                         logger.Warn(IsEnglishMode ? "The token has expired, please try again after re-registering..." : "Token已过期，重新注册后重试...");
                        RegDTO = null;
                        return EnsureRegistered() ? RcGetFlowResult_AOI(serialNumber) : null;
                    }

                    logger.ErrorFormat($"Get process result（SN：{0}）failed：{1} => {2}", serialNumber, respData.Message, contentResp);
                }

                return respData;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat($"Get process result（SN：{0}）{(IsEnglishMode ? "The returned content cannot be deserialized" : "Process Abnormal")}", serialNumber, ex);
                return null;
            }
        }
        private bool IsTokenExpired(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // 可根据实际接口返回的Token过期提示修改（示例关键词）
            var expiredKeywords = new[] { "Tokenexpired", "Unauthorized", "token invalid", "token expired" };
            return Array.Exists(expiredKeywords, kw => message.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
    }
}
