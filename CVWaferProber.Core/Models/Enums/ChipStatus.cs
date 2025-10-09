using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Models.Enums
{
    public enum ChipStatus
    {
        // 0: 未检测的mapping           蓝
        WAITING = 0,
        // 1: 正在检测的mapping         黄
        TESTING,
        // 2: 检测OK的mapping           绿
        OK,
        // 3: AOI外观检测NG的mapping    红
        AOI_NG,
        // 4: 定位NG的mapping           橙
        DW_NG,
        // 5: 完全不亮的mapping         灰
        BLIND,
        // 6: 提取失败的mapping         紫
        CAL_NG,
        // 7: I2C状态异常的mapping      白
        I2C_NG,
        // 8: 线缺陷检测NG的mapping     橄榄
        AOI_LINE_NG,
        IVL_TESTING,
        IVL_COMPLETED,
        FAILED,
    }

    enum JY_ERROR
    {
        // 0: 未检测的mapping           蓝
        JY_WAITING = 0,
        // 1: 正在检测的mapping         黄
        JY_TESTING = 1,
        // 2: 检测OK的mapping           绿
        JY_OK = 2,
        // 3: AOI外观检测NG的mapping    红
        JY_AOI_NG = 3,
        // 4: 定位NG的mapping           橙
        JY_DW_NG = 4,
        // 5: 完全不亮的mapping         灰
        JY_BLIND = 5,
        // 6: 提取失败的mapping         紫
        JY_CAL_NG = 6,
        // 7: I2C状态异常的mapping      白
        JY_I2C_NG = 7,
        // 8: 线缺陷检测NG的mapping     橄榄
        JY_AOI_LINE_NG = 8,
    };
}
