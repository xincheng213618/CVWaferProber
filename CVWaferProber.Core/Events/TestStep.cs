using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Events
{
    public enum TestStep
    {
        None = 0,        // 无状态
        Moving = 1,      // 机台移动到芯片位置
        Initializing = 2,// 测试初始化（参数配置/设备准备）
        Executing = 3,   // 测试执行中（核心流程，支持子进度）
        ParsingResult = 4,// 结果解析/数据处理
        Completed = 5,   // 测试完成
        Failed = 6       // 测试失败
    }
}
