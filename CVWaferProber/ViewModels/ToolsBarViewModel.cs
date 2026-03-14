using ColorVision.UI;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Services;
using MySqlX.XDevAPI;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WaferComm.Client;
using WaferComm.Core;
using WaferComm.StateMachine;

namespace CVWaferProber.ViewModels
{
    public static class WaferProberData
    {
        public static WPFlowViewModel? SelectedWPFlow { get; set; }
        public static DateTime? RunDateTime { get; set; }
    }

    public class ToolsBarConfig : ViewModelBase, IConfig
    {
        public string CameraPosition { get => _CameraPosition; set { _CameraPosition = value; OnPropertyChanged(); } }
        private string _CameraPosition = "Null";
    }

    public class ToolsBarViewModel : ViewModelBase
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(VAMService));

        public ToolsBarConfig Config => ConfigService.Instance.GetRequiredService<ToolsBarConfig>();

        private readonly IWaferProberClient _client;
        private readonly IStateMachine _proberState;
        private readonly MainViewModel _mainVM;

        public ICommand LiftAllCommand { get; }
        public ICommand ToMainCameraCommand { get; }
        public ICommand ToAuxCameraCommand { get; }
        public ICommand ToIntegratingSphereCommand { get; }
        public ICommand StartAutoTestCommand { get; }
        public ICommand ContinuAutoTestCommand { get; }
        public ICommand StopAutoTestCommand { get; }
        public ICommand CloseTestCommand { get; }
        public ICommand PauseAutoTestCommand { get; }

        // 所有 CanXxx 属性中加入 !_client.IsMoving 检查，移动中全部禁用
        public bool CanLiftAll => !_client.IsMoving &&
            (_proberState.CurrentState == ProberState.WaferLoaded || _proberState.CurrentState == ProberState.Stoped);
        public bool CanToMainCamera => !_client.IsMoving &&
            (_proberState.CurrentState == ProberState.WaferLoaded || _proberState.CurrentState == ProberState.Stoped);
        public bool CanToAuxCamera => !_client.IsMoving &&
            (_proberState.CurrentState == ProberState.WaferLoaded || _proberState.CurrentState == ProberState.Stoped);
        public bool CanToIntegratingSphere => !_client.IsMoving &&
            (_proberState.CurrentState == ProberState.WaferLoaded || _proberState.CurrentState == ProberState.Stoped);

        public bool CanStartAutoTest => !_client.IsMoving &&
            (_proberState.CurrentState == ProberState.WaferLoaded || _proberState.CurrentState == ProberState.Stoped);
        public bool CanContinuAutoTest => !_client.IsMoving &&
            _proberState.CurrentState == ProberState.Paused && _mainVM.IsNotProcessing;
        public bool CanStopAutoTest => !_client.IsMoving &&
            _mainVM.IsNotProcessing &&
            (_proberState.CurrentState == ProberState.Testing || _proberState.CurrentState == ProberState.WaferLoaded ||
             _proberState.CurrentState == ProberState.Paused || _proberState.CurrentState == ProberState.Stoped);
        public bool CanPauseAutoTest => !_client.IsMoving &&
            _proberState.CurrentState == ProberState.Testing;

        public ToolsBarViewModel(MainViewModel mainVM, IWaferProberClient client, IStateMachine proberState, IEventAggregator? eventAggregator = null)
        {
            ProberStateMachineLocation.LocationChanged += (s, e) =>
            {
                if (e == "i")
                {
                    Config.CameraPosition = "EQE";
                    logger.Info("Machine position EQE");
                }
                else if (e == "m")
                {
                    Config.CameraPosition = "AOI";
                    logger.Info("Machine position AOI");
                }
                else if (e == "a")
                {
                    Config.CameraPosition = "VAM";
                    logger.Info("Machine position VAM");
                }
                else if (e == "e")
                {
                    Config.CameraPosition = "e";
                    logger.Info("Machine position Out of position");
                }
                else if (ProberClientService.Instance.ProberClient.IsMoving)
                {
                    Config.CameraPosition = "Axismoving";
                   
                    logger.Info("Axis Moving");
                }
            };

            _mainVM = mainVM;
            _client = client;
            _proberState = proberState;
            _client.EventAggregator.Subscribe<ZAxisPosChangedEvent>(OnZAxisPosChanged);

            LiftAllCommand = new RelayCommand(_ => LiftAll(), _ => CanLiftAll);
            ToMainCameraCommand = new RelayCommand(_ => ToMainCamera(), _ => CanToMainCamera);
            ToAuxCameraCommand = new RelayCommand(_ => ToAuxCamera(), _ => CanToAuxCamera);
            ToIntegratingSphereCommand = new RelayCommand(_ => ToIntegratingSphere(), _ => CanToIntegratingSphere);
            StartAutoTestCommand = new RelayCommand(_ => StartAutoTest(), _ => CanStartAutoTest);
            ContinuAutoTestCommand = new RelayCommand(_ => ContinuAutoTest(), _ => CanContinuAutoTest);
            StopAutoTestCommand = new RelayCommand(_ => StopAutoTest(), _ => CanStopAutoTest);
            PauseAutoTestCommand = new RelayCommand(_ => PauseAutoTest(), _ => CanPauseAutoTest);
            CloseTestCommand = new RelayCommand(a => CloseTest(), a => CanPauseAutoTest);
        }

        public void CloseTest()
        {
            _mainVM.PauseAutoFlow();
            _proberState.CurrentState = ProberState.WaferLoaded;
        }

        private void PauseAutoTest()
        {
            _mainVM.PauseAutoFlow();
        }

        private void StopAutoTest()
        {
            _mainVM.StopAutoFlow();
            WaferProberData.RunDateTime = null;
        }

        private void ContinuAutoTest()
        {
            _mainVM.ContinuAutoFlow();
        }

        private void StartAutoTest()
        {
            WaferProberData.RunDateTime = DateTime.Now;
            _mainVM.StartAutoFlow();
        }

        private void OnZAxisPosChanged(ZAxisPosChangedEvent @event)
        {
        }

        /// <summary>
        /// 检查是否正在移动，如果是则弹出提示并返回true
        /// </summary>
        private bool CheckAndWarnIfMoving()
        {
            if (_client.IsMoving)
            {
                MessageBox.Show(
                    (string)Application.Current.FindResource("Axismoving"),
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 通知所有按钮重新评估 CanExecute（因为 IsMoving 状态变化了）
        /// </summary>
        private void RefreshAllCommands()
        {
            OnPropertyChanged(nameof(CanLiftAll));
            OnPropertyChanged(nameof(CanToMainCamera));
            OnPropertyChanged(nameof(CanToAuxCamera));
            OnPropertyChanged(nameof(CanToIntegratingSphere));
            OnPropertyChanged(nameof(CanStartAutoTest));
            OnPropertyChanged(nameof(CanContinuAutoTest));
            OnPropertyChanged(nameof(CanStopAutoTest));
            OnPropertyChanged(nameof(CanPauseAutoTest));
            // 触发 CommandManager 重新查询所有命令的 CanExecute
            Application.Current.Dispatcher.Invoke(() =>
            {
                CommandManager.InvalidateRequerySuggested();
            });
        }

        private async void ToIntegratingSphere()
        {
            if (CheckAndWarnIfMoving()) return;

            logger.Info("Switch to EQE mode.");
            RefreshAllCommands(); // 按钮立即禁用

            // 发送 gi 指令（普通发送，不等待）
            //await _client.ZToIntegratingSphereAsync();
            // 发送 gc 并等待 $67# 到位确认
            bool arrived = await _client.SendMoveCommandAndWaitAsync("gi", 240);
            if (!arrived)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "EQE移动超时，未收到到位确认！", "超时警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _mainVM?.ToIntegratingSphere();
            logger.Info("EQE movement completed, checking device status");

            await _client?.SendCommandAsync("gc");

            logger.Info("Status query completed.");
            RefreshAllCommands(); // 恢复按钮
        }

        private async void ToAuxCamera()
        {
            if (CheckAndWarnIfMoving()) return;

            logger.Info("Switch to VAM mode.");
            RefreshAllCommands();

            //await _client.ZToAuxCameraAsync();

            bool arrived = await _client.SendMoveCommandAndWaitAsync("ga", 240);
            if (!arrived)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "VAM移动超时，未收到到位确认！", "超时警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _mainVM?.ToAuxCamera();
            logger.Info("VAM movement completed, checking device status");

            await _client?.SendCommandAsync("gc");

            logger.Info("Status query completed.");
            RefreshAllCommands();
        }

        private async void ToMainCamera()
        {
            if (CheckAndWarnIfMoving()) return;

            logger.Info("Switch to AOI mode.");
            RefreshAllCommands();

            //await _client.ZToMainCameraAsync();

            bool arrived = await _client.SendMoveCommandAndWaitAsync("gm", 240);
            if (!arrived)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "AOI移动超时，未收到到位确认！", "超时警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            _mainVM?.ToMainCamera();
            await _client?.SendCommandAsync("gc");

            logger.Info("Status query completed.");
            RefreshAllCommands();
        }

        private async void LiftAll()
        {
            if (CheckAndWarnIfMoving()) return;

            logger.Info("Raise the equipment");
            RefreshAllCommands();

            //await _client.ZAllUpAsync();
            bool arrived = await _client.SendMoveCommandAndWaitAsync("gu", 240);
            if (!arrived)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),"LiftAll移动超时，未收到到位确认！", "超时警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await _client?.SendCommandAsync("gc");

            logger.Info("Status query completed.");
            RefreshAllCommands();
        }

        public void FireUI()
        {
            this.OnPropertyChanged(nameof(ContinuAutoTestCommand));
            this.OnPropertyChanged(nameof(PauseAutoTestCommand));
            this.OnPropertyChanged(nameof(CanPauseAutoTest));
            this.OnPropertyChanged(nameof(CanContinuAutoTest));
        }
    }
}