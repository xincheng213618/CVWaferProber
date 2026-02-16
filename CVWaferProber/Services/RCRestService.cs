using ColorVision.Core.Message.Response;
using CVWaferProber.Core.Restful;
using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.Models;
using Newtonsoft.Json;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using WaferComm.Core;

namespace CVWaferProber.Services
{
    public class RCRestService : IFlowService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(RCRestService));

        private CVRestfulHelper restful = new CVRestfulHelper();
        private RespDataRegDTO? RegDTO;
        private const int MaxRetryCount = 2; // 注册/接口调用最大重试次数
        private EventAggregator eventAggregator;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(60);


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
                logger.InfoFormat("Start registration #{0}", i + 1);// : $"开始第{0}次注册..."
                if (RcRegist())
                {
                    logger.Info("Regist success");
                    return true;
                }
                logger.WarnFormat( "Registration attempt {0} failed, retrying after 1 second" , i + 1);//: $"第{0}次注册failed，等待1秒后重试..."
                System.Threading.Thread.Sleep(1000); // 重试间隔1秒
            }

            logger.Error( "Registration failed, maximum retry attempts reached" );//: "Regist failed，已达到最大重试次数"
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
                    logger.Error("Regist failed：The interface returned empty content");// : "Regist failed：接口返回空内容"
                    PublishStatus(ConnectionStatus.Disconnected);
                    return false;
                }

                // 捕获反序列化异常
                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataRegDTO>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("Regist failed：The returned content cannot be deserialized => {0}" , contentResp);
                    PublishStatus(ConnectionStatus.Disconnected);//: $"Regist failed：返回内容无法反序列化 => {0}"
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
                logger.Error("Registration process error", ex);// : "注册过程异常"
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
                    logger.Error("Loading process failed: interface returned empty content" );//: "加载流程failed：接口返回空内容"
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<List<RespDataFlowTempDTO>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("Loading process failed: returned content could not be deserialized => {0}" , contentResp);//: $"加载流程failed：返回内容无法反序列化 => {0}"
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat( "Load process succeeded, a total of {0} processes" , respData.Data?.Count ?? 0);
                    return respData.Data;//: $"加载流程success，共{0}个流程"
                }

                // Token过期？重新注册后重试一次
                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn( "The token has expired, please try again after re-registering..." );//: "Token已过期，重新注册后重试..."
                    RegDTO = null; // 清空过期Token
                    return EnsureRegistered() ? RcLoadFlows() : null;
                }

                logger.ErrorFormat( "Load process failed: {0} => {1}" , respData.Message, contentResp);//: $"加载流程failed：{0} => {1}"
                return null;
            }
            catch (Exception ex)
            {
                logger.Error("Abnormal loading process" , ex);//: "加载流程过程异常"
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
                    logger.ErrorFormat("Execution process（ID：{0}）failed：The interface returned empty content", fid);//:"接口返回空内容"
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataRunFlowDTO>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("Execution process（ID：{0}）failed：The interface returned empty content => {1}", fid, contentResp);//: "返回内容无法反序列化")}
                    return null;
                }

                if (respData.IsSuccess || respData.IsProcessing)
                {
                    logger.InfoFormat("Execution process（ID：{0}）success，Status：{1}", fid, respData.IsProcessing ? "processing" : "success");//":"状态")}
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn("The token has expired, please try again after re-registering..." );//: "Token已过期，重新注册后重试..."
                    RegDTO = null;
                    return EnsureRegistered() ? RcRunFlowById(fid, serialNumber) : null;
                }

                logger.ErrorFormat("Execution process（ID：{0}）failed：{1} => {2}", fid, respData.Message, contentResp);
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat($"Execution process（ID：{0}） Process exception, fid, ex"); // "过程异常"
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
                    logger.ErrorFormat("Execution process（Name：{0}）failed：The interface returned empty content", fname);//:"接口返回空内容")}"
                    return false;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataRunFlowDTO>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("Execution process（Name：{0}）failed：The interface returned empty content => {1}", fname, contentResp);//" : "返回内容无法反序列化")}
                    return false;
                }

                if (respData.IsSuccess || respData.IsProcessing)
                {
                    logger.InfoFormat("Execution process（Name={0},sn={1}）completed, status：{2}", fname, serialNumber, respData.IsProcessing ? "processing" : "success");
                    return true;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn( "The token has expired, please try again after re-registering..." );//: "Token已过期，重新注册后重试..."
                    RegDTO = null;
                    return EnsureRegistered() ? RcRunFlowByName(fname, serialNumber) : false;
                }

                logger.ErrorFormat("Execution process（Name：{0}）failed：{1} => {2}", fname, respData.Message, contentResp);
                return false;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("Execution process（Name：{0}）Process exception" , fname, ex);//: "过程异常")}
            
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
                    logger.ErrorFormat("Failed to get POI process result (SN: {0}): interface returned empty content" , serialNumber);//:$"获取POI流程结果（SN：{0}）failed：接口返回空内容"
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("Failed to get POI process result (SN: {0}): returned content could not be deserialized => {1}", serialNumber, contentResp);// :$"获取POI流程结果（SN：{0}）failed：返回内容无法反序列化 => {1}"
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat("Retrieve POI process result (SN: {0}) successful, status: {1}" , serialNumber, (bool)(respData.Data?.IsFinished) ? "已完成" : "processing");//:"获取POI流程结果（SN：{0}）success，状态：{1}"
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn( "The token has expired, please try again after re-registering...");// : "Token已过期，重新注册后重试..."
                    RegDTO = null;
                    return EnsureRegistered() ? RcGetFlowResult_POI(serialNumber) : null;
                }

                logger.ErrorFormat("Failed to get POI process result (SN: {0}): {1} => {2}" , serialNumber, respData.Message, contentResp);//: $"获取POI流程结果（SN：{0}）failed：{1} => {2}"
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("An exception occurred during the process of obtaining the POI result (SN: {0})", serialNumber, ex);// : $"获取POI流程结果（SN：{0}）过程异常"
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
                    logger.ErrorFormat("Failed to get POI details (URL: {0}): API returned empty content" , getURL);//: $"获取POI详情（URL：{0}）failed：接口返回空内容"
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<List<RespDataDTO_CIE>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("Failed to get POI details (URL: {0}): returned content could not be deserialized => {1}" , getURL, contentResp);//:$"获取POI详情（URL：{0}）failed：返回内容无法反序列化 => {1}"
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat("Successfully retrieved POI details (URL: {0}), with a total of {1} entries" , getURL, respData.Data?.Count ?? 0);//:$"获取POI详情（URL：{0}）success，共{1}条数据"
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn(  "The token has expired, please try again after re-registering..." );//: "Token已过期，重新注册后重试..."
                    RegDTO = null;
                    return EnsureRegistered() ? RcGetFlowResult_POI_Detail(getURL) : null;
                }

                logger.ErrorFormat("Failed to get POI details (URL: {0}): {1} => {2}" , getURL, respData.Message, contentResp);//: $"获取POI详情（URL：{0}）failed：{1} => {2}"
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("An exception occurred while retrieving POI details (URL: {0})" , getURL, ex);//: $"获取POI详情（URL：{0}）过程异常"
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
                    logger.ErrorFormat("Failed to obtain SP process result (SN: {0}): API returned empty content" , serialNumber);//:$"获取SP流程结果（SN：{0}）failed：接口返回空内容"
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("Failed to obtain SP process result (SN: {0}): the returned content cannot be deserialized => {1}" , serialNumber, contentResp);//: $"获取SP流程结果（SN：{0}）failed：返回内容无法反序列化 => {1}"
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat("Get SP process results （SN：{0}）success", serialNumber);
                    return respData.Data;
                }

                if (IsTokenExpired(respData.Message))
                {
                    logger.Warn( "The token has expired, please try again after re-registering..." );//: "Token已过期，重新注册后重试..."
                    RegDTO = null;
                    return EnsureRegistered() ? RcGetFlowResult_SP(serialNumber) : null;
                }

                logger.ErrorFormat(" Get SP process results（SN：{0}）failed：{1} => {2}", serialNumber, respData.Message, contentResp);//" : "获取SP流程结果")}
                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("An exception occurred while retrieving the SP process result (SN: {0})" , serialNumber, ex);//:$"获取SP流程结果（SN：{0}）过程异常"
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
                    logger.ErrorFormat("Get process result（SN：{0}）failed：The interface returned empty content" , serialNumber);//: "接口返回空内容")}"
                    return null;
                }

                var respData = JsonConvert.DeserializeObject<RespDTO<RespDataFlowResultDTO<AlgResultItem>>>(contentResp);
                if (respData == null)
                {
                    logger.ErrorFormat("Get process result（SN：{0}）failed：The returned content cannot be deserialized  => {1} ", serialNumber, contentResp);//: \"返回内容无法反序列化\")}
                    return null;
                }

                if (respData.IsSuccess)
                {
                    logger.InfoFormat("Get process result（SN：{0}）completed, Status：{1}", serialNumber, (bool)(respData.Data?.IsFinished) ? "Finished" : "Processing");
                }
                else
                {
                    // Token过期重试
                    if (IsTokenExpired(respData.Message))
                    {
                         logger.Warn( "The token has expired, please try again after re-registering..." );//: "Token已过期，重新注册后重试..."
                        RegDTO = null;
                        return EnsureRegistered() ? RcGetFlowResult_AOI(serialNumber) : null;
                    }

                    logger.ErrorFormat("Get process result（SN：{0}）failed：{1} => {2}", serialNumber, respData.Message, contentResp);
                }

                return respData;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("Get process result（SN：{0}）The returned content cannot be deserialized ,Process Abnormal", serialNumber, ex);
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

        public async Task<DeviceResponseMessageHeader?> FlowRunAndWaitResponseAsync(int flowId, string flowName, string serialNumber, TimeSpan? timeout = null)
        {
            TimeSpan _timeout = timeout ?? _defaultTimeout;
            var resp = RcRunFlowByName(flowName, serialNumber);
            if (resp)
            {
                var flowResult = await PollFlowResultWithRxAsync(serialNumber,
                    new CancellationTokenSource(_timeout).Token);
                if (flowResult != null && flowResult.IsSuccess)
                {
                    return DeviceResponseMessageHeader.OK();
                }
            }

            return DeviceResponseMessageHeader.Failed();
        }

        protected async Task<RespDataBaseFlowResultDTO> PollFlowResultWithRxAsync(string sn, CancellationToken cancellationToken)
        {
            // 优化4：添加TakeWhile+超时兜底，避免无限轮询；同时优化异常提示
            return await Observable.Interval(TimeSpan.FromSeconds(1))
                 // 取消时立即终止轮询
                 .TakeUntil(_ => cancellationToken.IsCancellationRequested)
                 .Select(_ =>
                 {
                     // 轮询中检测取消信号，提前终止
                     cancellationToken.ThrowIfCancellationRequested();
                     return this.RcGetFlowResult_AOI(sn);
                 })
                 // 过滤null结果，只处理有效响应
                 .Where(resp => resp != null)
                 // 终止条件：流程完成 或 接口调用失败
                 .FirstAsync(resp => (resp.IsSuccess && resp.Data.IsFinished) || !resp.IsSuccess)
                 .Select(resp =>
                 {
                     // 接口返回失败时抛出业务异常
                     if (!resp.IsSuccess)
                         throw new InvalidOperationException($"Flow execution failed: {resp.Message} (SN: {sn})");
                     return resp.Data;
                 })
                 // 绑定取消令牌，超时/取消时抛出TaskCanceledException
                 .ToTask(cancellationToken);
        }

        protected async Task<RespDataBaseFlowResultDTO?> RunFlowAsync(string fname, string sn, int timeout)
        {
            // 优化3：使用using包裹CancellationTokenSource，确保资源释放
            using var cancellationTokenSource = timeout > 0
                ? new CancellationTokenSource(TimeSpan.FromSeconds(timeout))
                : new CancellationTokenSource();

            var cancellationToken = cancellationTokenSource.Token;

            // 启动流程
            var resp = this.RcRunFlowByName(fname, sn);
            if (resp)
            {
                // 异步轮询结果，避免阻塞UI线程
                return await PollFlowResultWithRxAsync(sn, cancellationToken);
            }
            else
            {
                return await Task.FromResult<RespDataBaseFlowResultDTO?>(null);
            }
        }

        public void Reconnect()
        {
            RcRegist();
        }
    }
}
