using ChipMapping.ViewModels;
using CVCommCore;
using CVWaferProber.Models;
using CVWaferProber.ViewModels;
using CVWaferProber.WinMsg;
using CVWPFCamImageCtrl;
using CVWPFSpectrometerCtrl.ViewModels;
using System.Text;

namespace CVWaferProber.Services
{
    public class MainService : ReflectionSingleton<MainService>
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MainService));
        public string ProberId { get; set; }
        #region Events
        public event EventHandler<TestCompletedEventArgs> TestingCompleted;
        public event EventHandler<ChipViewModel> ChipSelected;
        public event EventHandler<(DieViewModel?, DieViewModel)> AutoTestingNextDie;
        #endregion
        private MappingService mappingService;
        private GSWMProcessor? _wmProcessor;
        private ProberClientService proberClientService;
        private readonly Dictionary<CVWaferProberFlowType, BaseSerivce> flowServices =
            new Dictionary<CVWaferProberFlowType, BaseSerivce>();
        public AutoTestingItem? autoTestingItem { get; private set; }

        private MainService()
        {
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
        public void InitializeService(string proberId, RCRestService rcService)
        {
            this.ProberId = proberId;
            //
            BaseSerivce ivlService = new IVLService(proberId, mappingService.CustomVM, rcService);
            flowServices[CVWaferProberFlowType.IVL] = ivlService;
            ivlService.TestingCompleted += OnTestingCompleted;
            ivlService.AutoTestingNextCompleted += OnAutoTestingNextCompleted;

            BaseSerivce aoiService = new AOIService(rcService);
            flowServices[CVWaferProberFlowType.AOI] = aoiService;
            aoiService.TestingCompleted += OnTestingCompleted;
            aoiService.AutoTestingNextCompleted += OnAutoTestingNextCompleted;

            BaseSerivce eqeService = new EQEService(rcService);
            flowServices[CVWaferProberFlowType.EQE] = eqeService;
            eqeService.TestingCompleted += OnTestingCompleted;
            eqeService.AutoTestingNextCompleted += OnAutoTestingNextCompleted;

            BaseSerivce vamService = new VAMService(rcService);
            flowServices[CVWaferProberFlowType.VAM] = vamService;
            vamService.TestingCompleted += OnTestingCompleted;
            vamService.AutoTestingNextCompleted += OnAutoTestingNextCompleted;
        }
        private void InitializeClientProber()
        {
            proberClientService = ProberClientService.Instance;
            proberClientService.Initialize();

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
            //发送结果给机台
            proberClientService?.SendResultAsync(dieVM);
            if (autoTestingItem != null)
            {
                logger.InfoFormat("IsPaused={0},e.Status={1}", autoTestingItem.IsPaused, dieVM.Status.ToString());
                if (dieVM.Status != Core.Models.Enums.ChipStatus.OK)
                {
                    PauseAutoTesting();
                }
                else if (!autoTestingItem.IsPaused)
                {
                    DoDieFlowExec(autoTestingItem);
                }
            }
            else
            {
                DoAutoTestEnd(dieVM, true);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTestingCompleted(object? sender, TestCompletedEventArgs e)
        {
            if (e.IsAuto) autoTestingItem = null;
            DoAutoTestEnd(e.DieVM, e.IsAuto);
        }

        #region Window Message
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
        #endregion

        public void ResultDisplay(DieViewModel dieViewModel)
        {
            foreach (var item in flowServices)
            {
                item.Value.ResultDisplay(dieViewModel);
            }
        }

        public Task DoDieFlowExec(WPFlowViewModel _selectedWPFlow, DieViewModel die, bool hasNext, bool isAuto)
        {
            if (_selectedWPFlow == null)
            {
                if (logger.IsErrorEnabled) logger.ErrorFormat("No flow selected for current die");
                return Task.CompletedTask;
            }
            BaseSerivce? baseSerivce = null;
            switch (_selectedWPFlow?.FlowType)
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
            Task? task = baseSerivce?.StartTesting(die, _selectedWPFlow, hasNext, isAuto);

            return task;
        }
        private void DoDieFlowExec(AutoTestingItem item)
        {
            (DieViewModel? diePre, DieViewModel? die) dieNext = item.GetNextDieVM();
            if (dieNext.die != null)
            {
                logger.InfoFormat("Next Die={0}/{1}", dieNext.die.MapAxisToString(), dieNext.die.Status.ToString());
                if (dieNext.die.Status == Core.Models.Enums.ChipStatus.WAITING)
                {
                    //logger.InfoFormat("OnAutoTestingNext={0}/{1}", dieNext.die.MapAxisToString(), dieNext.die.Status.ToString());
                    AutoTestingNextDie?.Invoke(this, (dieNext.diePre, dieNext.die));
                    //logger.InfoFormat("DoAutoDieFlowExecAsync={0}/{1}", dieNext.die.MapAxisToString(), dieNext.die.Status.ToString());
                    Task.Factory.StartNew(async () =>
                    {
                        await DoAutoDieFlowExecAsync(item.CurSelectedWPFlow, dieNext.die, dieNext.diePre == null, item.HasNext, true);
                    });
                }
                else if (dieNext.die.Status == Core.Models.Enums.ChipStatus.OK)
                {
                    DoDieFlowExec(item);
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
                if (logger.IsWarnEnabled) logger.WarnFormat("AutoTesting is ended, pre = X:{0},Y:{1}", dieNext.diePre?.MapX, dieNext.diePre?.MapY);
            }
        }
        private async Task DoAutoDieFlowExecAsync(WPFlowViewModel _selectedWPFlow, DieViewModel die, bool isFirst, bool hasNext, bool isAuto)
        {
            if (logger.IsInfoEnabled) logger.InfoFormat("Process Current Die={0}[isFirst:{1}/HasNext:{2}/Auto:{3}] => {4}", die.ToMapAxis().ToString(), isFirst, hasNext, isAuto, die.SerialNumber);
            var isOK = await proberClientService.MoveToAsync(die, isFirst);
            if (isOK)
            {
                await DoDieFlowExec(_selectedWPFlow, die, hasNext, isAuto);
                if (!hasNext)
                {
                    await proberClientService.StopTestAsync();
                }
            }
            else
            {
                die.ChangeStatus(Core.Models.Enums.ChipStatus.FAILED);
                if (logger.IsErrorEnabled) logger.Error("Prober client Move Absolute failed");
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

        private void DoAutoTestEnd(DieViewModel dieVM, bool isAuto)
        {
            TestingCompleted?.Invoke(this, new TestCompletedEventArgs(dieVM, isAuto));
            if (isAuto)
            {
                proberClientService.SendResultAsync(dieVM);
                proberClientService.TestingCompleted();
            }
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

        public void StartAutoTesting(WPFlowViewModel? _selectedWPFlow, List<DieViewModel> dieVMList)
        {
            //OutputLog(dieVMList);
            proberClientService?.StartAutoTest();
            autoTestingItem = new AutoTestingItem(dieVMList, _selectedWPFlow);
            DoDieFlowExec(autoTestingItem);
        }

        public void StopAutoTesting()
        {
            autoTestingItem = null;
            //_clientProber?.StopAsync();
            proberClientService?.StopTestAsync();
        }

        public void PauseAutoTesting(bool isRollback = true)
        {
            if (autoTestingItem != null)
            {
                autoTestingItem.IsPaused = true;
                if (isRollback) autoTestingItem.RollbackToPrevious();
            }
            proberClientService?.PausedAutoTest();
            //MainViewModel.Instance.IsProcessing=false;
            MainViewModel.Instance.EnableBtnGUI(true);
            //MainViewModel.Instance.IsNotProcessing=true;
        }

        public void ContinuAutoTesting()
        {
            if (autoTestingItem != null)
            {
                autoTestingItem.IsPaused = false;
                proberClientService?.ContinuAutoTest();
                DoDieFlowExec(autoTestingItem);
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
    }
}
