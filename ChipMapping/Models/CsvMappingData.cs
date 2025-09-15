using CsvHelper;
using System.Globalization;
using System.IO;

namespace ChipMapping.Models
{
    public class CsvMappingData
    {
        public int MapX { get; set; }
        public int MapY { get; set; }
        public int DataMapX { get; set; }
        public int DataMapY { get; set; }
        public int OutsiderRing { get; set; }
        public int Bin { get; set; }
        public double AxisPosX { get; set; }
        public double AxisPosY { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public int Similar { get; set; }
        public double SizeX { get; set; }
        public double sizeY { get; set; }
    }

    public class CVMappingData : CsvMappingData
    {
        public uint Id { get; set; }

        public CVMappingData(uint id,CsvMappingData data)
        {
            this.Id = id;
            this.Bin = data.Bin;
            this.AxisPosX = data.AxisPosX;
            this.AxisPosY = data.AxisPosY;
            this.PosX = data.PosX;
            this.PosY = data.PosY;
            this.DataMapX = data.DataMapX;
            this.DataMapY = data.DataMapY;
            this.MapX = data.MapX;
            this.MapY = data.MapY;
            this.OutsiderRing = data.OutsiderRing;
            this.SizeX = data.SizeX;
            this.sizeY = data.sizeY;
            this.Similar = data.Similar;
        }
    }

    public class MappingPosDataRange
    {
        public double MinPosY { get; set; }
        public double MaxPosY { get; set; }
        public double MinPosX { get; set; }
        public double MaxPosX { get; set; }
    }
     public class MappingMapDataRange
    {
        public int MinMapY { get; set; }
        public int MaxMapY { get; set; }
        public int MinMapX { get; set; }
        public int MaxMapX { get; set; }
    }

    public static class CsvMappingDataTool
    {
        //public static Dictionary<(int Row, int Column), CsvMappingData> CreateDataDictionary(List<CsvMappingData> mappingData)
        //{
        //    var dataDict = new Dictionary<(double, double), CsvMappingData>();

        //    foreach (var data in mappingData)
        //    {
        //        var key = (data.PosY, data.PosX);
        //        dataDict[key] = data;
        //    }

        //    return dataDict;
        //}
        public static MappingPosDataRange GetPosDataRange(List<CVMappingData> mappingData)
        {
            if (mappingData == null || mappingData.Count == 0)
            {
                throw new ArgumentException("mappingData 不能为空");
            }

            return new MappingPosDataRange
            {
                MinPosY = mappingData.Min(d => d.PosY),
                MaxPosY = mappingData.Max(d => d.PosY),
                MinPosX = mappingData.Min(d => d.PosX),
                MaxPosX = mappingData.Max(d => d.PosX)
            };
        }
        public static MappingMapDataRange GetMapDataRange(List<CVMappingData> mappingData)
        {
            if (mappingData == null || mappingData.Count == 0)
            {
                throw new ArgumentException("mappingData 不能为空");
            }

            return new MappingMapDataRange
            {
                MinMapY = mappingData.Min(d => d.MapY),
                MaxMapY = mappingData.Max(d => d.MapY),
                MinMapX = mappingData.Min(d => d.MapX),
                MaxMapX = mappingData.Max(d => d.MapX)
            };
        }
        public static bool LoadMappingCsv(string csvPath, ref List<CVMappingData>? cvMappingData)
        {
            bool result = false;
            try
            {
                using (var fileStream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fileStream))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {

                    var records = csv.GetRecords<CsvMappingData>();
                    if (cvMappingData == null) cvMappingData = new List<CVMappingData>();
                    uint id = 0;
                    foreach (var item in records)
                    {
                        cvMappingData.Add(new CVMappingData(id++, item));
                    }
                    result = true;
                }
            }
            catch (Exception)
            {
                result = false;
            }

            return result;
        }
    }
}
