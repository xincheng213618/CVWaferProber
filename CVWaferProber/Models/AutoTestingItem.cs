using CVWaferProber.ViewModels;

namespace CVWaferProber.Models
{
    public class AutoTestingItem
    {
        public List<DieViewModel> TestingDieVMList { get; internal set; }
        public WPFlowViewModel? CurSelectedWPFlow { get; internal set; }
        public int CurTestingIndex { get; internal set; }

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
            if (IsEnd) next = null;
            else next = TestingDieVMList[CurTestingIndex++];

            return (pre, next);
        }
        /// <summary>
        /// 回滚到上一个测试的Die
        /// </summary>
        /// <returns>回滚后的当前DieViewModel，如果无法回滚则返回null</returns>
        public DieViewModel? RollbackToPrevious()
        {
            CurTestingIndex--;
            return TestingDieVMList[CurTestingIndex];
        }
        public DieViewModel? GetCurrentDieVM()
        {
            //if (IsEnd) return null;
            if (CurTestingIndex > 0) return TestingDieVMList[CurTestingIndex - 1];
            else return null;
        }
    }
}
