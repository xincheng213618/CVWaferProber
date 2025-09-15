using System;
using System.Diagnostics.PerformanceData;
using System.IO;
using System.Reflection;
using System.Text;

namespace ChipMapping.Models.HZCC
{
    public class S2000MappingDataReader
    {
        public static S2000MappingData Read(string filePath)
        {
            S2000MappingData data = new S2000MappingData();

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                PropertyInfo[] properties = typeof(S2000MappingData).GetProperties();

                foreach (PropertyInfo property in properties)
                {
                    BinaryFieldAttribute attribute = property.GetCustomAttribute<BinaryFieldAttribute>();
                    if (attribute == null)
                        continue;

                    try
                    {
                        reader.BaseStream.Seek(attribute.Offset, SeekOrigin.Begin);
                        object value = ReadField(reader, attribute);
                        property.SetValue(data, value);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading field {property.Name} at offset {attribute.Offset}: {ex.Message}");
                    }
                }

                // 读取扩展信息（根据Map Version）
                ReadExtendedInfo(reader, data);
                // 读取Die测试数据
                ReadDieTestData(reader, data);
            }

            return data;
        }
        private static void ReadDieTestData(BinaryReader reader, S2000MappingData data)
        {
            try
            {
                // 定位到Die信息地址
                reader.BaseStream.Seek(236, SeekOrigin.Begin);
                // 计算Die数据总大小
                int totalDieDataSize = (int)reader.BaseStream.Length - 236;
                //int totalDieDataSize = 6 * data.TotalTestedDice;
                int totalTestedDice = data.MapDataAreaRowSize*data.MapDataAreaLineSize;
                // 读取所有Die数据
                byte[] allDieData = reader.ReadBytes(totalDieDataSize);
                // 解析Die数据
                data.DieTestResults = ParseAllDieData(allDieData, totalTestedDice, data.GroupManagement);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading die test data: {ex.Message}");
                data.DieTestResults = Array.Empty<DieTestResult>();
            }
        }
        private static void ReadDieData(BinaryReader reader, S2000MappingData data)
        {
            reader.BaseStream.Seek(236, SeekOrigin.Begin);
            long len = reader.BaseStream.Length;
            int count = data.MapDataAreaRowSize * data.MapDataAreaLineSize;
            for (int i = 0; i < count; i++)
            {
            byte[] byteData = reader.ReadBytes(6);

            }
        }

        public static DieTestResult ParseDieData(byte[] dieData)
        {
            if (dieData == null || dieData.Length < 6)
                throw new ArgumentException("Die data must be at least 6 bytes");

            DieTestResult result = new DieTestResult();

            // 解析前2个字节（第一个字）
            byte[] lb = new byte[2];
            lb[0] = dieData[1];
            lb[1] = dieData[0];
            ushort firstWord = BitConverter.ToUInt16(lb, 0);
            ParseFirstWord(firstWord, result);

            // 解析第3-4个字节（第二个字）
            if (dieData.Length >= 4)
            {
                lb[0] = dieData[3];
                lb[1] = dieData[2];
                ushort secondWord = BitConverter.ToUInt16(lb, 0);
                ParseSecondWord(secondWord, result);
            }

            // 解析第5-6个字节（第三个字）
            if (dieData.Length >= 6)
            {
                lb[0] = dieData[5];
                lb[1] = dieData[4];
                ushort thirdWord = BitConverter.ToUInt16(lb, 0);
                ParseThirdWord(thirdWord, result);
            }

            // 计算考虑正负号的坐标值
            CalculateCoordinates(result);

            // 计算实际站点号和类别数据（根据NOTE：需要加1）
            CalculateActualValues(result);

            return result;
        }
        private static void ParseFirstWord(ushort firstWord, DieTestResult result)
        {
            // 位15-14: Die Test Result (2位)
            result.TestResult = (DieTestStatus)((firstWord >> 14) & 0x03);

            // 位13: Marking (1位)
            result.IsMarked = ((firstWord >> 13) & 0x01) == 1;

            // 位12: Fail Mark Inspection (1位)
            result.FailMarkInspection = ((firstWord >> 12) & 0x01) == 1;

            // 位11-10: Re-probing Result (2位)
            result.ReProbingResult = (ReProbingResult)((firstWord >> 10) & 0x03);

            // 位9: Needle Mark Inspection Result (1位)
            result.NeedleMarkInspectionResult = ((firstWord >> 9) & 0x01) == 1;

            // 位8-0: Die Coordinator X (9位，0-511)
            result.DieCoordinatorX = (ushort)(firstWord & 0x1FF);
        }
        private static void ParseSecondWord(ushort secondWord, DieTestResult result)
        {
            // 位15-14: Die Property (2位)
            result.DieProperty = (DieProperty)((secondWord >> 14) & 0x03);

            // 位13: Reject chip flag (1位)
            result.IsRejectChip = ((secondWord >> 13) & 0x01) == 1;

            // 位12: Needle Marking Inspection Execution Die Selection (1位)
            result.NeedleMarkingInspectionExecution = ((secondWord >> 12) & 0x01) == 1;

            // 位11: Sampling Die (1位)
            result.IsSamplingDie = ((secondWord >> 11) & 0x01) == 1;

            // 位10: Code Bit of Coordinator Value X (1位)
            result.IsCoordinatorXNegative = ((secondWord >> 10) & 0x01) == 1;

            // 位9: Code Bit of Coordinator Value Y (1位)
            result.IsCoordinatorYNegative = ((secondWord >> 9) & 0x01) == 1;

            // 位8: Dummy Data (1位)
            result.IsDummyData = ((secondWord >> 8) & 0x01) == 1;

            // 位7-0: Die Coordinator Value Y (8位，0-255)
            result.DieCoordinatorY = (ushort)(secondWord & 0xFF);
        }

        private static void ParseThirdWord(ushort thirdWord, DieTestResult result)
        {
            // 位15: Measurement Finish Flag (1位)
            result.MeasurementFinishFlag = ((thirdWord >> 15) & 0x01) == 1;

            // 位14-13: Reject Chip Flag (2位)
            result.RejectChipFlag = (RejectChipFlag)((thirdWord >> 13) & 0x03);

            // 位12-7: Test Execution Site No. (6位，0-63)
            result.TestExecutionSiteNo = (byte)((thirdWord >> 7) & 0x3F);

            // 位6-5: Block Area Judgement Function (2位)
            result.BlockAreaJudgement = (BlockAreaJudgement)((thirdWord >> 5) & 0x03);

            // 位4-0: Category Data (5位，0-31)
            // 或者根据用户特殊，使用8位区域
            if ((thirdWord & 0x1F) != 0) // 如果低5位有数据
            {
                result.CategoryData = (byte)(thirdWord & 0x1F);
            }
            else
            {
                // 用户特殊数据（8位）
                result.UserSpecialData = (byte)(thirdWord & 0xFF);
            }
        }

        private static void CalculateCoordinates(DieTestResult result)
        {
            // 计算X坐标（考虑正负号）
            result.CalculatedCoordinatorX = result.IsCoordinatorXNegative
                ? (short)-result.DieCoordinatorX
                : (short)result.DieCoordinatorX;

            // 计算Y坐标（考虑正负号）
            result.CalculatedCoordinatorY = result.IsCoordinatorYNegative
                ? (short)-result.DieCoordinatorY
                : (short)result.DieCoordinatorY;
        }

        private static void CalculateActualValues(DieTestResult result)
        {
            // 根据NOTE：实际站点号 = 测试执行站点号 + 1
            result.ActualSiteNo = (byte)(result.TestExecutionSiteNo + 1);

            // 根据NOTE：实际类别数据 = 类别数据 + 1
            result.ActualCategoryData = (byte)(result.CategoryData + 1);
        }
        // 批量解析Die数据
        public static DieTestResult[] ParseAllDieData(byte[] allDieData, int dieCount,uint mapDataForm)
        {
            if (allDieData == null || dieCount <= 0)
                return Array.Empty<DieTestResult>();

            int bytesPerDie = 0; // 每个Die占6字节
            switch (mapDataForm)
            {
                case 0:
                    bytesPerDie = 6;
                    break;
                case 1:
                    bytesPerDie = 1;
                    break;
                case 2:
                    bytesPerDie = 2;
                    break;
                case 3:
                    bytesPerDie = 3;
                    break;
                default:
                    break;
            }
            if (bytesPerDie > 0)
            {
                DieTestResult[] results = new DieTestResult[dieCount];

                for (int i = 0; i < dieCount; i++)
                {
                    int offset = i * bytesPerDie;
                    if (offset + bytesPerDie <= allDieData.Length)
                    {
                        byte[] dieBytes = new byte[bytesPerDie];
                        Array.Copy(allDieData, offset, dieBytes, 0, bytesPerDie);
                        results[i] = ParseDieData(dieBytes);
                    }
                }
                return results;
            }
            else
            {
                return Array.Empty<DieTestResult>();
            }
        }
        private static object ReadField(BinaryReader reader, BinaryFieldAttribute attribute)
        {
            //byte[] byteData = reader.ReadBytes(attribute.Size);
            switch (attribute.Type)
            {
                case FieldType.Byte:
                    byte[] byteData = reader.ReadBytes(attribute.Size);
                    return ConvertByteArray(byteData, attribute.Size);

                case FieldType.UInt16:
                    byte[] uint16Data = reader.ReadBytes(attribute.Size);
                    Array.Reverse(uint16Data);
                    return BitConverter.ToUInt16(uint16Data, 0);

                case FieldType.UInt32:
                    byte[] uint32Data = reader.ReadBytes(attribute.Size);
                    Array.Reverse(uint32Data);
                    return BitConverter.ToUInt32(uint32Data, 0);

                case FieldType.String:
                    byte[] stringData = reader.ReadBytes(attribute.Size);
                    return Encoding.ASCII.GetString(stringData).TrimEnd('\0', ' ');

                case FieldType.Pointer:
                    byte[] pointerData = reader.ReadBytes(attribute.Size);
                    return BitConverter.ToUInt32(pointerData, 0);

                default:
                    return reader.ReadBytes(attribute.Size);
            }
        }

        private static object ConvertByteArray(byte[] data, int size)
        {
            if (size == 1) return data[0];
            if (size == 2) return BitConverter.ToUInt16(data, 0);
            if (size == 4) return BitConverter.ToUInt32(data, 0);
            return data;
        }

        private static void ReadExtendedInfo(BinaryReader reader, S2000MappingData data)
        {
            // 根据Map Version读取扩展信息
            if (data.MapVersion >= MapVersion.MultiSites256)
            {
                data.ExtendedInfo = new ExtendedMapInfo();
                // 这里根据实际扩展信息的偏移量读取数据
                // 例如：reader.BaseStream.Seek(extendedOffset, SeekOrigin.Begin);
            }
        }
    }
}
