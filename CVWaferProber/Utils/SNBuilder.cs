using CVWaferProber.ViewModels;

namespace CVWaferProber.Utils
{
    public static class SNBuilder
    {
        public static string Build(string ProberId, string timestamp, DieViewModel dieViewModel)
        {
            // 修复：原格式化字符串的占位符索引错误（使用了3/4但参数只有0-4）
            if (string.IsNullOrEmpty(ProberId))
                return string.Format("{1}[{2}]", ProberId, timestamp, dieViewModel.MapAxisToString());
            else
                return string.Format("{0}_{1}[{2}]", ProberId, timestamp, dieViewModel.MapAxisToString());

        }
    }
}
