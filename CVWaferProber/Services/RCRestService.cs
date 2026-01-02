using CVWaferProber.Core.Restful;
using CVWaferProber.Core.Restful.DTO;
using Newtonsoft.Json;

namespace CVWaferProber.Services
{
    public class RCRestService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(RCRestService));

        private CVRestfulHelper restful = new CVRestfulHelper();
        private RespDataRegDTO? RegDTO;
        private const int MaxRetryCount = 2; // 注册/接口调用最大重试次数

        /// <summary>
        /// 确保已注册（未注册则自动触发注册）
        /// </summary>
        /// <returns>注册是否成功</returns>
        private bool EnsureRegistered()
        {
            // 已注册且Token有效（简单判断，可根据实际Token过期规则优化）
            if (RegDTO != null && !string.IsNullOrEmpty(RegDTO.Token?.AccessToken))
                return true;

            // 未注册，执行注册（最多重试2次）
            for (int i = 0; i < MaxRetryCount; i++)
            {
                logger.InfoFormat("开始第{0}次注册...", i + 1);
                if (RcRegist())
                {
                    logger.Info("注册成功");
                    return true;
                }
                logger.WarnFormat("第{0}次注册失败，等待1秒后重试...", i + 1);
                System.Threading.Thread.Sleep(1000); // 重试间隔1秒
            }

            logger.Error("注册失败，已达到最大重试次数");
            return false;
        }

        public bool RcRegist()
        {
            try
            {
                var contentResp = restful.RcRegist();
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.Error("注册失败：接口返回空内容");
                    return false;
                }

                // 捕获反序列化异常
                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataRegDTO>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("注册失败：返回内容无法反序列化 => {0}", contentResp);
                    return false;
                }

                if (respData.IsSuccess && respData.Data != null && !string.IsNullOrEmpty(respData.Data.Token?.AccessToken))
                {
                    RegDTO = respData.Data;
                    logger.InfoFormat("注册成功 => {0}", JsonConvert.SerializeObject(RegDTO, Formatting.Indented));
                    return true;
                }

                logger.ErrorFormat("注册失败：{0} => {1}", respData.Message, contentResp);
                return false;
            }
            catch (Exception ex)
            {
                logger.Error("注册过程异常", ex);
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
            if (!EnsureRegistered())
                return null;

            try
            {
                var contentResp =  restful.RcLoadFlows(RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.Error("加载流程失败：接口返回空内容");
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<List<RespDataFlowTempDTO>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("加载流程失败：返回内容无法反序列化 => {0}", contentResp);
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat("加载流程成功，共{0}个流程", respData.Data?.Count ?? 0);
                    return respData.Data;
                }

                // Token过期？重新注册后重试一次
                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn("Token已过期，重新注册后重试...");
                    RegDTO = null; // 清空过期Token
                    return EnsureRegistered() ? RcLoadFlows() : null;
                }

                logger.ErrorFormat("加载流程失败：{0} => {1}", respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.Error("加载流程过程异常", ex);
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
                    logger.ErrorFormat("执行流程（ID：{0}）失败：接口返回空内容", fid);
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataRunFlowDTO>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("执行流程（ID：{0}）失败：返回内容无法反序列化 => {1}", fid, contentResp);
                    return null;
                }

                if (respData.IsSuccess || respData.IsProcessing)
                {
                    logger.InfoFormat("执行流程（ID：{0}）成功，状态：{1}", fid, respData.IsProcessing ? "处理中" : "成功");
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn("Token已过期，重新注册后重试...");
                    RegDTO = null;
                    return EnsureRegistered() ? RcRunFlowById(fid, serialNumber) : null;
                }

                logger.ErrorFormat("执行流程（ID：{0}）失败：{1} => {2}", fid, respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("执行流程（ID：{0}）过程异常", fid, ex);
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
                    logger.ErrorFormat("执行流程（名称：{0}）失败：接口返回空内容", fname);
                    return false;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataRunFlowDTO>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("执行流程（名称：{0}）失败：返回内容无法反序列化 => {1}", fname, contentResp);
                    return false;
                }

                if (respData.IsSuccess || respData.IsProcessing)
                {
                    logger.InfoFormat("执行流程（名称={0},sn={1}） 成功，状态：{2}", fname, serialNumber, respData.IsProcessing ? "处理中" : "成功");
                    return true;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn("Token已过期，重新注册后重试...");
                    RegDTO = null;
                    return EnsureRegistered() ? RcRunFlowByName(fname, serialNumber) : false;
                }

                logger.ErrorFormat("执行流程（名称：{0}）失败：{1} => {2}", fname, respData.Message, contentResp);
                return false;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("执行流程（名称：{0}）过程异常", fname, ex);
                return false;
            }
        }

        public RespDataFlowResultDTO<AlgResultItem>? RcGetFlowResult_POI(string serialNumber)
        {
            //if (RegDTO != null)
            //{
            //    var contentResp = restful.RcGetFlowResult_POI(serialNumber, RegDTO.Token.AccessToken);
            //    if (!string.IsNullOrEmpty(contentResp))
            //    {
            //        RespDTO<RespDataFlowResultDTO<AlgResultItem>>? respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
            //        if (respData != null && respData.IsSuccess)
            //        {
            //            if (logger.IsInfoEnabled) logger.InfoFormat("GetFlow Result ok => {0}", respData.Data.IsFinished ? "Finished" : "Pending");
            //            //if (logger.IsDebugEnabled) logger.DebugFormat("Flow Result Data => {0}", JsonConvert.SerializeObject(respData.Data));
            //            return respData.Data;
            //        }
            //    }
            //    if (logger.IsErrorEnabled) logger.ErrorFormat("GetFlow Result failed => {0}", contentResp);

            //}
            //else
            //{
            //    if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            //}
            //return null;

            if (!EnsureRegistered())
                return null;

            try
            {
                var contentResp = restful.RcGetFlowResult_POI(serialNumber, RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.ErrorFormat("获取POI流程结果（SN：{0}）失败：接口返回空内容", serialNumber);
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("获取POI流程结果（SN：{0}）失败：返回内容无法反序列化 => {1}", serialNumber, contentResp);
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat("获取POI流程结果（SN：{0}）成功，状态：{1}", serialNumber, (bool)(respData.Data?.IsFinished) ? "已完成" : "处理中");
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn("Token已过期，重新注册后重试...");
                    RegDTO = null;
                    return EnsureRegistered() ? RcGetFlowResult_POI(serialNumber) : null;
                }

                logger.ErrorFormat("获取POI流程结果（SN：{0}）失败：{1} => {2}", serialNumber, respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("获取POI流程结果（SN：{0}）过程异常", serialNumber, ex);
                return null;
            }
        }

        public List<RespDataDTO_CIE> RcGetFlowResult_POI_Detail(string getURL)
        {
            //if (RegDTO != null)
            //{
            //    var contentResp = restful.RcGetFlowResult_POI_Detail(getURL, RegDTO.Token.AccessToken);
            //    if (!string.IsNullOrEmpty(contentResp))
            //    {
            //        RespDTO<List<RespDataDTO_CIE>>? respData = JsonConvert.DeserializeObject<RespDTO<List<RespDataDTO_CIE>>>(contentResp);
            //        if (respData != null && respData.IsSuccess)
            //        {
            //            if (logger.IsInfoEnabled) logger.InfoFormat("Get POI Result ok => {0}", JsonConvert.SerializeObject(respData.Data));
            //            return respData.Data;
            //        }
            //    }
            //    if (logger.IsErrorEnabled) logger.ErrorFormat("Get POI Result failed => {0}", contentResp);

            //}
            //else
            //{
            //    if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            //}
            //return null;

            if (!EnsureRegistered())
                return null;

            try
            {
                var contentResp = restful.RcGetFlowResult_POI_Detail(getURL, RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.ErrorFormat("获取POI详情（URL：{0}）失败：接口返回空内容", getURL);
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<List<RespDataDTO_CIE>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("获取POI详情（URL：{0}）失败：返回内容无法反序列化 => {1}", getURL, contentResp);
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat("获取POI详情（URL：{0}）成功，共{1}条数据", getURL, respData.Data?.Count ?? 0);
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn("Token已过期，重新注册后重试...");
                    RegDTO = null;
                    return EnsureRegistered() ? RcGetFlowResult_POI_Detail(getURL) : null;
                }

                logger.ErrorFormat("获取POI详情（URL：{0}）失败：{1} => {2}", getURL, respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("获取POI详情（URL：{0}）过程异常", getURL, ex);
                return null;
            }
        }

        public RespDataFlowResultDTO<AlgResultItem>? RcGetFlowResult_SP(string serialNumber)
        {
            //if (RegDTO != null)
            //{
            //    var contentResp = restful.RcGetFlowResult_SP(serialNumber, RegDTO.Token.AccessToken);
            //    if (!string.IsNullOrEmpty(contentResp))
            //    {
            //        RespDTO<RespDataFlowResultDTO<AlgResultItem>>? respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
            //        if (respData != null && respData.IsSuccess)
            //        {
            //            if (logger.IsInfoEnabled) logger.InfoFormat("GetFlow Result ok => {0}", JsonConvert.SerializeObject(respData.Data));
            //            return respData.Data;
            //        }
            //    }
            //    if (logger.IsErrorEnabled) logger.ErrorFormat("GetFlow Result failed => {0}", contentResp);

            //}
            //else
            //{
            //    if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            //}
            //return null;


            if (!EnsureRegistered())
                return null;

            try
            {
                var contentResp = restful.RcGetFlowResult_SP(serialNumber, RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.ErrorFormat("获取SP流程结果（SN：{0}）失败：接口返回空内容", serialNumber);
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("获取SP流程结果（SN：{0}）失败：返回内容无法反序列化 => {1}", serialNumber, contentResp);
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat("获取SP流程结果（SN：{0}）成功", serialNumber);
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn("Token已过期，重新注册后重试...");
                    RegDTO = null;
                    return EnsureRegistered() ? RcGetFlowResult_SP(serialNumber) : null;
                }

                logger.ErrorFormat("获取SP流程结果（SN：{0}）失败：{1} => {2}", serialNumber, respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("获取SP流程结果（SN：{0}）过程异常", serialNumber, ex);
                return null;
            }
        }

        public RespDTO<RespDataFlowResultDTO<AlgResultItem>>? RcGetFlowResult_AOI(string serialNumber)
        {
            //if (RegDTO != null)
            //{
            //    var contentResp = restful.RcGetFlowResult_AOI(serialNumber, RegDTO.Token.AccessToken);
            //    if (!string.IsNullOrEmpty(contentResp))
            //    {
            //        RespDTO<RespDataFlowResultDTO<AlgResultItem>>? respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
            //        if (respData != null && respData.IsSuccess)
            //        {
            //            if (logger.IsInfoEnabled) logger.InfoFormat("GetFlow Result ok => {0}", respData.Data.ToDisString());
            //            if (respData.Data.IsFinished && logger.IsDebugEnabled) logger.DebugFormat("Flow Result Data => {0}", JsonConvert.SerializeObject(respData.Data, Formatting.Indented));
            //        }
            //        return respData;
            //    }
            //    if (logger.IsErrorEnabled) logger.ErrorFormat("GetFlow Result failed => {0}", contentResp);
            //    throw new Exception("GetFlow Result failed");
            //}
            //else
            //{
            //    if (logger.IsErrorEnabled) logger.ErrorFormat("Rc UnRegist.");
            //    throw new Exception("Rc UnRegist");
            //}
            //return null;

            if (!EnsureRegistered())
                return null;

            try
            {
                var contentResp = restful.RcGetFlowResult_AOI(serialNumber, RegDTO!.Token.AccessToken);
                if (string.IsNullOrEmpty(contentResp))
                {
                    logger.ErrorFormat("获取AOI流程结果（SN：{0}）失败：接口返回空内容", serialNumber);
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("获取AOI流程结果（SN：{0}）失败：返回内容无法反序列化 => {1}", serialNumber, contentResp);
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat("获取AOI流程结果（SN：{0}）成功，状态：{1}", serialNumber, (bool)(respData.Data?.IsFinished) ? "已完成" : "处理中");
                }
                else
                {
                    // Token过期重试
                    if (IsTokenExpired(respData.Message))
                    {
                        logger.Warn("Token已过期，重新注册后重试...");
                        RegDTO = null;
                        return EnsureRegistered() ? RcGetFlowResult_AOI(serialNumber) : null;
                    }

                    logger.ErrorFormat("获取AOI流程结果（SN：{0}）失败：{1} => {2}", serialNumber, respData.Message, contentResp);
                }

                return respData;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("获取AOI流程结果（SN：{0}）过程异常", serialNumber, ex);
                return null;
            }
        }
        private bool IsTokenExpired(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // 可根据实际接口返回的Token过期提示修改（示例关键词）
            var expiredKeywords = new[] { "Token过期", "Unauthorized", "token invalid", "token expired" };
            return Array.Exists(expiredKeywords, kw => message.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
    }
}
