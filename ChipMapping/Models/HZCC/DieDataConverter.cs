using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
                    DieTestStatus.NotTested =>(string)Application.Current.FindResource("StatusPanel.WAITING"),
                    DieTestStatus.PassDie => (string)Application.Current.FindResource("DieTestStatus.PassDie"),
                    DieTestStatus.Fail1Die => (string)Application.Current.FindResource("DieTestStatus.Fail1Die"),
                    DieTestStatus.Fail2Die => (string)Application.Current.FindResource("DieTestStatus.Fail2Die"),
                    _ => (string)Application.Current.FindResource("State.Unknown"),
                },
                MarkingStatus = dieResult.IsMarked ? (string)Application.Current.FindResource("Marked") : (string)Application.Current.FindResource("Unmarked"),
                InspectionStatus = dieResult.FailMarkInspection ? (string)Application.Current.FindResource("Checkfailed") : (string)Application.Current.FindResource("Checkpassed"),
                ReProbingStatus = dieResult.ReProbingResult switch
                {
                    ReProbingResult.NotReProbed => (string)Application.Current.FindResource("Notretested"),
                    ReProbingResult.PassedAtReProbing => (string)Application.Current.FindResource("RetestPassed"),
                    ReProbingResult.FailedAtReProbing => (string)Application.Current.FindResource("Retestfailed"),
                    ReProbingResult.PerformFail => (string)Application.Current.FindResource("Specialfailure"),
                    _ => (string)Application.Current.FindResource("State.Unknown"),
                },
                NeedleMarkStatus = dieResult.NeedleMarkInspectionResult ? (string)Application.Current.FindResource("MarkNG") : (string)Application.Current.FindResource("MarkOK"),

                // 第二个字的字段
                DieProperty = dieResult.DieProperty switch
                {
                    DieProperty.SkipDie => (string)Application.Current.FindResource("SkipDie"),
                    DieProperty.ProbingDie => (string)Application.Current.FindResource("TestDie") ,
                    DieProperty.CompulsoryMarkingDie => (string)Application.Current.FindResource("MandatorymarkingDie"),
                    _ => (string)Application.Current.FindResource("State.Unknown"),
                },
                IsRejectChip = dieResult.IsRejectChip ? (string)Application.Current.FindResource("Scrapchip") : (string)Application.Current.FindResource("Normalchip"),
                NeedleMarkingExecution = dieResult.NeedleMarkingInspectionExecution ? (string)Application.Current.FindResource("Performneedleinspection") : (string)Application.Current.FindResource("Donotperformneedleinspection"),
                IsSamplingDie = dieResult.IsSamplingDie ? (string)Application.Current.FindResource("SamplingDie") : (string)Application.Current.FindResource("Non-sampledDie"),
                CoordinatorXSign = dieResult.IsCoordinatorXNegative ? "-" : "+",
                CoordinatorYSign = dieResult.IsCoordinatorYNegative ? "-" : "+",
                IsDummyData = dieResult.IsDummyData ? (string)Application.Current.FindResource("VirtualData") : (string)Application.Current.FindResource("Realdata"),

                // 第三个字的新字段
                MeasurementFinishFlag = dieResult.MeasurementFinishFlag ? (string)Application.Current.FindResource("Tested") : (string)Application.Current.FindResource("Nottested"),
                RejectChipFlag = dieResult.RejectChipFlag switch
                {
                    RejectChipFlag.None => "null",
                    RejectChipFlag.PeripheralProbingDie =>(string)Application.Current.FindResource("OutTestDie"),
                    RejectChipFlag.InkDie => (string)Application.Current.FindResource("InkDie"),
                    RejectChipFlag.PartialPW => (string)Application.Current.FindResource("PartP/W"),
                   
                    _ => (string)Application.Current.FindResource("State.Unknown"),
                },
                TestExecutionSiteNo = dieResult.TestExecutionSiteNo.ToString(),
                ActualSiteNo = dieResult.ActualSiteNo.ToString(),
                BlockAreaJudgement = dieResult.BlockAreaJudgement switch
                {
                    BlockAreaJudgement.None => "null",
                    BlockAreaJudgement.Block1 => (string)Application.Current.FindResource("Block1"),
                  
                    BlockAreaJudgement.Block2 => (string)Application.Current.FindResource("Block2"),
                
                    BlockAreaJudgement.Block3 => (string)Application.Current.FindResource("Block3"),
                  
                    _ => (string)Application.Current.FindResource("State.Unknown"),
                },
                CategoryData = dieResult.CategoryData.ToString(),
                ActualCategoryData = dieResult.ActualCategoryData.ToString(),
                UserSpecialData = dieResult.UserSpecialData > 0 ? dieResult.UserSpecialData.ToString("X2") : "null",

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
