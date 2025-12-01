using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.Restful.DTO;
using CVWaferProber.Utils;
using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl.ViewModels;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace CVWaferProber.Services
{

    public class IVLService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(IVLService));

        public string ProberId { get; set; }
        public bool IsIVLCameraEnabled { get; set; }
        //
        private RCRestService rcService;
        private CVSpectrumViewModel CustomIVLVM { get; set; }

        public event EventHandler TestingCompleted;
        public IVLService(CVSpectrumViewModel customIVLVM, RCRestService rcModel)
        {
            this.CustomIVLVM = customIVLVM;
            this.ProberId = string.Empty;
            this.rcService = rcModel;
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
            Task task = RunIVLFlowAsync(_selectedWPFlow.Name, dieViewModel);
        }

        private string BuildFlowSN(DieViewModel dieViewModel, string timestamp)
        {
            if (string.IsNullOrEmpty(ProberId)) return string.Format("{1}[{3},{4}]", ProberId, timestamp, Snowflake.Instance.NextSeqId(), dieViewModel.MapY, dieViewModel.MapX);
            else return string.Format("{0}_{1}[{3},{4}]", ProberId, timestamp, Snowflake.Instance.NextSeqId(), dieViewModel.MapY, dieViewModel.MapX);
        }

        private async Task RunIVLFlowAsync(string fname, DieViewModel dieViewModel)
        {
            try
            {
                Task<RespDataBaseFlowResultDTO> resp = AsyncRunIVLFlow(fname, dieViewModel.SerialNumber);
                await resp;
                if (resp.Result.IsSuccess)
                {
                    IVLResultDisplay(dieViewModel);
                    dieViewModel.ChangeStatus(ChipStatus.IVL_COMPLETED, true);
                }
                else
                {
                    dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                }
            }
            catch (OperationCanceledException)
            {
                // 处理取消操作
                if (logger.IsDebugEnabled) logger.Debug("IVL Flow execution was cancelled.");
            }
            catch (Exception ex)
            {
                dieViewModel.ChangeStatus(ChipStatus.FAILED, true);
                // 处理其他异常
                if (logger.IsDebugEnabled) logger.Debug($"IVL Flow execution failed: {ex.Message}");
                throw;
            }
            finally
            {
                EndTesting();
            }
        }
        private void EndTesting()
        {
            TestingCompleted?.Invoke(this, EventArgs.Empty);
        }


        private async Task<RespDataBaseFlowResultDTO> AsyncRunIVLFlow(string fname, string sn)
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // 启动流程
            rcService.RcRunFlowByName(fname, sn);

            // 异步轮询结果，避免阻塞UI线程
            return await PollFlowResultWithRxAsync(sn, cancellationToken);
        }
        private async Task<RespDataBaseFlowResultDTO> PollFlowResultWithRxAsync(string sn, CancellationToken cancellationToken)
        {
            return await Observable.Interval(TimeSpan.FromSeconds(1))
                 .Select(_ => rcService.RcGetFlowResult_AOI(sn))
                 .Where(resp => resp != null)
                 .FirstAsync(resp => (resp.IsSuccess && resp.Data.IsFinished) || !resp.IsSuccess)
                 .Select(resp =>
                 {
                     if (!resp.IsSuccess) throw new InvalidOperationException($"Flow execution failed: {resp.Message}");
                     return resp.Data;
                 })
                 .ToTask(cancellationToken);
        }

        public void IVLResultDisplay(DieViewModel dieViewModel)
        {
            CustomIVLVM.ClearResult();
            CustomIVLVM.LoadData(dieViewModel.SerialNumber, dieViewModel.IsIVLCameraEnabled);
        }
    }
}
