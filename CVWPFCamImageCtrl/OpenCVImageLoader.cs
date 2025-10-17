using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.IO;
using System.Windows.Media.Imaging;

namespace CVWPFCamImageCtrl
{
    public static class OpenCVImageLoader
    {
        /// <summary>
        /// 高效加载大TIFF图像（带内存优化）
        /// </summary>
        public static BitmapSource LoadTiffImage(string filePath, double scaleFactor = 0.1)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                using (var highResMat = Cv2.ImRead(filePath, ImreadModes.Unchanged))
                {
                    if (highResMat.Empty())
                        return null;

                    // 如果是超大图像，先进行缩放
                    if (highResMat.Width > 2000 || highResMat.Height > 2000)
                    {
                        using (var resizedMat = new Mat())
                        {
                            Cv2.Resize(highResMat, resizedMat,
                                new Size(highResMat.Width * scaleFactor, highResMat.Height * scaleFactor),
                                0, 0, InterpolationFlags.Area);

                            return ConvertMatToBitmap(resizedMat);
                        }
                    }
                    else
                    {
                        return ConvertMatToBitmap(highResMat);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载图像失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取图像信息（不加载完整图像）
        /// </summary>
        public static (int width, int height, int channels) GetImageInfo(string filePath)
        {
            try
            {
                using (var mat = Cv2.ImRead(filePath, ImreadModes.AnyColor | ImreadModes.AnyDepth))
                {
                    if (!mat.Empty())
                    {
                        return (mat.Width, mat.Height, mat.Channels());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取图像信息失败: {ex.Message}");
            }

            return (0, 0, 0);
        }

        public static BitmapSource ConvertMatToBitmap(Mat mat)
        {
            if (mat.Empty()) return null;

            // 根据通道数进行颜色转换
            if (mat.Channels() == 3)
            {
                Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2RGB);
            }
            else if (mat.Channels() == 4)
            {
                Cv2.CvtColor(mat, mat, ColorConversionCodes.BGRA2RGBA);
            }
            // 单通道图像保持不变

            return mat.ToBitmapSource();
        }
    }
}
