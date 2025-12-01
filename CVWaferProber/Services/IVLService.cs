using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl.ViewModels;

namespace CVWaferProber.Services
{

    public class IVLService : BaseSerivce
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(IVLService));

        public bool IsIVLCameraEnabled { get; set; }
        //
        private CVSpectrumViewModel CustomIVLVM { get; set; }

        public IVLService(CVSpectrumViewModel customIVLVM, RCRestService rcService) : base(rcService)
        {
            this.CustomIVLVM = customIVLVM;
        }

        public void StartTestingIVL(string timestamp,DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow)
        {
            string sn = BuildFlowSN(dieViewModel, timestamp);
            dieViewModel.SerialNumber = sn;
            dieViewModel.ChangeStatus(ChipStatus.IVL_TESTING);
            CustomIVLVM.ClearResult();
            IsIVLCameraEnabled = _selectedWPFlow.FlowType == CVWaferProberFlowType.IVL_Camera;
            dieViewModel.IsIVLCameraEnabled = IsIVLCameraEnabled;
            if (IsIVLCameraEnabled) CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.IVLCamera;
            else CustomIVLVM.SelectedTab = CVWPFSpectrometerCtrl.Models.TabType.Spectrum;
            //Task.Factory.StartNew(() => RunIVLFlowAsync(_selectedFlow.Id, sn));
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel);
        }

        public void IVLResultDisplay(DieViewModel dieViewModel)
        {
            CustomIVLVM.ClearResult();
            CustomIVLVM.LoadData(dieViewModel.SerialNumber, dieViewModel.IsIVLCameraEnabled);
        }

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.FAILED;
        }

        protected override void FlowResultDisplay(DieViewModel dieViewModel)
        {
            IVLResultDisplay(dieViewModel);
        }
    }
}
