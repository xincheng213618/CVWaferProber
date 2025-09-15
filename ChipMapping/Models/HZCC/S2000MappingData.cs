using System.Windows.Media;

namespace ChipMapping.Models.HZCC
{
    public class S2000MappingData
    {
        // Wafer Testing Setup Data
        [BinaryField(0, 20, FieldType.String)]
        public string OperatorName { get; set; }

        [BinaryField(20, 16, FieldType.String)]
        public string DeviceName { get; set; }

        [BinaryField(36, 2, FieldType.UInt16)]
        public ushort WaferSize { get; set; }

        [BinaryField(38, 2, FieldType.UInt16)]
        public ushort MachineNo { get; set; }

        [BinaryField(40, 4, FieldType.UInt32)]
        public uint IndexSizeX { get; set; }

        [BinaryField(44, 4, FieldType.UInt32)]
        public uint IndexSizeY { get; set; }

        [BinaryField(48, 2, FieldType.UInt16)]
        public ushort OrientationFlatDirection { get; set; }

        [BinaryField(50, 1, FieldType.Byte)]
        public byte FinalEditingMachineType { get; set; }

        [BinaryField(51, 1, FieldType.Byte)]
        public MapVersion MapVersion { get; set; }

        [BinaryField(52, 2, FieldType.UInt16)]
        public ushort MapDataAreaRowSize { get; set; }

        [BinaryField(54, 2, FieldType.UInt16)]
        public ushort MapDataAreaLineSize { get; set; }

        // Wafer Specific Data / Wafer Probing Coordinate System Data
        [BinaryField(56, 4, FieldType.UInt32)]
        public uint GroupManagement { get; set; }

        [BinaryField(60, 21, FieldType.String)]
        public string WaferID { get; set; }

        [BinaryField(81, 1, FieldType.Byte)]
        public byte NumberOfProbing { get; set; }

        [BinaryField(82, 18, FieldType.String)]
        public string LotNo { get; set; }

        [BinaryField(100, 2, FieldType.UInt16)]
        public ushort CassetteNo { get; set; }

        [BinaryField(102, 2, FieldType.UInt16)]
        public ushort SlotNo { get; set; }

        [BinaryField(104, 1, FieldType.Byte)]
        public XIncreaseDirection XCoordinatesIncreaseDirection { get; set; }

        [BinaryField(105, 1, FieldType.Byte)]
        public YIncreaseDirection YCoordinatesIncreaseDirection { get; set; }

        [BinaryField(106, 1, FieldType.Byte)]
        public ReferenceDieSetting ReferenceDieSettingProcedures { get; set; }

        [BinaryField(107, 1, FieldType.Byte)]
        public byte Reserved107 { get; set; }

        // Wafer Probing Coordinate System Data (continued)
        [BinaryField(108, 4, FieldType.UInt32)]
        public uint TargetDiePositionX { get; set; }

        [BinaryField(112, 4, FieldType.UInt32)]
        public uint TargetDiePositionY { get; set; }

        [BinaryField(116, 2, FieldType.UInt16)]
        public ushort ReferenceDieCoordinatorX { get; set; }

        [BinaryField(118, 2, FieldType.UInt16)]
        public ushort ReferenceDieCoordinatorY { get; set; }

        [BinaryField(120, 1, FieldType.Byte)]
        public ProbingStartPosition ProbingStartPosition { get; set; }

        [BinaryField(121, 1, FieldType.Byte)]
        public ProbingDirection ProbingDirection { get; set; }

        [BinaryField(122, 2, FieldType.UInt16)]
        public ushort Reserved122 { get; set; }

        [BinaryField(124, 4, FieldType.UInt32)]
        public uint DistanceXToWaferCenterDieOrigin { get; set; }

        [BinaryField(128, 4, FieldType.UInt32)]
        public uint DistanceYToWaferCenterDieOrigin { get; set; }

        [BinaryField(132, 4, FieldType.UInt32)]
        public uint CoordinatorXOfWaferCenterDie { get; set; }

        [BinaryField(136, 4, FieldType.UInt32)]
        public uint CoordinatorYOfWaferCenterDie { get; set; }

        // Information Per Die
        [BinaryField(140, 4, FieldType.UInt32)]
        public uint FirstDieCoordinatorX { get; set; }

        [BinaryField(144, 4, FieldType.UInt32)]
        public uint FirstDieCoordinatorY { get; set; }

        // Wafer Testing Start Time Data
        [BinaryField(148, 2, FieldType.String)]
        public string StartYear { get; set; }

        [BinaryField(150, 2, FieldType.String)]
        public string StartMonth { get; set; }

        [BinaryField(152, 2, FieldType.String)]
        public string StartDay { get; set; }

        [BinaryField(154, 2, FieldType.String)]
        public string StartHour { get; set; }

        [BinaryField(156, 2, FieldType.String)]
        public string StartMinute { get; set; }

        [BinaryField(158, 2, FieldType.UInt16)]
        public ushort Reserved158 { get; set; }

        // Wafer Testing End Time Data
        [BinaryField(160, 2, FieldType.String)]
        public string EndYear { get; set; }

        [BinaryField(162, 2, FieldType.String)]
        public string EndMonth { get; set; }

        [BinaryField(164, 2, FieldType.String)]
        public string EndDay { get; set; }

        [BinaryField(166, 2, FieldType.String)]
        public string EndHour { get; set; }

        [BinaryField(168, 2, FieldType.String)]
        public string EndMinute { get; set; }

        [BinaryField(170, 2, FieldType.UInt16)]
        public ushort Reserved170 { get; set; }

        // Wafer Loading Time Data
        [BinaryField(172, 2, FieldType.String)]
        public string LoadEndYear { get; set; }

        [BinaryField(174, 2, FieldType.String)]
        public string LoadEndMonth { get; set; }

        [BinaryField(176, 2, FieldType.String)]
        public string LoadEndDay { get; set; }

        [BinaryField(178, 2, FieldType.String)]
        public string LoadEndHour { get; set; }

        [BinaryField(180, 2, FieldType.String)]
        public string LoadEndMinute { get; set; }

        [BinaryField(182, 2, FieldType.UInt16)]
        public ushort Reserved182 { get; set; }

        // Wafer Unloading Time Data
        [BinaryField(184, 2, FieldType.String)]
        public string UnloadStartYear { get; set; }

        [BinaryField(186, 2, FieldType.String)]
        public string UnloadStartMonth { get; set; }

        [BinaryField(188, 2, FieldType.String)]
        public string UnloadStartDay { get; set; }

        [BinaryField(190, 2, FieldType.String)]
        public string UnloadStartHour { get; set; }

        [BinaryField(192, 2, FieldType.String)]
        public string UnloadStartMinute { get; set; }

        [BinaryField(194, 2, FieldType.UInt16)]
        public ushort Reserved194 { get; set; }

        // Machine No. Special Characters
        [BinaryField(196, 12, FieldType.String)]
        public string MachineNoSpecialCharacters { get; set; }

        // Testing Result
        [BinaryField(208, 1, FieldType.Byte)]
        public TestingEndInformation TestingEndInformation { get; set; }

        [BinaryField(209, 1, FieldType.Byte)]
        public byte Reserved209 { get; set; }

        [BinaryField(210, 2, FieldType.UInt16)]
        public ushort TotalTestedDice { get; set; }

        [BinaryField(212, 2, FieldType.UInt16)]
        public ushort TotalPassDice { get; set; }

        [BinaryField(214, 2, FieldType.UInt16)]
        public ushort TotalFailDice { get; set; }

        [BinaryField(216, 4, FieldType.Pointer)]
        public uint TestDieInformationAddress { get; set; }

        [BinaryField(220, 4, FieldType.UInt32)]
        public uint NumberOfLineCategoryData { get; set; }

        [BinaryField(224, 4, FieldType.UInt32)]
        public uint LineCategoryAddress { get; set; }

        // 扩展字段（根据Map Version决定是否有效）
        public ExtendedMapInfo ExtendedInfo { get; set; }

        // Die测试结果数组
        public DieTestResult[] DieTestResults { get; set; }

    }

    public class ExtendedMapInfo
    {
        // 扩展地图信息字段（根据实际需要定义）
        public uint MaxMultiSite { get; set; }
        public uint MaxCategories { get; set; }
        // 其他扩展字段...
    }
public class DieTestResult
{
    // 第一个字的字段（已存在）
    public DieTestStatus TestResult { get; set; }
    public bool IsMarked { get; set; }
    public bool FailMarkInspection { get; set; }
    public ReProbingResult ReProbingResult { get; set; }
    public bool NeedleMarkInspectionResult { get; set; }
    public ushort DieCoordinatorX { get; set; }
    
    // 第二个字的字段（已存在）
    public DieProperty DieProperty { get; set; }
    public bool IsRejectChip { get; set; }
    public bool NeedleMarkingInspectionExecution { get; set; }
    public bool IsSamplingDie { get; set; }
    public bool IsCoordinatorXNegative { get; set; }
    public bool IsCoordinatorYNegative { get; set; }
    public bool IsDummyData { get; set; }
    public ushort DieCoordinatorY { get; set; }
    
    // 第三个字的新字段
    public bool MeasurementFinishFlag { get; set; }
    public RejectChipFlag RejectChipFlag { get; set; }
    public byte TestExecutionSiteNo { get; set; }
    public BlockAreaJudgement BlockAreaJudgement { get; set; }
    public byte CategoryData { get; set; }
    public byte UserSpecialData { get; set; }
    
    // 计算后的值
    public byte ActualSiteNo { get; set; }
    public byte ActualCategoryData { get; set; }
    
    // 计算后的坐标值（考虑正负号）
    public short CalculatedCoordinatorX { get; set; }
    public short CalculatedCoordinatorY { get; set; }
}

public enum RejectChipFlag : byte
{
    None = 0,
    PeripheralProbingDie = 1,
    InkDie = 2,
    PartialPW = 3
}

public enum BlockAreaJudgement : byte
{
    None = 0,
    Block1 = 1,
    Block2 = 2,
    Block3 = 3
}

    public enum DieTestStatus : byte
    {
        NotTested = 0,
        PassDie = 1,
        Fail1Die = 2,
        Fail2Die = 3
    }

    public enum ReProbingResult : byte
    {
        NotReProbed = 0,
        PassedAtReProbing = 1,
        FailedAtReProbing = 2,
        PerformFail = 3
    }
    public enum DieProperty : byte
    {
        SkipDie = 0,
        ProbingDie = 1,
        CompulsoryMarkingDie = 2
    }
}
