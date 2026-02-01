using CVWaferProber.ViewModels;
using WaferComm.StateMachine;

namespace CVWaferProber.Models
{
    public class AutoTestingItem
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AutoTestingItem));
        public List<DieViewModel> TestingDieVMList { get; private set; }
        public WPFlowViewModel? CurSelectedWPFlow { get; private set; }
        public int CurTestingIndex { get; private set; }

        public AutoTestingItem(List<DieViewModel> testingDieVMList, WPFlowViewModel? curSelectedWPFlow)
        {
            TestingDieVMList = testingDieVMList;
            CurSelectedWPFlow = curSelectedWPFlow;
            CurTestingIndex = 0;
        }

        public bool IsEnd { get => TestingDieVMList.Count == CurTestingIndex; }

        public bool HasNext { get => TestingDieVMList.Count > CurTestingIndex; }

        public bool IsPaused { get; set; }

        public (DieViewModel? pre, DieViewModel? next) GetNextDieVM()
        {
            DieViewModel? pre = null;
            DieViewModel? next = null;
            if (CurTestingIndex > 0) pre = TestingDieVMList[CurTestingIndex - 1];
            if (IsEnd)
            {
                next = null;
                if (logger.IsInfoEnabled) logger.Info("Get NextDie => IsEnded");
            }
            else
            {
                next = TestingDieVMList[CurTestingIndex];
                if (logger.IsInfoEnabled) logger.InfoFormat("Get NextDie => {0}/{1}/{2}", CurTestingIndex, next.MapAxisToString(), next.Status.ToString());
                CurTestingIndex++;
            }
            return (pre, next);
        }
        /// <summary>
        /// 回滚到上一个测试的Die
        /// </summary>
        /// <returns>回滚后的当前DieViewModel，如果无法回滚则返回null</returns>
        public DieViewModel? RollbackToPrevious()
        {
            if (CurTestingIndex > 0)
            {
                CurTestingIndex--;
                return TestingDieVMList[CurTestingIndex];
            }

            return null;
        }
        public DieViewModel? GetCurrentDieVM()
        {
            if (CurTestingIndex > 0) return TestingDieVMList[CurTestingIndex - 1];
            else return null;
        }

        public void UpdateCurDieMotionAxis(ProberMotionAxisStatus axis)
        {
            var cur = GetCurrentDieVM();
            if(cur == null) return;
            cur.UpdateMotionAxis(axis);
        }
    }
}
