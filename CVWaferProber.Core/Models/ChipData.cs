using CVWaferProber.Core.Models.Enums;
using System.Windows;

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
                ChipStatus.IVL_TESTING => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0)),
                ChipStatus.OK => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7C, 0xFC, 0)),
                ChipStatus.IVL_COMPLETED => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7C, 0xFC, 0)),
                ChipStatus.AOI_NG => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0, 0)),
                ChipStatus.FAILED => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0, 0)),
                ChipStatus.DW_NG => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xA5, 0)),
                ChipStatus.BLIND => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0x80, 0x80)),
                ChipStatus.CAL_NG => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0, 0x80)),
                ChipStatus.I2C_NG => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF)),
                ChipStatus.AOI_LINE_NG => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xC0, 0xCB)),
                ChipStatus.SKIP => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 127, 0)),
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243))
            };
        }
        // 新增反查方法
        public static ChipStatus GetStatusFromDisplay(string displayText, bool isChinese)
        {
            // 根据显示文本和语言反查枚举（示例逻辑，需匹配你的GetStatusDisplay实现）
            return displayText switch
            {
                "等待" or "Waiting" => ChipStatus.WAITING,
                "测试中" or "Testing" => ChipStatus.TESTING,
                "IVL完成" or "IVL Completed" => ChipStatus.IVL_COMPLETED,
                "OK" or "合格" => ChipStatus.OK,
                "NG" or "不合格" => ChipStatus.AOI_NG,
                _ => ChipStatus.WAITING // 默认值
            };
        }
        public static string GetStatusDisplay(ChipStatus status, bool isChinese)
        {
            if(isChinese)
            {
                return status switch
                {
                    ChipStatus.WAITING => (string)Application.Current.FindResource("StatusPanel.WAITING"),
                    ChipStatus.TESTING => (string)Application.Current.FindResource("StatusPanel.TESTING"),
                    ChipStatus.OK => (string)Application.Current.FindResource("StatusPanel.OK"),
                    ChipStatus.AOI_NG => (string)Application.Current.FindResource("StatusPanel.AOI_NG"),
                    ChipStatus.DW_NG => (string)Application.Current.FindResource("StatusPanel.DW_NG"),
                    ChipStatus.BLIND => (string)Application.Current.FindResource("StatusPanel.BLIND"),
                    ChipStatus.CAL_NG => (string)Application.Current.FindResource("StatusPanel.CAL_NG"),
                    ChipStatus.I2C_NG => (string)Application.Current.FindResource("StatusPanel.I2C_NG"),
                    ChipStatus.AOI_LINE_NG => (string)Application.Current.FindResource("StatusPanel.AOI_LINE_NG"),
                    ChipStatus.IVL_TESTING => (string)Application.Current.FindResource("StatusPanel.IVL_TESTING"),
                    ChipStatus.IVL_COMPLETED => (string)Application.Current.FindResource("StatusPanel.IVL_COMPLETED"),
                    ChipStatus.VAM_TESTING => (string)Application.Current.FindResource("StatusPanel.VAM_TESTING"),
                    ChipStatus.VAM_COMPLETED => (string)Application.Current.FindResource("StatusPanel.VAM_COMPLETED"),
                    ChipStatus.EQE_TESTING => (string)Application.Current.FindResource("StatusPanel.EQE_TESTING"),
                    ChipStatus.EQE_COMPLETED => (string)Application.Current.FindResource("StatusPanel.EQE_COMPLETED"),
                    ChipStatus.FAILED => (string)Application.Current.FindResource("StatusPanel.FAILED"),
                    ChipStatus.OVERTIME => (string)Application.Current.FindResource("StatusPanel.OVERTIME"),
                    ChipStatus.SKIP => (string)Application.Current.FindResource("StatusPanel.SKIP"),
                    _ => "Unknown"
                };
            }
            else
            {
                return status.ToString();
            }
          
        }

        public static ChipStatus GetStatusFromErrCode(string errCode)
        {
            ChipStatus result = ChipStatus.AOI_NG;
            switch (errCode)
            {
                case "MURA_E":
                    result = ChipStatus.AOI_NG;
                    break;
                case "PARTICLE_E":
                    result = ChipStatus.AOI_NG;
                    break;
                case "VH_LINE_E":
                    result = ChipStatus.AOI_LINE_NG;
                    break;
                default:
                    result = ChipStatus.AOI_NG;
                    break;
            }

            return result;
        }
    }
}