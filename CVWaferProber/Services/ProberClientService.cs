using CVCommCore;
using CVWaferProber.Models;
using CVWaferProber.ViewModels;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.StateMachine;

namespace CVWaferProber.Services
{
    public class ProberClientService : ReflectionSingleton<ProberClientService>
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ProberClientService));

        private ConnectionInfo _connectionInfo;
        private IWaferProberClient? _clientProber;
        private IStateMachine? _proberState;
        public IWaferProberClient ProberClient { get => _clientProber; }
        public IStateMachine StateMachine { get => _proberState; }
        public ConnectionInfo ConnectionInfo { get => _connectionInfo; }
        public void Startup(string ip, int port)
        {
            _clientProber?.ConnectAsync(ip, port).Wait();
        }
        public void Startup()
        {
            _clientProber?.ConnectAsync(_connectionInfo.ServerIP, _connectionInfo.Port).Wait();
        }
        private ProberClientService()
        {
            this._connectionInfo = new ConnectionInfo() { ServerIP = "192.168.1.100", Port = 8898 };
            //this._connectionInfo = new ConnectionInfo() { ServerIP = "127.0.0.1", Port = 8898 };
            this._clientProber = new WaferProberTCPClient();
            var eventAggregator = _clientProber.EventAggregator;
            eventAggregator.Subscribe<ConnectionStateChangedEvent>(OnClientProberStateChanged);
            eventAggregator.Subscribe<StateTransitionEvent>(OnStateTransition);

            ProberStateMachine proberState = new ProberStateMachine(eventAggregator, _clientProber);
            _proberState = proberState;
            eventAggregator.Subscribe<StateUpdatedEvent>(OnProberStateUpdated);
            _proberState.StartAsync().Wait();

        }
        public void Initialize()
        {

        }
        //public void Initialize(IWaferProberClient clientProber, IStateMachine proberState)
        //{
        //this._clientProber = clientProber;
        //this._proberState = proberState;

        //var eventAggregator = _clientProber.EventAggregator;
        //eventAggregator.Subscribe<ConnectionStateChangedEvent>(OnClientProberStateChanged);
        //eventAggregator.Subscribe<StateTransitionEvent>(OnStateTransition);
        ////
        //eventAggregator.Subscribe<StateUpdatedEvent>(OnProberStateUpdated);
        //}

        private void OnProberStateUpdated(StateUpdatedEvent @event)
        {
            if (logger.IsInfoEnabled) logger.InfoFormat("StateUpdated => {0}", @event.Status.ToString());
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
                _clientProber.GetMappingAsync();
            }
            else if (@event.ToState == ProberState.WaferLoaded)
            {
                MainViewModel.Instance?.LoadMappingFile(_proberState.GetStatus().CurrentMappingFile);
            }
            if (logger.IsInfoEnabled) logger.InfoFormat("StateTransition {0} => {1}", @event.FromState.ToString(), @event.ToState.ToString());
            if (logger.IsInfoEnabled) logger.InfoFormat("CurrentState = {0}", _proberState.CurrentState.ToString());
            _connectionInfo.DevCurrentState = _proberState.CurrentState;
        }

        private void OnClientProberStateChanged(ConnectionStateChangedEvent @event)
        {
            logger.InfoFormat("ConnectionStateChanged => {0}:{1}, IsConnected={2}", @event.ServerIp, @event.Port, @event.IsConnected);
            _connectionInfo?.SetConnected(@event.IsConnected);
        }

        public void SendResultAsync(int result)
        {
            _clientProber?.SendResultAsync(result);
        }

        public async Task<bool> MoveTo(DieViewModel die, bool isFirst)
        {
            if (_clientProber != null)
            {
                if (_clientProber.IsConnected)
                {
                    //if (logger.IsInfoEnabled) logger.InfoFormat("Process Current Die={0}[isFirst:{1}/isEnd:{2}] => {3}", die.ToMapAxis().ToString(), isFirst, isEnd, die.SerialNumber);
                    var isOK = await MoveAbsoluteAxisAsync(die);
                    //第一die时需要发送扎针
                    if (isFirst) isOK = isOK && await ZUpAsync(die);
                    return isOK;
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
            if (logger.IsInfoEnabled) logger.InfoFormat("Prober client Moving Absolute Axis => {0}", absAxis.ToString());
            _clientProber?.MoveAbsoluteAsync(absAxis.y, absAxis.x);
            return await WaitingMotionMoveAsync(die);
            //Task.Delay(2000).Wait();
            //return true;
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

        public async Task<bool> StopTestAsync()
        {
            if (logger.IsInfoEnabled) logger.Info("Prober client StopTest");
            _clientProber?.StopAsync();
            return await WaitingStopTestAsync();
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

    }
}
