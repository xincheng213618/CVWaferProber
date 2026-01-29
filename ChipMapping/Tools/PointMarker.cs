using ChipMapping.Models;

namespace ChipMapping.Tools
{
    public class PointMarker
    {
        /// <summary>
        /// 使用凸包算法标记外圈点
        /// </summary>
        /// <param name="datas">点数据列表</param>
        /// <param name="edgePointCount">期望的外圈点数量（如果为0则使用所有凸包点）</param>
        public void MarkOuterPoints(List<CVMappingData> datas, int edgePointCount = 0)
        {
            if (datas == null || datas.Count < 3)
                return;

            // 将所有IsOuter重置为false
            foreach (var data in datas)
            {
                data.IsOuter = false;
            }

            // 计算凸包（外圈点）
            var hullPoints = ComputeConvexHull(datas);

            // 如果指定了边缘点数量，均匀采样
            if (edgePointCount > 0 && hullPoints.Count > edgePointCount)
            {
                hullPoints = SamplePointsEvenly(hullPoints, edgePointCount);
            }

            // 标记外圈点
            foreach (var hullPoint in hullPoints)
            {
                hullPoint.IsOuter = true;
            }
        }

        /// <summary>
        /// 使用Graham Scan算法计算凸包
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
    }
}
