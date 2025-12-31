using CVAVMControl;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Services
{
    public class VAMService : BaseSerivce
    {
        public VAMService(RCRestService rcService) : base(rcService)
        {
        }

        public CVVAMAnalyzer? VamAnalyzer { get; set; }

        protected override ChipStatus GetResultStatus(string serialNumber)
        {
            return ChipStatus.OK;
        }
        protected override ChipStatus FlowResultDisplay(DieViewModel dieViewModel)
        {
            string cieFileName = GetFlowResult(dieViewModel);
            VamAnalyzer?.ResultDisplay(cieFileName);
            return ChipStatus.OK;
        }

        private string GetFlowResult(DieViewModel dieViewModel)
        {
            return string.Empty;
        }

        public void StartTestingVAM(string timestamp, DieViewModel dieViewModel, WPFlowViewModel _selectedWPFlow)
        {
            Task task = RunFlowAsync(_selectedWPFlow, dieViewModel);
        }
    }
}
