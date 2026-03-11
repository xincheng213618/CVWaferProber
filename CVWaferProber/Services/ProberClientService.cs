using CVCommCore;
using CVWaferProber.Core.Config;
using CVWaferProber.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.StateMachine;
using ConnectionInfo = CVWaferProber.Models.ConnectionInfo;

namespace CVWaferProber.Services
{
    public class ProberClientService : ReflectionSingleton<ProberClientService>
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ProberClientService));

        private ConnectionInfo _connectionInfo;
        private IWaferProberClient _clientProber;
        private IStateMachine _proberState;
        private MappingDataViewModel? _mappingDataViewModel;
        public IWaferProberClient ProberClient { get => _clientProber; }
        public IStateMachine StateMachine { get => _proberState; }
        public ConnectionInfo ConnectionInfo { get => _connectionInfo; }
        public void Startup(string ip, int port)
        {
            _connectionInfo.ServerIP = ip;
            _connectionInfo.Port = port;
            _clientProber?.DisconnectAsync().Wait();
            Startup();
        } 
        public void SetConnectionSettings(string ip, int port)
        {
            _connectionInfo.ServerIP = ip;
            _connectionInfo.Port = port;
        }
        public void Startup()
        {
            try
            {
                _clientProber?.ConnectAsync(_connectionInfo.ServerIP, _connectionInfo.Port).Wait();
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
        }
        private ProberClientService()
        {
            var connsettings = ConfigManager.Config.ConnectionSettings;
            this._connectionInfo = new ConnectionInfo()
            {
                ServerIP = connsettings.ServerIP,
                Port = connsettings.Port
            };
            this._clientProber = new WaferProberTCPClient();
            var eventAggregator = _clientProber.EventAggregator;
            eventAggregator.Subscribe<ConnectionStateChangedEvent>(OnClientProberStateChanged);
            eventAggregator.Subscribe<StateTransitionEvent>(OnStateTransition);
            eventAggregator.Subscribe<CommandSentEvent>(OnCommandSented);

            var motionSettings = ConfigManager.Config.MotionSettings;
            ProberStateMachine proberState = new ProberStateMachine(eventAggregator, _clientProber,
                motionSettings.DefaultXYMotionTimeout, motionSettings.DefaultZMotionTimeout);
            _proberState = proberState;
            eventAggregator.Subscribe<StateUpdatedEvent>(OnProberStateUpdated);
            _proberState.StartAsync().Wait();
        }
        private void OnMappingFileLoad(MappingFileLoadEvent @event)
        {
            _mappingDataViewModel?.LoadMappingFile(@event.MappingFile);
        }
        private void OnCommandSented(CommandSentEvent @event)
        {
            string cmd = @event.Command.Trim('$', '#');
            logger.DebugFormat("SEND => {0}", cmd);
        }

        public void InitializeMapVM(MappingDataViewModel mappingDataViewModel)
        {
            _mappingDataViewModel = mappingDataViewModel;
        }
        public void ContinuAutoTest()
        {
            _proberState?.TransitionToAsync(ProberState.Testing);
        }
        private void OnProberStateUpdated(StateUpdatedEvent @event)
        {
            if (logger.IsDebugEnabled) logger.DebugFormat("StateUpdated => {0}", @event.Status.ToString());
            _connectionInfo.DevCurrentState = @event.Status.CurrentState;
            var dieVM = MainService.Instance.autoTestingItem?.GetCurrentDieVM();
            if (dieVM == null)
            {
                return;
            }
            dieVM.MStatus = @event.Status.MotionStatus;
        }

        private void OnStateTransition(StateTransitionEvent @event)
        {
            if (@event.ToState == ProberState.Ready)
            {
                _clientProber?.GetWaferIdAsync();
                _clientProber?.GetMappingAsync();
            }
            else if (@event.ToState == ProberState.WaferLoaded)
            {
                //_mappingDataViewModel?.LoadMappingFile(_proberState.GetStatus().CurrentMappingFile);
                _mappingDataViewModel.WaferId = _proberState.GetStatus().CurrentWaferId;
            }
            if (logger.IsInfoEnabled) logger.InfoFormat("StateTransition {0} => {1}", @event.FromState.ToString(), @event.ToState.ToString());
            _connectionInfo.DevCurrentState = _proberState.CurrentState;
        }

        private void OnClientProberStateChanged(ConnectionStateChangedEvent @event)
        {
            _connectionInfo?.SetConnected(@event.IsConnected);
            if (@event.IsConnected)
            {
                logger.InfoFormat("ConnectionStateChanged => {0}:{1}, IsConnected={2}", @event.ServerIp, @event.Port, @event.IsConnected);
                _clientProber.QueryStatusAsync();
                _clientProber.GetCurrentTemperatureAsync();
            }
        }
        public async Task GetCurrentDieAxisAsync()
        {
            await _clientProber.GetCurrentDieAxisAsync();
        }
        public async Task SendResultAsync(int result)
        {
            await _clientProber?.SendResultAsync(result);
        }

        public async Task SendResultAsync(DieViewModel dieVM)
        {
            Core.Models.Enums.ChipStatus status = Core.Models.Enums.ChipStatus.WAITING;
            if(dieVM.Status.HasValue) { status = dieVM.Status.Value; }
            await SendResultAsync(GetResultVal(status));
        }

        private int GetResultVal(Core.Models.Enums.ChipStatus status)
        {
            int reVal = 2;
            switch (status)
            {
                case Core.Models.Enums.ChipStatus.WAITING:
                    break;
                case Core.Models.Enums.ChipStatus.TESTING:
                    break;
                case Core.Models.Enums.ChipStatus.OK:
                case Core.Models.Enums.ChipStatus.IVL_COMPLETED:
                case Core.Models.Enums.ChipStatus.EQE_COMPLETED:
                case Core.Models.Enums.ChipStatus.VAM_COMPLETED:
                    reVal = 1;
                    break;
                case Core.Models.Enums.ChipStatus.AOI_NG:
                    break;
                case Core.Models.Enums.ChipStatus.DW_NG:
                    break;
                case Core.Models.Enums.ChipStatus.BLIND:
                    break;
                case Core.Models.Enums.ChipStatus.CAL_NG:
                    break;
                case Core.Models.Enums.ChipStatus.I2C_NG:
                    break;
                case Core.Models.Enums.ChipStatus.AOI_LINE_NG:
                    break;
                case Core.Models.Enums.ChipStatus.IVL_TESTING:
                    break;
                case Core.Models.Enums.ChipStatus.EQE_TESTING:
                    break;
                case Core.Models.Enums.ChipStatus.VAM_TESTING:
                    break;
                case Core.Models.Enums.ChipStatus.FAILED:
                    break;
                case Core.Models.Enums.ChipStatus.OVERTIME:
                    break;
                case Core.Models.Enums.ChipStatus.SKIP:
                    break;
                default:
                    reVal = 2;
                    break;
            }
            return reVal;
        }

        public async Task<bool> MoveToAsync(DieViewModel die, bool isFirst = false)
        {
            if (_clientProber != null)
            {
                if (_clientProber.IsConnected)
                {
                    if(_proberState.CurrentState != ProberState.Maintenance)
                    {
                        var isOK = await MoveAbsoluteAxisAsync(die);
                        //第一die时需要发送扎针
                        if (isFirst) isOK = isOK && await ZUpAsync(die);
                        return isOK;
                    }
                    else
                    {
                        if (logger.IsWarnEnabled) logger.Warn("Currently in manual mode, cannot send.");
                        return false;
                    }
                }
                else
                {
                    if (logger.IsErrorEnabled) logger.Error("Prober client is Disconnected");
                    return false;
                }
            }
            else
            {
                if (logger.IsErrorEnabled) logger.Error("Prober client is null");
                return false;
            }
        }
        private async Task<bool> MoveAbsoluteAxisAsync(DieViewModel die)
        {
            var absAxis = die.ToMapAxis();
            die.MStatus = MotionStatus.MovingAbsolute;
            if (logger.IsInfoEnabled) logger.InfoFormat("Moving Absolute Map Axis To Die => {0}", die.MapAxisToString());
            _clientProber?.MoveAbsoluteAsync(absAxis.y, absAxis.x);
            return await WaitingMotionMoveAsync(die);
        }
        private async Task<bool> ZUpAsync(DieViewModel die)
        {
            die.MStatus = MotionStatus.ZUpMoving;
            if (logger.IsInfoEnabled) logger.Info("ZUp Moving");
            _clientProber?.StartTestConfirmAsync();
            return await WaitingZMotionAsync(die, MotionStatus.ZUp);
        }
        private async Task<bool> ZDownAsync(DieViewModel die)
        {
            die.MStatus = MotionStatus.ZDownMoving;
            if (logger.IsInfoEnabled) logger.Info("ZDown Moving");
            _clientProber?.GetZDownStatusAsync();
            return await WaitingZMotionAsync(die, MotionStatus.ZDown);
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

        public void PausedAutoTest()
        {
            _proberState?.TransitionToAsync(ProberState.Paused);
        }
        public async Task<bool> StopTestAsync()
        {
            if (logger.IsInfoEnabled) logger.Info("Prober client StopTest");
            _clientProber?.StopAsync();
            _proberState?.TransitionToAsync(ProberState.WaitingForWafer);
            return await WaitingStopTestAsync();
        }
        public void TestingCompleted()
        {
            _proberState?.TransitionToAsync(ProberState.Stoped);
            _connectionInfo.DevCurrentState = _proberState.CurrentState;
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

        public void StartAutoTest()
        {
            _proberState?.TransitionToAsync(ProberState.Testing);
        }

        public async Task ReconnectAsync()
        {
            await _clientProber.DisconnectAsync();
            Task.Delay(1000).ConfigureAwait(true);
            await _clientProber.ConnectAsync(_connectionInfo.ServerIP, _connectionInfo.Port);
        }

        public void Maintenance()
        {
            _proberState?.TransitionToAsync(ProberState.Maintenance);
        }

        public void ReloadSettings()
        {
            var motionSettings = ConfigManager.Config.MotionSettings;
            _proberState?.ReloadSettings(motionSettings.DefaultXYMotionTimeout, motionSettings.DefaultZMotionTimeout);
        }

        public void Subscribe<T>(Action<T> handler) where T : class
        {
            _clientProber.EventAggregator.Subscribe<T>(handler);
        }

        public async Task<bool> TryConnectAsync()
        {
            try
            {
                bool bR = await _clientProber?.TryConnectAsync(_connectionInfo.ServerIP, _connectionInfo.Port);
                return bR;
            }
            catch (Exception e)
            {
                logger.Error(e);
                return false;
            }
        }

        public void InitUI()
        {
            for (int i = 0; i < 50; i++)
            {
                if (_connectionInfo.DevCurrentState == ProberState.WaferLoaded)
                {
                    _mappingDataViewModel?.LoadMappingFile(_proberState.GetStatus().CurrentMappingFile);
                    break;
                }
                Task.Delay(100).Wait();
            }
            var eventAggregator = _clientProber.EventAggregator;
            eventAggregator.Subscribe<MappingFileLoadEvent>(OnMappingFileLoad);
        }
    }
}
