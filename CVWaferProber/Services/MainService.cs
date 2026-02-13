using ChipMapping.ViewModels;
using CVAVMControl;
using CVCommCore;
using CVWaferProber.Config;
using CVWaferProber.Models;
using CVWaferProber.ViewModels;
using CVWPFCamImageCtrl;
using CVWPFSpectrometerCtrl.ViewModels;
using System.Text;
using Application = System.Windows.Application;
using ConnectionInfo = CVWaferProber.Models.ConnectionInfo;

namespace CVWaferProber.Services
{
    public class MainService : ReflectionSingleton<MainService>
    {
        public static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MainService));

        #region Events
        public event EventHandler<TestCompletedEventArgs> TestingCompleted;
        public event EventHandler<ChipViewModel> ChipSelected;
        public event EventHandler<(DieViewModel?, DieViewModel)> PreAutoTestingNextDie;
        #endregion
        private MQTTService mqttService;
        private MappingService mappingService;
        //private GSWMProcessor? _wmProcessor;
        private ProberClientService proberClientService;
        private readonly Dictionary<CVWaferProberFlowType, BaseSerivce> flowServices =
            new Dictionary<CVWaferProberFlowType, BaseSerivce>();
        public AutoTestingItem? autoTestingItem { get; private set; }
        public ConnectionInfo ConnectionInfo { get => mqttService.ConnectionInfo; }

        private MainService()
        {
            mqttService = new MQTTService();
            mappingService = new MappingService();
            mappingService.ChipSelected += MappingService_ChipSelected;
            InitializeClientProber();
        }

        private void MappingService_ChipSelected(object? sender, ChipViewModel e)
        {
            ChipSelected?.Invoke(this, e);
        }
        public CVSpectrumViewModel? GetIVLVM()
        {
            IVLService ivlService = flowServices[CVWaferProberFlowType.IVL] as IVLService;
            if (ivlService != null) return ivlService.CustomIVLVM;
            return null;
        }
        public CVEQEViewModel? GetEQEVM()
        {
            EQEService eqeService = flowServices[CVWaferProberFlowType.EQE] as EQEService;
            if (eqeService != null) return eqeService.CustomEQEVM;
            return null;
        }
        public CVCamImagerViewModel? GetAOIVM()
        {
            AOIService aoiService = flowServices[CVWaferProberFlowType.AOI] as AOIService;
            if (aoiService != null) return aoiService.CustomImageVM;
            return null;
        }
        public ChipMappingControlViewModel? GetMappingVM()
        {
            if (mappingService != null) return mappingService.CustomVM;
            return null;
        }

        public void Startup()
        {
            proberClientService.Startup();
        }

        public bool TryConnectAsync()
        {
            var task = proberClientService.TryConnectAsync();
            task.Wait();
            return task.Result;
        }
        public void InitializeService(CVVAMAnalyzer _cVVAMAnalyzer, CVWPFSpectrometerCtrl.CVSpectrumAnalyzer ivlAnalyzer)
        {
            //
            BaseSerivce ivlService = new IVLService(mqttService, ivlAnalyzer);
            flowServices[CVWaferProberFlowType.IVL] = ivlService;
            ivlService.TestingCompleted += OnTestingCompleted;
            ivlService.AutoTestingNextCompleted += OnAutoTestingNextCompleted;

            BaseSerivce aoiService = new AOIService(mqttService);
            flowServices[CVWaferProberFlowType.AOI] = aoiService;
            aoiService.TestingCompleted += OnTestingCompleted;
            aoiService.AutoTestingNextCompleted += OnAutoTestingNextCompleted;
         
            BaseSerivce eqeService = new EQEService(mqttService);
            flowServices[CVWaferProberFlowType.EQE] = eqeService;
            eqeService.TestingCompleted += OnTestingCompleted;
            eqeService.AutoTestingNextCompleted += OnAutoTestingNextCompleted;
          
            BaseSerivce vamService = new VAMService(mqttService, _cVVAMAnalyzer);
            flowServices[CVWaferProberFlowType.VAM] = vamService;
            vamService.TestingCompleted += OnTestingCompleted;
            vamService.AutoTestingNextCompleted += OnAutoTestingNextCompleted;          
        }
        private void InitializeClientProber()
        {
            proberClientService = ProberClientService.Instance;
            proberClientService.Subscribe<MotionAxisUpdatedEvent>(OnMotionAxisUpdated);
        }

        private void OnMotionAxisUpdated(MotionAxisUpdatedEvent @event)
        {
            if (autoTestingItem != null) autoTestingItem.UpdateCurDieMotionAxis(@event.Axis);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="dieVM"></param>
        private void OnAutoTestingNextCompleted(object? sender, DieViewModel dieVM)
        {
            // 更新进度：完成当前Die测试
            UpdateProgressForDieCompletion(dieVM);
            
            //发送结果给机台
            proberClientService?.SendResultAsync(dieVM);
            //
            if (autoTestingItem != null)
            {
                if(logger.IsInfoEnabled) logger.InfoFormat("IsPaused={0},Status={1} => {2}",
                    autoTestingItem.IsPaused, dieVM.Status.ToString(), dieVM.MapAxisToString());
                //
                bool canExecute = !autoTestingItem.IsPaused;

                int errorCount = autoTestingItem.CheckError(dieVM);
                if (IsTestBreak(dieVM, errorCount))
                {
                    if (logger.IsWarnEnabled) logger.WarnFormat("The maximum number of error max. => {0}/{1}", errorCount,ConfigManager.Config.BreakOnErrorNum);
                    PauseAutoTesting();
                }
                else if (canExecute)
                {
                    // 关键：在开始下一个Die测试之前，先完成当前Die的进度
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mappingVM = MainViewModel.Instance?.DataMappingVM;
                        if (mappingVM != null)
                        {
                            // 完成当前Die测试（增加CompletedTestCount）
                            mappingVM.CompleteSingleDieTest();
                        }
                    });
                    DoNextDieFlowExec(autoTestingItem);
                }
                else
                {
                    if (logger.IsWarnEnabled) logger.Warn("Tester is paused.");
                }
            }
            else
            {
                DoAutoTestEnd(dieVM, true);
            }
        }
        /// <summary>
        /// 更新Die测试完成进度
        /// </summary>
        private void UpdateProgressForDieCompletion(DieViewModel dieVM)
        {
            try
            {
                // 增加空值检查
                if (MainViewModel.Instance?.DataMappingVM != null)
                {
                    // 确保只在UI线程执行
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 进度更新现在在OnAutoTestingNextCompleted中处理
                        var mappingVM = MainViewModel.Instance.DataMappingVM;

                        // 只更新单Die进度到100%，但不增加CompletedTestCount
                        mappingVM.SingleDieTestProgress = 100;
                        mappingVM.OnPropertyChanged(nameof(mappingVM.SingleDieTestProgress));
                        mappingVM.OnPropertyChanged(nameof(mappingVM.ProgressText));
                        mappingVM.UpdateTotalProgress();
                    });

                    if (logger.IsDebugEnabled)
                        logger.DebugFormat($"进度更新: {dieVM.MapAxisToString()} 测试完成, 当前完成数: {MainViewModel.Instance.DataMappingVM.CompletedTestCount}");
                }
            }
            catch (Exception ex)
            {
                logger.Error("更新进度失败 (Complete)", ex);
            }
        }
        private bool IsTestBreak(DieViewModel dieVM, int errorCount)
        {
            return !IsDieCompleted(dieVM) &&
                ConfigManager.Config.IsBreakOnError &&
                 ConfigManager.Config.BreakOnErrorNum <= errorCount;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTestingCompleted(object? sender, TestCompletedEventArgs e)
        {
            // 如果测试队列完成，重置进度状态
            if (e.IsAuto)
            {
                // 确保所有Die测试都标记为完成
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (MainViewModel.Instance?.DataMappingVM != null)
                    {
                        // 如果还有未完成的Die，强制标记为完成
                        if (MainViewModel.Instance.DataMappingVM.CompletedTestCount < MainViewModel.Instance.DataMappingVM.TotalTestCount)
                        {
                            MainViewModel.Instance.DataMappingVM.CompleteSingleDieTest();
                        }

                        MainViewModel.Instance.DataMappingVM.SingleDieTestProgress = 100;
                        MainViewModel.Instance.DataMappingVM.CurrentDieInfo = "Test completed";
                    }
                });
            }

            if (e.IsAuto) autoTestingItem = null;
            DoAutoTestEnd(e.DieVM, e.IsAuto);
        }

        #region Window Message
        /*
        public void InitializeWindow(System.Windows.Window win)
        {
            _wmProcessor = new GSWMProcessor(win);
            _wmProcessor.OnStopTest += _wmProcessor_OnStopTest;
            _wmProcessor.OnSOT += _wmProcessor_OnSOT;
        }
        private void _wmProcessor_OnSOT(object sender, int row, int col)
        {
            StartTestingDie(row, col);
        }

        private void StartTestingDie(int row, int col)
        {
            throw new NotImplementedException();
        }

        private void _wmProcessor_OnStopTest(object sender)
        {
            StopAutoTest(this);
        }

        private void StopAutoTest(MainService mainService)
        {
            if (_wmProcessor != null)
            {
                _wmProcessor.MeasurementStoped();
            }
        }
        //////////////////////////////*/
        #endregion

        public void ResultDisplay(DieViewModel dieViewModel)
        {
            foreach (var item in flowServices)
            {
                item.Value.ResultDisplay(dieViewModel);
            }
        }

        public async Task StartDieTestingAsync(WPFlowViewModel selectedFlow, DieViewModel dieVM)
        {
            if (selectedFlow == null || dieVM == null)
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("No flow selected for current die");
                return;
            }
            BaseSerivce? baseSerivce = null;
            switch (selectedFlow?.FlowType)
            {
                case CVWaferProberFlowType.AOI:
                    baseSerivce = flowServices[CVWaferProberFlowType.AOI];
                    break;
                case CVWaferProberFlowType.IVL:
                case CVWaferProberFlowType.IV:
                case CVWaferProberFlowType.IVL_SP:
                case CVWaferProberFlowType.IVL_Camera:
                    baseSerivce = flowServices[CVWaferProberFlowType.IVL];
                    break;
                case CVWaferProberFlowType.EQE:
                    baseSerivce = flowServices[CVWaferProberFlowType.EQE];
                    break;
                case CVWaferProberFlowType.VAM:
                    baseSerivce = flowServices[CVWaferProberFlowType.VAM];
                    break;
                default:
                    break;
            }
            if(baseSerivce == null)
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("No flow selected");
                return;
            }
            ////获取Motion Axis信息
            //proberClientService?.GetCurrentDieAxisAsync();
            //
            Task task = baseSerivce.StartTestingAsync(dieVM, selectedFlow, false, false);

            await task;
        }
        private void DoNextDieFlowExec(AutoTestingItem item)
        {
            (DieViewModel? diePre, DieViewModel? die) dieNext = item.GetNextDieVM();
            if (dieNext.die != null)
            {
                //if(logger.IsInfoEnabled) logger.InfoFormat("Next Die={0}/{1}", dieNext.die.MapAxisToString(), dieNext.die.Status.ToString());
                if (dieNext.die.Status == Core.Models.Enums.ChipStatus.WAITING)
                {
                    if(logger.IsDebugEnabled) logger.DebugFormat("Starting Visual Inspection... => {0}", dieNext.die.MapAxisToString());
                    //// 修复：提前初始化下一个Die的进度
                    //Application.Current.Dispatcher.Invoke(() =>
                    //{
                    //    MainViewModel.Instance?.DataMappingVM?.StartSingleDieTest(dieNext.die);
                    //});
                    PreAutoTestingNextDie?.Invoke(this, (dieNext.diePre, dieNext.die));
                    //logger.InfoFormat("DoAutoDieFlowExecAsync={0}/{1}", dieNext.die.MapAxisToString(), dieNext.die.Status.ToString());
                    Task.Factory.StartNew(async () =>
                    {
                        await MoveToDieAndTestingAsync(item.CurSelectedWPFlow, dieNext.die, dieNext.diePre == null, item.HasNext, true);
                        //TODO Testing
                        //await ExecuteDieTestWithProgress(item.CurSelectedWPFlow, dieNext.die, item.HasNext, true);
                    });
                }
                else if (IsDieCompleted(dieNext.die))
                {
                    if (logger.IsDebugEnabled) logger.DebugFormat("The current die has been tested => {0}", dieNext.die.MapAxisToString());
                    DoNextDieFlowExec(item);
                }
                else
                {
                    logger.Warn("Automated die test failed, please proceed with manual testing.");
                    //DoAutoTestEnd(true);
                    proberClientService?.PausedAutoTest();
                }
            }
            else
            {
                DoAutoTestEnd(null, true);
                if (logger.IsWarnEnabled) logger.WarnFormat("AutoTesting is ended, pre = X:{0},Y:{1}", dieNext.diePre?.MapX, dieNext.diePre?.MapY);
            }
        }

        private bool IsDieCompleted(DieViewModel die)
        {
            return die.Status == Core.Models.Enums.ChipStatus.OK ||
               die.Status == Core.Models.Enums.ChipStatus.VAM_COMPLETED ||
               die.Status == Core.Models.Enums.ChipStatus.IVL_COMPLETED ||
               die.Status == Core.Models.Enums.ChipStatus.EQE_COMPLETED;
        }
        private async Task MoveToDieAndTestingAsync(WPFlowViewModel selectedWPFlow, DieViewModel die, bool isFirst, bool hasNext, bool isAuto)
        {
            if (logger.IsInfoEnabled) logger.InfoFormat("Process Current Die={0}[isFirst:{1}/HasNext:{2}/Auto:{3}] => {4}", die.ToMapAxis().ToString(), isFirst, hasNext, isAuto, die.SerialNumber);
            try
            {
                var isOK = await proberClientService.MoveToAsync(die, isFirst);

                if (isOK)
                {
                    // 新增：开始单Die进度跟踪
                    MainViewModel.Instance?.DataMappingVM?.StartSingleDieTest(die);
                    // 3. 执行测试流程 - 分阶段更新进度
                    await ExecuteDieTestWithProgress(selectedWPFlow, die, hasNext, isAuto);

                    if (!hasNext)
                    {
                        if (ConfigManager.Config.IsAutoStop)
                        {
                            StopAutoTestAndExitWafer();
                        }
                        else
                        {
                            DoAutoTestEnd(die, isAuto);
                            //AutoTestingCompleted();
                        }
                    }
                }
                else
                {
                    die.ChangeStatus(Core.Models.Enums.ChipStatus.FAILED);

                    if (logger.IsErrorEnabled) logger.Error("Probe station movement failed");

                    if (autoTestingItem != null)
                    {
                        PauseAutoTesting();
                    }
                    else
                    {
                        DoAutoTestEnd(die, isAuto);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Die test execution failed: {ex.Message}", ex);
                PauseAutoTesting();
            }
        }
        private void StopAutoTestAndExitWafer()
        {
            proberClientService?.StopTestAsync();
            if (logger.IsInfoEnabled) logger.Info("Stop testing and exit the wafer.");
        }

        private void AutoTestingCompleted()
        {
            if (logger.IsInfoEnabled) logger.Info("Auto Testing Completed");
            proberClientService?.TestingCompleted();
        }

        /// <summary>
        /// 执行Die测试并更新进度
        /// </summary>
        private async Task ExecuteDieTestWithProgress(WPFlowViewModel _selectedWPFlow, DieViewModel die, bool hasNext, bool isAuto)
        {
            if (_selectedWPFlow == null)
            {
                logger.Error("No test procedure selected");
                return;
            }
            // 关键修复：确保每个Die测试开始时调用StartSingleDieTest
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainViewModel.Instance?.DataMappingVM?.StartSingleDieTest(die);
            });
            BaseSerivce? baseService = null;
            switch (_selectedWPFlow?.FlowType)
            {
                case CVWaferProberFlowType.AOI:
                    baseService = flowServices[CVWaferProberFlowType.AOI];
                    break;
                case CVWaferProberFlowType.IVL:
                case CVWaferProberFlowType.IVL_SP:
                case CVWaferProberFlowType.IVL_Camera:
                    baseService = flowServices[CVWaferProberFlowType.IVL];
                    break;
                case CVWaferProberFlowType.EQE:
                    baseService = flowServices[CVWaferProberFlowType.EQE];
                    break;
                case CVWaferProberFlowType.VAM:
                    baseService = flowServices[CVWaferProberFlowType.VAM];
                    break;
                default:
                    logger.Error($"Unsupported procedure type: {_selectedWPFlow?.FlowType}");
                    return;
            }

            if (baseService == null)
            {
                logger.Error("Corresponding test service not found");
                return;
            }

            //获取Motion Axis信息
            proberClientService?.GetCurrentDieAxisAsync();
            try
            {
                // 执行测试流程
                await baseService.StartTestingAsync(die, _selectedWPFlow, hasNext, isAuto);

            }
            catch (Exception ex)
            {
                logger.Error($"Test procedure execution failed: {ex.Message}", ex);

                throw;
            }
        }

        private void DoAutoTestEnd(DieViewModel? dieVM, bool isAuto)
        {
            TestingCompleted?.Invoke(this, new TestCompletedEventArgs(dieVM, isAuto));
            if (isAuto && dieVM != null)
            {
                proberClientService?.SendResultAsync(dieVM);
            }
            proberClientService?.TestingCompleted();
            // 新增：手动测试异常结束时，重置IsManualTesting
            if (!isAuto)
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // 替换：MainViewModel.Instance?.DataMappingVM.IsManualTesting = false;
                    var mappingVM = MainViewModel.Instance?.DataMappingVM;
                    if (mappingVM != null)
                    {
                        mappingVM.IsManualTesting = false;
                    }
                });
            }
            //
            dieVM?.CompleteTestProgress();
            if (logger.IsInfoEnabled) logger.Info("Auto Testing End");
        }

        private void OutputLog(List<DieViewModel> dieVMList)
        {
            StringBuilder sb = new StringBuilder();
            int? Y = int.MinValue;
            for (int i = 0; i < dieVMList.Count; i++)
            {
                if (Y != dieVMList[i].MapY)
                {
                    if (sb.Length > 0) logger.InfoFormat("{0}", sb.ToString());
                    Y = dieVMList[i].MapY;
                    sb.Clear();
                    sb.Append("[");
                    sb.Append(dieVMList[i].MapY).Append("]");
                    sb.Append(dieVMList[i].MapX);
                }
                else
                {
                    sb.Append(",");
                    sb.Append(dieVMList[i].MapX);
                }
            }
        }

        public void StartAutoTesting(WPFlowViewModel selectedWPFlow, List<DieViewModel> dieVMList)
        {
            if (selectedWPFlow == null ||dieVMList == null || dieVMList.Count == 0)
            {
                logger.Warn("No dice selected for testing");
                return;
            }
            // 初始化进度条
            // 初始化进度条 - 重要：必须在UI线程执行
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mappingVM = MainViewModel.Instance?.DataMappingVM;
                if (mappingVM != null)
                {
                    mappingVM.InitializeAutoTestProgress(dieVMList);
                    // 重置手动测试标记
                    mappingVM.IsManualTesting = false;
                }
            });
            //OutputLog(dieVMList);
            proberClientService?.StartAutoTest();
            autoTestingItem = new AutoTestingItem(dieVMList, selectedWPFlow);
            DoNextDieFlowExec(autoTestingItem);
        }

        public void StopAutoTesting()
        {
            // 重置进度条
            MainViewModel.Instance?.DataMappingVM?.ResetProgressBars();

            autoTestingItem = null;
            proberClientService?.StopTestAsync();
            // 停止但不重置进度条，保留当前进度状态
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mappingVM = MainViewModel.Instance?.DataMappingVM;
                if (mappingVM != null)
                {
                    mappingVM._progressUpdateTimer.Stop(); // 仅停止定时器
                    mappingVM.IsProcessing = false;
                }
            });
        }

        public void PauseAutoTesting(bool isRollback = true)
        {
            if (autoTestingItem != null)
            {
                // 暂停时保存当前测试上下文
                _pauseContext = (
                    autoTestingItem.TestingDieVMList,
                    autoTestingItem.CurTestingIndex,
                    MainViewModel.Instance.DataMappingVM.CompletedTestCount
                );
                autoTestingItem.IsPaused = true;
                if (isRollback) autoTestingItem.RollbackToPrevious();
                // 停止进度定时器，但不重置进度数据
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mappingVM = MainViewModel.Instance.DataMappingVM;
                    mappingVM._progressUpdateTimer.Stop(); // 仅停止定时器，不重置进度值
                    mappingVM.IsProcessing = false; // 仅恢复按钮状态，不重置进度
                });
            }
            proberClientService?.PausedAutoTest();
            //MainViewModel.Instance.IsProcessing=false;
            MainViewModel.Instance.DataMappingVM.EnableBtnGUI(true);
            //MainViewModel.Instance.IsNotProcessing=true;
            if (logger.IsInfoEnabled) logger.Info("Pause auto testing");
        }

        public void ContinuAutoTesting()
        {
            //if (autoTestingItem != null)
            //{
            //    autoTestingItem.IsPaused = false;
            //    proberClientService?.ContinuAutoTest();
            //    DoNextDieFlowExec(autoTestingItem);
            //}
            if (autoTestingItem != null && _pauseContext.HasValue)
            {
                autoTestingItem.IsPaused = false;
                proberClientService?.ContinuAutoTest();

                // 恢复时重新初始化进度状态
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mappingVM = MainViewModel.Instance.DataMappingVM;
                    // 恢复进度上下文
                    mappingVM.TotalTestCount = _pauseContext.Value.testQueue.Count;
                    mappingVM.CompletedTestCount = _pauseContext.Value.completedCount;
                    mappingVM.IsProcessing = true; // 标记为处理中，显示进度条
                    mappingVM.IsManualTesting = false; // 确保自动测试进度条显示
                                                       // 重启进度定时器
                    mappingVM._progressUpdateTimer.Start();
                    // 强制更新总进度
                    mappingVM.UpdateTotalProgress();
                });

                DoNextDieFlowExec(autoTestingItem);
                // 清空暂停上下文
                _pauseContext = null;
            }
        }

        public void ReconnectDev()
        {
            proberClientService?.ReconnectAsync();
        }

        public void Maintenance()
        {
            proberClientService?.Maintenance();
        }

        public void ReRegist()
        {
            mqttService.Reconnect();
        }

        private (List<DieViewModel> testQueue, int currentIndex, int completedCount)? _pauseContext;

    }
}
