using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Models.Enums
{
    public enum TestStep
    {
        None = 0,          // 未开始（0%）
        Moving = 1,        // 机台移动到芯片位置（15%）
        Initializing = 2,  // 测试模块初始化（30%）
        Executing = 3,     // 测试指令执行中（50%~85%）
        ParsingResult = 4, // 结果解析/数据回写（85%）
        Completed = 5,     // 测试完成（100%）
        Failed = 6         // 测试失败（100%，特殊状态）
    }
}
