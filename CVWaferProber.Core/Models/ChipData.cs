using CVWaferProber.Core.Models.Enums;

namespace CVWaferProber.Core.Models
{
    public class ChipData
    {
        public uint Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double RawX { get; set; }
        public double RawY { get; set; }
        public ChipStatus Status { get; set; }
        public double? DataValue { get; set; } = null;
        public int Row { get; set; }
        public int Column { get; set; }
    }

    /*
    public enum ChipStatus
    {
        // 0: 未检测的mapping           蓝
        WAITING = 0,
        // 1: 正在检测的mapping         黄
        TESTING,
        // 2: 检测OK的mapping           绿
        OK,
        // 3: AOI外观检测NG的mapping    红
        AOI_NG ,
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
    }
    */
    public static class ChipStatusTool
    {
        public static System.Windows.Media.SolidColorBrush GetStatusBrush(ChipStatus? status)
        {
            return status switch
            {
                ChipStatus.WAITING => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0xFF)),
                ChipStatus.TESTING => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0)),
                ChipStatus.OK => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7C, 0xFC, 0)),
                ChipStatus.AOI_NG => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0, 0)),
                ChipStatus.DW_NG => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xA5, 0)),
                ChipStatus.BLIND => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0x80, 0x80)),
                ChipStatus.CAL_NG => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0, 0x80)),
                ChipStatus.I2C_NG => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF)),
                ChipStatus.AOI_LINE_NG => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xC0, 0xCB)),
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243))
            };
        }

        public static string GetStatusDisplay(ChipStatus status)
        {
            return status switch
            {
                ChipStatus.WAITING => "未检测",
                ChipStatus.TESTING => "正在检测",
                ChipStatus.OK => "检测合格",
                ChipStatus.AOI_NG => "AOI外观检测NG",
                ChipStatus.DW_NG => "定位NG",
                ChipStatus.BLIND => "完全不亮",
                ChipStatus.CAL_NG => "提取失败",
                ChipStatus.I2C_NG => "I2C状态异常",
                ChipStatus.AOI_LINE_NG => "线缺陷检测NG",
                _ => "未知"
            };
        }
    }
}