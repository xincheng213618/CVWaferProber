using CVAVMControl;
using CVDB.Services.Image;
using CVWaferProber.Core.Events;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;
namespace CVWaferProber.Services
{
    public class VAMService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(VAMService));
        CVVAMAnalyzer cVVAMAnalyzer = new CVVAMAnalyzer();
        public VAMService(RCRestService rcService) : base(rcService, CVWPEventAggregatorInstance.Instance)
        {
        }
        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.FAILED;
        }
        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            if (string.IsNullOrEmpty(dieViewModel.SerialNumber)) return ChipStatus.FAILED;
            var uiTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            // 核心改造1：用Task.Run包裹所有耗时逻辑，放到后台线程
            Task.Run(() =>
            {
                try
                {
                    // 后台线程：数据库查询（耗时IO操作）
                    var results = ImageResultService.LoadCIEResultByBatchCode(dieViewModel.SerialNumber);

                    if (results != null && results.Count == 1)
                    {
                        var result = results[0];
                        if (result.ResultCode.HasValue && result.ResultCode.Value == 0)
                        {
                            string cieFileName = result.FileUrl;
                            logger.InfoFormat("VAM result cie => {0}", cieFileName);

                            // 核心改造2：发布UI相关事件时，切回UI线程
                            // 确保订阅方的UI操作在主线程执行，避免跨线程异常
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                EventAggregator?.Publish(new VAMFlowCompletedEvent(cieFileName));
                            });

                            // 核心改造3：延迟导出事件，续体切回UI线程（避免跨线程）
                            Task.Delay(1000).ContinueWith(t =>
                            {
                                // 捕获续体异常，避免后台线程崩溃
                                if (t.Exception != null)
                                {
                                    logger.Error("VAM延迟导出事件异常", t.Exception);
                                    return;
                                }
                                // UI线程发布导出事件，确保订阅方安全执行
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    EventAggregator?.Publish(new VAMAutoExportCsvEvent
                                    {
                                        CvcieFilePath = cieFileName
                                    });
                                });
                            }, uiTaskScheduler); // 续体跑在UI线程
                        }
                        else
                        {
                            // 后台线程：JSON序列化（耗时操作），不阻塞UI
                            var errorMsg = JsonConvert.SerializeObject(result);
                            logger.ErrorFormat("VAM result is failed => {0}", errorMsg);
                        }
                    }
                    else
                    {
                        var count = results != null ? results.Count : 0;
                        logger.ErrorFormat("VAM result is empty or count > 1 => {0}", count);
                    }
                }
                catch (Exception ex)
                {
                    // 后台线程：捕获所有异常，避免崩溃且不阻塞UI
                    logger.Error("VAM FlowResultDisplay 后台执行异常", ex);
                }
            });

            // 立即返回状态，不阻塞UI线程
            return ChipStatus.VAM_COMPLETED;
            //var results = ImageResultService.LoadCIEResultByBatchCode(dieViewModel.SerialNumber);
            //if (results != null && results.Count == 1)
            //{
            //    var result = results[0];
            //    if (result.ResultCode.HasValue && result.ResultCode.Value == 0)
            //    {
            //        string cieFileName = result.FileUrl;
            //        logger.InfoFormat("VAM result cie => {0}", cieFileName);
            //        EventAggregator?.Publish(new VAMFlowCompletedEvent(cieFileName));

            //        // 2.延迟1秒后发布自动导出事件（确保文件加载完成）
            //        Task.Delay(1000).ContinueWith(t =>
            //        {
            //            EventAggregator?.Publish(new VAMAutoExportCsvEvent
            //            {
            //                CvcieFilePath = cieFileName
            //            });
            //        });
            //    }
            //    else
            //    {
            //        if (logger.IsErrorEnabled) logger.ErrorFormat("VAM result is failed => {0}", JsonConvert.SerializeObject(result));
            //    }
            //}
            //else
            //{
            //    if (logger.IsErrorEnabled) logger.ErrorFormat("VAM result is empty or count > 1 => {0}", results != null ? results.Count : 0);
            //}
            //return ChipStatus.VAM_COMPLETED;
        }

        public override void ResultDisplay(DieViewModel dieViewModel)
        {
            if (!string.IsNullOrEmpty(dieViewModel.SerialNumber))
            {
                FlowResultDisplay(dieViewModel);
            }
            else
            {
                // UI操作：直接在主线程发布清空事件
                Application.Current.Dispatcher.Invoke(() =>
                {
                    EventAggregator?.Publish(new VAMResultGUIClearEvent());
                });
            }
        }

        public override Task StartTesting(DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow, bool hasNext)
        {
            // UI操作：发布开始事件、更新芯片状态，切回主线程确保安全
            Application.Current.Dispatcher.Invoke(() =>
            {
                EventAggregator?.Publish(new VAMFlowStartingEvent());
                dieViewModel.ChangeStatus(ChipStatus.VAM_TESTING);
            });

            // 异步执行测试流程，不阻塞UI
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel, hasNext);
            return task;
            //
            //EventAggregator?.Publish(new VAMFlowStartingEvent());

            //dieViewModel.ChangeStatus(ChipStatus.VAM_TESTING);
            //Task task = RunFlowAsync(_selectedWPFlow, dieViewModel, hasNext);
            //return task;
        }
      
        public override void AutoExportData()
        { 
            // 核心：如果CVVAMAnalyzer是UI控件，必须在UI线程调用其方法
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    cVVAMAnalyzer.BtnExportClick();
                    logger.Info("VAM 自动导出数据成功");
                }
                catch (Exception ex)
                {
                    logger.Error("VAM 自动导出数据失败", ex);
                }
            });
            //cVVAMAnalyzer.BtnExportClick();
        }
     
    }
}
