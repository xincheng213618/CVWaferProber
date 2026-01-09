using ChipMapping.ViewModels;
using CVCommCore;
using CVWaferProber.Models;
using CVWaferProber.ViewModels;
using CVWaferProber.WinMsg;
using CVWPFCamImageCtrl;
using CVWPFSpectrometerCtrl.ViewModels;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.StateMachine;

namespace CVWaferProber.Services
{
    public class MainService : ReflectionSingleton<MainService>
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MainService));
        public string ProberId { get; set; }
        #region Events
        public event EventHandler TestingCompleted;
        public event EventHandler<ChipViewModel> ChipSelected;
        public event EventHandler<(DieViewModel?, DieViewModel)> AutoTestingNext;
        #endregion
        private MappingService mappingService;
        private GSWMProcessor? _wmProcessor;
        private IWaferProberClient _clientProber;
        private IStateMachine _proberState;
        private readonly Dictionary<CVWaferProberFlowType, BaseSerivce> flowServices =
            new Dictionary<CVWaferProberFlowType, BaseSerivce>();
        private AutoTestingItem? autoTestingItem;
        private ConnectionInfo _connectionInfo;

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

        public IWaferProberClient ProberClient { get => _clientProber; }
        public IStateMachine StateMachine { get => _proberState; }
        public ConnectionInfo ConnectionInfo { get => _connectionInfo; }
        public void Startup(string ip, int port)
        {
            _clientProber?.ConnectAsync(ip, port).Wait();
        }
        public void Startup()
        {
            Startup(_connectionInfo.ServerIP, _connectionInfo.Port);
        }
        public void InitializeService(string proberId, RCRestService rcService)
        {
            this.ProberId = proberId;
            //this._connectionInfo = connectionInfo;
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
            this._connectionInfo = new ConnectionInfo() { ServerIP = "127.0.0.1", Port = 8898 };
            this._clientProber = new WaferProberTCPClient();
            var eventAggregator = _clientProber.EventAggregator;
            eventAggregator.Subscribe<ConnectionStateChangedEvent>(OnClientProberStateChanged);
            eventAggregator.Subscribe<StateTransitionEvent>(OnStateTransition);

            ProberStateMachine proberState = new ProberStateMachine(eventAggregator, _clientProber);
            _proberState = proberState;
            eventAggregator.Subscribe<StateUpdatedEvent>(OnProberStateUpdated);
            _proberState.StartAsync().Wait();
        }

        private void OnStateTransition(StateTransitionEvent @event)
        {
            if (logger.IsInfoEnabled) logger.InfoFormat("StateTransition {0} => {1}", @event.FromState.ToString(), @event.ToState.ToString());
            if (logger.IsInfoEnabled) logger.InfoFormat("CurrentState = {0}", _proberState.CurrentState.ToString());
            _connectionInfo.DevCurrentState = _proberState.CurrentState;
        }

        private void OnProberStateUpdated(StateUpdatedEvent @event)
        {
            if (logger.IsInfoEnabled) logger.InfoFormat("StateUpdated => {0}", @event.Status.ToString());
            _connectionInfo.DevCurrentState = @event.Status.CurrentState;
            var dieVM = autoTestingItem?.GetCurrentDieVM();
            if (dieVM == null)
            {
                return;
            }
            dieVM.MStatus = @event.Status.MotionStatus;
            //if (@event.Status.MotionStatus == MotionStatus.MotionComplete)
            //{
            //    var axis = dieVM.ToMapAxis();
            //    string x = @event.Status.CurrentPosition.CurrentX;
            //    string y = @event.Status.CurrentPosition.CurrentY;
            //    if (axis.x == x && axis.y == y)
            //    {
            //        if (logger.IsInfoEnabled) logger.InfoFormat("Move Absolute Pos => {0}", @event.Status.CurrentPosition.ToString());
            //    }
            //}
        }

        private void OnClientProberStateChanged(ConnectionStateChangedEvent @event)
        {
            logger.InfoFormat("ConnectionStateChanged => {0}:{1}, IsConnected={2}", @event.ServerIp, @event.Port, @event.IsConnected);
            _connectionInfo?.SetConnected(@event.IsConnected);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAutoTestingNextCompleted(object? sender, DieViewModel e)
        {
            if (autoTestingItem != null) DoDieFlowExec(autoTestingItem);
            else TestingCompleted?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTestingCompleted(object? sender, EventArgs e)
        {
            autoTestingItem = null;
            TestingCompleted?.Invoke(this, e);
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

        public Task DoDieFlowExec(WPFlowViewModel? _selectedWPFlow, DieViewModel die, bool isEnd = true)
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
            Task task = baseSerivce?.StartTesting(die, _selectedWPFlow, isEnd);

            return task;
        }
        private void DoDieFlowExec(AutoTestingItem item)
        {
            (DieViewModel? diePre, DieViewModel? die) dieNext = item.GetNextDieVM();
            if (dieNext.die != null)
            {
                AutoTestingNext?.Invoke(this, (dieNext.diePre, dieNext.die));
                Task.Factory.StartNew(async () =>
                {
                    await DoAutoDieFlowExecAsync(item.CurSelectedWPFlow, dieNext.die, dieNext.diePre == null, item.IsEnd);
                });
            }
            else
            {
                if (logger.IsWarnEnabled) logger.WarnFormat("AutoTesting is ended, pre = X:{0},Y:{1}", dieNext.diePre?.MapX, dieNext.diePre?.MapY);
            }
        }
        private async Task DoAutoDieFlowExecAsync(WPFlowViewModel? _selectedWPFlow, DieViewModel die, bool isFirst, bool isEnd)
        {
            if (_clientProber != null)
            {
                if (_clientProber.IsConnected)
                {
                    if (logger.IsInfoEnabled) logger.InfoFormat("Process Current Die={0}[isFirst:{1}/isEnd:{2}] => {3}", die.ToMapAxis().ToString(), isFirst, isEnd,die.SerialNumber);
                    var isOK = await MoveAbsoluteAxisAsync(die);
                    if (isFirst) isOK = isOK && await ZUpAsync(die);
                    if (isOK) await DoDieFlowExec(_selectedWPFlow, die, isEnd);
                    else
                    {
                        die.ChangeStatus(Core.Models.Enums.ChipStatus.FAILED);
                        if (logger.IsErrorEnabled) logger.Error("Prober client Move Absolute failed");
                        TestingCompleted?.Invoke(this, new EventArgs());
                    }
                    if (isEnd) 
                    {
                        isOK = await StopTestAsync();
                    }
                }
                else
                {
                    if (logger.IsErrorEnabled) logger.Error("Prober client is Disconnected");
                    TestingCompleted?.Invoke(this, new EventArgs());
                }
            }
            else
            {
                if (logger.IsErrorEnabled) logger.Error("Prober client is null");
                TestingCompleted?.Invoke(this, new EventArgs());
            }
        }
        private async Task<bool> StopTestAsync()
        {
            if (logger.IsInfoEnabled) logger.Info("Prober client StopTest");
            _clientProber?.StopAsync();
            return await WaitingStopTestAsync();
        }

        private async Task<bool> MoveAbsoluteAxisAsync(DieViewModel die)
        {
            var absAxis = die.ToMapAxis();
            die.MStatus = MotionStatus.MovingAbsolute;
            if (logger.IsInfoEnabled) logger.InfoFormat("Prober client Moving Absolute Axis => {0}", absAxis.ToString());
            _clientProber?.MoveAbsoluteAsync(absAxis.y, absAxis.x);
            return await WaitingMotionMoveAsync(die);
        }  
        private async Task<bool> ZUpAsync(DieViewModel die)
        {
            die.MStatus = MotionStatus.ZUpMoving;
            if (logger.IsInfoEnabled) logger.Info("Prober client ZUp Moving");
            _clientProber?.StartTestConfirmAsync();
            return await WaitingZMotionAsync(die, MotionStatus.ZUp);
        } 
        private async Task<bool> ZDownAsync(DieViewModel die)
        {
            die.MStatus = MotionStatus.ZDownMoving;
            if (logger.IsInfoEnabled) logger.Info("Prober client ZDown Moving");
            _clientProber?.GetZDownStatusAsync();
            return await WaitingZMotionAsync(die, MotionStatus.ZDown);
        }

        private async Task<bool> LoopWaitingMotionMoveAsync(DieViewModel dieVM, CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < 300; i++)
            {
                if (dieVM.MStatus == MotionStatus.MotionComplete) return await Task.FromResult(true);
                else if (dieVM.MStatus == MotionStatus.MotionFailed) return await Task.FromResult(false);
                else if (dieVM.MStatus == MotionStatus.MotionTimeout) return await Task.FromResult(false);
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            return await Task.FromResult(false);
        }
        private async Task<bool> WaitingMotionMoveAsync(DieViewModel dieVM, CancellationToken cancellationToken = default)
        {
            const int checkInterval = 10;
            const int maxChecks = 3000;
            using var semaphore = new SemaphoreSlim(1, 1);

            // 启动一个后台任务轮询状态
            var pollingTask = Task.Run(async () =>
            {
                for (int i = 0; i < maxChecks; i++)
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var status = dieVM.MStatus;
                        if (status == MotionStatus.MotionComplete ||
                            status == MotionStatus.MotionFailed ||
                            status == MotionStatus.MotionTimeout)
                        {
                            return status;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    await Task.Delay(checkInterval, cancellationToken).ConfigureAwait(false);
                }
                return MotionStatus.MotionTimeout;
            }, cancellationToken);

            try
            {
                var finalStatus = await pollingTask.ConfigureAwait(false);
                return finalStatus == MotionStatus.MotionComplete;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
        private async Task<bool> WaitingZMotionAsync(DieViewModel dieVM, MotionStatus statusOk, CancellationToken cancellationToken = default)
        {
            const int checkInterval = 10;
            const int maxChecks = 3000;
            using var semaphore = new SemaphoreSlim(1, 1);

            // 启动一个后台任务轮询状态
            var pollingTask = Task.Run(async () =>
            {
                for (int i = 0; i < maxChecks; i++)
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var status = dieVM.MStatus;
                        if (status == MotionStatus.ZUp ||
                            status == MotionStatus.ZDown ||
                            status == MotionStatus.MotionTimeout)
                        {
                            return status;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    await Task.Delay(checkInterval, cancellationToken).ConfigureAwait(false);
                }
                return MotionStatus.MotionTimeout;
            }, cancellationToken);

            try
            {
                var finalStatus = await pollingTask.ConfigureAwait(false);
                return finalStatus == statusOk;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Task<bool> WaitingStopTestAsync(CancellationToken cancellationToken = default)
        {
            const int checkInterval = 10;
            const int maxChecks = 3000;
            using var semaphore = new SemaphoreSlim(1, 1);

            // 启动一个后台任务轮询状态
            var pollingTask = Task.Run(async () =>
            {
                for (int i = 0; i < maxChecks; i++)
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var status = MotionStatus.MotionComplete;
                        if (status == MotionStatus.ZUp ||
                            status == MotionStatus.ZDown ||
                            status == MotionStatus.MotionTimeout)
                        {
                            return status;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    await Task.Delay(checkInterval, cancellationToken).ConfigureAwait(false);
                }
                return MotionStatus.MotionTimeout;
            }, cancellationToken);

            try
            {
                var finalStatus = await pollingTask.ConfigureAwait(false);
                return finalStatus == MotionStatus.MotionComplete;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        public void StartAutoTesting(WPFlowViewModel? _selectedWPFlow, List<DieViewModel> dieVMList)
        {
            autoTestingItem = new AutoTestingItem(dieVMList, _selectedWPFlow);
            DoDieFlowExec(autoTestingItem);
        }

        public void StopAutoTesting()
        {
            autoTestingItem = null;
        }
    }
}
