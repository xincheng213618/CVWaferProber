using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CVWaferProber.Services
{
    public static class SimpleLuminanceUniformity
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SimpleLuminanceUniformity));

        public static double Calculate(string tifPath, int radius)
        {
            using Mat src = Cv2.ImRead(tifPath, ImreadModes.Unchanged);
            if (src.Empty())
            {
                logger.Error("读取图像失败");
                return -1;
            }
            if (src.Channels() != 1)
            {
                logger.Error("图像必须是单通道");
                return -1;
            }

            using Mat img = new Mat();
            src.ConvertTo(img, MatType.CV_64FC1);

            int w = img.Width;
            int h = img.Height;

            var centers = new List<Point>
        {
            new Point(w / 4, h / 4),
            new Point(w / 2, h / 4),
            new Point(w * 3 / 4, h / 4),

            new Point(w / 4, h / 2),
            new Point(w / 2, h / 2),
            new Point(w * 3 / 4, h / 2),

            new Point(w / 4, h * 3 / 4),
            new Point(w / 2, h * 3 / 4),
            new Point(w * 3 / 4, h * 3 / 4),
        };

            List<double> means = new();

            foreach (var center in centers)
            {
                using Mat mask = Mat.Zeros(img.Size(), MatType.CV_8UC1);
                Cv2.Circle(mask, center, radius, Scalar.White, -1);
                var mean = Cv2.Mean(img, mask);
                means.Add(mean.Val0);
            }

            double min = means.Min();
            double max = means.Max();

            if (max == 0)
            {
                logger.Error("max亮度为0，无法计算");
                return -1;
            }
            return min / max * 100.0;
        }
    }
}