
using OpenCvSharp;

using Point = System.Windows.Point;

namespace CVVAMControl
{
    public class CurveSmoother
    {
        /// <summary>
        /// 按X坐标排序点
        /// </summary>
        public static List<Point> SortByX(List<Point> points, bool ascending = true)
        {
            if (points == null || points.Count == 0) return points;

            return ascending ?
                points.OrderBy(p => p.X).ToList() :
                points.OrderByDescending(p => p.X).ToList();
        }
        /// <summary>
        /// 高斯滤波平滑
        /// </summary>
        public static List<Point> GaussianSmooth(List<Point> points, int kernelSize = 5, double sigma = 1.0)
        {
            if (points == null || points.Count < 3) return points;

            var xValues = points.Select(p => p.X).ToArray();
            var yValues = points.Select(p => p.Y).ToArray();

            // 使用高斯滤波
            var smoothedX = GaussianBlur1D(xValues, kernelSize, sigma);
            var smoothedY = GaussianBlur1D(yValues, kernelSize, sigma);

            var result = new List<Point>();
            for (int i = 0; i < points.Count; i++)
            {
                result.Add(new Point(smoothedX[i], smoothedY[i]));
            }

            return result;
        }

        /// <summary>
        /// 移动平均平滑
        /// </summary>
        public static List<Point> MovingAverageSmooth(List<Point> points, int windowSize = 5)
        {
            if (points == null || points.Count < windowSize) return points;

            var result = new List<Point>();
            int halfWindow = windowSize / 2;

            for (int i = 0; i < points.Count; i++)
            {
                double sumX = 0, sumY = 0;
                int count = 0;

                for (int j = -halfWindow; j <= halfWindow; j++)
                {
                    int index = i + j;
                    if (index >= 0 && index < points.Count)
                    {
                        sumX += points[index].X;
                        sumY += points[index].Y;
                        count++;
                    }
                }

                result.Add(new Point(sumX / count, sumY / count));
            }

            return result;
        }

        /// <summary>
        /// Savitzky-Golay滤波
        /// </summary>
        public static List<Point> SavitzkyGolaySmooth(List<Point> points, int windowSize = 5, int polynomialOrder = 2)
        {
            if (points == null || points.Count < windowSize) return points;

            var xValues = points.Select(p => p.X).ToArray();
            var yValues = points.Select(p => p.Y).ToArray();

            var smoothedX = SavitzkyGolayFilter(xValues, windowSize, polynomialOrder);
            var smoothedY = SavitzkyGolayFilter(yValues, windowSize, polynomialOrder);

            var result = new List<Point>();
            for (int i = 0; i < points.Count; i++)
            {
                result.Add(new Point(smoothedX[i], smoothedY[i]));
            }

            return result;
        }

        /// <summary>
        /// 使用OpenCV的GaussianBlur
        /// </summary>
        public static List<Point> OpenCVGaussianSmooth(List<Point> points, Size ksize, double sigmaX)
        {
            if (points == null || points.Count < 3) return points;

            // 转换为Mat
            var data = points.Select(p => new float[] { (float)p.X, (float)p.Y }).ToArray();
            //using var mat = new Mat(points.Count, 2, MatType.CV_32F, data);
            using var mat = Mat.FromPixelData(points.Count, 2, MatType.CV_32F, data);

            // 分离X和Y坐标
            using var xMat = mat.Col(0);
            using var yMat = mat.Col(1);

            // 应用高斯滤波
            using var xSmoothed = new Mat();
            using var ySmoothed = new Mat();

            Cv2.GaussianBlur(xMat, xSmoothed, ksize, sigmaX);
            Cv2.GaussianBlur(yMat, ySmoothed, ksize, sigmaX);

            // 合并结果
            var result = new List<Point>();
            for (int i = 0; i < points.Count; i++)
            {
                float x = xSmoothed.At<float>(i);
                float y = ySmoothed.At<float>(i);
                result.Add(new Point(x, y));
            }

            return result;
        }
        /// <summary>
        /// 使用OpenCV GaussianBlur进行曲线平滑
        /// </summary>
        /// <param name="points">输入点集</param>
        /// <param name="ksize">高斯核大小</param>
        /// <param name="sigmaX">X方向标准差</param>
        /// <param name="sigmaY">Y方向标准差</param>
        /// <param name="borderType">边界处理类型</param>
        /// <returns>平滑后的点集</returns>
        public static List<Point> OpenCVGaussianSmooth(
            List<Point> points,
            Size ksize,
            double sigmaX,
            double sigmaY = 0,
            BorderTypes borderType = BorderTypes.Reflect101)
        {
            if (points == null || points.Count < 3)
                return new List<Point>(points);

            // 1. 按X坐标排序
            var sortedPoints = SortByX(points);

            // 2. 分离X和Y坐标
            double[] xValues = sortedPoints.Select(p => p.X).ToArray();
            double[] yValues = sortedPoints.Select(p => p.Y).ToArray();

            // 3. 转换为OpenCV Mat格式
            using (Mat xMat = Mat.FromPixelData(sortedPoints.Count, 1, MatType.CV_64F, xValues))
            using (Mat yMat = Mat.FromPixelData(sortedPoints.Count, 1, MatType.CV_64F, yValues))
            {
                // 4. 应用高斯滤波
                Mat xSmoothed = new Mat();
                Mat ySmoothed = new Mat();

                Cv2.GaussianBlur(xMat, xSmoothed, ksize, sigmaX, sigmaY, borderType);
                Cv2.GaussianBlur(yMat, ySmoothed, ksize, sigmaX, sigmaY, borderType);

                // 5. 提取结果并组合
                List<Point> result = new List<Point>();
                for (int i = 0; i < sortedPoints.Count; i++)
                {
                    double x = xSmoothed.At<double>(i, 0);
                    double y = ySmoothed.At<double>(i, 0);
                    result.Add(new Point(x, y));
                }

                return result;
            }
        }

        /// <summary>
        /// 增强版OpenCV高斯平滑，支持多种选项
        /// </summary>
        public static List<Point> EnhancedOpenCVGaussianSmooth(
            List<Point> points,
            GaussianOptions options)
        {
            if (points == null || points.Count < 3)
                return new List<Point>(points);

            // 预处理
            List<Point> processedPoints = PreprocessPoints(points, options);

            // 应用高斯平滑
            var result = OpenCVGaussianSmooth(
                processedPoints,
                options.KernelSize,
                options.SigmaX,
                options.SigmaY,
                options.BorderType);

            // 后处理
            return PostprocessPoints(result, options);
        }

        /// <summary>
        /// 预处理点集
        /// </summary>
        private static List<Point> PreprocessPoints(List<Point> points, GaussianOptions options)
        {
            List<Point> result = new List<Point>(points);

            // 1. 按X排序
            if (options.SortByX)
            {
                result = SortByX(result);
            }

            // 2. 移除重复点
            if (options.RemoveDuplicates)
            {
                result = RemoveDuplicatePoints(result, options.DuplicateTolerance);
            }

            // 3. 重采样
            if (options.Resample)
            {
                result = ResamplePoints(result, options.ResampleCount);
            }

            // 4. 归一化（可选）
            if (options.Normalize)
            {
                result = NormalizePoints(result);
            }

            return result;
        }

        /// <summary>
        /// 后处理点集
        /// </summary>
        private static List<Point> PostprocessPoints(List<Point> points, GaussianOptions options)
        {
            List<Point> result = new List<Point>(points);

            // 反归一化
            if (options.Normalize && options.OriginalRange != null)
            {
                result = DenormalizePoints(result, options.OriginalRange.Value);
            }

            return result;
        }
        /// <summary>
        /// 移除重复点
        /// </summary>
        private static List<Point> RemoveDuplicatePoints(List<Point> points, double tolerance = 0.001)
        {
            if (points.Count < 2) return points;

            List<Point> result = new List<Point>();
            Point? lastPoint = null;

            foreach (var point in points)
            {
                if (lastPoint == null ||
                    Math.Abs(point.X - lastPoint.Value.X) > tolerance ||
                    Math.Abs(point.Y - lastPoint.Value.Y) > tolerance)
                {
                    result.Add(point);
                    lastPoint = point;
                }
            }

            return result;
        }

        /// <summary>
        /// 重采样点集（确保均匀分布）
        /// </summary>
        private static List<Point> ResamplePoints(List<Point> points, int targetCount)
        {
            if (points.Count < 2 || targetCount <= 0)
                return new List<Point>(points);

            var sortedPoints = SortByX(points);
            double minX = sortedPoints.Min(p => p.X);
            double maxX = sortedPoints.Max(p => p.X);

            // 使用线性插值进行重采样
            List<Point> result = new List<Point>();
            double step = (maxX - minX) / (targetCount - 1);

            for (int i = 0; i < targetCount; i++)
            {
                double targetX = minX + i * step;
                result.Add(InterpolatePoint(sortedPoints, targetX));
            }

            return result;
        }

        /// <summary>
        /// 线性插值
        /// </summary>
        private static Point InterpolatePoint(List<Point> points, double x)
        {
            if (points.Count == 0) return new Point(x, 0);
            if (points.Count == 1) return new Point(x, points[0].Y);

            // 查找x所在的区间
            int index = points.FindLastIndex(p => p.X <= x);

            if (index < 0) return points[0]; // x小于所有点
            if (index >= points.Count - 1) return points[points.Count - 1]; // x大于所有点

            Point p1 = points[index];
            Point p2 = points[index + 1];

            if (Math.Abs(p2.X - p1.X) < double.Epsilon)
                return p1;

            double t = (x - p1.X) / (p2.X - p1.X);
            double y = p1.Y + t * (p2.Y - p1.Y);

            return new Point(x, y);
        }

        /// <summary>
        /// 归一化点集到[0,1]范围
        /// </summary>
        private static List<Point> NormalizePoints(List<Point> points)
        {
            if (points.Count == 0) return points;

            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            double rangeX = maxX - minX;
            double rangeY = maxY - minY;

            if (rangeX < double.Epsilon) rangeX = 1;
            if (rangeY < double.Epsilon) rangeY = 1;

            List<Point> result = new List<Point>();
            foreach (var point in points)
            {
                double normalizedX = (point.X - minX) / rangeX;
                double normalizedY = (point.Y - minY) / rangeY;
                result.Add(new Point(normalizedX, normalizedY));
            }

            return result;
        }

        /// <summary>
        /// 反归一化点集
        /// </summary>
        private static List<Point> DenormalizePoints(List<Point> points, (double minX, double maxX, double minY, double maxY) range)
        {
            double rangeX = range.maxX - range.minX;
            double rangeY = range.maxY - range.minY;

            List<Point> result = new List<Point>();
            foreach (var point in points)
            {
                double denormalizedX = point.X * rangeX + range.minX;
                double denormalizedY = point.Y * rangeY + range.minY;
                result.Add(new Point(denormalizedX, denormalizedY));
            }

            return result;
        }

        /// <summary>
        /// 计算最佳高斯核参数
        /// </summary>
        public static (Size ksize, double sigma) CalculateOptimalGaussianParams(int pointCount)
        {
            // 根据数据点数量动态计算参数
            int kernelSize = Math.Max(3, Math.Min(21, pointCount / 10));
            if (kernelSize % 2 == 0) kernelSize++; // 确保奇数

            double sigma = kernelSize / 6.0; // 经验公式

            return (new Size(kernelSize, kernelSize), sigma);
        }
        private static double[] GaussianBlur1D(double[] input, int kernelSize, double sigma)
        {
            if (kernelSize % 2 == 0) kernelSize++;

            // 创建高斯核
            double[] kernel = CreateGaussianKernel(kernelSize, sigma);
            int halfSize = kernelSize / 2;
            double[] output = new double[input.Length];

            for (int i = 0; i < input.Length; i++)
            {
                double sum = 0;
                double weightSum = 0;

                for (int j = -halfSize; j <= halfSize; j++)
                {
                    int index = i + j;
                    if (index >= 0 && index < input.Length)
                    {
                        double weight = kernel[j + halfSize];
                        sum += input[index] * weight;
                        weightSum += weight;
                    }
                }

                output[i] = sum / weightSum;
            }

            return output;
        }

        private static double[] CreateGaussianKernel(int size, double sigma)
        {
            double[] kernel = new double[size];
            int halfSize = size / 2;
            double sum = 0;

            for (int i = -halfSize; i <= halfSize; i++)
            {
                double value = Math.Exp(-(i * i) / (2 * sigma * sigma));
                kernel[i + halfSize] = value;
                sum += value;
            }

            // 归一化
            for (int i = 0; i < size; i++)
            {
                kernel[i] /= sum;
            }

            return kernel;
        }

        private static double[] SavitzkyGolayFilter(double[] input, int windowSize, int polynomialOrder)
        {
            if (windowSize % 2 == 0) windowSize++;
            if (windowSize <= polynomialOrder) windowSize = polynomialOrder + 1;

            int halfWindow = windowSize / 2;
            double[] output = new double[input.Length];

            // 计算卷积系数
            double[,] coefficients = CalculateSavitzkyGolayCoefficients(windowSize, polynomialOrder);

            for (int i = 0; i < input.Length; i++)
            {
                double sum = 0;
                for (int j = -halfWindow; j <= halfWindow; j++)
                {
                    int index = i + j;
                    if (index >= 0 && index < input.Length)
                    {
                        sum += coefficients[j + halfWindow, halfWindow] * input[index];
                    }
                }
                output[i] = sum;
            }

            return output;
        }

        private static double[,] CalculateSavitzkyGolayCoefficients(int windowSize, int polynomialOrder)
        {
            // 简化的系数计算，实际应用可能需要更完整的实现
            int halfWindow = windowSize / 2;
            double[,] coefficients = new double[windowSize, windowSize];

            // 这里使用简单的平均作为示例
            for (int i = 0; i < windowSize; i++)
            {
                for (int j = 0; j < windowSize; j++)
                {
                    coefficients[i, j] = 1.0 / windowSize;
                }
            }

            return coefficients;
        }
    }

    /// <summary>
    /// 高斯平滑选项
    /// </summary>
    public class GaussianOptions
    {
        public Size KernelSize { get; set; } = new Size(5, 5);
        public double SigmaX { get; set; } = 1.0;
        public double SigmaY { get; set; } = 0;
        public BorderTypes BorderType { get; set; } = BorderTypes.Reflect101;
        public bool SortByX { get; set; } = true;
        public bool RemoveDuplicates { get; set; } = true;
        public double DuplicateTolerance { get; set; } = 0.001;
        public bool Resample { get; set; } = false;
        public int ResampleCount { get; set; } = 100;
        public bool Normalize { get; set; } = false;
        public (double minX, double maxX, double minY, double maxY)? OriginalRange { get; set; } = null;
    }
}
