using ChipMapping.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipMapping.Tools
{
    public class MultiLayerBoundaryMarker
    {
        /// <summary>
        /// 标记距离最外圈指定距离内的所有外圈点
        /// </summary>
        /// <param name="datas">点数据列表</param>
        /// <param name="outerLayerDistance">距离最外圈的距离阈值（单位：像素）</param>
        /// <param name="edgePointCount">最外圈点的数量</param>
        public void MarkOuterPointsWithinDistance(List<CVMappingData> datas, double outerLayerDistance, int edgePointCount = 0)
        {
            if (datas == null || datas.Count < 3)
                return;

            // 1. 重置所有点的标记
            ResetAllPoints(datas);

            // 2. 找到最外圈点（凸包点）
            var outermostPoints = ComputeOutermostPoints(datas, edgePointCount);
            MarkPoints(outermostPoints, true); // 标记最外圈点

            // 3. 计算所有点到最外圈的平均距离
            CalculateDistancesToOutermost(datas, outermostPoints);

            // 4. 标记距离最外圈在指定距离内的点
            MarkPointsWithinDistance(datas, outermostPoints, outerLayerDistance);

            // 5. 重新计算距离并排序（可选）
            RecalculateAndSortDistances(datas);
        }

        /// <summary>
        /// 重置所有点的标记
        /// </summary>
        private void ResetAllPoints(List<CVMappingData> datas)
        {
            foreach (var data in datas)
            {
                data.IsOuter = false;
                data.Distance = 0;
            }
        }

        /// <summary>
        /// 计算最外圈点（凸包）
        /// </summary>
        private List<CVMappingData> ComputeOutermostPoints(List<CVMappingData> points, int targetCount = 0)
        {
            if (points.Count <= 3)
                return new List<CVMappingData>(points);

            // 使用凸包算法找到最外圈点
            var hullPoints = ComputeConvexHull(points);

            // 如果需要特定数量的点，均匀采样
            if (targetCount > 0 && hullPoints.Count > targetCount)
            {
                hullPoints = SamplePointsEvenly(hullPoints, targetCount);
            }

            return hullPoints;
        }

        /// <summary>
        /// 凸包算法（Graham Scan）
        /// </summary>
        private List<CVMappingData> ComputeConvexHull(List<CVMappingData> points)
        {
            if (points.Count <= 3)
                return new List<CVMappingData>(points);

            // 找到最左下角的点作为起始点
            var startPoint = points.OrderBy(p => p.DataMapY).ThenBy(p => p.DataMapX).First();

            // 按极角排序
            var sortedPoints = points
                .Where(p => p != startPoint)
                .OrderBy(p => Math.Atan2(p.DataMapY - startPoint.DataMapY, p.DataMapX - startPoint.DataMapX))
                .ToList();

            var hull = new Stack<CVMappingData>();
            hull.Push(startPoint);
            hull.Push(sortedPoints[0]);

            for (int i = 1; i < sortedPoints.Count; i++)
            {
                var nextPoint = sortedPoints[i];

                while (hull.Count > 1)
                {
                    var top = hull.Pop();
                    var second = hull.Peek();

                    if (CrossProduct(second, top, nextPoint) > 0)
                    {
                        hull.Push(top);
                        break;
                    }
                }

                hull.Push(nextPoint);
            }

            return hull.ToList();
        }

        /// <summary>
        /// 计算叉积
        /// </summary>
        private double CrossProduct(CVMappingData o, CVMappingData a, CVMappingData b)
        {
            return (a.DataMapX - o.DataMapX) * (b.DataMapY - o.DataMapY) -
                   (a.DataMapY - o.DataMapY) * (b.DataMapX - o.DataMapX);
        }

        /// <summary>
        /// 均匀采样点
        /// </summary>
        private List<CVMappingData> SamplePointsEvenly(List<CVMappingData> points, int targetCount)
        {
            if (points.Count <= targetCount)
                return points;

            var result = new List<CVMappingData>();
            var step = (double)points.Count / targetCount;

            for (int i = 0; i < targetCount; i++)
            {
                var index = (int)Math.Round(i * step) % points.Count;
                result.Add(points[index]);
            }

            return result;
        }

        /// <summary>
        /// 计算所有点到最外圈的平均距离
        /// </summary>
        private void CalculateDistancesToOutermost(List<CVMappingData> allPoints, List<CVMappingData> outermostPoints)
        {
            foreach (var point in allPoints)
            {
                // 计算点到所有最外圈点的最小距离
                double minDistance = double.MaxValue;

                foreach (var outerPoint in outermostPoints)
                {
                    if (point == outerPoint)
                    {
                        minDistance = 0;
                        break;
                    }

                    double distance = CalculateDistance(point, outerPoint);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }

                point.Distance = minDistance;
            }
        }

        /// <summary>
        /// 计算两点之间的欧氏距离
        /// </summary>
        private double CalculateDistance(CVMappingData p1, CVMappingData p2)
        {
            return Math.Sqrt(
                Math.Pow(p1.DataMapX - p2.DataMapX, 2) +
                Math.Pow(p1.DataMapY - p2.DataMapY, 2));
        }

        /// <summary>
        /// 标记距离最外圈在指定距离内的点
        /// </summary>
        private void MarkPointsWithinDistance(List<CVMappingData> allPoints, List<CVMappingData> outermostPoints, double maxDistance)
        {
            // 创建最外圈点的索引以加速距离计算
            var outerIndex = new HashSet<CVMappingData>(outermostPoints);

            foreach (var point in allPoints)
            {
                // 如果已经是外圈点，跳过
                if (outerIndex.Contains(point))
                    continue;

                // 检查是否在距离阈值内
                bool isWithinDistance = false;
                foreach (var outerPoint in outermostPoints)
                {
                    double distance = CalculateDistance(point, outerPoint);
                    if (distance <= maxDistance)
                    {
                        isWithinDistance = true;
                        break;
                    }
                }

                if (isWithinDistance)
                {
                    point.IsOuter = true;
                }
            }
        }

        /// <summary>
        /// 标记点集
        /// </summary>
        private void MarkPoints(List<CVMappingData> points, bool isOuter)
        {
            foreach (var point in points)
            {
                point.IsOuter = isOuter;
            }
        }

        /// <summary>
        /// 重新计算距离并排序
        /// </summary>
        private void RecalculateAndSortDistances(List<CVMappingData> datas)
        {
            // 计算中心点
            double centerX = datas.Average(p => p.DataMapX);
            double centerY = datas.Average(p => p.DataMapY);

            // 更新每个点到中心点的距离
            foreach (var data in datas)
            {
                if (!data.IsOuter) // 只更新外圈点的距离
                    continue;

                data.Distance = Math.Sqrt(
                    Math.Pow(data.DataMapX - centerX, 2) +
                    Math.Pow(data.DataMapY - centerY, 2));
            }
        }
    }
}
