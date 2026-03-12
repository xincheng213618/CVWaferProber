using ColorVision.UI;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Services;
using Org.BouncyCastle.Utilities.Collections;
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

        public string CameraPosition { get => _CameraPosition; set { _CameraPosition = value;OnPropertyChanged(); } }
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

        public bool CanLiftAll => _proberState.CurrentState == ProberState.WaferLoaded || _proberState.CurrentState == ProberState.Stoped;
        public bool CanToMainCamera => _proberState.CurrentState == ProberState.WaferLoaded ||  _proberState.CurrentState == ProberState.Stoped;
        public bool CanToAuxCamera => _proberState.CurrentState == ProberState.WaferLoaded || _proberState.CurrentState == ProberState.Stoped;
        public bool CanToIntegratingSphere => _proberState.CurrentState == ProberState.WaferLoaded ||  _proberState.CurrentState == ProberState.Stoped;

        //private bool _CanContinuAutoTest;
        public bool CanStartAutoTest => _proberState.CurrentState == ProberState.WaferLoaded ||  _proberState.CurrentState == ProberState.Stoped;
        public bool CanContinuAutoTest => _proberState.CurrentState == ProberState.Paused && _mainVM.IsNotProcessing;
        public bool CanStopAutoTest => _mainVM.IsNotProcessing &&   (_proberState.CurrentState == ProberState.Testing || _proberState.CurrentState == ProberState.WaferLoaded || _proberState.CurrentState == ProberState.Paused || _proberState.CurrentState == ProberState.Stoped);
        public bool CanPauseAutoTest => _proberState.CurrentState == ProberState.Testing;

        public ToolsBarViewModel(MainViewModel mainVM,IWaferProberClient client, IStateMachine proberState, IEventAggregator? eventAggregator = null)
        {
            ProberStateMachineLocation.LocationChanged += (s, e) =>
            {
                if (e == "i")
                {
                    Config.CameraPosition = "EQE";
                    logger.Info("机台位置 EQE");

                }
                else if (e == "m")
                {
                    Config.CameraPosition = "AOI";
                    logger.Info("机台位置 AOI");

                }
                else if (e == "a")
                {
                    Config.CameraPosition = "VAM";
                    logger.Info("机台位置 VAM");

                }
                else if (e == "e")
                {
                    Config.CameraPosition = "e";
                    logger.Info("机台位置 不在标准位置");
                }
            };

            _mainVM = mainVM;
            _client = client;
            _proberState = proberState;
            _client.EventAggregator.Subscribe<ZAxisPosChangedEvent>(OnZAxisPosChanged);

            LiftAllCommand = new RelayCommand(_ => LiftAll(),  _ => CanLiftAll);
            ToMainCameraCommand = new RelayCommand(  _ => ToMainCamera(), _ => CanToMainCamera);
            ToAuxCameraCommand = new RelayCommand(  _ => ToAuxCamera(),  _ => CanToAuxCamera);
            ToIntegratingSphereCommand = new RelayCommand(  _ => ToIntegratingSphere(),   _ => CanToIntegratingSphere);
            StartAutoTestCommand = new RelayCommand(  _ => StartAutoTest(), _ => CanStartAutoTest);
            ContinuAutoTestCommand = new RelayCommand(  _ => ContinuAutoTest(),  _ => CanContinuAutoTest);
            StopAutoTestCommand = new RelayCommand(  _ => StopAutoTest(),  _ => CanStopAutoTest);
            PauseAutoTestCommand = new RelayCommand( _ => PauseAutoTest(),  _ => CanPauseAutoTest);
            CloseTestCommand = new RelayCommand(a => CloseTest(), a=> CanPauseAutoTest);
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

        bool IsMove = false;

        private async void ToIntegratingSphere()
        {
            if (IsMove)
            {
                MessageBox.Show("机台正在移动");
                return;
            }
            IsMove = true;
            logger.Info("切换机台模式到EQE");

            await _client?.ZToIntegratingSphereAsync();
            _mainVM?.ToIntegratingSphere();
            logger.Info("EQE移动完成，查询设备状态");

            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(100);
                await _client?.SendCommandAsync("gc");
                logger.Info("状态查询完成");
                IsMove = false;
            });



        }

        private async void ToAuxCamera()
        {
            if (IsMove)
            {
                MessageBox.Show("机台正在移动");
                return;
            }
            IsMove = true;

            logger.Info("切换机台模式到VAM");

            await _client?.ZToAuxCameraAsync();
            _mainVM?.ToAuxCamera();
            logger.Info("VAM移动完成，查询设备状态");
            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(100);
                await _client?.SendCommandAsync("gc");
                logger.Info("状态查询完成");
                IsMove = false;
            });

        }

        private async void ToMainCamera()
        {
            if (IsMove)
            {
                MessageBox.Show("机台正在移动");
                return;
            }
            IsMove = true;

            logger.Info("切换机台模式到AOI");

            await _client?.ZToMainCameraAsync();
            _mainVM?.ToMainCamera();

            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(100);
                await _client?.SendCommandAsync("gc");
                logger.Info("状态查询完成");
                IsMove = false;
            });


        }

        private async void LiftAll()
        {
            if (IsMove)
            {
                MessageBox.Show("机台正在移动");
                return;
            }
            IsMove = true;
            logger.Info("抬起设备");

            await _client?.ZAllUpAsync();

            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(100);
                await _client?.SendCommandAsync("gc");
                logger.Info("状态查询完成");
                IsMove = false;
            });

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
