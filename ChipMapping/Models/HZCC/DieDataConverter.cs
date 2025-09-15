using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChipMapping.Models.HZCC
{
    public class DieDataConverter
    {
        public static DieViewModel ConvertToViewModel(DieTestResult dieResult, int index)
        {
            return new DieViewModel
            {
                Index = index + 1,
                TestResult = dieResult.TestResult switch
                {
                    DieTestStatus.NotTested => "未测试",
                    DieTestStatus.PassDie => "通过",
                    DieTestStatus.Fail1Die => "失败1",
                    DieTestStatus.Fail2Die => "失败2",
                    _ => "未知"
                },
                MarkingStatus = dieResult.IsMarked ? "已标记" : "未标记",
                InspectionStatus = dieResult.FailMarkInspection ? "检查失败" : "检查通过",
                ReProbingStatus = dieResult.ReProbingResult switch
                {
                    ReProbingResult.NotReProbed => "未重测",
                    ReProbingResult.PassedAtReProbing => "重测通过",
                    ReProbingResult.FailedAtReProbing => "重测失败",
                    ReProbingResult.PerformFail => "特殊失败",
                    _ => "未知"
                },
                NeedleMarkStatus = dieResult.NeedleMarkInspectionResult ? "针标记NG" : "针标记OK",

                // 第二个字的字段
                DieProperty = dieResult.DieProperty switch
                {
                    DieProperty.SkipDie => "跳过Die",
                    DieProperty.ProbingDie => "测试Die",
                    DieProperty.CompulsoryMarkingDie => "强制标记Die",
                    _ => "未知"
                },
                IsRejectChip = dieResult.IsRejectChip ? "废品芯片" : "正常芯片",
                NeedleMarkingExecution = dieResult.NeedleMarkingInspectionExecution ? "执行针检" : "不执行针检",
                IsSamplingDie = dieResult.IsSamplingDie ? "采样Die" : "非采样Die",
                CoordinatorXSign = dieResult.IsCoordinatorXNegative ? "-" : "+",
                CoordinatorYSign = dieResult.IsCoordinatorYNegative ? "-" : "+",
                IsDummyData = dieResult.IsDummyData ? "虚拟数据" : "真实数据",

                // 第三个字的新字段
                MeasurementFinishFlag = dieResult.MeasurementFinishFlag ? "已测试" : "未测试",
                RejectChipFlag = dieResult.RejectChipFlag switch
                {
                    RejectChipFlag.None => "无",
                    RejectChipFlag.PeripheralProbingDie => "外围测试Die",
                    RejectChipFlag.InkDie => "墨水Die",
                    RejectChipFlag.PartialPW => "部分P/W",
                    _ => "未知"
                },
                TestExecutionSiteNo = dieResult.TestExecutionSiteNo.ToString(),
                ActualSiteNo = dieResult.ActualSiteNo.ToString(),
                BlockAreaJudgement = dieResult.BlockAreaJudgement switch
                {
                    BlockAreaJudgement.None => "无",
                    BlockAreaJudgement.Block1 => "区块1",
                    BlockAreaJudgement.Block2 => "区块2",
                    BlockAreaJudgement.Block3 => "区块3",
                    _ => "未知"
                },
                CategoryData = dieResult.CategoryData.ToString(),
                ActualCategoryData = dieResult.ActualCategoryData.ToString(),
                UserSpecialData = dieResult.UserSpecialData > 0 ? dieResult.UserSpecialData.ToString("X2") : "无",

                // 坐标信息
                RawCoordinatorX = dieResult.DieCoordinatorX,
                RawCoordinatorY = dieResult.DieCoordinatorY,
                CoordinatorX = dieResult.CalculatedCoordinatorX,
                CoordinatorY = dieResult.CalculatedCoordinatorY
            };
        }
    }

    public class DieViewModel
    {
        public int Index { get; set; }
        public string TestResult { get; set; }
        public string MarkingStatus { get; set; }
        public string InspectionStatus { get; set; }
        public string ReProbingStatus { get; set; }
        public string NeedleMarkStatus { get; set; }

        // 第二个字的字段
        public string DieProperty { get; set; }
        public string IsRejectChip { get; set; }
        public string NeedleMarkingExecution { get; set; }
        public string IsSamplingDie { get; set; }
        public string CoordinatorXSign { get; set; }
        public string CoordinatorYSign { get; set; }
        public string IsDummyData { get; set; }

        // 第三个字的新字段
        public string MeasurementFinishFlag { get; set; }
        public string RejectChipFlag { get; set; }
        public string TestExecutionSiteNo { get; set; }
        public string ActualSiteNo { get; set; }
        public string BlockAreaJudgement { get; set; }
        public string CategoryData { get; set; }
        public string ActualCategoryData { get; set; }
        public string UserSpecialData { get; set; }

        // 坐标信息
        public ushort RawCoordinatorX { get; set; }
        public ushort RawCoordinatorY { get; set; }
        public short CoordinatorX { get; set; }
        public short CoordinatorY { get; set; }
    }
}
