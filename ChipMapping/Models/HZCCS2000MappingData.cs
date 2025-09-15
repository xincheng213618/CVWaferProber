using Microsoft.VisualBasic.FileIO;
using System.IO;
using System.Reflection;
using System.Text;

namespace ChipMapping.Models
{
    public class HZCCS2000MappingData
    {
        public string OperatorName { get; set; } // offset 0, 20 bytes
        public string DeviceName { get; set; }   // offset 20, 16 bytes
        public ushort WaferSize { get; set; }    // offset 36, 2 bytes (B means byte? but 2 bytes so ushort)
        public ushort MachineNo { get; set; }    // offset 38, 2 bytes
        public uint IndexSizeX { get; set; }     // offset 40, 4 bytes
        public uint IndexSizeY { get; set; }     // offset 44, 4 bytes
        public ushort OrientationFlatDirection { get; set; } // offset 48, 2 bytes
        public byte FinalEditingMachineType { get; set; } // offset 50, 1 byte
        public byte MapVersion { get; set; }     // offset 51, 1 byte
        public ushort MapDataAreaRowSize { get; set; } // offset 52, 2 bytes
        public ushort MapDataAreaLineSize { get; set; } // offset 54, 2 bytes
        public uint MapDataForm { get; set; }    // offset 56, 4 bytes
        public string WaferId { get; set; }      // offset 60, 21 bytes
        public byte NumberOfProbing { get; set; } // offset 81, 1 byte
        public string LotNo { get; set; }        // offset 82, 18 bytes
        public ushort CassetteNo { get; set; }   // offset 100, 2 bytes
        public ushort SlotNo { get; set; }       // offset 102, 2 bytes
        public byte XCoordinatesIncreaseDirection { get; set; } // offset 104, 1 byte
        public byte YCoordinatesIncreaseDirection { get; set; } // offset 105, 1 byte
        public byte ReferenceDieSettingProcedures { get; set; } // offset 106, 1 byte
        public byte Reserved { get; set; }       // offset 107, 1 byte
        /// <summary>
        /// 
        /// </summary>
        public int TargetDiePositionX { get; set; }
        public int TargetDiePositionY { get; set; }
        public int ReferenceDieCoordinatorX { get; set; }
        public int ReferenceDieCoordinatorY { get; set; }
        public int ProbingStartPosition { get; set; }
        public int ProbingDirection { get; set; }
        public int DistanceXToWaferCenterDieOrigin { get; set; }
        public int DistanceYToWaferCenterDieOrigin { get; set; }
        public int CoordinatorXOfWaferCenterDie { get; set; }
        public int CoordinatorYOfWaferCenterDie { get; set; }
        public int FirstDieCoordinatorX { get; set; }
        public int FirstDieCoordinatorY { get; set; }
        public TimeData StartTime { get; set; }
        public TimeData EndTime { get; set; }
        public TimeData LoadingTime { get; set; }
        public TimeData UnLoadingTime { get; set; }

        // Machine No. Special Characters
        public string MachineNoSC { get; set; }

        public ResultData resultData { get; set; }
        // Test Die Information Address
        public uint TestDieInfoAddress { get; set; }

        // Number of line category data
        public uint NumberOfLineCategoryData { get; set; }

        // Line category address
        public uint LineCategoryAddress { get; set; }
        public ExtendedMapInformation ExMapInfo { get; set; }
    }
    public class ExtendedMapInformation
    {
        public short MapFileConfiguration { get; set; }
        public short MaxMultiSite { get; set; }
        public short MaxCategories { get; set; }
        public short Reserved { get; set; }
    }
    public class ResultData
    {
        // Testing Result - Testing End Information
        public byte TestingEndInfo { get; set; }

        // (Reserved)
        public byte Reserved { get; set; }

        // Total tested dice
        public ushort TotalTestedDice { get; set; }

        // Total pass dice
        public ushort TotalPassDice { get; set; }

        // Total fail dice
        public ushort TotalFailDice { get; set; }
    }
    public class TimeData
    {
        public string Year { get; set; }
        public string Month { get; set; }
        public string Day { get; set; }
        public string Hour { get; set; }
        public string Minute { get; set; }
        public ushort Reserved { get; set; }
    }

    public class HZCCS2000MappingDataTool
    {
        public static bool LoadMapping(string filePath, ref HZCCS2000MappingData data)
        {
            bool result = false;
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader br = new BinaryReader(fs, Encoding.ASCII)) // 使用ASCII编码读取字符串
                {
                    // 读取Operator Name (offset 0, 20 bytes)
                    data.OperatorName = Encoding.ASCII.GetString(br.ReadBytes(20)).TrimEnd('\0', ' ');
                    // 读取Device Name (offset 20, 16 bytes)
                    data.DeviceName = Encoding.ASCII.GetString(br.ReadBytes(16)).TrimEnd('\0', ' ');
                    // 跳过偏移量36之前的未定义区域（从20+16=36，所以已经到36了）
                    // 实际上，读取完DeviceName后，位置已经在36了，所以直接读取WaferSize
                    data.WaferSize = br.ReadUInt16(); // offset 36, 2 bytes

                    data.MachineNo = br.ReadUInt16(); // offset 38, 2 bytes

                    data.IndexSizeX = br.ReadUInt32(); // offset 40, 4 bytes
                    data.IndexSizeY = br.ReadUInt32(); // offset 44, 4 bytes

                    data.OrientationFlatDirection = br.ReadUInt16(); // offset 48, 2 bytes

                    data.FinalEditingMachineType = br.ReadByte(); // offset 50, 1 byte
                    data.MapVersion = br.ReadByte(); // offset 51, 1 byte

                    data.MapDataAreaRowSize = br.ReadUInt16(); // offset 52, 2 bytes
                    data.MapDataAreaLineSize = br.ReadUInt16(); // offset 54, 2 bytes

                    data.MapDataForm = br.ReadUInt32(); // offset 56, 4 bytes

                    // 读取Wafer ID (offset 60, 21 bytes)
                    data.WaferId = Encoding.ASCII.GetString(br.ReadBytes(21)).TrimEnd('\0', ' ');

                    // 读取NumberOfProbing (offset 81, 1 byte) - 需要跳过60+21=81，所以正好
                    data.NumberOfProbing = br.ReadByte();

                    // 读取LotNo (offset 82, 18 bytes)
                    data.LotNo = Encoding.ASCII.GetString(br.ReadBytes(18)).TrimEnd('\0', ' ');

                    // 读取CassetteNo (offset 100, 2 bytes) - 82+18=100，所以正好
                    data.CassetteNo = br.ReadUInt16();

                    data.SlotNo = br.ReadUInt16(); // offset 102, 2 bytes

                    data.XCoordinatesIncreaseDirection = br.ReadByte(); // offset 104, 1 byte
                    data.YCoordinatesIncreaseDirection = br.ReadByte(); // offset 105, 1 byte
                    data.ReferenceDieSettingProcedures = br.ReadByte(); // offset 106, 1 byte
                    data.Reserved = br.ReadByte(); // offset 107, 1 byte

                    // TargetDiePositionX: Offset 108, Length 4, Type B (int)
                    fs.Seek(108, SeekOrigin.Begin);
                    data.TargetDiePositionX = br.ReadInt32();

                    // TargetDiePositionY: Offset 112, Length 4, Type B (int)
                    fs.Seek(112, SeekOrigin.Begin);
                    data.TargetDiePositionY = br.ReadInt32();

                    // ReferenceDieCoordinatorX: Offset 116, Length 2, Type B (int)
                    fs.Seek(116, SeekOrigin.Begin);
                    data.ReferenceDieCoordinatorX = br.ReadInt16();

                    // ReferenceDieCoordinatorY: Offset 118, Length 2, Type B (int)
                    fs.Seek(118, SeekOrigin.Begin);
                    data.ReferenceDieCoordinatorY = br.ReadInt16();

                    // ProbingStartPosition: Offset 120, Length 1, Type B (int)
                    fs.Seek(120, SeekOrigin.Begin);
                    data.ProbingStartPosition = br.ReadByte();

                    // ProbingDirection: Offset 121, Length 1, Type B (int)
                    fs.Seek(121, SeekOrigin.Begin);
                    data.ProbingDirection = br.ReadByte();

                    // DistanceXToWaferCenterDieOrigin: Offset 124, Length 4, Type B (int)
                    fs.Seek(124, SeekOrigin.Begin);
                    data.DistanceXToWaferCenterDieOrigin = br.ReadInt32();

                    // DistanceYToWaferCenterDieOrigin: Offset 128, Length 4, Type B (int)
                    fs.Seek(128, SeekOrigin.Begin);
                    data.DistanceYToWaferCenterDieOrigin = br.ReadInt32();

                    // CoordinatorXOfWaferCenterDie: Offset 132, Length 4, Type B (int)
                    fs.Seek(132, SeekOrigin.Begin);
                    data.CoordinatorXOfWaferCenterDie = br.ReadInt32();

                    // CoordinatorYOfWaferCenterDie: Offset 136, Length 4, Type B (int)
                    fs.Seek(136, SeekOrigin.Begin);
                    data.CoordinatorYOfWaferCenterDie = br.ReadInt32();

                    // FirstDieCoordinatorX: Offset 140, Length 4, Type B (int)
                    fs.Seek(140, SeekOrigin.Begin);
                    data.FirstDieCoordinatorX = br.ReadInt32();

                    // FirstDieCoordinatorY: Offset 144, Length 4, Type B (int)
                    fs.Seek(144, SeekOrigin.Begin);
                    data.FirstDieCoordinatorY = br.ReadInt32();
                    TimeData startTime = new TimeData();
                    ReadTime(br, ref startTime);
                    data.StartTime = startTime;
                    TimeData endTime = new TimeData();
                    ReadTime(br, ref endTime);
                    data.EndTime = endTime;
                    TimeData loadingTime = new TimeData();
                    ReadTime(br, ref loadingTime);
                    data.LoadingTime = loadingTime;
                    TimeData unloadingTime = new TimeData();
                    ReadTime(br, ref unloadingTime);
                    data.UnLoadingTime = unloadingTime;

                    data.MachineNoSC = Encoding.ASCII.GetString(br.ReadBytes(12)).TrimEnd('\0', ' ');

                    ResultData dataR = new ResultData();
                    ReadResult(br,ref dataR);
                    data.resultData = dataR;

                    // Test Die Information Address
                    br.BaseStream.Seek(216, SeekOrigin.Begin);
                    data.TestDieInfoAddress = br.ReadUInt32();

                    // Number of line category data
                    br.BaseStream.Seek(220, SeekOrigin.Begin);
                    data.NumberOfLineCategoryData = br.ReadUInt32();

                    // Line category address
                    br.BaseStream.Seek(224, SeekOrigin.Begin);
                    data.LineCategoryAddress = br.ReadUInt32();

                    ExtendedMapInformation extendedMapInfo = new ExtendedMapInformation();
                    ReadExtendedMapInformation(br,ref extendedMapInfo);
                }
            }
            catch (Exception)
            {
                result = false;
            }
            return result;
        }

        private static void ReadExtendedMapInformation(BinaryReader br, ref ExtendedMapInformation extendedMapInfo)
        {

        }

        private static void ReadResult(BinaryReader br, ref ResultData data)
        {
            // Testing Result - Testing End Information
            //br.BaseStream.Seek(208, SeekOrigin.Begin);
            data.TestingEndInfo = br.ReadByte();

            // (Reserved)
            //br.BaseStream.Seek(209, SeekOrigin.Begin);
            data.Reserved = br.ReadByte();

            // Total tested dice
            //br.BaseStream.Seek(210, SeekOrigin.Begin);
            data.TotalTestedDice = br.ReadUInt16();

            // Total pass dice
            //br.BaseStream.Seek(212, SeekOrigin.Begin);
            data.TotalPassDice = br.ReadUInt16();

            // Total fail dice
            //br.BaseStream.Seek(214, SeekOrigin.Begin);
            data.TotalFailDice = br.ReadUInt16();

        }

        private static void ReadTime(BinaryReader br,ref TimeData data)
        {
            // Year: Offset 148, Length 2, Type C (int)
            //fs.Seek(148, SeekOrigin.Begin);
            data.Year = Encoding.ASCII.GetString(br.ReadBytes(2)).TrimEnd('\0', ' ');
            //data.Year = br.ReadInt16();

            // Month: Offset 150, Length 2, Type C (int)
            //fs.Seek(150, SeekOrigin.Begin);
            data.Month = Encoding.ASCII.GetString(br.ReadBytes(2)).TrimEnd('\0', ' ');

            // Day: Offset 152, Length 2, Type C (int)
            //fs.Seek(152, SeekOrigin.Begin);
            data.Day = Encoding.ASCII.GetString(br.ReadBytes(2)).TrimEnd('\0', ' ');

            // Hour: Offset 154, Length 2, Type C (int)
            //fs.Seek(154, SeekOrigin.Begin);
            data.Hour = Encoding.ASCII.GetString(br.ReadBytes(2)).TrimEnd('\0', ' ');

            // Minute: Offset 156, Length 2, Type C (int)
            //fs.Seek(156, SeekOrigin.Begin);
            data.Minute = Encoding.ASCII.GetString(br.ReadBytes(2)).TrimEnd('\0', ' ');
        }
    }
}
