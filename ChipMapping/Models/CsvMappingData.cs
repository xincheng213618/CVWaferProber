using CsvHelper;
using System.Globalization;
using System.IO;
using System.Windows;

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
        public double Distance { get; set; }
        public bool IsOuter { get; set; }

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
                throw new ArgumentException( $"mappingData {Application.Current.FindResource("Cannotbeempty")}");
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
                throw new ArgumentException($"mappingData {Application.Current.FindResource("Cannotbeempty")}");
            }

            return new MappingMapDataRange
            {
                MinMapY = mappingData.Min(d => d.DataMapY),
                MaxMapY = mappingData.Max(d => d.DataMapY),
                MinMapX = mappingData.Min(d => d.DataMapX),
                MaxMapX = mappingData.Max(d => d.DataMapX)
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

        #region Markout
        public static void MarkOutsiderRingPoints(List<CVMappingData> records)
        {
            // 1. 统计坐标范围
            int minX = records.Min(r => r.DataMapX);
            int maxX = records.Max(r => r.DataMapX);
            int minY = records.Min(r => r.DataMapY);
            int maxY = records.Max(r => r.DataMapY);

            // 2. 计算椭圆参数
            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;
            double radiusX = (maxX - minX) / 2.0;
            double radiusY = (maxY - minY) / 2.0;
            // 3. 标记椭圆外圈点
            var markedRecords = MarkEllipseOuterPoints(records, centerX, centerY, radiusX, radiusY);
        }

        static List<CVMappingData> MarkEllipseOuterPoints(List<CVMappingData> records, double centerX, double centerY, double radiusX, double radiusY)
        {
            // 方法1: 基于椭圆方程判断
            foreach (var record in records)
            {
                // 计算点到中心的标准化距离
                double ellipseValue = CalculateEllipseDistance(record.DataMapX, record.DataMapY, centerX, centerY, radiusX, radiusY);

                // 方法1: 简单阈值法 - 靠近椭圆边界的点
                double threshold = 0.9; // 调整这个值来控制外圈厚度
                record.IsOuter = ellipseValue >= threshold;

                // 方法2: 基于距离排名 - 取距离最远的N%作为外圈
                record.Distance = ellipseValue; // 保存计算的距离
            }
            return records;
        }

        static List<CVMappingData> MarkByRanking(List<CVMappingData> records, double outerPercentage)
        {
            // 按椭圆距离排序
            var sorted = records.OrderByDescending(r => r.Distance).ToList();

            // 计算外圈点的数量
            int outerCount = (int)(records.Count * outerPercentage);

            // 标记外圈点
            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].IsOuter = i < outerCount;
            }

            return sorted;
        }

        static double CalculateEllipseDistance(double x, double y, double centerX, double centerY, double radiusX, double radiusY)
        {
            double normalizedX = (x - centerX) / radiusX;
            double normalizedY = (y - centerY) / radiusY;
            return Math.Pow(normalizedX, 2) + Math.Pow(normalizedY, 2);
        }

        #endregion

        /// <summary>
        /// 综合多种方法找出外圈点
        /// </summary>
        public static List<CVMappingData> FindOuterPointsComprehensive(List<CVMappingData> points,
            int desiredOuterPointCount = 0)
        {
            if (points == null || points.Count == 0)
                return new List<CVMappingData>();

            // 方法1：凸包算法（精确的外圈点）
            var convexHullPoints = FindOuterPointsByConvexHull(points);

            // 方法2：基于距离的方法
            var distancePoints = FindOuterPointsByNearestNeighbor(points);

            // 合并结果
            var allOuterPoints = new List<CVMappingData>();
            AddUniquePoints(allOuterPoints, convexHullPoints);
            AddUniquePoints(allOuterPoints, distancePoints);

            // 如果指定了期望的外圈点数量，进行筛选
            if (desiredOuterPointCount > 0 && desiredOuterPointCount < allOuterPoints.Count)
            {
                return SelectBestOuterPoints(allOuterPoints, points, desiredOuterPointCount);
            }

            return allOuterPoints;
        }
        /// <summary>
        /// 基于最近邻距离找出外圈点
        /// </summary>
        public static List<CVMappingData> FindOuterPointsByNearestNeighbor(List<CVMappingData> points)
        {
            if (points == null || points.Count < 2)
                return points?.ToList() ?? new List<CVMappingData>();

            var distances = new Dictionary<CVMappingData, double>();

            // 计算每个点到最近邻居的距离
            foreach (var point in points)
            {
                double minDistance = double.MaxValue;

                foreach (var otherPoint in points)
                {
                    if (point == otherPoint) continue;

                    double distance = GetDistance(point.DataMapX, point.DataMapY, otherPoint.DataMapX, otherPoint.DataMapY);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }

                distances[point] = minDistance;
            }

            // 找出距离较大的点（外圈点）
            double maxDistance = distances.Values.Max();
            double threshold = maxDistance * 0.5; // 阈值设为最大距离的50%

            return distances
                .Where(kv => kv.Value > threshold)
                .Select(kv => kv.Key)
                .ToList();
        }
        /// <summary>
        /// 使用凸包算法找出最外圈的点（Graham Scan算法）
        /// </summary>
        public static List<CVMappingData> FindOuterPointsByConvexHull(List<CVMappingData> points)
        {
            if (points == null || points.Count < 3)
                return points?.ToList() ?? new List<CVMappingData>();

            // 找出最下面的点作为起点
            var startPoint = points.OrderBy(p => p.DataMapY).ThenBy(p => p.DataMapX).First();
            var sortedPoints = points
                .Where(p => p != startPoint)
                .OrderBy(p => GetPolarAngle(startPoint.DataMapX, startPoint.DataMapY, p.DataMapX, p.DataMapY))
                .ThenBy(p => GetDistance(startPoint.DataMapX, startPoint.DataMapY, p.DataMapX, p.DataMapY))
                .ToList();

            var hull = new Stack<CVMappingData>();
            hull.Push(startPoint);
            hull.Push(sortedPoints[0]);

            for (int i = 1; i < sortedPoints.Count; i++)
            {
                while (hull.Count > 1)
                {
                    var top = hull.Pop();
                    var nextToTop = hull.Peek();

                    if (IsCounterClockwise(nextToTop, top, sortedPoints[i]) > 0)
                    {
                        hull.Push(top);
                        break;
                    }
                }
                hull.Push(sortedPoints[i]);
            }

            return hull.Reverse().ToList();
        }
        /// <summary>
        /// 判断方向（叉积）
        /// </summary>
        private static double IsCounterClockwise(CVMappingData a, CVMappingData b, CVMappingData c)
        {
            return (b.DataMapX - a.DataMapX) * (c.DataMapY - a.DataMapY) - (b.DataMapY - a.DataMapY) * (c.DataMapX - a.DataMapX);
        }
        private static void AddUniquePoints(List<CVMappingData> target, List<CVMappingData> source)
        {
            foreach (var point in source)
            {
                if (!target.Any(p => p.Id == point.Id))
                {
                    target.Add(point);
                }
            }
        }

        private static List<CVMappingData> SelectBestOuterPoints(
            List<CVMappingData> candidatePoints,
            List<CVMappingData> allPoints,
            int count)
        {
            // 计算中心点
            double centerX = allPoints.Average(p => p.DataMapX);
            double centerY = allPoints.Average(p => p.DataMapY);

            // 按距离中心点的距离排序，选择最远的点
            return candidatePoints
                .OrderByDescending(p => GetDistance(centerX, centerY, p.DataMapX, p.DataMapY))
                .Take(count)
                .ToList();
        }
        /// <summary>
        /// 计算极角
        /// </summary>
        private static double GetPolarAngle(double x1, double y1, double x2, double y2)
        {
            return Math.Atan2(y2 - y1, x2 - x1);
        }
        private static double GetDistance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        }
    }
}
